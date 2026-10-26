// The level's backdrop (U.RICO-T46, docs/BIBLE.md "Level backdrop"), drawn on every peer under and around LevelView's
// level: the round's floor (setup_backgrounds: one background from each of three groups, one group chosen, tiled at its
// own pixel size), gamescreen's eight clouds at 0.15 alpha drifting a pixel a step to the left and coming back in at the
// right (obj_cloud_parent), obj_spotlight's soft white glow pulsing between 0.05 and 0.15, obj_lights' ten coloured lights
// (draw_light, through the render style's 2D lights), and draw_borders' four border sprites framing the arena. The floor
// and lights are rolled by Core's Backdrop from the level's seed and version, which every peer already has, so all peers
// see the same backdrop and nothing new is sent. Presentation only.
using System;
using System.Collections.Generic;
using Ricochet.Core.Art;
using Ricochet.Core.Level;
using UnityEngine;

namespace Ricochet
{
    public sealed class BackdropView : MonoBehaviour
    {
        /// <summary>The floor: under everything (the room's background).</summary>
        public const int FloorOrder = -30;

        /// <summary>The spotlight and clouds: over the floor, under the level's shapes (obj_polygon's depth -2) and the tanks.</summary>
        public const int SpotlightOrder = -29;
        public const int CloudOrder = -28;

        /// <summary>
        /// The border sprites: draw_borders runs in obj_game's Draw (depth -1999), over the fog of war (depth -999,
        /// <see cref="FogOfWar.SortingOrder"/>); the sides are drawn after the top and bottom.
        /// </summary>
        public const int BorderOrder = 12;
        public const int SideBorderOrder = 13;

        // The most steps one frame catches up on (a long hitch skips the rest rather than stalling).
        private const int MaxStepsPerFrame = 30;

        private sealed class Cloud
        {
            public SpriteRenderer Renderer;
            public double X, Y, Width;
        }

        private BackdropRules _rules;
        private Func<bool> _frozen;
        private SpriteMask _mask;
        private SpriteRenderer _floor;
        private SpriteRenderer _spotlight;
        private SpotlightPulse _pulse;
        private readonly List<Cloud> _clouds = new List<Cloud>();
        private readonly List<GameObject> _lights = new List<GameObject>();
        private readonly List<SpriteRenderer> _borders = new List<SpriteRenderer>();
        private System.Random _cloudDice;
        private Level _framed;
        private Vector2 _roomCentre;
        private int _seed;
        private bool _planned;
        private float _clock;

        /// <summary>The round's backdrop as Core rolled it (null before the first frame).</summary>
        public BackdropPlan Plan { get; private set; }

        /// <summary>The floor's GML background name.</summary>
        public string Background => Plan != null ? Plan.Background : null;

        public SpriteRenderer Floor => _floor;

        public SpriteRenderer Spotlight => _spotlight;

        public int CloudCount => _clouds.Count;

        /// <summary>The 2D lights on the floor now (none when the render pipeline has no 2D lights).</summary>
        public int LightCount => _lights.Count;

        public IReadOnlyList<SpriteRenderer> Borders => _borders;

        /// <summary>Cloud <paramref name="index"/>'s top-left corner in the original's room pixels.</summary>
        public Vector2 CloudRoomPosition(int index) => new Vector2((float)_clouds[index].X, (float)_clouds[index].Y);

        public SpriteRenderer CloudRenderer(int index) => _clouds[index].Renderer;

        /// <summary>
        /// Draws the backdrop from now on, following <see cref="Arena.Level"/> and its rounds. The clouds and spotlight stand
        /// still while <paramref name="frozen"/> says so (the pause menu: isPaused froze the clouds).
        /// </summary>
        public static BackdropView Create(Func<bool> frozen = null)
        {
            var go = new GameObject("Backdrop");
            var view = go.AddComponent<BackdropView>();
            view._frozen = frozen;
            view.Build();
            view.Refresh();
            return view;
        }

        private void Build()
        {
            _rules = BackdropRules.Default;
            _cloudDice = new System.Random(_rules.Clouds.Count * 7919 + 17);

            // The original's view was its room: the clouds and the spotlight are clipped to the framed area.
            var maskObject = new GameObject("Backdrop mask");
            maskObject.transform.SetParent(transform, false);
            _mask = maskObject.AddComponent<SpriteMask>();
            _mask.sprite = Shapes.SquareSprite;

            _floor = Part("Floor", null, FloorOrder);
            _floor.drawMode = SpriteDrawMode.Tiled;
            _floor.tileMode = SpriteTileMode.Continuous;

            _spotlight = Part("Spotlight", GameArt.Frame(_rules.SpotlightSprite), SpotlightOrder);
            _spotlight.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            _pulse = new SpotlightPulse(_rules);

            for (int i = 0; i < _rules.Clouds.Count; i++)
            {
                CloudSpot spot = _rules.Clouds[i];
                Sprite sprite = GameArt.Frame(spot.Sprite);
                SpriteRenderer renderer = Part("Cloud " + i, sprite, CloudOrder);
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                renderer.color = new Color(1f, 1f, 1f, (float)_rules.CloudAlpha);
                double width = sprite != null ? sprite.rect.width : 0;
                _clouds.Add(new Cloud { Renderer = renderer, X = spot.X, Y = spot.Y, Width = width });
            }

            AddBorder("Top border", _rules.TopBorder, BorderOrder);
            AddBorder("Bottom border", _rules.BottomBorder, BorderOrder);
            AddBorder("Left border", _rules.LeftBorder, SideBorderOrder);
            AddBorder("Right border", _rules.RightBorder, SideBorderOrder);
        }

