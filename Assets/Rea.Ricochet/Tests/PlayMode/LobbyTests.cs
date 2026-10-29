// The host's lobby on the wire and on screen (UR-T11): two of the kit's front ends, a host and a client, over real Unity
// Transport on loopback, driven through their menus the way a player would. Hosting from the map room opens the lobby with
// the join code and the roster, and the game's match box says what Start will begin and holds Start back while a match
// could not seat two; the client joins by that code from its main menu, the roster and the box follow, and the host's Start
// begins the match on the map room's map with both of them seated. Hosting from the main menu's Host opens the same lobby
// for free practice, and a client joins it by IP.
// The seat rules and the box's wording are tested without Unity: dotnet test Core.slnx (Tests/Core.Tests/LobbyTests.cs).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MindAttic.Components.Unity;
using NUnit.Framework;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using Unity.Multiplayer.UI;
using Unity.Multiplayer.UI.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class LobbyTests : NetSessionTest
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private InProcessRendezvousServer _rendezvous;
        private NetFrontEnd _host;
        private NetFrontEnd _client;
        private LobbyRoom _lobby;
        private Arena _arena;

        public LobbyTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
            RegisterPrefab(GamePrefabs.Hazard);
            FrontEndTesting.ForceMenus = true;
        }

        protected override IEnumerator OnSessionsTearDown()
        {
            _lobby?.Dispose();
            _lobby = null;
            foreach (GameObject go in _objects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            _objects.Clear();
            FrontEndTesting.ForceMenus = null;
            FrontEndTesting.CleanUpUi();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HostingFromPlayersStart_OpensTheLobbyWithTheCodeAndTheMatch_AClientJoinsByCode_AndStartPlaysTheMatchThere()
        {
            _rendezvous = StartRendezvous();
            yield return CreateFrontEnds(withCodes: true);

            var setup = new MatchSetup { AiCount = 0, Map = 1, Goal = MatchSetup.OneRound, Announcements = 0 };
            _ = _lobby.HostMatch(setup);
            yield return WaitUntil(() => _host.State == FrontEndState.Lobby, () => "the host never reached the lobby: " + _host.State);
            yield return WaitUntil(() => FrontEndTesting.LobbyCode(_host) != null, "the lobby never showed a join code");
            yield return WaitUntil(() => _lobby.Lines.Count > 0, "the lobby never showed the match box");

            string code = FrontEndTesting.LobbyCode(_host).Code;
            Assert.That(code, Is.EqualTo(Server.PublishedCodeText));
            Assert.That(Roster(), Is.EqualTo(new[] { "Ann" }));
            Assert.That(_lobby.Pending, Is.Not.Null);
            Assert.That(_lobby.Lines, Has.Some.EqualTo(Maps.At(1).Name + ", one round"));
            Assert.That(_lobby.Lines, Has.Some.StartsWith("1 player and no AI opponents: a match needs 2"));
            Assert.That(StartButton().Interactable, Is.False, "Start waits for a second seat");

            // Bob types the code (any case, no dashes) on his main menu.
            Assert.That(FrontEndTesting.HasButton(FrontEndTesting.MainScreen(_client), "Join by code"), Is.True);
            FrontEndTesting.Click(FrontEndTesting.MainScreen(_client), "Join by code");
            MenuScreen join = _client.MainMenu.Current;
            join.GetComponentInChildren<MaTextbox>().Text = code.Replace("-", string.Empty).ToLowerInvariant();
            FrontEndTesting.Click(join, "Join");
            yield return WaitUntil(() => _client.State == FrontEndState.InGame, () => "Bob never got in: " + _client.State + ", " + Client.LastDisconnect);

            yield return WaitUntil(() => Roster().Length == 2, "the roster never showed Bob");
            Assert.That(Roster(), Is.EqualTo(new[] { "Ann", "Bob" }));
            yield return WaitUntil(() => _lobby.Lines.Contains("2 players and no AI opponents: 2 of 8 seats"),
                () => "the match box never counted Bob: " + string.Join(" | ", _lobby.Lines));
            Assert.That(StartButton().Interactable, Is.True, "two seats: Start can begin the match");
            yield return WaitUntil(() => Player.In(Server).Count(p => !p.IsAi) == 2, "the server never spawned both players");

            FrontEndTesting.Click(FrontEndTesting.LobbyScreen(_host), "Start");
            Assert.That(_host.State, Is.EqualTo(FrontEndState.InGame));
            Assert.That(_lobby.Pending, Is.Null, "the lobby handed its match over");

            Turns turns = null;
            yield return WaitUntil(() => (turns = Turns.For(Client)) != null && turns.Round == 1, "Bob never got the match");
            Assert.That(turns.MapIndex, Is.EqualTo(3), "played on the map room's map");
            Assert.That(turns.GoalPoints, Is.EqualTo(MatchSetup.OneRound));
            Assert.That(_arena.Match, Is.Not.Null);
            yield return WaitUntil(() => Player.In(Client).Count(p => p.Seat >= 0) == 2, "Bob's client never saw both seated");
        }

        [UnityTest]
        public IEnumerator HostFromTheMainMenu_OpensTheLobbyForPractice_AClientJoinsByIp_AndStartGoesIntoPractice()
        {
            yield return CreateFrontEnds(withCodes: false);
            Assert.That(FrontEndTesting.Buttons(FrontEndTesting.MainScreen(_host)).Take(2), Is.EqualTo(new[] { "Host", "Join by IP" }));

            FrontEndTesting.Click(FrontEndTesting.MainScreen(_host), "Host");
            yield return WaitUntil(() => _host.State == FrontEndState.Lobby, () => "the host never reached the lobby: " + _host.State);
            yield return WaitUntil(() => _lobby.Lines.Count > 0, "the lobby never showed the match box");
            Assert.That(FrontEndTesting.LobbyCode(_host), Is.Null, "no join-code service: players join by IP");
            Assert.That(FrontEndTesting.LobbyScreen(_host).Subtitle, Does.Contain("join by IP").And.Contain(":" + Port));
            Assert.That(_lobby.Pending, Is.Null);
            Assert.That(_lobby.Lines, Has.Some.EqualTo(Lobby.PracticeLine));
            Assert.That(StartButton().Interactable, Is.True, "practice starts with anyone");

            FrontEndTesting.Click(FrontEndTesting.MainScreen(_client), "Join by IP");
            MenuScreen join = _client.MainMenu.Current;
            join.GetComponentInChildren<MaTextbox>().Text = "127.0.0.1:" + Port;
            FrontEndTesting.Click(join, "Join");
            yield return WaitUntil(() => _client.State == FrontEndState.InGame, () => "Bob never got in: " + _client.State + ", " + Client.LastDisconnect);
            yield return WaitUntil(() => Roster().Length == 2, "the roster never showed Bob");

            FrontEndTesting.Click(FrontEndTesting.LobbyScreen(_host), "Start");
            Assert.That(_host.State, Is.EqualTo(FrontEndState.InGame));
            yield return null;
            yield return null;
            Assert.That(_arena.Match, Is.Null, "no match: free practice");
        }

        // Both front ends, bound to the test's sessions, with their menus built; the arena served and the lobby attached.
        private IEnumerator CreateFrontEnds(bool withCodes)
        {
            _arena = Arena.Serve(Server, GamePrefabs.Dummy);
            _host = NewFrontEnd("Host front end", Server, () => HostConfig(withCodes), new FixedIdentitySource("Ann", new Color32(230, 80, 60, 255)));
            _client = NewFrontEnd("Client front end", Client, () => ClientConfig(withCodes), new FixedIdentitySource("Bob", new Color32(52, 152, 219, 255)));
            _lobby = LobbyRoom.Attach(_host, _host, s => _arena.StartMatch(s));
            yield return null;
            Assert.That(_host.HasMenus && _client.HasMenus, Is.True, "FrontEndTesting.ForceMenus builds the menus in batch mode");
            Assert.That(_host.State, Is.EqualTo(FrontEndState.Menu));
        }

        private NetFrontEnd NewFrontEnd(string name, NetSession session, System.Func<NetConfig> config, IPlayerIdentitySource identity)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            NetFrontEnd frontEnd = go.AddComponent<NetFrontEnd>();
            FrontEndTesting.Bind(frontEnd, session, config);
            frontEnd.Identity = identity;
            _objects.Add(go);
            go.SetActive(true);
            return frontEnd;
        }

        private NetConfig HostConfig(bool withCodes)
        {
            NetConfig config = Config();
            config.JoinCodes = JoinCodeProvider.None;
            if (withCodes)
            {
                config.JoinCodes = JoinCodeProvider.Rendezvous;
                config.RendezvousUrl = _rendezvous.Url;
                config.RendezvousSecret = _rendezvous.Secret;
            }

            return config;
        }

        private NetConfig ClientConfig(bool withCodes)
        {
            NetConfig config = Config();
            config.JoinCodes = JoinCodeProvider.None;
            if (withCodes)
            {
                config.JoinCodes = JoinCodeProvider.Auto;
                config.RendezvousUrl = _rendezvous.Url;
            }

            return config;
        }

        private string[] Roster()
        {
            MaList roster = FrontEndTesting.LobbyRoster(_host);
            return roster == null ? new string[0] : roster.Rows.Select(r => r.Text).ToArray();
        }

        private MaButton StartButton()
        {
            MenuScreen lobby = FrontEndTesting.LobbyScreen(_host);
            Assert.That(lobby, Is.Not.Null, "no lobby showing");
            MaButton start = lobby.GetComponentsInChildren<MaButton>(true).FirstOrDefault(b => b.Label == "Start");
            Assert.That(start, Is.Not.Null, "the lobby has no Start");
            return start;
        }
    }
}
