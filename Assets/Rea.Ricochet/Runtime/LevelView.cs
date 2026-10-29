// Draws Arena.Level on every peer: the border (draw_borders' red line), the mirror walls (on a random level, the shapes
// they outline, each as its original sprite, U.RICO-T46), and the redirectors with a tick pointing the way they send a
// shot. Presentation only: the level itself is Core data, the same everywhere. When a match is played on another map
// (UR-T9) or a new round is laid, the drawing is rebuilt from the new level. The floor, clouds, lights and border
// sprites under and around it are BackdropView's.
using System.Collections.Generic;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
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

        // A drawn shape that a vortex's storm can move (U.RICO-T134): a sprite or an outline, where it was laid (Home), where
        // the storms already over left it (Kept) and how far the storm now playing has taken it (Walking).
        private sealed class Piece
        {
            public Transform Sprite;
            public Vector3 SpriteHome;
            public LineRenderer Line;
            public Vector3[] LineHome;
            public Vec2 First; // the laid corner the first wall starts at
            public int FirstWall = -1;
            public Vec2 Kept;
            public Vec2 Walking;
            public Vec2 FirstDir; // the laid first wall's direction (B - A), to tell how far a storm has turned the piece
            public double KeptAngle; // degrees the level has turned the piece about First, as the storm left it
            public float SpriteHomeAngle;
            public Vec2 Pivot; // what the storm turns the piece about while it walks
            public double WalkingAngle; // degrees the storm has turned it so far
        }

        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly Dictionary<int, Piece> _pieceOfWall = new Dictionary<int, Piece>();

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

        /// <summary>
        /// A vortex's storm <paramref name="steps"/> steps after it went off (U.RICO-T134): every shape it moves drawn where it
        /// has taken it, in step with the implosion. False when no level is drawn.
        /// </summary>
        public static bool WalkStorm(StormResult storm, double steps)
        {
            if (s_current == null || storm == null)
            {
                return false;
            }

            s_current.Walk(storm, steps);
            return true;
        }

        /// <summary>
        /// A vortex landed and set its storm off (U.RICO-T134): this peer's level keeps the shapes where the storm leaves them, once
        /// (nothing is kept if another round has been laid since <paramref name="level"/>), though they are drawn walking there only
        /// as the implosion plays (<see cref="WalkStorm"/>, then <see cref="SettleStorm"/>).
        /// </summary>
        public static void KeepStorm(StormResult storm, Level level)
        {
            if (storm != null && level != null && ReferenceEquals(level, Arena.Level))
            {
                level.Apply(storm);
            }
        }

        /// <summary>
        /// The storm is over (or cut short): the shapes it moved are drawn where the level has them now. The level kept them when the
        /// shot landed (<see cref="KeepStorm"/>); <paramref name="startedOn"/> is the level the storm played on.
        /// </summary>
        public static void SettleStorm(StormResult storm, Level startedOn)
        {
            if (storm == null)
            {
                return;
            }

            if (s_current != null && ReferenceEquals(s_current.Shown, startedOn))
            {
                s_current.Rebase();
            }
        }

        /// <summary>Walls of <paramref name="level"/> were moved outside a storm this peer watched (a late joiner's replay, U.RICO-T140): draw them where the level has them.</summary>
        public static void Refresh(Level level)
        {
            if (s_current != null && level != null && ReferenceEquals(s_current.Shown, level))
            {
                s_current.Rebase();
            }
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
            _pieces.Clear();
            _pieceOfWall.Clear();
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
                int first = 0;
                for (int i = 0; i < layout.Shapes.Count; i++)
                {
                    Piece piece = DrawShape(i, layout.Shapes[i]);
                    piece.FirstWall = first;
                    piece.First = layout.Shapes[i].Corners[0];
                    piece.FirstDir = layout.Shapes[i].Corners[1 % layout.Shapes[i].Corners.Length] - piece.First;
                    for (int w = 0; w < layout.Shapes[i].Corners.Length; w++)
                    {
                        _pieceOfWall[first + w] = piece;
                    }

                    first += layout.Shapes[i].Corners.Length;
                }

                Rebase();
            }
            else
            {
                for (int i = 0; i < level.Walls.Count; i++)
                {
                    Wall wall = level.Walls[i];
                    LineRenderer line = Lines.Create(transform, "Wall " + i, WallColor, 0.15f, -4);
                    Lines.Set(line, Arena.ToUnity(wall.A), Arena.ToUnity(wall.B));
                    var piece = new Piece { Line = line, LineHome = new[] { (Vector3)Arena.ToUnity(wall.A), (Vector3)Arena.ToUnity(wall.B) }, FirstWall = i, First = wall.A, FirstDir = wall.B - wall.A };
                    _pieces.Add(piece);
                    _pieceOfWall[i] = piece; // drawn where the level has the wall now; a storm later moves it from there
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

        // Every piece's Kept offset from the level as it is now (a layout shape's first wall against where it was laid), and the storm's reset.
        private void Rebase()
        {
            Level level = Shown;
            foreach (Piece piece in _pieces)
            {
                if (piece.FirstWall >= 0 && level != null && piece.FirstWall < level.Walls.Count)
                {
                    Wall wall = level.Walls[piece.FirstWall];
                    piece.Kept = wall.A - piece.First;
                    Vec2 dir = wall.B - wall.A;
                    piece.KeptAngle = dir.Length > 1e-9 && piece.FirstDir.Length > 1e-9 ? Angles.Normalize(dir.Angle - piece.FirstDir.Angle) : 0;
                }
                piece.Walking = default;
                piece.WalkingAngle = 0;

                Place(piece);
            }
        }

        private void Walk(StormResult storm, double steps)
        {
            foreach (Piece piece in _pieces)
            {
                piece.Walking = default;
                piece.WalkingAngle = 0;
            }

            foreach (StormShape shape in storm.Shapes)
            {
                Vec2 offset = shape.OffsetAt(steps, storm.FlingSteps);
                double angle = StormShape.AngleAt(steps);
                foreach (int wall in shape.Walls)
                {
                    if (_pieceOfWall.TryGetValue(wall, out Piece piece))
                    {
                        piece.Walking = offset;
                        piece.WalkingAngle = angle;
                        piece.Pivot = shape.Pivot;
                    }
                }
            }

            foreach (Piece piece in _pieces)
            {
                Place(piece);
            }
        }

        // A laid point as the piece shows it: turned about its first corner and moved to where the level kept it, then the storm's
        // turn about the shape's pivot and its offset on top.
        private static Vector3 Posed(Piece piece, Vector3 home)
        {
            Vec2 kept = StormShape.Turned(new Vec2(home.x, home.y), piece.First, piece.KeptAngle) + piece.Kept;
            Vec2 walked = StormShape.Turned(kept, piece.Pivot, piece.WalkingAngle) + piece.Walking;
            return new Vector3((float)walked.X, (float)walked.Y, home.z);
        }

        private static void Place(Piece piece)
        {
            if (piece.Sprite != null)
            {
                piece.Sprite.position = Posed(piece, piece.SpriteHome);
                piece.Sprite.rotation = Quaternion.Euler(0f, 0f, piece.SpriteHomeAngle + (float)(piece.KeptAngle + piece.WalkingAngle));
            }

            if (piece.Line != null && piece.LineHome != null)
            {
                for (int i = 0; i < piece.LineHome.Length; i++)
                {
                    piece.Line.SetPosition(i, Posed(piece, piece.LineHome[i]));
                }
            }
        }

        private Piece DrawShape(int index, LaidShape shape)
        {
            var piece = new Piece();
            _pieces.Add(piece);
            Sprite sprite = GameArt.Frame(GmlSprites.Shape(shape.Id.Kind, shape.Id.Index));
            if (sprite == null)
            {
                var outline = new Vector3[shape.Corners.Length + 1];
                for (int k = 0; k <= shape.Corners.Length; k++)
                {
                    outline[k] = Arena.ToUnity(shape.Corners[k % shape.Corners.Length]);
                }

                piece.Line = Lines.Create(transform, "Shape " + index, WallColor, 0.15f, -4);
                Lines.Set(piece.Line, outline);
                piece.LineHome = outline;
                return piece;
            }

            var go = new GameObject("Shape " + index);
            go.transform.SetParent(transform, false);
            go.transform.SetLocalPositionAndRotation(Arena.ToUnity(shape.Origin), Quaternion.Euler(0f, 0f, (float)shape.Degrees));
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = ShapeOrder;
            ShapesFromArt++;
            piece.Sprite = go.transform;
            piece.SpriteHome = go.transform.position;
            piece.SpriteHomeAngle = (float)shape.Degrees;
            return piece;
        }
    }
}