        private void AddBorder(string partName, string sprite, int order)
        {
            Sprite art = GameArt.Frame(sprite);
            if (art != null)
            {
                _borders.Add(Part(partName, art, order));
            }
        }

        private SpriteRenderer Part(string partName, Sprite sprite, int order)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void Update()
        {
            Refresh();

            bool frozen = _frozen != null && _frozen();
            float roomSpeed = GameArt.Catalogue != null ? GameArt.Catalogue.RoomSpeed : 60f;
            float stepSeconds = 1f / roomSpeed;
            _clock += frozen ? 0f : Mathf.Max(0f, Time.deltaTime);
            int steps = 0;
            while (_clock >= stepSeconds && steps < MaxStepsPerFrame)
            {
                _clock -= stepSeconds;
                steps++;
                _pulse.Step();
            }

            if (steps == MaxStepsPerFrame)
            {
                _clock = 0f;
            }

            foreach (Cloud cloud in _clouds)
            {
                Backdrop.DriftCloud(ref cloud.X, ref cloud.Y, cloud.Width, steps, _rules, _cloudDice);
            }

            Place();
        }

        // A new level frames the backdrop again; a new round (the level's seed or version) rolls a new floor and lights.
        private void Refresh()
        {
            Level level = Arena.Level;
            int seed = Backdrop.SeedFor(Arena.LevelSeed, Arena.LevelVersion);
            bool reframe = level != null && !ReferenceEquals(level, _framed);
            if (reframe)
            {
                Frame(level);
            }

            if (reframe || !_planned || seed != _seed)
            {
                _seed = seed;
                _planned = true;
                Apply(Backdrop.Plan(seed, _rules));
            }

            Place();
        }

        // The rebuild's arena fills what was the original's room inside its borders, so the frame goes round the level:
        // the side borders `border` pixels wide, the top and bottom twice that (draw_borders' sizes), the floor under all.
        private void Frame(Level level)
        {
            _framed = level;
            float u = (float)ShapeLibrary.UnitsPerPixel;
            float side = _rules.Border * u, cap = 2f * _rules.Border * u;
            float minX = (float)level.MinX, minY = (float)level.MinY, maxX = (float)level.MaxX, maxY = (float)level.MaxY;
            _roomCentre = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
            var outer = Rect.MinMaxRect(minX - side, minY - cap, maxX + side, maxY + cap);

            _mask.transform.localPosition = outer.center;
            _mask.transform.localScale = new Vector3(outer.width, outer.height, 1f);

            if (_floor.sprite != null)
            {
                _floor.size = outer.size;
                _floor.transform.localPosition = outer.min + Vector2.Scale(PivotOf(_floor.sprite), outer.size);
            }

            foreach (SpriteRenderer border in _borders)
            {
                Rect rect;
                switch (border.name)
                {
                    case "Top border": rect = Rect.MinMaxRect(outer.xMin, maxY, outer.xMax, outer.yMax); break;
                    case "Bottom border": rect = Rect.MinMaxRect(outer.xMin, outer.yMin, outer.xMax, minY); break;
                    case "Left border": rect = Rect.MinMaxRect(outer.xMin, outer.yMin, minX, outer.yMax); break;
                    default: rect = Rect.MinMaxRect(maxX, outer.yMin, outer.xMax, outer.yMax); break;
                }

                Stretch(border, rect);
            }
        }

        // Draws the sprite over exactly `rect` (the border sprites are the original room's size; the frame is a little larger).
        private static void Stretch(SpriteRenderer renderer, Rect rect)
        {
            Vector2 native = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(rect.width / native.x, rect.height / native.y, 1f);
            renderer.transform.localPosition = rect.min + Vector2.Scale(PivotOf(renderer.sprite), rect.size);
        }

        // Where a sprite's pivot is, from its bottom-left corner, as a fraction of its size.
        private static Vector2 PivotOf(Sprite sprite) => new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);

        private void Apply(BackdropPlan plan)
        {
            Plan = plan;
            _floor.sprite = GameArt.Background(plan.Background);
            if (_framed != null)
            {
                Frame(_framed); // the new floor's size and pivot
            }

            foreach (GameObject light in _lights)
            {
                Destroy(light);
            }

            _lights.Clear();
            float u = (float)ShapeLibrary.UnitsPerPixel;
            for (int i = 0; i < plan.Lights.Count; i++)
            {
                BackdropLight light = plan.Lights[i];
                var holder = new GameObject("Light " + i);
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = RoomToWorld(light.X, light.Y);
                // draw_light: a circle from the light's colour at its centre to black at its edge, blended over the room.
                if (RenderStyle.Current.AddGlow(holder.transform, new Color32(light.R, light.G, light.B, 255), light.Radius * u) == null)
                {
                    Destroy(holder);
                    continue;
                }

                _lights.Add(holder);
            }
        }

        private void Place()
        {
            if (_spotlight != null)
            {
                _spotlight.transform.localPosition = RoomToWorld(_rules.SpotlightX, _rules.SpotlightY);
                _spotlight.color = new Color(1f, 1f, 1f, (float)_pulse.Alpha);
            }

            foreach (Cloud cloud in _clouds)
            {
                cloud.Renderer.transform.localPosition = RoomToWorld(cloud.X, cloud.Y);
            }
        }

        // The original's room pixels (y down from its top-left) to the arena, the room centred on the level.
        private Vector2 RoomToWorld(double x, double y)
        {
            double u = ShapeLibrary.UnitsPerPixel;
            return new Vector2(_roomCentre.x + (float)((x - _rules.RoomWidth / 2.0) * u), _roomCentre.y + (float)((_rules.RoomHeight / 2.0 - y) * u));
        }
    }
}
