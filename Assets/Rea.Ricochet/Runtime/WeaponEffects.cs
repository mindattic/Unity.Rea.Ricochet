// The weapons' own drawing beyond a sprite in flight (U.RICO-T47, docs/BIBLE.md "Weapon and hazard visuals"), each run by
// ShotView along the path the server solved, step by step at the room speed (WeaponLooks holds the rules):
//   LaserBeam   laser_draw.gml's red line under a white core from the muzzle to the head, which once the laser is spent
//               runs out at the head's end five times as fast as it flew; obj_laser_pulse's spr_raingun_ring sent along it
//               from the muzzle 40-60 steps in and every 25-75 steps after, fading 0.02 a step once the beam is spent.
//   Rail        railgun_draw.gml's faint 9 px line and 1 px core in railColor, and generate_swirl.gml's spr_railgun_swirl
//               every 56 px along it, tinted railColor; all at railAlpha, from 0.8 losing 0.005 a step until gone.
//   BlastRing   obj_grenade_explosion's white circle at 0.7 growing 2 px a step to the blast's radius.
//   Implosion   a vortex set off: vortex_step.gml's modes (spr_vortex at 0.5 growing, waiting, shrinking while its
//               burst circle spreads) with obj_vortex_child turning over it, run at the shot's step rate.
using System.Collections.Generic;
using Ricochet.Core.Aim;
using Ricochet.Core.Art;
using Ricochet.Core.Level;
using UnityEngine;

namespace Ricochet
{
    /// <summary>Steps of GameMaker time: fractions of a step for the linear rules, whole steps for the stepped ones.</summary>
    internal static class Steps
    {
        public static readonly float Pixel = (float)ShapeLibrary.UnitsPerPixel;

        /// <summary>The room speed: 60 steps a second.</summary>
        public static float PerSecond => GameArt.Catalogue != null ? GameArt.Catalogue.RoomSpeed : 60f;

        public static Color Of(GmlColor color, float alpha)
        {
            return new Color(color.R / 255f, color.G / 255f, color.B / 255f, alpha);
        }

        public static LineRenderer Line(Transform parent, string name, Color color, float pixels, int order)
        {
            LineRenderer line = Lines.Create(parent, name, color, pixels * Pixel, order);
            line.numCapVertices = 0; // draw_line_width_color's ends are flat
            return line;
        }

        public static void SetPoints(LineRenderer line, List<Vec2> points)
        {
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                line.SetPosition(i, new Vector3((float)points[i].X, (float)points[i].Y, 0f));
            }
        }

