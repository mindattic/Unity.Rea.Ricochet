// The turn-start Ready card and ping (UR-T42: obj_ready, ping_player), on every peer, read from the synced state only
// (Turns and each Player). When a turn opens (MatchPhase.Ready) the card goes up with the switch sound (sfx_switch):
// "Ready" in white at the middle of the screen, the player's name in their colour above it, and their name in white over
// their tank; it holds for obj_ready's 80 steps, then fades (alpha -= 0.02 a step). The server holds the turn clock for
// exactly that long (MatchRules.ReadySeconds); when it starts (Ready gives way to Aiming) the tank is pinged: the sonar
// sound (sfx_sonar_ping, not over itself) and two rings spreading from it, white and the player's colour (effect_create_above
// ef_ring). A uGUI canvas of MindAttic Components labels and a TextMeshPro world label (UR-T38: no IMGUI); the rings are
// LineRenderers. It sorts above the match HUD and under the announcer cards and every menu.
using System.Collections.Generic;
using MindAttic.Components.Unity; // MaCanvas, MaText, TextStyle
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using TMPro;
using Unity.Multiplayer;          // NetSession
using UnityEngine;

namespace Ricochet
{
    public sealed class ReadyCard : MonoBehaviour
    {
        /// <summary>The card's canvas order: over the match HUD (8), under the announcer cards (10) and the menus.</summary>
        public const int SortingOrder = 9;

        /// <summary>Seconds a ping's rings take to spread and fade (GameMaker's medium ef_ring).</summary>
        public const float RingSeconds = 0.6f;

        /// <summary>How far a ping's rings spread, in world units.</summary>
        public const float RingRadius = 1.6f;

        // The name over the tank sits above the turn marker (Player's, at 0.95), as obj_ready drew it 60 pixels up.
        private const float OverTankHeight = 1.4f;
        private const int RingSegments = 48;

        private sealed class Ring
        {
            public LineRenderer Line;
            public Vector3 Centre;
            public Color Color;
            public float Started;
        }

        private readonly List<Ring> _rings = new List<Ring>();
        private readonly List<Vector3> _points = new List<Vector3>(RingSegments);
        private MatchHud _hud;
        private Canvas _canvas;
        private RectTransform _root;
        private CanvasGroup _fade;
        private MaText _ready;
        private MaText _name;
        private GameObject _worldRoot;
        private TextMeshPro _overTank;

        private Turns _lastTurns;
        private MatchPhase _lastPhase = MatchPhase.Practice;
        private int _lastCurrent = -1;
        private float _shownAt = -1f;
        private float _pingedAt = -100f;

        /// <summary>Adds the Ready card to <paramref name="hud"/>'s GameObject; it shows whatever match the HUD's session plays.</summary>
        public static ReadyCard Create(MatchHud hud)
        {
            var card = hud.gameObject.AddComponent<ReadyCard>();
            card._hud = hud;
            return card;
        }

        /// <summary>True while the card is up (holding or fading).</summary>
        public bool Showing => _root != null && _root.gameObject.activeSelf;

        /// <summary>The card's strength now: 1 while it holds, falling to 0 as it fades.</summary>
        public float Alpha => _fade != null && Showing ? _fade.alpha : 0f;

        /// <summary>The name on the card ("" before the first).</summary>
        public string NameText => _name != null ? _name.Tmp.text : string.Empty;

        /// <summary>The card's colour for the name (the player's).</summary>
        public Color NameColor => _name != null ? _name.Tmp.color : Color.white;

        /// <summary>The name over the tank while it shows ("" otherwise).</summary>
        public string OverTankText => _overTank != null && _overTank.gameObject.activeInHierarchy ? _overTank.text : string.Empty;

        /// <summary>The seat the card is (or was last) for, or -1.</summary>
        public int Seat { get; private set; } = -1;

        /// <summary>How many cards have gone up, and how many pings have gone off (for tests and logs).</summary>
        public int Cards { get; private set; }

