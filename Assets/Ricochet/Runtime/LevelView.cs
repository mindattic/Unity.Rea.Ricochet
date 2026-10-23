// Draws Arena.Level on every peer: the border, the mirror walls, and the redirectors with a tick pointing the way
// they send a shot. Presentation only: the level itself is Core data, the same everywhere. When a match is played on
// another map (UR-T9), the drawing is rebuilt from the new level.
using Ricochet.Core.Aim;
using Ricochet.Core.Level;
using UnityEngine;

namespace Ricochet
{
    public sealed class LevelView : MonoBehaviour
    {
        private static readonly Color BorderColor = new Color(0.55f, 0.6f, 0.7f);
        private static readonly Color WallColor = new Color(0.85f, 0.9f, 1f);
        private static readonly Color RedirectorColor = new Color(1f, 0.55f, 0.15f);

        /// <summary>Builds the drawing for <paramref name="level"/> under a new GameObject.</summary>
        public static LevelView Create(Level level)
        {
            var go = new GameObject("Level");
            var view = go.AddComponent<LevelView>();
            view.Draw(level);
            Arena.LevelChanged += view.Redraw;
            return view;
        }

        /// <summary>The level drawn now.</summary>
        public Level Shown { get; private set; }

        private void OnDestroy()
        {
            Arena.LevelChanged -= Redraw;
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
            var corners = new[]
            {
                new Vector3((float)level.MinX, (float)level.MinY), new Vector3((float)level.MaxX, (float)level.MinY),
                new Vector3((float)level.MaxX, (float)level.MaxY), new Vector3((float)level.MinX, (float)level.MaxY),
                new Vector3((float)level.MinX, (float)level.MinY),
            };
            Lines.Set(Lines.Create(transform, "Border", BorderColor, 0.12f, -5), corners);

            for (int i = 0; i < level.Walls.Count; i++)
            {
                Wall wall = level.Walls[i];
                Lines.Set(Lines.Create(transform, "Wall " + i, WallColor, 0.15f, -4), Arena.ToUnity(wall.A), Arena.ToUnity(wall.B));
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
    }
}
