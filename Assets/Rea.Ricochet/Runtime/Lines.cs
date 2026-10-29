// Coloured lines made in code (no art, no material assets), like Shapes' squares: the level's outline, the aim
// preview and a shot's trail. Replace with real art later.
using System.Collections.Generic;
using UnityEngine;

namespace Ricochet
{
    internal static class Lines
    {
        private static Material s_material;

        /// <summary>A child of <paramref name="parent"/> with a LineRenderer <paramref name="width"/> wide.</summary>
        public static LineRenderer Create(Transform parent, string name, Color color, float width, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Material();
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;
            line.sortingOrder = sortingOrder;
            line.positionCount = 0;
            return line;
        }

        public static void Set(LineRenderer line, IReadOnlyList<Vector3> points)
        {
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                line.SetPosition(i, points[i]);
            }
        }

        public static void Set(LineRenderer line, Vector2 a, Vector2 b)
        {
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        /// <summary>The one vertex-coloured material every code-drawn line and mesh shares.</summary>
        public static Material Material()
        {
            if (s_material == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? Shader.Find("Hidden/Internal-Colored");
                s_material = new Material(shader);
            }

            return s_material;
        }
    }
}
