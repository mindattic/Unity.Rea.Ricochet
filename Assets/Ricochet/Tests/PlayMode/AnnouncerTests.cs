// The announcer and the HUD on the wire (UR-T8): a host and one client start a match; the client sees the announcer's
// welcome in the personality the vote chose, its HUD slides that personality's card in with the line and shows the turn
// card (whose turn, their weapon and item); a shot at nobody gets a heckle naming its shooter, a knock-out a cheer naming
// both, and the shooter's score for the hit reaches the client; with the announcer off it says nothing.
// The announcer's rules and the scoring are tested without Unity: dotnet test Core.slnx (Tests/Core.Tests/AnnouncerTests.cs).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class AnnouncerTests : NetSessionTest
    {
        private int _weaponDamage;
        private double _endTurnSeconds;
        private MatchHud _hud;

        public AnnouncerTests() : base(Topology.Host, clients: 1) { }

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
            _endTurnSeconds = Arena.MatchRules.EndTurnSeconds;
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            Arena.MatchRules.WeaponDamage = _weaponDamage;
            Arena.MatchRules.EndTurnSeconds = _endTurnSeconds;
            if (_hud != null)
            {
                Object.Destroy(_hud.gameObject);
                _hud = null;
            }
        }

        [UnityTest]
        public IEnumerator TheClientSeesTheWelcome_InTheChosenVoice_AndItsHudShowsTheCardAndWhoseTurnItIs()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            _hud = MatchHud.Create(Client);

            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 0, AnnouncerVoice = 3, Announcements = 1 }), Is.True);
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.Announced >= 1, "the client never heard the welcome");
            Assert.That(turns.Voice, Is.EqualTo(3));
            Assert.That(turns.LastAnnouncement.Kind, Is.EqualTo(AnnouncementKind.Welcome));

            yield return WaitUntil(() => _hud.Overlay.AnnouncerShowing && _hud.Overlay.AnnouncerName == "Vlad", "the HUD never slid Vlad's card in");
            Assert.That(_hud.Overlay.AnnouncerLine, Is.Not.Empty.And.Not.Contain("{"));

            yield return WaitUntil(() => _hud.Overlay.TurnCardShowing && _hud.Overlay.TurnCardText.Contains(host.DisplayName),
                () => "the turn card never named the host: " + _hud.Overlay.TurnCardText);
            Assert.That(_hud.Overlay.TurnCardText, Does.Contain("Weapon: Cannon"));
            Assert.That(_hud.Overlay.TurnCardText, Does.Contain("Item:"));
            Assert.That(_hud.Overlay.TurnCardText, Does.Contain("Score 0"));
        }

        [UnityTest]
        public IEnumerator AShotAtNobodyIsHeckled_NamingItsShooter()
        {
            Arena.MatchRules.EndTurnSeconds = 5; // keep the turn from passing while we look
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 0, Announcements = 1 }), Is.True);
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.Announced == 1, "the client never heard the welcome");

            host.ServerFire(0f, 0f); // a short shot to the right, at nobody
            yield return WaitUntil(() => turns.Announced == 2, "no heckle for a shot that hit nobody", 10f);
            Assert.That(turns.LastAnnouncement.Kind, Is.EqualTo(AnnouncementKind.Lose));
            Assert.That(turns.LastAnnouncement.Subject, Is.EqualTo(host.Seat));
            string line = Announcer.Text(turns.LastAnnouncement, seat => Player.In(Client).First(p => p.Seat == seat).DisplayName);
            Assert.That(line, Does.Contain(host.DisplayName));
            Assert.That(Player.In(Client).All(p => p.Score == 0), Is.True, "a miss scores nothing");
        }

        [UnityTest]
        public IEnumerator AKnockOutIsCheered_AndTheHitScoresTheShooterForItsBouncesAndFlight_ForEveryone()
        {
            Arena.MatchRules.WeaponDamage = 1000; // one hit is a knock-out
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 0, Announcements = 1 }), Is.True);
            yield return WaitUntil(() => Turns.For(Client) != null && Turns.For(Client).Announced == 1, "the client never heard the welcome");

            string target = Arena.SeatTarget(ann.Seat);
            double? heading = BounceSolver.FindHeading(Arena.ToCore(host.Position), 1, target, Arena.Level, Arena.Targets(Server), Arena.Rules);
            Assert.That(heading, Is.Not.Null, "no aim from " + host.Position + " reaches Ann at " + ann.Position);
            host.ServerFire((float)heading.Value, 1f);
            Assert.That(host.LastShot.TargetId, Is.EqualTo(target));
            int expected = Arena.MatchRules.Points(host.LastShot.Bounces, host.LastVolley.Duration);

            Player hostOnClient = Player.In(Client).First(p => p.Seat == host.Seat);
            yield return WaitUntil(() => hostOnClient.Score > 0, "the client never saw the host score", 20f);
            Assert.That(hostOnClient.Score, Is.EqualTo(expected), "100 + 25 a bounce + 5 a tenth of a second in the air");

            Turns turns = Turns.For(Client);
            yield return WaitUntil(() => turns.Announced == 2, "no cheer for the knock-out");
            Assert.That(turns.LastAnnouncement.Kind, Is.EqualTo(AnnouncementKind.Win));
            Assert.That((turns.LastAnnouncement.Subject, turns.LastAnnouncement.Other), Is.EqualTo((host.Seat, ann.Seat)));
        }

        [UnityTest]
        public IEnumerator WithTheAnnouncerOffNothingIsSaid()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return Begin(a => arena = a, (h, c) => { host = h; ann = c; });
            Assert.That(arena.StartMatch(new MatchSetup { AiCount = 0, Announcements = 0 }), Is.True);
            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.Round == 1, "the client never got the match");

            host.ServerFire(0f, 0f);
            yield return new WaitForSeconds(1.5f);
            Assert.That(turns.Announced, Is.EqualTo(0));
            Assert.That(Turns.For(Server).Announced, Is.EqualTo(0));
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
