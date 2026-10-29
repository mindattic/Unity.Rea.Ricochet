// What the original drew in the world around the player whose turn it is (U.RICO-T49), on every peer, read from the synced
// state only (Turns and each Player), at the original's pixels (GameArt.PixelsToUnits):
//   obj_guide         while the turn runs and its player has not driven: two circles in their colour at 0.7 (60 and 61 px),
//                     two in white at 0.4 (59 and 62 px), and a tick along the aim from 49 to 71 px, 3 px wide in white under
//                     one from 50 to 70 px, 1 px wide in their colour (HudLooks).
//   draw_angle / obj_degree  the angle in fnt_score outlined 1 px, 36 px under the tank while it aims up and 60 px over it
//                     otherwise, with spr_symbol_degree at string_width / 2 + 20 from its left edge.
//   obj_indicator_extrabounce  spr_indicator_extrabounce beside the angle while the player has an extra bounce on this turn
//                     (never created in the original: a fixed defect).
// The turn arrow over the tank is the Player's own marker (spr_turnarrow since U.RICO-T49).
using System.Collections.Generic;
using Ricochet.Core.Art;
using Ricochet.Core.Match;
using TMPro;
using Unity.Multiplayer;          // NetSession
using UnityEngine;

namespace Ricochet
{
    public sealed class TurnGuide : MonoBehaviour
    {
        private const int RingSegments = 64;
        private const int GuideOrder = 3;      // over the tanks (0 to 2), under the world labels (10)

        private readonly List<LineRenderer> _rings = new List<LineRenderer>();
        private readonly List<Vector3> _points = new List<Vector3>(RingSegments);
        private MatchHud _hud;
        private GameObject _root;
        private LineRenderer _tickWhite;
        private LineRenderer _tick;
        private TextMeshPro _angle;
        private SpriteRenderer _degree;
        private SpriteRenderer _extraBounce;
        private GuideRing[] _ringLooks;

        /// <summary>Adds the guide to <paramref name="hud"/>'s GameObject.</summary>
        public static TurnGuide Create(MatchHud hud)
        {
            var guide = hud.gameObject.AddComponent<TurnGuide>();
            guide._hud = hud;
            return guide;
        }

        /// <summary>True while obj_guide's rings show.</summary>
        public bool RingShowing => _tick != null && _tick.gameObject.activeSelf;

        /// <summary>The angle readout over or under the current tank ("" while none shows).</summary>
        public string AngleText => _angle != null && _angle.gameObject.activeSelf ? _angle.text : string.Empty;

        /// <summary>True while the degree sign stands after the angle.</summary>
        public bool DegreeShowing => _degree != null && _degree.gameObject.activeSelf && _degree.sprite != null;

        /// <summary>True while the extra-bounce indicator shows.</summary>
        public bool ExtraBounceShowing => _extraBounce != null && _extraBounce.gameObject.activeSelf;

        /// <summary>Where the angle readout's top left is now, in world units.</summary>
        public Vector3 AnglePosition => _angle != null ? _angle.transform.position : Vector3.zero;

        private void OnDestroy()
        {
            if (_root != null)
            {
                Destroy(_root);
            }
        }

        private void Update()
        {
            NetSession session = _hud != null ? _hud.CurrentSession : null;
            Turns turns = session != null && session.IsLive ? Turns.For(session) : null;
            Player current = null;
            bool running = turns != null && (turns.MatchPhase == MatchPhase.Aiming || turns.MatchPhase == MatchPhase.ShotInFlight);
            if (running)
            {
                foreach (Player player in Player.In(session))
                {
                    if (player.Seat >= 0 && player.Seat == turns.Current && player.InRound)
                    {
                        current = player;
                    }
                }
            }

            if (current == null)
            {
                if (_root != null && _root.activeSelf)
                {
                    _root.SetActive(false);
                }

                return;
            }

            if (_root == null)
            {
                Build();
            }

            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }

            Vector3 centre = current.transform.position;
            double heading = current.ShownHeading;
            Color color = current.Color;

