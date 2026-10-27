// Draws Arena.Level on every peer: the border (draw_borders' red line), the mirror walls (on a random level, the shapes
// they outline, each as its original sprite, U.RICO-T46), and the redirectors with a tick pointing the way they send a
// shot. Presentation only: the level itself is Core data, the same everywhere. When a match is played on another map
// (UR-T9) or a new round is laid, the drawing is rebuilt from the new level. The floor, clouds, lights and border
// sprites under and around it are BackdropView's.
using Ricochet.Core.Aim;
using Ricochet.Core.Art;
using Ricochet.Core.Level;
using UnityEngine;

namespace Ricochet
{
    public sealed class LevelView : MonoBehaviour
    {
        /// <summary>The level shapes' sprites: over the floor and clouds (obj_polygon's depth -2), under the tracks and tanks.</summary>
        public const int ShapeOrder = -6;

        // draw_borders' 1 px c_red line round the playing area, inside the border sprites (BackdropView).
        private static readonly Color BorderColor = Color.red;
        private const float BorderWidth = 0.06f;
        private static readonly Color WallColor = new Color(0.85f, 0.9f, 1f);
        private static readonly Color RedirectorColor = new Color(1f, 0.55f, 0.15f);

        private static LevelView s_current;

        /// <summary>Builds the drawing for <paramref name="level"/> under a new GameObject.</summary>
        public static LevelView Create(Level level)
        {
            var go = new GameObject("Level");
            var view = go.AddComponent<LevelView>();
            view.Draw(level);
            Arena.LevelChanged += view.Redraw;
            s_current = view;
            return view;
        }

        /// <summary>
        /// obj_polygon_parent shrinking (U.RICO-T90): draws shape <paramref name="shape"/> of the laid level
        /// <paramref name="steps"/> room steps after a shrink ray struck it, scaled down 0.01 a step in a colour drawn from
        /// blue, red, orange and yellow each step, and gone at nothing. False when there is no such shape drawn.
        /// </summary>
        public static bool ShrinkShape(int shape, float steps)
        {
            if (s_current == null || shape < 0)
            {
                return false;
            }

            Transform drawn = s_current.transform.Find("Shape " + shape);
            if (drawn == null)
            {
                return false;
            }

            float scale = (float)ItemLooks.ShapeScaleAt(steps);
            drawn.gameObject.SetActive(scale > 0f);
            drawn.localScale = new Vector3(scale, scale, 1f);
            if (drawn.TryGetComponent(out SpriteRenderer renderer) && Mathf.FloorToInt(steps) != Mathf.FloorToInt(steps - Time.deltaTime * Steps.PerSecond))
            {
                renderer.color = Steps.Of(ItemLooks.ShapeShrinkBlends[Random.Range(0, ItemLooks.ShapeShrinkBlends.Length)], 1f);
            }

            return true;
        }

        /// <summary>The level drawn now.</summary>
        public Level Shown { get; private set; }

        /// <summary>How many of the level's shapes are drawn with their sprite (U.RICO-T46).</summary>
        public int ShapesFromArt { get; private set; }

        private void OnDestroy()
        {
            Arena.LevelChanged -= Redraw;
            if (s_current == this)
            {
                s_current = null;
            }
        }

        private void Redraw(Level level)
        {
            if (this == null)
            {
                return;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            Draw(level);
        }

        private void Draw(Level level)
        {
            Shown = level;
            ShapesFromArt = 0;
            var corners = new[]
            {
                new Vector3((float)level.MinX, (float)level.MinY), new Vector3((float)level.MaxX, (float)level.MinY),
                new Vector3((float)level.MaxX, (float)level.MaxY), new Vector3((float)level.MinX, (float)level.MaxY),
                new Vector3((float)level.MinX, (float)level.MinY),
            };
            Lines.Set(Lines.Create(transform, "Border", BorderColor, BorderWidth, -5), corners);

            // A random level's walls are its shapes' outlines: each shape is drawn as the original's instance was, its
            // sprite at its origin turned by its image_angle (U.RICO-T46), or as its outline if the art is missing.
            RoundLayout layout = Arena.Layout;
            if (layout != null && ReferenceEquals(layout.Level, level) && layout.Shapes.Count > 0)
            {
                for (int i = 0; i < layout.Shapes.Count; i++)
                {
                    DrawShape(i, layout.Shapes[i]);
                }
            }
            else
            {
                for (int i = 0; i < level.Walls.Count; i++)
                {
                    Wall wall = level.Walls[i];
                    Lines.Set(Lines.Create(transform, "Wall " + i, WallColor, 0.15f, -4), Arena.ToUnity(wall.A), Arena.ToUnity(wall.B));
                }
            }

            for (int i = 0; i < level.Redirectors.Count; i++)
            {
                Redirector redirector = level.Redirectors[i];
                Lines.Set(Lines.Create(transform, "Redirector " + i, RedirectorColor, 0.18f, -4), Arena.ToUnity(redirector.A), Arena.ToUnity(redirector.B));
                Vector2 center = Arena.ToUnity(redirector.Center);
                Vector2 heading = Arena.ToUnity(Vec2.FromAngle(redirector.Heading));
                Lines.Set(Lines.Create(transform, "Redirector " + i + " heading", RedirectorColor, 0.06f, -4), center, center + heading * 0.6f);
            }
        }

        private void DrawShape(int index, LaidShape shape)
        {
            Sprite sprite = GameArt.Frame(GmlSprites.Shape(shape.Id.Kind, shape.Id.Index));
            if (sprite == null)
            {
                var outline = new Vector3[shape.Corners.Length + 1];
                for (int k = 0; k <= shape.Corners.Length; k++)
                {
                    outline[k] = Arena.ToUnity(shape.Corners[k % shape.Corners.Length]);
                }

                Lines.Set(Lines.Create(transform, "Shape " + index, WallColor, 0.15f, -4), outline);
                return;
            }

            var go = new GameObject("Shape " + index);
            go.transform.SetParent(transform, false);
            go.transform.SetLocalPositionAndRotation(Arena.ToUnity(shape.Origin), Quaternion.Euler(0f, 0f, (float)shape.Degrees));
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = ShapeOrder;
            ShapesFromArt++;
        }
    }
}