        public int Pings { get; private set; }

        /// <summary>The seat pinged last, or -1.</summary>
        public int LastPingSeat { get; private set; } = -1;

        /// <summary>How many ping rings are spreading now.</summary>
        public int RingsShowing => _rings.Count;

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }

            if (_worldRoot != null)
            {
                Destroy(_worldRoot);
            }
        }

        private void Update()
        {
            NetSession session = _hud != null ? _hud.CurrentSession : null;
            Turns turns = session != null && session.IsLive ? Turns.For(session) : null;
            if (turns != _lastTurns)
            {
                _lastTurns = turns;
                _lastPhase = MatchPhase.Practice;
                _lastCurrent = -1;
                Hide();
            }

            MatchPhase phase = turns != null ? turns.MatchPhase : MatchPhase.Practice;
            int current = turns != null ? turns.Current : -1;

            if (phase == MatchPhase.Ready && (_lastPhase != MatchPhase.Ready || current != _lastCurrent))
            {
                Open(session, current);
            }
            else if (phase == MatchPhase.Aiming && (current != _lastCurrent || (_lastPhase != MatchPhase.Aiming && _lastPhase != MatchPhase.ShotInFlight)))
            {
                // The clock has started (obj_ready's last fade step, or at once with no hold): ping the tank.
                Hide();
                Ping(session, current);
            }
            else if (phase != MatchPhase.Ready)
            {
                Hide();
            }

            _lastPhase = phase;
            _lastCurrent = current;

            if (turns != null && turns.IsPaused && _shownAt >= 0f)
            {
                _shownAt += Time.deltaTime; // paused (UR-T43): the card holds as long as the server's hold does
            }

            Refresh(session);
            UpdateRings();
        }

        // obj_ready's create event: sfx_switch, the clock full (the server's), the card at full strength.
        private void Open(NetSession session, int seat)
        {
            Build();
            Seat = seat;
            Cards++;
            _shownAt = Time.time;
            Player player = Find(session, seat);
            string name = player != null ? player.DisplayName : string.Empty;
            HudParts.Set(_name, name);
            WorldText.Set(_overTank, name);
            _canvas.gameObject.SetActive(true);
            _root.gameObject.SetActive(true);
            _name.WithColor(player != null ? (Color)player.Color : Color.white); // after it is enabled, so the theme does not repaint it
            _worldRoot.SetActive(player != null);
            _fade.alpha = 1f;
            GameAudio.Play(SoundCue.Switch);
        }

        private void Hide()
        {
            if (_root != null && _root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }

            if (_worldRoot != null && _worldRoot.activeSelf)
            {
                _worldRoot.SetActive(false);
            }

            _shownAt = -1f;
        }

        // The hold, then the fade; the name over the tank follows the tank.
        private void Refresh(NetSession session)
        {
            if (_shownAt < 0f || _root == null)
            {
                return;
            }

            MatchRules rules = Arena.MatchRules;
            float hold = (float)System.Math.Max(0, rules.ReadyHoldSeconds);
            float fade = (float)System.Math.Max(0, rules.ReadyFadeSeconds);
            float t = Time.time - _shownAt;
            float alpha = t <= hold ? 1f : fade > 0f ? Mathf.Clamp01(1f - (t - hold) / fade) : 0f;
            _fade.alpha = alpha;
            Player player = Find(session, Seat);
            if (player != null)
            {
                _worldRoot.transform.position = player.transform.position + new Vector3(0f, OverTankHeight, 0f);
                Color white = Color.white;
                white.a = alpha;
                _overTank.color = white;
            }
            else if (_worldRoot.activeSelf)
            {
                _worldRoot.SetActive(false);
            }

            if (alpha <= 0f)
            {
                Hide(); // faded out; the ping comes with the clock
            }
        }

        // ping_player: the sonar (unless it is still ringing) and two rings, white and the player's colour.
        private void Ping(NetSession session, int seat)
        {
            Player player = Find(session, seat);
            if (player == null)
            {
                return;
            }

            Pings++;
            LastPingSeat = seat;
            if (Time.time - _pingedAt >= (float)Tones.Seconds(SoundCue.SonarPing))
            {
                GameAudio.Play(SoundCue.SonarPing);
                _pingedAt = Time.time;
            }

            Vector3 centre = player.transform.position;
            AddRing(centre, Color.white, 0);
            AddRing(centre, player.Color, 1);
        }

        private void AddRing(Vector3 centre, Color color, int layer)
        {
            LineRenderer line = Lines.Create(transform, "Ping ring", color, 0.06f, WorldText.Order - 3 + layer);
            line.loop = true;
            line.useWorldSpace = true;
            _rings.Add(new Ring { Line = line, Centre = centre, Color = color, Started = Time.time });
        }

        private void UpdateRings()
        {
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                Ring ring = _rings[i];
                float t = (Time.time - ring.Started) / RingSeconds;
                if (t >= 1f || ring.Line == null)
                {
                    if (ring.Line != null)
                    {
                        Destroy(ring.Line.gameObject);
                    }

                    _rings.RemoveAt(i);
                    continue;
                }

                float radius = Mathf.Lerp(0.2f, RingRadius, 1f - (1f - t) * (1f - t)); // spreads fast, then slows
                _points.Clear();
                for (int s = 0; s < RingSegments; s++)
                {
                    float a = s * Mathf.PI * 2f / RingSegments;
                    _points.Add(ring.Centre + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
                }

                Lines.Set(ring.Line, _points);
                Color c = ring.Color;
                c.a = 1f - t;
                ring.Line.startColor = ring.Line.endColor = c;
            }
        }

        private static Player Find(NetSession session, int seat)
        {
            if (seat < 0)
            {
                return null;
            }

            foreach (Player player in Player.In(session))
            {
                if (player.Seat == seat)
                {
                    return player;
                }
            }

            return null;
        }

        private void Build()
        {
            if (_canvas != null)
            {
                return;
            }

            _canvas = MaCanvas.Create("Ready card canvas", SortingOrder);
            _canvas.transform.SetParent(transform, false);
            CanvasGroup group = _canvas.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            _root = HudParts.Rect("Ready card", _canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(1200f, 220f), Vector2.zero);
            _fade = _root.gameObject.AddComponent<CanvasGroup>();
            _fade.interactable = false;
            _fade.blocksRaycasts = false;

            // obj_ready's draw: "Ready" in white at the middle, the player's name in their colour 32 pixels above it.
            _ready = HudParts.Label(_root, TextStyle.Title, Color.white, 40f, 96f, TextAlignmentOptions.Center, bold: true);
            Place(_ready.RectTransform, new Vector2(1200f, 110f), new Vector2(0f, -40f));
            HudParts.Set(_ready, "Ready");
            Outline(_ready);
            _name = HudParts.Label(_root, TextStyle.Heading, Color.white, 24f, 54f, TextAlignmentOptions.Center, bold: true);
            Place(_name.RectTransform, new Vector2(1200f, 70f), new Vector2(0f, 50f));
            Outline(_name);

            _worldRoot = new GameObject("Ready card: name over the tank");
            _overTank = WorldText.Create(_worldRoot.transform, "Name", 0.3f, Color.white, bold: true);
            _overTank.sortingOrder = WorldText.Order + 1;
            _overTank.outlineWidth = 0.2f;
            _overTank.outlineColor = Color.black;

            _root.gameObject.SetActive(false);
            _worldRoot.SetActive(false);
        }

        // draw_text_outline: the card's words have a black edge.
        private static void Outline(MaText text)
        {
            text.Tmp.outlineWidth = 0.2f;
            text.Tmp.outlineColor = Color.black;
        }

        private static void Place(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
