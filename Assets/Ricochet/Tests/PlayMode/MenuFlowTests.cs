// The menu flow on the wire and on screen (UR-T9): a host and one client play a one-round game; when the round ends the
// game is over for everyone, the client's HUD brings up the endgame recap with the champion and the leaderboard, nobody may
// fire, and a new match takes it away again; a match on another map is played there by every peer; and the rooms' screens
// (the title, players, map room and how-to-play slides) do what their buttons say.
// The room graph, the maps, the goal and the recap's ranking are tested without Unity: dotnet test Core.slnx
// (Tests/Core.Tests/MenuFlowTests.cs).
using System.Collections;
using System.Linq;
using MindAttic.Components.Unity;
using NUnit.Framework;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class MenuFlowTests : NetSessionTest
    {
        private int _weaponDamage;
        private double _roundOverSeconds;
        private MatchHud _hud;

        public MenuFlowTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
            RegisterPrefab(GamePrefabs.Hazard);
            Client.Identity = new PlayerIdentity("ann-token", "Ann", new PlayerColor(230, 80, 60));
        }

        [SetUp]
        public void KeepTheRules()
        {
            _weaponDamage = Arena.MatchRules.WeaponDamage;
            _roundOverSeconds = Arena.MatchRules.RoundOverSeconds;
            TestMatches.WithoutReadyCard();
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            Arena.MatchRules.WeaponDamage = _weaponDamage;
            Arena.MatchRules.RoundOverSeconds = _roundOverSeconds;
            TestMatches.StockReadyCard();
            if (_hud != null)
            {
                Object.Destroy(_hud.gameObject);
                _hud = null;
            }
        }

        [UnityTest]
        public IEnumerator AOneRoundGameEnds_TheClientSeesTheChampionAndTheRecap_NobodyFires_AndANewMatchClosesIt()
        {
            Arena.MatchRules.RoundOverSeconds = 0.3;
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            _hud = MatchHud.Create(Client);

            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 0, Map = 0, Goal = MatchSetup.OneRound, Announcements = 0, WeaponDamage = MatchSetup.MaxWeaponDamage }), Is.True);
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.Round == 1, "the client never got the match");
            Assert.That(turns.GoalPoints, Is.EqualTo(MatchSetup.OneRound));

            string target = Arena.SeatTarget(ann.Seat);
            double? heading = BounceSolver.FindHeading(Arena.ToCore(host.Position), 1, target, Arena.Level, Arena.Targets(Server), Arena.Rules);
            Assert.That(heading, Is.Not.Null, "no aim from " + host.Position + " reaches Ann at " + ann.Position);
            host.ServerFire((float)heading.Value, 1f);

            yield return WaitUntil(() => turns.MatchPhase == MatchPhase.GameOver, () => "the game never ended for the client: " + turns.MatchPhase, 20f);
            Assert.That(turns.ChampionSeat, Is.EqualTo(host.Seat));
            Assert.That(arena.Match.Champion, Is.EqualTo(host.Seat));

            yield return WaitUntil(() => _hud.Recap.Showing, "the client's HUD never showed the recap");
            Assert.That(_hud.Recap.Headline, Is.EqualTo(host.DisplayName + " wins!"));
            Assert.That(_hud.Recap.Lines.Count, Is.EqualTo(2));
            Assert.That(_hud.Recap.Lines[0], Does.StartWith("1st").And.Contain(host.DisplayName));
            Assert.That(_hud.Recap.Lines[1], Does.StartWith("2nd").And.Contain("Ann").And.EndWith(" 0"));

            Assert.That(arena.MayFire(host) || arena.MayFire(ann), Is.False, "nobody fires once the game is over");
            Assert.That(Turns.MayFire(Client, ann.Seat), Is.False);

            Assert.That(arena.StartMatch(arena.Setup), Is.True, "a rematch");
            yield return WaitUntil(() => turns.MatchPhase == MatchPhase.Aiming && !_hud.Recap.Showing, "the rematch never closed the recap");
            Assert.That(turns.ChampionSeat, Is.EqualTo(-1));
        }

        // UR-T25: starting a match gives the arena's live rules the setup's turn time, bounces, damage, boxes and drops.
        [UnityTest]
        public IEnumerator StartingAMatchAppliesTheSetupsRulesToTheArena()
        {
            double turnSeconds = Arena.MatchRules.TurnSeconds;
            int bounces = Arena.Rules.MaxBounces;
            bool railgunOn = Arena.ArsenalRules.InPlay(Weapon.Railgun);
            bool healthDrops = Arena.DropRules.Enabled(DropKind.Health);
            bool spins = Arena.HazardRules.RedirectorsSpin;
            try
            {
                Arena arena = null;
                Player host = null, ann = null;
                yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
                var setup = new MatchSetup { AiCount = 0, Map = 0, TurnSeconds = 45, MaxBounces = 7, WeaponDamage = 33, PauseRedirector = false };
                setup.SetWeaponOn(Weapon.Railgun, !railgunOn);
                setup.SetDropOn(DropKind.Health, !healthDrops);
                Assert.That(arena.StartMatch(setup), Is.True);

                Assert.That(Arena.MatchRules.TurnSeconds, Is.EqualTo(45));
                Assert.That(Arena.MatchRules.WeaponDamage, Is.EqualTo(33));
                Assert.That(Arena.Rules.MaxBounces, Is.EqualTo(7));
                Assert.That(Arena.ArsenalRules.InPlay(Weapon.Railgun), Is.EqualTo(!railgunOn));
                Assert.That(Arena.DropRules.Enabled(DropKind.Health), Is.EqualTo(!healthDrops));
                Assert.That(Arena.HazardRules.RedirectorsSpin, Is.True, "pause_redirector off: placed redirectors turn");
            }
            finally
            {
                Arena.Rules.MaxBounces = bounces;
                Arena.MatchRules.TurnSeconds = turnSeconds;
                Arena.ArsenalRules.SetInPlay(Weapon.Railgun, railgunOn);
                Arena.DropRules.SetEnabled(DropKind.Health, healthDrops);
                Arena.HazardRules.RedirectorsSpin = spins;
            }
        }

        [UnityTest]
        public IEnumerator AMatchOnAnotherMapIsPlayedThere_ByEveryPeer()
        {
            Arena arena = null;
            yield return Begin(a => arena = a, (h, c) => { });
            LevelView view = LevelView.Create(Arena.Level);
            try
            {
                Assert.That(Arena.MapIndex, Is.EqualTo(0), "a session opens on the practice range");
                Assert.That(arena.StartMatch(new MatchSetup { AiCount = 0, Map = 2 }), Is.True);
                Turns turns = null;
                yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.MapIndex == 2, "the client never heard which map");

                Level pillars = Maps.At(2).Build();
                Assert.That(Arena.MapIndex, Is.EqualTo(2));
                Assert.That(Arena.Level.Walls.Count, Is.EqualTo(pillars.Walls.Count));
                Assert.That(Arena.LevelFor(Client, 0).Walls.Count, Is.EqualTo(pillars.Walls.Count), "the client's shots bounce in the same room");
                Assert.That(view.Shown, Is.SameAs(Arena.Level), "the level view redrew the new map");
                yield return null;
                Assert.That(view.transform.childCount, Is.EqualTo(1 + pillars.Walls.Count + 2 * pillars.Redirectors.Count));
            }
            finally
            {
                Object.Destroy(view.gameObject);
            }
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

    // The rooms' screens on their own stack, no session needed.
    public class MenuScreenTests
    {
        private MenuStack _stack;

        [TearDown]
        public void RemoveTheStack()
        {
            if (_stack != null)
            {
                Object.Destroy(_stack.transform.root.gameObject);
                _stack = null;
            }
        }

        [UnityTest]
        public IEnumerator TheTitleGoesToTheMainMenu_OnPlay()
        {
            TitleScreen title = TitleScreen.Show(null, "test");
            yield return null;
            Assert.That(title.Showing, Is.True);
            Assert.That(title.Screen.Title, Is.EqualTo("RICOCHET"));
            bool dismissed = false;
            title.Dismissed += () => dismissed = true;
            Button(title.Screen, "Play").Click();
            yield return null;
            Assert.That(dismissed, Is.True);
            Assert.That(title == null, Is.True, "the title is gone once dismissed");
        }

        [UnityTest]
        public IEnumerator ThePlayersRoomSetsTheAiAndGoesOnToTheMapRoom_WhichSetsTheMapAndTheGoal()
        {
            _stack = MenuStack.Create();
            var setup = new MatchSetup();
            bool started = false;
            MenuScreen players = _stack.Push(MenuFlow.Title(Room.Players), s => Menus.BuildPlayers(s, setup, "Ann", null,
                from => from.Stack.Push(MenuFlow.Title(Room.MapRoom), m => Menus.BuildMap(m, setup, "Start", () => started = true))));
            yield return null;
            Assert.That(Texts(players), Has.Some.Contains("You play as Ann"));

            Button(players, "Next: Map").Click();
            yield return null;
            MenuScreen map = _stack.Current;
            Assert.That(map.Title, Is.EqualTo("Map"));
            MaSelect[] selects = map.GetComponentsInChildren<MaSelect>();
            Assert.That(selects.Length, Is.EqualTo(2), "the map and the goal");
            selects[0].Value = 2;
            selects[1].Value = 0;
            yield return null;
            Assert.That(setup.Map, Is.EqualTo(2));
            Assert.That(setup.Goal, Is.EqualTo(MatchSetup.OneRound));
            Assert.That(Texts(map), Has.Some.EqualTo(Maps.At(2).Description));

            Button(map, "Start").Click();
            Assert.That(started, Is.True);
        }

        // UR-T25: the settings room's rule controls: turn time, bounces and damage, a box and a starting count for each
        // weapon (never the cannon) and item, the drops' boxes and the pause-redirector box, all writing to the setup.
        [UnityTest]
        public IEnumerator TheSettingsScreenSetsTheMatchRules_OnTheSetup()
        {
            _stack = MenuStack.Create();
            var setup = new MatchSetup();
            MenuScreen settings = _stack.Push(MenuFlow.Title(Room.Settings), s => Menus.AddGameSettings(s, setup));
            yield return null;

            MaRange[] ranges = settings.GetComponentsInChildren<MaRange>();
            // After the announcements range: the turn time, the bounces and the damage.
            MaRange turn = ranges[1], bounces = ranges[2], damage = ranges[3];
            Assert.That((turn.Min, turn.Max), Is.EqualTo(((float)MatchSetup.MinTurnSeconds, (float)MatchSetup.MaxTurnSeconds)));
            Assert.That((bounces.Min, bounces.Max), Is.EqualTo((1f, (float)MatchSetup.MaxBouncesLimit)));
            Assert.That((damage.Min, damage.Max), Is.EqualTo((1f, (float)MatchSetup.MaxWeaponDamage)));
            Assert.That(turn.Value, Is.EqualTo(setup.TurnSeconds));
            turn.Value = 50;
            bounces.Value = 4;
            damage.Value = 25;
            Assert.That((setup.TurnSeconds, setup.MaxBounces, setup.WeaponDamage), Is.EqualTo((50, 4, 25)));

            Assert.That(Toggle(settings, ArsenalRules.Name(Weapon.Cannon) + " in play"), Is.Null, "the cannon is always in play");
            MaToggle railgun = Toggle(settings, ArsenalRules.Name(Weapon.Railgun) + " in play");
            Assert.That(railgun, Is.Not.Null);
            bool railgunWas = setup.WeaponOn(Weapon.Railgun);
            railgun.Value = !railgunWas;
            Assert.That(setup.WeaponOn(Weapon.Railgun), Is.EqualTo(!railgunWas));

            MaToggle teleporter = Toggle(settings, ArsenalRules.Name(Item.Teleporter) + " in play");
            Assert.That(teleporter, Is.Not.Null);
            teleporter.Value = !setup.ItemOn(Item.Teleporter);
            Assert.That(setup.ItemOn(Item.Teleporter), Is.EqualTo(teleporter.Value));

            foreach ((string label, DropKind kind) in new[] { ("Weapon drops", DropKind.Weapon), ("Item drops", DropKind.Item), ("Health drops", DropKind.Health), ("Random drops", DropKind.Powerup) })
            {
                MaToggle drops = Toggle(settings, label);
                Assert.That(drops, Is.Not.Null, label);
                drops.Value = !setup.DropOn(kind);
                Assert.That(setup.DropOn(kind), Is.EqualTo(drops.Value), label);
            }

            MaToggle pause = Toggle(settings, "Pause redirectors once placed");
            Assert.That(pause.Value, Is.EqualTo(setup.PauseRedirector));
            pause.Value = !setup.PauseRedirector;
            Assert.That(setup.PauseRedirector, Is.EqualTo(pause.Value));
        }

        [UnityTest]
        public IEnumerator HowToPlayTurnsItsSlidesBothWays_WithTheCounter()
        {
            _stack = MenuStack.Create();
            MenuScreen slides = _stack.Push(MenuFlow.Title(Room.HowToPlay), s => Menus.BuildHowToPlay(s));
            yield return null;
            int count = MenuFlow.Slides.Count;
            Assert.That(Texts(slides), Has.Some.EqualTo("1/" + count));
            Assert.That(Texts(slides), Has.Some.EqualTo(MenuFlow.Slides[0].Title));

            Button(slides, "Next").Click();
            Assert.That(Texts(slides), Has.Some.EqualTo("2/" + count));
            Assert.That(Texts(slides), Has.Some.EqualTo(MenuFlow.Slides[1].Title));
            Button(slides, "Last").Click();
            Button(slides, "Last").Click();
            Assert.That(Texts(slides), Has.Some.EqualTo(count + "/" + count), "Last from the first wraps to the last");
        }

        private static MaButton Button(MenuScreen screen, string label)
        {
            MaButton button = screen.GetComponentsInChildren<MaButton>().FirstOrDefault(b => b.Label == label);
            Assert.That(button, Is.Not.Null, "no " + label + " button on " + screen.Title);
            return button;
        }

        private static MaToggle Toggle(MenuScreen screen, string label) => screen.GetComponentsInChildren<MaToggle>().FirstOrDefault(t => t.Label == label);

        private static string[] Texts(MenuScreen screen) => screen.GetComponentsInChildren<MaText>().Select(t => t.Text).ToArray();
    }
}
