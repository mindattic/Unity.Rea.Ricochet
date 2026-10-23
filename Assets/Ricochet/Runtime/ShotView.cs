// Draws one player's shot in the air: the projectile walking its ProjectileFlight, the trail behind it, and a brief
// flash where it lands. Every peer runs its own flight from the same synced inputs (Player's ShotHeading/ShotPower),
// so this is presentation only; the server's own flight decides what the impact hit.
using System.Collections.Generic;
using Ricochet.Core;
using UnityEngine;

namespace Ricochet
{
    public sealed class ShotView : MonoBehaviour
    {
        private const float FlashSeconds = 0.6f;

        private ProjectileFlight _flight;
        private SpriteRenderer _projectile;
        private SpriteRenderer _flash;
        private LineRenderer _trail;
        private readonly List<Vector3> _trailPoints = new List<Vector3>();
        private float _flashLeft;

        /// <summary>The flight being drawn (null before the first shot).</summary>
        public ProjectileFlight Flight => _flight;

        /// <summary>True while a shot is in the air.</summary>
        public bool Flying => _flight != null && !_flight.Landed;

        /// <summary>Raised once per contact passed, with the contact point (a bounce sound or puff hangs off this).</summary>
        public event System.Action<Vector2> Bounced;

        /// <summary>Raised when the shot lands, with its path.</summary>
        public event System.Action<ShotPath> Landed;

        /// <summary>A new, unparented view (the player's own transform is scaled to its square); the player destroys it.</summary>
        public static ShotView Create(string name)
        {
            return new GameObject(name).AddComponent<ShotView>();
        }

        public void Launch(ShotPath path, Color color)
        {
            EnsureParts();
            _flight = new ProjectileFlight(path, Arena.Rules);
            _projectile.color = color;
            _projectile.enabled = true;
            _trail.startColor = new Color(color.r, color.g, color.b, 0.15f);
            _trail.endColor = new Color(color.r, color.g, color.b, 0.8f);
            _trailPoints.Clear();
            _trailPoints.Add(Arena.ToUnity(path.Start));
            Lines.Set(_trail, _trailPoints);
            _flash.enabled = false;
            Place(_projectile.transform, path.Start);
        }

        private void Update()
        {
            if (_flight != null && !_flight.Landed)
            {
                int before = _flight.Contacts;
                int passed = _flight.Advance(Time.deltaTime);
                for (int i = 0; i < passed; i++)
                {
                    Vec2 point = _flight.Path.Contacts[before + i].Point;
                    _trailPoints.Add(Arena.ToUnity(point));
                    Bounced?.Invoke(Arena.ToUnity(point));
                }

                Place(_projectile.transform, _flight.Position);
                _trailPoints.Add(_projectile.transform.position);
                Lines.Set(_trail, _trailPoints);
                _trailPoints.RemoveAt(_trailPoints.Count - 1);

                if (_flight.Landed)
                {
                    _projectile.enabled = false;
                    _flash.enabled = true;
                    _flash.color = _projectile.color;
                    Place(_flash.transform, _flight.Path.Impact);
                    _flashLeft = FlashSeconds;
                    Landed?.Invoke(_flight.Path);
                }
            }
            else if (_flashLeft > 0f)
            {
                _flashLeft -= Time.deltaTime;
                float t = Mathf.Clamp01(_flashLeft / FlashSeconds);
                _flash.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.4f, t);
                Color c = _flash.color;
                _flash.color = new Color(c.r, c.g, c.b, t);
                _trail.startColor = new Color(c.r, c.g, c.b, 0.15f * t);
                _trail.endColor = new Color(c.r, c.g, c.b, 0.8f * t);
                if (_flashLeft <= 0f)
                {
                    _flash.enabled = false;
                    _trail.positionCount = 0;
                }
            }
        }

        private void EnsureParts()
        {
            if (_projectile != null)
            {
                return;
            }

            var projectile = new GameObject("Projectile");
            projectile.transform.SetParent(transform, false);
            _projectile = Shapes.Square(projectile, 0.22f, 5);

            var flash = new GameObject("Impact");
            flash.transform.SetParent(transform, false);
            _flash = Shapes.Square(flash, 0.8f, 4);
            _flash.enabled = false;

            _trail = Lines.Create(transform, "Trail", Color.white, 0.07f, 3);
        }

        private static void Place(Transform t, Vec2 point)
        {
            t.position = new Vector3((float)point.X, (float)point.Y, 0f);
        }
    }
}
