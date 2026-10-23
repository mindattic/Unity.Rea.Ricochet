// Draws Arena.Level on every peer: the border, the mirror walls, and the redirectors with a tick pointing the way
// they send a shot. Presentation only: the level itself is Core data, the same everywhere.
using Ricochet.Core;
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
            return view;
        }

        private void Draw(Level level)
        {
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
