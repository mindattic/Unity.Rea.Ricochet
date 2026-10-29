// draw_beam.gml's dotted line (U.RICO-T47): draw_dottedline 6 px dashes with 6 px gaps, black and 4 px wide, then the
// same dashes white and 2 px wide over them, at the caller's alpha (draw_set_alpha). Each leg of a bounce path is its own
// draw_dottedline call, so the dashes start afresh at every corner. Drawn as one mesh of quads (draw_line_width_color's
// flat-ended lines), the black ones first so the white ones lie over them; the dashes are WeaponLooks.Dashes.
using System.Collections.Generic;
using Ricochet.Core.Art;
using Ricochet.Core.Level;
using UnityEngine;

namespace Ricochet
{
    public sealed class DottedBeam : MonoBehaviour
    {
        private static readonly float Pixel = (float)ShapeLibrary.UnitsPerPixel;

        private readonly List<Vector3> _points = new List<Vector3>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _triangles = new List<int>();
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private float _alpha = -1f;

        /// <summary>Dashes drawn now (each is a black and a white quad).</summary>
        public int DashCount { get; private set; }

        /// <summary>The alpha it is drawn at.</summary>
        public float Alpha => _alpha;

        /// <summary>A dotted beam under <paramref name="parent"/>, drawing nothing yet.</summary>
        public static DottedBeam Create(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var beam = go.AddComponent<DottedBeam>();
            beam._mesh = new Mesh { name = name };
            beam._mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = beam._mesh;
            beam._renderer = go.AddComponent<MeshRenderer>();
            beam._renderer.sharedMaterial = Lines.Material();
            beam._renderer.sortingOrder = sortingOrder;
            return beam;
        }

        /// <summary>Draws the beam along <paramref name="points"/> (world space) at <paramref name="alpha"/>.</summary>
        public void Set(IReadOnlyList<Vector3> points, float alpha)
        {
            if (Same(points) && Mathf.Approximately(alpha, _alpha))
            {
                return;
            }

            _points.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                _points.Add(points[i]);
            }

            _alpha = alpha;
            Rebuild();
        }

        /// <summary>Draws nothing.</summary>
        public void Clear()
        {
            if (_points.Count == 0 && DashCount == 0)
            {
                return;
            }

            _points.Clear();
            Rebuild();
        }

        private bool Same(IReadOnlyList<Vector3> points)
        {
            if (points.Count != _points.Count)
            {
                return false;
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] != _points[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void Rebuild()
        {
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
            DashCount = 0;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            if (_points.Count >= 2 && _alpha > 0f)
            {
                AddLayer(new Color(0f, 0f, 0f, _alpha), (float)WeaponLooks.BeamOuterWidth * Pixel, true);
                AddLayer(new Color(1f, 1f, 1f, _alpha), (float)WeaponLooks.BeamInnerWidth * Pixel, false);
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
        }

        // One draw_dottedline pass over every leg.
        private void AddLayer(Color color, float width, bool count)
        {
            float dash = (float)WeaponLooks.DashPixels * Pixel;
            for (int i = 1; i < _points.Count; i++)
            {
                Vector3 a = _points[i - 1];
                Vector3 b = _points[i];
                Vector3 leg = b - a;
                leg.z = 0f;
                float length = leg.magnitude;
                if (length <= 0f)
                {
                    continue;
                }

                Vector3 along = leg / length;
                Vector3 across = new Vector3(-along.y, along.x, 0f) * (width * 0.5f);
                foreach ((double from, double to) in WeaponLooks.Dashes(length, dash))
                {
                    Vector3 p = a + along * (float)from;
                    Vector3 q = a + along * (float)to;
                    int v = _vertices.Count;
                    _vertices.Add(p - across);
                    _vertices.Add(p + across);
                    _vertices.Add(q + across);
                    _vertices.Add(q - across);
                    _colors.Add(color);
                    _colors.Add(color);
                    _colors.Add(color);
                    _colors.Add(color);
                    _triangles.Add(v);
                    _triangles.Add(v + 1);
                    _triangles.Add(v + 2);
                    _triangles.Add(v);
                    _triangles.Add(v + 2);
                    _triangles.Add(v + 3);
                    if (count)
                    {
                        DashCount++;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }
}
