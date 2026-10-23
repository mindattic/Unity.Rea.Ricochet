// The match HUD on every peer, read from the synced state only (Turns and each Player): the turn clock at the top
// centre (draw_turntime.gml's yellow "SS.FF"), a health bar per seat in two rows of eight either side of it, each in
// the player's colour with their name and an AI tag (setup_players.gml's obj_healthbar row and draw_scores.gml's
// icons), the seat whose turn it is outlined, and under the clock the order the living seats play in from now.
// IMGUI keeps the template free of fonts and canvases; the HUD proper moves to MindAttic Components widgets in UR-T8.
using System.Collections.Generic;
using System.Text;
using Ricochet.Core;     // MatchPhase, Seating
using Unity.Multiplayer; // NetBootstrap, NetSession
using UnityEngine;

namespace Ricochet
{
    public sealed class MatchHud : MonoBehaviour
    {
        private const float BarWidth = 150f;
        private const float BarHeight = 10f;
        private const float SlotHeight = 34f;
        private const float ClockGap = 150f;

        private static Texture2D s_white;
        private readonly List<Player> _seated = new List<Player>();
        private GUIStyle _clockStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _centerStyle;

        /// <summary>Adds the HUD to the scene; it draws whatever match <paramref name="session"/> (or the bootstrap's) is in.</summary>
        public static MatchHud Create(NetSession session = null)
        {
            var hud = new GameObject("Match HUD").AddComponent<MatchHud>();
            hud.Session = session;
            return hud;
        }

        /// <summary>The session read; null for NetBootstrap's.</summary>
        public NetSession Session { get; set; }

        /// <summary>A health bar: a dark back, the remaining fraction in <paramref name="color"/>, faint when out.</summary>
        public static void Bar(Rect rect, float fraction, Color color, bool alive)
        {
            Fill(rect, new Color(0f, 0f, 0f, 0.6f));
            Color fill = color;
            fill.a = alive ? 1f : 0.3f;
            Fill(new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, (rect.width - 2f) * Mathf.Clamp01(fraction)), rect.height - 2f), fill);
        }

        private void OnGUI()
        {
            NetSession session = Session ?? (NetBootstrap.Instance != null ? NetBootstrap.Instance.Session : null);
            Turns turns = Turns.For(session);
            if (turns == null)
            {
                return; // practice: no match, no HUD
            }

            EnsureStyles();
            float centre = Screen.width / 2f;

            // The clock, or the round's result.
            string clock = turns.MatchPhase == MatchPhase.RoundOver ? "--.--" : Seating.FormatClock(turns.ShownTimeLeft);
            GUI.Label(new Rect(centre - 70f, 4f, 140f, 34f), clock, _clockStyle);

            _seated.Clear();
            foreach (Player player in Player.In(session))
            {
                if (player.Seat >= 0)
                {
                    _seated.Add(player);
                }
            }

            _seated.Sort((a, b) => a.Seat.CompareTo(b.Seat));
            for (int i = 0; i < _seated.Count && i < Ricochet.Core.MatchRules.MaxSeats; i++)
            {
                DrawSlot(i, _seated[i], turns, centre);
            }

            GUI.Label(new Rect(centre - 300f, 38f + SlotHeight * 2f, 600f, 22f), Caption(turns), _centerStyle);
        }

        private void DrawSlot(int slot, Player player, Turns turns, float centre)
        {
            Seating.BarSlot(slot, out int column, out int row);
            float half = Seating.BarsPerRow / 2f;
            float x = column < half
                ? centre - ClockGap / 2f - (half - column) * (BarWidth + 8f)
                : centre + ClockGap / 2f + (column - half) * (BarWidth + 8f);
            float y = 6f + row * SlotHeight;
            Color color = player.Color;

            if (player.HasTurn)
            {
                Fill(new Rect(x - 3f, y - 2f, BarWidth + 6f, SlotHeight - 2f), new Color(1f, 0.92f, 0.2f, 0.35f));
            }

            string tag = player.IsAi ? "[AI] " : string.Empty;
            string state = player.InRound ? player.Health.ToString() : turns.MatchPhase == MatchPhase.RoundOver || player.Health <= 0 ? "OUT" : "next round";
            _nameStyle.normal.textColor = player.InRound ? color : new Color(color.r, color.g, color.b, 0.45f);
            GUI.Label(new Rect(x, y, BarWidth, 18f), tag + player.DisplayName + "  " + state, _nameStyle);
            Bar(new Rect(x, y + 17f, BarWidth, BarHeight), player.Health / (float)Arena.MatchRules.MaxHealth, color, player.InRound);
        }

        private string Caption(Turns turns)
        {
            if (turns.MatchPhase == MatchPhase.RoundOver)
            {
                Player winner = Find(turns.Winner);
                return winner != null ? winner.DisplayName + " wins round " + turns.Round + "!" : "Nobody survived round " + turns.Round + ".";
            }

            // The living seats from the current one round the table: the turn order.
            var order = new StringBuilder("Round " + turns.Round + "   Turn: ");
            int start = turns.Current;
            bool first = true;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Player player in _seated)
                {
                    bool inThisPass = pass == 0 ? player.Seat >= start : player.Seat < start;
                    if (!inThisPass || !player.InRound)
                    {
                        continue;
                    }

                    order.Append(first ? string.Empty : "  >  ").Append(player.DisplayName);
                    first = false;
                }
            }

            return order.ToString();
        }

        private Player Find(int seat)
        {
            foreach (Player player in _seated)
            {
                if (player.Seat == seat)
                {
                    return player;
                }
            }

            return null;
        }

        private void EnsureStyles()
        {
            if (_clockStyle != null)
            {
                return;
            }

            _clockStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 26, fontStyle = FontStyle.Bold };
            _clockStyle.normal.textColor = new Color(1f, 0.92f, 0.2f); // draw_turntime's c_yellow
            _nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, clipping = TextClipping.Clip };
            _centerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter };
        }

        private static void Fill(Rect rect, Color color)
        {
            if (s_white == null)
            {
                s_white = Texture2D.whiteTexture;
            }

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, s_white);
            GUI.color = previous;
        }
    }
}