        public static void Circle(LineRenderer line, Vector2 center, float radius)
        {
            const int Segments = 48;
            if (radius <= 0f)
            {
                line.positionCount = 0;
                return;
            }

            line.positionCount = Segments + 1;
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                line.SetPosition(i, new Vector3(center.x + radius * Mathf.Cos(a), center.y + radius * Mathf.Sin(a), 0f));
            }
        }

        public static SpriteRenderer Sprite(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return renderer;
        }
    }

    /// <summary>laser_draw.gml and obj_laser_pulse for one laser.</summary>
    internal sealed class LaserBeam
    {
        private readonly LineRenderer _outer;
        private readonly LineRenderer _inner;
        private readonly Transform _parent;
        private readonly List<SpriteRenderer> _pulseSprites = new List<SpriteRenderer>();
        private readonly List<Pulse> _pulses = new List<Pulse>();
        private readonly List<Vec2> _slice = new List<Vec2>();
        private readonly System.Random _dice = new System.Random();
        private ShotPath _path;
        private double _head;
        private double _tail;
        private double _nextPulse;
        private bool _spent;

        public LaserBeam(Transform parent, int index)
        {
            _parent = parent;
            _outer = Steps.Line(parent, "Laser " + index, Steps.Of(WeaponLooks.LaserColor, (float)WeaponLooks.LaserOuterAlpha), (float)WeaponLooks.LaserOuterWidth, 3);
            _inner = Steps.Line(parent, "Laser core " + index, new Color(1f, 1f, 1f, (float)WeaponLooks.LaserInnerAlpha), (float)WeaponLooks.LaserInnerWidth, 4);
        }

        /// <summary>True while any of it is drawn.</summary>
        public bool Showing => _path != null;

        /// <summary>Pulses on their way along the beam.</summary>
        public int PulseCount => _pulses.Count;

        /// <summary>How far along the path the beam's back end is (0 until it is spent).</summary>
        public double Tail => _tail;

        public void Launch(ShotPath path)
        {
            _path = path;
            _head = 0;
            _tail = 0;
            _spent = false;
            _pulses.Clear();
            _nextPulse = PulseGap(true);
            Draw();
        }

        public void Hide()
        {
            _path = null;
            _pulses.Clear();
            _outer.positionCount = 0;
            _inner.positionCount = 0;
            foreach (SpriteRenderer sprite in _pulseSprites)
            {
                sprite.enabled = false;
            }
        }

        /// <summary>The head is <paramref name="head"/> along the path; the pulses sent so far ride along behind it.</summary>
        public void Fly(double head)
        {
            if (_path == null)
            {
                return;
            }

            _head = head;
            while (!_spent && _head >= _nextPulse)
            {
                _pulses.Add(new Pulse { Sent = _nextPulse, Alpha = 1f });
                _nextPulse += PulseGap(false);
            }

            Draw();
        }

        /// <summary>The laser's last bounce is spent: from now the beam runs out and the pulses fade.</summary>
        public void Spend()
        {
            _spent = true;
            _head = _path != null ? _path.Length : 0;
        }

        /// <summary>After it is spent: a frame of <paramref name="seconds"/> at <paramref name="speed"/> units a second; false once gone.</summary>
        public bool Fade(float seconds, double speed)
        {
            if (_path == null)
            {
                return false;
            }

            float steps = seconds * Steps.PerSecond;
            _tail += WeaponLooks.LaserTailSpeedFactor * speed * seconds;
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                Pulse pulse = _pulses[i];
                pulse.Sent -= speed * seconds; // a pulse flies on at the head's speed: the same as being sent that much earlier
                pulse.Alpha -= (float)WeaponLooks.PulseFadePerStep * steps;
                _pulses[i] = pulse;
                if (pulse.Alpha <= 0f)
                {
                    _pulses.RemoveAt(i);
                }
            }

            Draw();
            if (_tail >= _path.Length && _pulses.Count == 0)
            {
                Hide();
                return false;
            }

            return true;
        }

        // The steps until the next pulse, as the distance the head (and the pulses, at its speed) covers meanwhile.
        private double PulseGap(bool first)
        {
            return WeaponLooks.PulseSteps(_dice, first) * WeaponLooks.BulletSpeedPixels * ShapeLibrary.UnitsPerPixel;
        }

        private void Draw()
        {
            _path.Slice(_tail, _head, _slice);
            Steps.SetPoints(_outer, _slice);
            Steps.SetPoints(_inner, _slice);

            Sprite ring = GameArt.Frame(GmlSprites.LaserPulse);
            int shown = 0;
            foreach (Pulse pulse in _pulses)
            {
                double at = _head - pulse.Sent;
                if (ring == null || at < 0 || at > _path.Length)
                {
                    continue;
                }

                if (shown == _pulseSprites.Count)
                {
                    _pulseSprites.Add(Steps.Sprite(_parent, "Laser pulse " + shown, 5));
                }

                SpriteRenderer sprite = _pulseSprites[shown++];
                Vec2 spot = _path.PositionAt(at, out double heading);
                sprite.enabled = true;
                sprite.sprite = ring;
                sprite.color = new Color(1f, 1f, 1f, pulse.Alpha);
                sprite.transform.position = new Vector3((float)spot.X, (float)spot.Y, 0f);
                sprite.transform.rotation = Quaternion.Euler(0f, 0f, (float)heading);
            }

            for (int i = shown; i < _pulseSprites.Count; i++)
            {
                _pulseSprites[i].enabled = false;
            }
        }

        private struct Pulse
        {
            public double Sent;  // how far the head had flown when it was sent
            public float Alpha;
        }
    }

    /// <summary>railgun_draw.gml with generate_swirl's swirls (obj_rail_swirl), for one rail.</summary>
    internal sealed class Rail
    {
        private readonly LineRenderer _glow;
        private readonly LineRenderer _core;
        private readonly Transform _parent;
        private readonly List<SpriteRenderer> _swirls = new List<SpriteRenderer>();
        private readonly List<Vec2> _slice = new List<Vec2>();
        private ShotPath _path;
        private double _shown;
        private float _age;

        public Rail(Transform parent)
        {
            _parent = parent;
            _glow = Steps.Line(parent, "Rail glow", Steps.Of(WeaponLooks.RailColor, (float)WeaponLooks.RailGlowAlpha), (float)WeaponLooks.RailGlowWidth, 3);
            _core = Steps.Line(parent, "Rail", Steps.Of(WeaponLooks.RailColor, (float)WeaponLooks.RailStartAlpha), (float)WeaponLooks.RailCoreWidth, 4);
        }

        /// <summary>True while the rail is drawn.</summary>
        public bool Showing => _path != null;

        /// <summary>railAlpha now.</summary>
        public float Alpha => (float)WeaponLooks.RailAlpha(_age);

        /// <summary>Swirls drawn now.</summary>
        public int SwirlCount
        {
            get
            {
                int count = 0;
                foreach (SpriteRenderer swirl in _swirls)
                {
                    count += swirl.enabled ? 1 : 0;
                }

                return count;
            }
        }

        public void Launch(ShotPath path)
        {
            _path = path;
            _shown = 0;
            _age = 0f;
            Draw();
        }

        public void Hide()
        {
            _path = null;
            _glow.positionCount = 0;
            _core.positionCount = 0;
            foreach (SpriteRenderer swirl in _swirls)
            {
                swirl.enabled = false;
            }
        }

        /// <summary>The slug has flown <paramref name="travelled"/> (the original's rail was there at once; here it is drawn as fast as the slug goes).</summary>
        public void Reach(double travelled)
        {
            if (_path != null)
            {
                _shown = travelled;
            }
        }

        /// <summary>A frame of <paramref name="seconds"/>: railAlpha fades; false once the rail is gone.</summary>
        public bool Step(float seconds)
        {
            if (_path == null)
            {
                return false;
            }

            _age += seconds * Steps.PerSecond;
            if (WeaponLooks.RailAlpha(_age) <= 0)
            {
                Hide();
                return false;
            }

            Draw();
            return true;
        }

        /// <summary>Adds a light for each swirl drawn (swirl_create's lightSize = 1).</summary>
        public void CollectLights(List<(Vector2 Spot, double Size)> lights)
        {
            foreach (SpriteRenderer swirl in _swirls)
            {
                if (swirl.enabled)
                {
                    lights.Add((swirl.transform.position, 1.0));
                }
            }
        }

        private void Draw()
        {
            float alpha = Alpha;
            _path.Slice(0, _shown, _slice);
            Steps.SetPoints(_glow, _slice);
            Steps.SetPoints(_core, _slice);
            Color rail = Steps.Of(WeaponLooks.RailColor, alpha);
            _core.startColor = _core.endColor = rail;

            Sprite swirlArt = GameArt.Frame(GmlSprites.RailSwirl);
            int shown = 0;
            if (swirlArt != null)
            {
                foreach (double offset in WeaponLooks.SwirlOffsets(_path.Length / ShapeLibrary.UnitsPerPixel))
                {
                    double at = offset * ShapeLibrary.UnitsPerPixel;
                    if (at > _shown)
                    {
                        break;
                    }

                    if (shown == _swirls.Count)
                    {
                        _swirls.Add(Steps.Sprite(_parent, "Rail swirl " + shown, 5));
                    }

                    SpriteRenderer swirl = _swirls[shown++];
                    Vec2 spot = _path.PositionAt(at, out double heading);
                    swirl.enabled = true;
                    swirl.sprite = swirlArt;
                    swirl.color = rail; // swirl_create: image_blend = railColor, image_alpha = railAlpha
                    swirl.transform.position = new Vector3((float)spot.X, (float)spot.Y, 0f);
                    swirl.transform.rotation = Quaternion.Euler(0f, 0f, (float)heading);
                }
            }

            for (int i = shown; i < _swirls.Count; i++)
            {
                _swirls[i].enabled = false;
            }
        }
    }

    /// <summary>obj_grenade_explosion's circle.</summary>
    internal sealed class BlastRing
    {
        private readonly LineRenderer _ring;
        private Vector2 _center;
        private double _radiusPixels;
        private float _age;

        public BlastRing(Transform parent)
        {
            _ring = Steps.Line(parent, "Blast ring", new Color(1f, 1f, 1f, (float)WeaponLooks.BlastAlpha), 1f, 4);
            _ring.loop = false;
        }

        public bool Showing { get; private set; }

        /// <summary>The circle's radius now, in units.</summary>
        public float Radius => (float)(WeaponLooks.BlastRing(_age, _radiusPixels) * ShapeLibrary.UnitsPerPixel);

        public void Start(Vector2 center, double radius)
        {
            _center = center;
            _radiusPixels = radius / ShapeLibrary.UnitsPerPixel;
            _age = 0f;
            Showing = true;
            Steps.Circle(_ring, _center, 0f);
        }

        public void Hide()
        {
            Showing = false;
            _ring.positionCount = 0;
        }

        /// <summary>A frame: the circle grows; false once it has reached the blast's radius (obj_grenade_explosion is gone).</summary>
        public bool Step(float seconds)
        {
            if (!Showing)
            {
                return false;
            }

            _age += seconds * Steps.PerSecond;
            if (_age >= WeaponLooks.BlastSteps(_radiusPixels))
            {
                Hide();
                return false;
            }

            Steps.Circle(_ring, _center, Radius);
            return true;
        }
    }

    /// <summary>A vortex set off: vortex_step.gml's modes, vortex_draw.gml's circle and obj_vortex_child.</summary>
    internal sealed class Implosion
    {
        private const int Order = -3; // obj_vortex and obj_vortex_child's depth 2: under the tanks
        private readonly SpriteRenderer _body;
        private readonly SpriteRenderer _child;
        private readonly LineRenderer _burst;
        private Vortex _vortex;
        private Vector2 _center;
        private double _stepsPerSecond;
        private float _clock;
        private int _steps;

        public Implosion(Transform parent)
        {
            _body = Steps.Sprite(parent, "Vortex", Order);
            _child = Steps.Sprite(parent, "Vortex child", Order);
            _burst = Steps.Line(parent, "Vortex burst", new Color(1f, 1f, 1f, (float)Vortex.BurstAlpha), 1f, Order);
            Hide();
        }

        public bool Showing => _vortex != null;

        /// <summary>Steps since it went off, with the part of the next one the clock has run (what the storm over the level's shapes is walked by).</summary>
        public double ElapsedSteps => _steps + (_stepsPerSecond > 0 ? _clock * _stepsPerSecond : 0);

        /// <summary>The mode it is in (Done when not showing).</summary>
        public VortexMode Mode => _vortex != null ? _vortex.Mode : VortexMode.Done;

        /// <summary>The vortex's size now (image_xscale).</summary>
        public double Scale => _vortex != null ? _vortex.Scale : 0;

        /// <summary>Starts at <paramref name="center"/>, at <paramref name="stepsPerSecond"/> (the shot's own step rate).</summary>
        public void Start(Vector2 center, double stepsPerSecond)
        {
            _vortex = new Vortex();
            _vortex.SetOff();
            _center = center;
            _stepsPerSecond = stepsPerSecond;
            _clock = 0f;
            _steps = 0;
            Sprite art = GameArt.Frame(GmlSprites.Vortex);
            _body.sprite = art;
            _child.sprite = art;
            _body.transform.position = _child.transform.position = new Vector3(center.x, center.y, 0f);
            _body.color = _child.color = new Color(1f, 1f, 1f, (float)Vortex.ImplodedAlpha);
            Draw();
        }

        public void Hide()
        {
            _vortex = null;
            _body.enabled = false;
            _child.enabled = false;
            _burst.positionCount = 0;
        }

        /// <summary>A frame of <paramref name="seconds"/>; false once it has finished.</summary>
        public bool Step(float seconds)
        {
            if (_vortex == null)
            {
                return false;
            }

            _clock += seconds;
            float step = (float)(1.0 / _stepsPerSecond);
            int guard = 0;
            while (_clock >= step && !_vortex.Done && guard++ < 2000)
            {
                _clock -= step;
                _vortex.Step();
                _steps++;
            }

            if (_vortex.Done)
            {
                Hide();
                return false;
            }

            Draw();
            return true;
        }

        private void Draw()
        {
            float scale = Mathf.Max(0f, (float)_vortex.Scale);
            bool art = _body.sprite != null;
            _body.enabled = art && scale > 0f;
            _child.enabled = art && scale > 0f;
            _body.transform.localScale = _child.transform.localScale = new Vector3(scale, scale, 1f);
            _body.transform.rotation = Quaternion.Euler(0f, 0f, (float)_vortex.Angle);
            _child.transform.rotation = Quaternion.Euler(0f, 0f, (float)_vortex.ChildAngle);
            Steps.Circle(_burst, _center, (float)(_vortex.Burst * ShapeLibrary.UnitsPerPixel));
        }
    }
}
