// Draws one player's volley in the air: each projectile walking its flight (one shell, a beam, a railgun slug, a
// grenade, or a shotgun's fan of pellets), the trail behind it, and a brief flash where it lands; a grenade's flash is
// its blast, drawn at the blast's size. Every peer flies the volley the server solved (UR-T12: Player's ShotResolved
// carries it, packed by Core's ShotWire), so this is presentation only; the server's own flight decides what the volley
// struck. A vortex set off in mid-flight carries on along the cut-short volley the server sends again (Detonate) and
// goes off where the server set it off (UR-T6).
using System.Collections.Generic;
using Ricochet.Core;
using UnityEngine;

namespace Ricochet
{
    public sealed class ShotView : MonoBehaviour
    {
        private const float FlashSeconds = 0.6f;

        private readonly List<Part> _parts = new List<Part>();
        private VolleyFlight _flight;
        private SpriteRenderer _blast;
        private LineRenderer _blastRing;
        private float _flashLeft;
        private Color _color = Color.white;

        /// <summary>The volley being drawn (null before the first).</summary>
        public VolleyFlight Flight => _flight;

        /// <summary>True while any projectile is in the air.</summary>
        public bool Flying => _flight != null && !_flight.Landed;

        /// <summary>Raised once per contact passed, with the contact point (a bounce sound or puff hangs off this).</summary>
        public event System.Action<Vector2> Bounced;

        /// <summary>Raised when the whole volley has landed.</summary>
        public event System.Action<Volley> Landed;

        /// <summary>A new, unparented view (the player's own transform is scaled to its square); the player destroys it.</summary>
        public static ShotView Create(string name)
        {
            return new GameObject(name).AddComponent<ShotView>();
        }

        public void Launch(Volley volley, Color color)
        {
            EnsureBlast();
            _flight = new VolleyFlight(volley);
            _color = color;
            while (_parts.Count < volley.Paths.Count)
            {
                _parts.Add(new Part(transform, _parts.Count));
            }

            float size = volley.Weapon == Weapon.Shotgun ? 0.14f : volley.Weapon == Weapon.Grenade ? 0.3f : 0.22f;
            for (int i = 0; i < _parts.Count; i++)
            {
                Part part = _parts[i];
                bool used = i < volley.Paths.Count;
                part.Projectile.enabled = used;
                part.Flash.enabled = false;
                part.Trail.positionCount = 0;
                part.Points.Clear();
                part.Landed = !used;
                if (!used)
                {
                    continue;
                }

                part.Projectile.color = color;
                part.Projectile.transform.localScale = new Vector3(size, size, 1f);
                float width = volley.Weapon == Weapon.Laser || volley.Weapon == Weapon.Railgun ? 0.11f : 0.07f;
                part.Trail.startWidth = width;
                part.Trail.endWidth = width;
                part.Trail.startColor = new Color(color.r, color.g, color.b, 0.15f);
                part.Trail.endColor = new Color(color.r, color.g, color.b, 0.8f);
                part.Points.Add(Arena.ToUnity(volley.Paths[i].Start));
                Lines.Set(part.Trail, part.Points);
                Place(part.Projectile.transform, volley.Paths[i].Start);
            }

            _blast.enabled = false;
            _blastRing.positionCount = 0;
            _flashLeft = 0f;
        }

        /// <summary>
        /// The volley in the air was set off early (a vortex, vortex_detonate.gml): it carries on from where it is along
        /// <paramref name="cut"/>, the volley cut short, and goes off where that ends.
        /// </summary>
        public void Detonate(Volley cut)
        {
            if (_flight == null || cut == null)
            {
                return;
            }

            double travelled = _flight.Flights.Count > 0 ? _flight.Flights[0].Travelled : 0;
            var flight = new VolleyFlight(cut);
            double upTo = System.Math.Min(travelled, cut.Main.Length * 0.999);
            if (upTo > 0)
            {
                flight.Advance(upTo / cut.Speed);
            }

            _flight = flight;
            for (int i = 0; i < _parts.Count; i++)
            {
                bool used = i < cut.Paths.Count;
                _parts[i].Landed = !used;
                _parts[i].Projectile.enabled = used;
            }
        }