            // obj_guide
            bool guide = HudLooks.GuideShows(running, turns.HasMoved);
            for (int i = 0; i < _rings.Count; i++)
            {
                HudParts.Show(_rings[i], guide);
            }

            HudParts.Show(_tickWhite, guide);
            HudParts.Show(_tick, guide);
            if (guide)
            {
                for (int i = 0; i < _rings.Count; i++)
                {
                    GuideRing look = _ringLooks[i];
                    Color ring = look.White ? Color.white : color;
                    ring.a = (float)look.Alpha;
                    _rings[i].startColor = _rings[i].endColor = ring;
                    Circle(centre, GameArt.PixelsToUnits(look.Radius));
                    Lines.Set(_rings[i], _points);
                }

                Vector3 along = Quaternion.Euler(0f, 0f, (float)heading) * Vector3.right;
                Lines.Set(_tickWhite, centre + along * GameArt.PixelsToUnits(HudLooks.GuideTickWhiteFrom), centre + along * GameArt.PixelsToUnits(HudLooks.GuideTickWhiteTo));
                Lines.Set(_tick, centre + along * GameArt.PixelsToUnits(HudLooks.GuideTickFrom), centre + along * GameArt.PixelsToUnits(HudLooks.GuideTickTo));
                Color tick = color;
                tick.a = 1f;
                _tick.startColor = _tick.endColor = tick;
            }

            // draw_angle and obj_degree: the readout's top left at the tank's x, 36 px under it or 60 px over it.
            WorldText.Set(_angle, HudLooks.AngleText(heading));
            Vector3 top = centre + new Vector3(0f, -GameArt.PixelsToUnits(HudLooks.AngleOffsetY(heading)), 0f);
            _angle.transform.position = top;
            float widthPixels = _angle.preferredWidth / GameArt.PixelsToUnits(1f);
            _degree.transform.position = top + new Vector3(GameArt.PixelsToUnits((float)HudLooks.DegreeX(widthPixels)), 0f, 0f);

            // obj_indicator_extrabounce (origin at its centre), just left of the readout.
            bool bounce = HudLooks.ExtraBounceShows(current.ExtraBounces) && _extraBounce.sprite != null;
            HudParts.Show(_extraBounce, bounce);
            if (bounce)
            {
                _extraBounce.transform.position = top + new Vector3(-GameArt.PixelsToUnits(21f), -GameArt.PixelsToUnits(10f), 0f);
            }
        }

        private void Circle(Vector3 centre, float radius)
        {
            _points.Clear();
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                _points.Add(centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }
        }

        private void Build()
        {
            _root = new GameObject("Turn guide");
            _ringLooks = HudLooks.GuideRings;
            float pixel = GameArt.PixelsToUnits(1f);
            for (int i = 0; i < _ringLooks.Length; i++)
            {
                LineRenderer ring = Lines.Create(_root.transform, "Ring " + _ringLooks[i].Radius, Color.white, pixel, GuideOrder);
                ring.loop = true;
                ring.numCapVertices = 0;
                _rings.Add(ring);
            }

            _tickWhite = Lines.Create(_root.transform, "Tick (white)", Color.white, pixel * HudLooks.GuideTickWhiteWidth, GuideOrder);
            _tick = Lines.Create(_root.transform, "Tick", Color.white, pixel * HudLooks.GuideTickWidth, GuideOrder + 1);
            _tickWhite.numCapVertices = _tick.numCapVertices = 0;

            _angle = WorldText.Create(_root.transform, "Angle", GmlFonts.Score, HudLooks.AngleOutline, Color.white, 0.27f);
            _angle.alignment = TextAlignmentOptions.TopLeft;
            _angle.rectTransform.pivot = new Vector2(0f, 1f);
            _angle.rectTransform.sizeDelta = new Vector2(0.1f, 0.1f);

            _degree = Sprite("Degree", GameArt.Frame(GmlSprites.Degree), WorldText.Order);
            _extraBounce = Sprite("Extra bounce", GameArt.Frame(GmlSprites.ExtraBounceIndicator), WorldText.Order);
        }

        private SpriteRenderer Sprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            go.SetActive(sprite != null);
            return renderer;
        }
    }
}
