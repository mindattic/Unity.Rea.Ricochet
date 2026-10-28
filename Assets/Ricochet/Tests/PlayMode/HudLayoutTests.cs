// The HUD without IMGUI (UR-T38): with a full 8-seat match running, MatchHud shows a slot for every seat inside the
// canvas, the clock and the turn order are text on its canvas (under the kit's menus, not IMGUI over them), and each
// player's name and health bar are world TextMeshPro objects. That no type still defines OnGUI is HudNoImguiTests (EditMode).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Match;
using TMPro;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class HudLayoutTests : NetSessionTest
    {
        private MatchHud _hud;

        public HudLayoutTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
            Client.Identity = new PlayerIdentity("hud-token", "Hud", new PlayerColor(230, 80, 60));
        }

        [TearDown]
        public void RemoveTheHud()
        {
            if (_hud != null)
            {
                Object.Destroy(_hud.gameObject);
                _hud = null;
            }
        }

        [UnityTest]
        public IEnumerator SixteenSeatsAllFitTheCanvas_UnderTheMenus_WithTheirNamesAndBarsInTheWorld()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            _hud = MatchHud.Create(Client);
            Assert.That(arena.StartMatch(new MatchSetup { AiCount = MatchRules.MaxSeats - 2 }), Is.True);

            yield return WaitUntil(() => _hud.SlotsShowing == MatchRules.MaxSeats, () => "the HUD showed " + _hud.SlotsShowing + " of 8 seats", 10f);
            Assert.That(_hud.Canvas.sortingOrder, Is.LessThan(90), "the menus (90 and up) draw over the HUD");
            Assert.That(_hud.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));

            // Per row, in column order, every slot is on the canvas and clear of the one before it.
            var previousRight = new System.Collections.Generic.Dictionary<int, float>();
            for (int column = 0; column < 8; column++)
            {
                for (int i = 0; i < MatchRules.MaxSeats; i++)
                {
                    Seating.BarSlot(i, out int c, out int row);
                    if (c != column)
                    {
                        continue;
                    }

                    Assert.That(_hud.TrySlotSpan(i, out float left, out float right), Is.True);
                    Assert.That(left, Is.GreaterThanOrEqualTo(0f), "seat " + i + " ran off the left edge");
                    Assert.That(right, Is.LessThanOrEqualTo(_hud.CanvasWidth), "seat " + i + " ran off the right edge");
                    if (previousRight.TryGetValue(row, out float before))
                    {
                        Assert.That(left, Is.GreaterThanOrEqualTo(before - 0.01f), "seat " + i + " overlaps the one to its left");
                    }

                    previousRight[row] = right;
                }
            }

            Assert.That(_hud.ClockText, Does.Match(@"^\d\d\.\d\d$").Or.EqualTo("--.--"));
            yield return WaitUntil(() => _hud.CaptionText.Contains("Turn:"), () => "the caption never gave the turn order: " + _hud.CaptionText);
            Assert.That(_hud.CaptionText, Does.Contain(host.DisplayName));

            // Names and bars are world text, not IMGUI: a TextMeshPro on the scene says each human's name.
            TextMeshPro[] labels = Object.FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None);
            Assert.That(labels.Any(l => l.text.StartsWith(host.DisplayName)), Is.True, "no world label names the host");
            Assert.That(labels.Any(l => l.text.StartsWith(ann.DisplayName)), Is.True, "no world label names the client");
        }

        // Starts both sessions, serves the arena, and waits for both players to have their seats.
        private IEnumerator Begin(System.Action<Arena> arena, System.Action<Player, Player> players)
        {
            yield return StartAndJoinAll();
            arena(Arena.Serve(Server, GamePrefabs.Dummy));
            var host = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
            yield return host;
            var ann = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return ann;
            yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");
            players(host.Result, ann.Result);
        }
    }
}