        private void Update()
        {
            if (_flight != null && !_flight.Landed)
            {
                for (int i = 0; i < _flight.Flights.Count; i++)
                {
                    ProjectileFlight flight = _flight.Flights[i];
                    Part part = _parts[i];
                    if (part.Landed)
                    {
                        continue;
                    }

                    int before = flight.Contacts;
                    int passed = flight.Advance(Time.deltaTime);
                    for (int c = 0; c < passed; c++)
                    {
                        Vec2 point = flight.Path.Contacts[before + c].Point;
                        part.Points.Add(Arena.ToUnity(point));
                        Bounced?.Invoke(Arena.ToUnity(point));
                    }

                    Place(part.Projectile.transform, flight.Position);
                    part.Points.Add(part.Projectile.transform.position);
                    Lines.Set(part.Trail, part.Points);
                    part.Points.RemoveAt(part.Points.Count - 1);

                    if (flight.Landed)
                    {
                        part.Landed = true;
                        part.Projectile.enabled = false;
                        if (!_flight.Volley.HasBlast)
                        {
                            part.Flash.enabled = true;
                            part.Flash.color = _color;
                            Place(part.Flash.transform, flight.Path.Impact);
                        }
                    }
                }

                if (_flight.Landed)
                {
                    _flashLeft = FlashSeconds;
                    if (_flight.Volley.HasBlast)
                    {
                        _blast.enabled = true;
                        _blast.color = new Color(1f, 0.6f, 0.2f, 0.45f);
                        Place(_blast.transform, _flight.Volley.BlastCenter);
                        Ring(_flight.Volley.BlastCenter, _flight.Volley.BlastRadius);
                    }

                    Landed?.Invoke(_flight.Volley);
                }
            }
            else if (_flashLeft > 0f)
            {
                _flashLeft -= Time.deltaTime;
                float t = Mathf.Clamp01(_flashLeft / FlashSeconds);
                foreach (Part part in _parts)
                {
                    if (part.Flash.enabled)
                    {
                        part.Flash.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.4f, t);
                        part.Flash.color = new Color(_color.r, _color.g, _color.b, t);
                    }

                    part.Trail.startColor = new Color(_color.r, _color.g, _color.b, 0.15f * t);
                    part.Trail.endColor = new Color(_color.r, _color.g, _color.b, 0.8f * t);
                }

                if (_blast.enabled)
                {
                    float diameter = (float)(_flight.Volley.BlastRadius * 2.0) * Mathf.Lerp(1f, 0.6f, t);
                    _blast.transform.localScale = new Vector3(diameter, diameter, 1f);
                    _blast.color = new Color(1f, 0.6f, 0.2f, 0.45f * t);
                    _blastRing.startColor = _blastRing.endColor = new Color(1f, 1f, 1f, 0.7f * t);
                }

                if (_flashLeft <= 0f)
                {
                    foreach (Part part in _parts)
                    {
                        part.Flash.enabled = false;
                        part.Trail.positionCount = 0;
                    }

                    _blast.enabled = false;
                    _blastRing.positionCount = 0;
                }
            }
        }

        // obj_grenade_explosion's draw_circle outline at the blast's full radius.
        private void Ring(Vec2 center, double radius)
        {
            const int Segments = 40;
            _blastRing.positionCount = Segments + 1;
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                _blastRing.SetPosition(i, new Vector3((float)(center.X + radius * Mathf.Cos(a)), (float)(center.Y + radius * Mathf.Sin(a)), 0f));
            }
        }

        private void EnsureBlast()
        {
            if (_blast != null)
            {
                return;
            }

            var blast = new GameObject("Blast");
            blast.transform.SetParent(transform, false);
            _blast = Shapes.Square(blast, 1f, 3);
            _blast.enabled = false;
            _blastRing = Lines.Create(transform, "Blast ring", Color.white, 0.05f, 4);
        }

        private static void Place(Transform t, Vec2 point)
        {
            t.position = new Vector3((float)point.X, (float)point.Y, 0f);
        }

        // One projectile's drawing: the projectile, its trail, and its impact flash.
        private sealed class Part
        {
            public Part(Transform parent, int index)
            {
                var projectile = new GameObject("Projectile " + index);
                projectile.transform.SetParent(parent, false);
                Projectile = Shapes.Square(projectile, 0.22f, 5);

                var flash = new GameObject("Impact " + index);
                flash.transform.SetParent(parent, false);
                Flash = Shapes.Square(flash, 0.8f, 4);
                Flash.enabled = false;

                Trail = Lines.Create(parent, "Trail " + index, Color.white, 0.07f, 3);
            }

            public SpriteRenderer Projectile { get; }

            public SpriteRenderer Flash { get; }

            public LineRenderer Trail { get; }

            public List<Vector3> Points { get; } = new List<Vector3>();

            public bool Landed { get; set; }
        }
    }
}
