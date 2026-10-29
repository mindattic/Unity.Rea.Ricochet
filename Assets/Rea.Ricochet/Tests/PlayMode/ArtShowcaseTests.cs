// U.RICO-T44: looks at the original's art in a running match and saves what the camera and the HUD show to
// Screenshots/ (gitignored): the Ready card in fnt_impact and fnt_mono with draw_text_outline's outlines, the four
// crates with their icons, a shell in flight, its spr_smallexplosion, a grenade's spr_bigexplosion, and the tanks with
// one driving and leaving tracks (U.RICO-T45), and the level backdrop with a random level's shapes (U.RICO-T46); and the
// weapons and hazards (U.RICO-T47, TheWeaponsAndHazards), the hit and knock-out effects (U.RICO-T48,
// TheHitAndKnockOutEffects), and the HUD details (U.RICO-T49, TheHudDetails). Explicit: it needs graphics (Tools/unity.ps1 -Graphics), so it runs only when asked for by name:
//   powershell -File Tools/unity.ps1 -Graphics -Log art-showcase -Extra "-runTests -testPlatform PlayMode -testFilter Ricochet.Tests.ArtShowcaseTests -testResults Logs/TestResults-ArtShowcase.xml"
using System.Collections;
using System.IO;
using System.Linq;
using MindAttic.Components.Unity;
using NUnit.Framework;
using Ricochet.Core.Menu;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    [Explicit("Needs graphics; saves screenshots of the imported art in a match (U.RICO-T44)")]
    public class ArtShowcaseTests : NetSessionTest
    {
        private MatchHud _hud;
        private Camera _camera;
        private LevelView _level;
        private BackdropView _backdrop;

        public ArtShowcaseTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
            RegisterPrefab(GamePrefabs.Hazard);
            Client.Identity = new PlayerIdentity("art-token", "Ann", new PlayerColor(230, 80, 60));
        }

        [TearDown]
        public void Clear()
        {
            if (_hud != null)
            {
                Object.Destroy(_hud.gameObject);
            }

            if (_camera != null)
            {
                Object.Destroy(_camera.gameObject);
            }

            if (_level != null)
            {
                Object.Destroy(_level.gameObject);
            }

            if (_backdrop != null)
            {
                Object.Destroy(_backdrop.gameObject);
            }
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TheArtInAMatch()
        {
            Arena arena = null;
            Player host = null, ann = null;
            yield return StartAndJoinAll();
            arena = Arena.Serve(Server, GamePrefabs.Dummy);
            var hostSpawn = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
            yield return hostSpawn;
            var annSpawn = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return annSpawn;
            host = hostSpawn.Result;
            ann = annSpawn.Result;

            Level level = Arena.Level;
            _level = LevelView.Create(level);
            var cameraObject = new GameObject("Showcase camera");
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = (float)(level.MaxY - level.MinY) / 2f + 0.5f;
            _camera.transform.position = new Vector3((float)(level.MinX + level.MaxX) / 2f, (float)(level.MinY + level.MaxY) / 2f, -10f);
            _camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            RenderStyle.Current.ConfigureCamera(_camera);

            // U.RICO-T46: the backdrop (floor, clouds, spotlight, lights, border sprites) on the practice range, then on a
            // random level with its shapes drawn as their sprites.
            _backdrop = BackdropView.Create();
            _camera.orthographicSize = 7f;
            yield return new WaitForSeconds(0.3f);
            yield return Capture("backdrop-practice");
            Assert.That(arena.RerollLevel(), Is.True);
            yield return new WaitForSeconds(0.3f);
            Debug.Log("[Ricochet] Backdrop floor " + _backdrop.Background + ", " + _level.ShapesFromArt + " shapes from art, " + _backdrop.LightCount + " lights");
            yield return Capture("backdrop-random");

            _hud = MatchHud.Create(Client);
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            yield return WaitUntil(() => _hud.Ready != null && _hud.Ready.Showing && _hud.Ready.Alpha > 0.9f, "the Ready card never showed", 10f);
            yield return Capture("ready");

            yield return WaitUntil(() => Turns.For(Server).MatchPhase == MatchPhase.Aiming && !_hud.Ready.Showing, "the turn never opened", 15f);
            DropKind[] kinds = { DropKind.Weapon, DropKind.Item, DropKind.Health, (DropKind)(DropRules.KindCount - 1) };
            for (int i = 0; i < kinds.Length; i++)
            {
                Assert.That(arena.DropCrate(kinds[i], new Vector2(-6f + 4f * i, 2.5f)), Is.Not.Null, kinds[i].ToString());
            }

            yield return WaitUntil(() => Object.FindObjectsByType<Crate>().Count(c => c.Landed) >= 2 * kinds.Length, "the crates never landed", 10f);
            Assert.That(Object.FindObjectsByType<Crate>().All(c => c.DrawnFromArt), Is.True, "a crate fell back to its square");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("crates");

            // The host's shell, mid-flight and then where it lands.
            Player current = Turns.For(Server).Current == host.Seat ? host : ann;
            current.ServerFire(30f);
            yield return new WaitForSeconds(0.08f);
            yield return Capture("shell");
            yield return WaitUntil(() => current.LastVolley != null, "the shell never flew");
            ShotView view = Object.FindObjectsByType<ShotView>().First(v => v.Flight != null);
            yield return WaitUntil(() => !view.Flying, "the shell never landed", 10f);
            yield return new WaitForSeconds(0.15f);
            yield return Capture("shell-explosion");

            // The other seat's grenade, going off half a second after it is thrown.
            Player other = current == host ? ann : host;
            yield return WaitUntil(() => Turns.For(Server).Current == other.Seat && Turns.For(Server).MatchPhase == MatchPhase.Aiming, "the turn never passed", 20f);
            other.ServerCycleWeapon(-1);
            other.ServerCycleWeapon(-1);
            other.ServerCycleWeapon(-1);
            Assert.That(other.Loadout.Weapon, Is.EqualTo(Weapon.Grenade));
            other.Loadout.SetFuse(0.5);
            other.MirrorLoadout();
            other.ServerFire(200f);
            ShotView grenade = null;
            yield return WaitUntil(() => (grenade = Object.FindObjectsByType<ShotView>().FirstOrDefault(v => v.Flying && v.Flight.Volley.Weapon == Weapon.Grenade)) != null, "the grenade never flew", 5f);
            yield return WaitUntil(() => !grenade.Flying, "the grenade never went off", 10f);
            yield return new WaitForSeconds(0.4f);
            yield return Capture("grenade-explosion");

            // U.RICO-T45: the tanks in their colours, and the next seat driving, its hull turning after its turret and its
            // tracks left behind (driving forfeits the turn's shot, so this comes last).
            Turns turns = Turns.For(Server);
            yield return WaitUntil(() => turns.MatchPhase == MatchPhase.Aiming && turns.Current != other.Seat, "the turn never passed again", 20f);
            Player driver = Object.FindObjectsByType<Player>().First(p => p.IsLocalHuman && p.Seat == turns.Current);
            Assert.That(driver.Tank, Is.Not.Null, "the tank fell back to squares");
            driver.Aim(driver.Heading + 120f);
            yield return new WaitForSeconds(0.3f);
            float until = Time.time + 1.5f;
            while (Time.time < until)
            {
                driver.Drive(1);
                yield return new WaitForSeconds(0.1f);
            }

            driver.Drive(0);
            Assert.That(driver.Tank.TrackCount, Is.GreaterThan(0), "driving left no tracks");
            yield return Capture("tanks");
        }

        // U.RICO-T47: the weapons and hazards as the original drew them, in practice: the preview in draw_beam's dots, the
        // laser's beam and pulses, the railgun's rail and swirls, a vortex set off, a grenade's circle, the shield wall and
        // redirector items, and then a laid level's electric arc and laser gate in a match.
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TheWeaponsAndHazards()
        {
            yield return StartAndJoinAll();
            Arena arena = Arena.Serve(Server, GamePrefabs.Dummy, true);
            var hostSpawn = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
            yield return hostSpawn;
            var annSpawn = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return annSpawn;
            Player host = hostSpawn.Result;
            Player ann = annSpawn.Result;
            yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");

            Level level = Arena.Level;
            _level = LevelView.Create(level);
            var cameraObject = new GameObject("Showcase camera");
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 7f;
            _camera.transform.position = new Vector3((float)(level.MinX + level.MaxX) / 2f, (float)(level.MinY + level.MaxY) / 2f, -10f);
            _camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            RenderStyle.Current.ConfigureCamera(_camera);
            _backdrop = BackdropView.Create();

            // The host's own preview, drawn on the host's peer.
            yield return WaitUntil(() => host.PreviewBeam != null && host.PreviewBeam.DashCount > 0, "the preview was never drawn");
            yield return Capture("preview", "U.RICO-T47");

            ShotView view = Object.FindObjectsByType<ShotView>().First(v => v.Owner == host);
            host.ServerCycleWeapon(1);
            Assert.That(host.Loadout.Weapon, Is.EqualTo(Weapon.Laser));
            host.ServerFire(35f);
            yield return WaitUntil(() => view.LaserPulses > 1 || !view.Flying, "no pulses rode the beam", 10f);
            yield return Capture("laser", "U.RICO-T47");
            yield return WaitUntil(() => !view.Flying, "the laser never landed", 10f);
            yield return null;
            yield return Capture("laser-running-out", "U.RICO-T47");
            yield return WaitUntil(() => !view.LaserShowing && !host.ShotInFlight, "the beam never ran out", 10f);

            host.ServerCycleWeapon(-1);
            host.ServerCycleWeapon(-1);
            host.ServerCycleWeapon(-1);
            Assert.That(host.Loadout.Weapon, Is.EqualTo(Weapon.Railgun));
            host.ServerFire(35f);
            yield return WaitUntil(() => !view.Flying && view.RailShowing, "the rail never reached the border", 5f);
            yield return Capture("railgun", "U.RICO-T47");
            yield return WaitUntil(() => !view.RailShowing && !host.ShotInFlight, "the rail never faded", 10f);

            host.ServerCycleWeapon(1);
            Assert.That(host.Loadout.Weapon, Is.EqualTo(Weapon.Vortex));
            float clear = 60f; // a heading whose vortex flies a good way before anything stops it
            while (clear < 420f && (host.SolveVolley(Weapon.Vortex, clear, 2, 0).Main.End == ShotEndKind.Absorbed
                || host.SolveVolley(Weapon.Vortex, clear, 2, 0).Main.Length < 6))
            {
                clear += 5f;
            }

            host.ServerFire(clear);
            yield return new WaitForSeconds(0.25f);
            yield return Capture("vortex-flying", "U.RICO-T47");
            host.ServerDetonate();
            yield return WaitUntil(() => view.ImplosionMode == VortexMode.Waiting,
                () => "the vortex never filled out: " + view.ImplosionMode + ", imploding " + view.Imploding + ", flying " + view.Flying + ", blast "
                    + host.LastVolley.HasBlast + ", end " + host.LastVolley.Main.End + ", weapon " + host.LastVolley.Weapon, 10f);
            yield return Capture("vortex", "U.RICO-T47");
            yield return WaitUntil(() => view.ImplosionMode == VortexMode.Waiting2, "the vortex never blew", 10f);
            yield return Capture("vortex-blown", "U.RICO-T47");
            yield return WaitUntil(() => !view.Imploding && !host.ShotInFlight, "the vortex never finished", 10f);

            host.ServerCycleWeapon(-1);
            host.ServerCycleWeapon(-1);
            Assert.That(host.Loadout.Weapon, Is.EqualTo(Weapon.Grenade));
            host.Loadout.SetFuse(0.5);
            host.MirrorLoadout();
            host.ServerFire(200f);
            yield return WaitUntil(() => view.BlastShowing, "the grenade never went off", 10f);
            yield return new WaitForSeconds(0.4f);
            yield return Capture("grenade-circle", "U.RICO-T47");

            // Ann's shield wall and redirector, as they stand.
            Player annOnHerClient = Player.In(Client).First(p => p.OwnerClientId == Client.LocalClientId);
            annOnHerClient.CycleItem(1);
            yield return WaitUntil(() => ann.Loadout.Item == Item.ShieldWall, "the shield wall was never selected");
            ann.ServerUseItem(0f);
            annOnHerClient.CycleItem(-1);
            yield return WaitUntil(() => ann.Loadout.Item == Item.ExtraBounce, "the extra bounce was never selected");
            annOnHerClient.CycleItem(-1);
            yield return WaitUntil(() => ann.Loadout.Item == Item.Redirector, "the redirector was never selected");
            ann.ServerUseItem(90f);
            yield return new WaitForSeconds(0.1f);
            ann.ServerUseItem(90f); // U.RICO-T80: the second press stops it where it is
            yield return WaitUntil(() => LevelHazard.In(Client).Count(h => h.Ready && h.Art != null) == 2, "the items never stood", 5f);
            yield return new WaitForSeconds(1.6f);
            yield return Capture("items", "U.RICO-T47");

            // A laid level's arc and laser gate (the gate is on every other turn: shown on whichever turn has it on).
            Assert.That(arena.StartMatch(0), Is.True);
            yield return WaitUntil(() => LevelHazard.In(Client).Any(h => h.Ready && h.HazardKind == HazardKind.Electricity), "the arc was never laid", 10f);
            yield return new WaitForSeconds(1f);
            yield return Capture("hazards", "U.RICO-T47");
        }

        // U.RICO-T48: the hit and knock-out effects in a match on the practice range: the crates falling in their smoke and
        // landing with a firework (the powerup sparking), a shield wall shattering into glass, the "+N pts" and damage
        // figures with a knocked-out tank's blast, its smoke and the scorch marks left behind.
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TheHitAndKnockOutEffects()
        {
            int maxHealth = Arena.MatchRules.MaxHealth;
            TestMatches.WithoutReadyCard();
            try
            {
                yield return StartAndJoinAll();
                Arena arena = Arena.Serve(Server, GamePrefabs.Dummy);
                var hostSpawn = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
                yield return hostSpawn;
                var annSpawn = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
                yield return annSpawn;
                Player host = hostSpawn.Result;
                Player ann = annSpawn.Result;
                yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");

                Level level = Arena.Level;
                _level = LevelView.Create(level);
                var cameraObject = new GameObject("Showcase camera");
                _camera = cameraObject.AddComponent<Camera>();
                _camera.orthographic = true;
                _camera.orthographicSize = 4.5f;
                _camera.transform.position = new Vector3(-2f, -2.5f, -10f); // the two seats and the floor between them
                _camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
                _camera.clearFlags = CameraClearFlags.SolidColor;
                RenderStyle.Current.ConfigureCamera(_camera);
                _backdrop = BackdropView.Create();

                // Ann's shield wall, shattered by the host's shell.
                Player annOnHerClient = Player.In(Client).First(p => p.OwnerClientId == Client.LocalClientId);
                yield return WaitUntil(() => annOnHerClient.Weapons != 0, "her loadout never came");
                annOnHerClient.CycleItem(1);
                yield return WaitUntil(() => ann.Loadout.Item == Item.ShieldWall, "the shield wall was never selected");
                ann.ServerUseItem(0f);
                yield return WaitUntil(() => LevelHazard.In(Client).Any(h => h.Ready && h.Art != null), "the wall never stood", 5f);
                yield return new WaitForSeconds(1.2f);
                host.ServerFire(180f);
                yield return WaitUntil(() => FieldEffects.Shards > 0, "the glass never flew", 10f);
                yield return new WaitForSeconds(0.1f);
                yield return Capture("glass", "U.RICO-T48");
                yield return WaitUntil(() => !host.ShotInFlight, "the shell never landed", 10f);

                // The crates in a match: two falling in their smoke, then down with the powerup's sparks.
                Arena.MatchRules.MaxHealth = 1;
                Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
                yield return WaitUntil(() => Turns.For(Client) != null && Turns.For(Server).MatchPhase == MatchPhase.Aiming, "the match never opened", 10f);
                Assert.That(arena.DropCrate(DropKind.Weapon, new Vector2(-6f, 0f)), Is.Not.Null);
                Assert.That(arena.DropCrate((DropKind)(DropRules.KindCount - 1), new Vector2(1.5f, -1f)), Is.Not.Null);
                yield return new WaitForSeconds(0.4f);
                yield return Capture("crates-falling", "U.RICO-T48");
                yield return WaitUntil(() => Object.FindObjectsByType<Crate>().Count(c => c.LandedHere) >= 2, "the crates never landed here", 10f);
                yield return null;
                yield return Capture("crates-landing", "U.RICO-T48");
                yield return new WaitForSeconds(1f);
                yield return Capture("crates-sparks", "U.RICO-T48");

                // The host's shell knocks Ann out: the points and damage float, her tank blows up and smokes.
                Player current = Turns.For(Server).Current == host.Seat ? host : ann;
                Player target = current == host ? ann : host;
                Vector2 toTarget = target.Position - current.Position;
                current.ServerFire(Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg);
                yield return WaitUntil(() => FieldEffects.KnockOuts > 0, "the tank never blew up", 10f);
                yield return new WaitForSeconds(0.15f);
                yield return Capture("knock-out", "U.RICO-T48");
                yield return WaitUntil(() => FieldEffects.Flames > 0, "it never smoked", 5f);
                yield return new WaitForSeconds(0.8f);
                yield return Capture("knock-out-smoke", "U.RICO-T48");
            }
            finally
            {
                Arena.MatchRules.MaxHealth = maxHealth;
                TestMatches.StockReadyCard();
                FieldEffects.Clear();
                GmlParticles.Clear();
            }
        }

        // U.RICO-T49: the HUD details in a match on the practice range with two AI: the icons with their counts and the infinity
        // sign, the slots' health bars, marks and turn arrow, the guide ring, angle and turn arrow at the current tank, the
        // extra-bounce indicator, and a hover's text.
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TheHudDetails()
        {
            TestMatches.WithoutReadyCard();
            try
            {
                yield return StartAndJoinAll();
                Arena arena = Arena.Serve(Server, GamePrefabs.Dummy);
                var hostSpawn = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
                yield return hostSpawn;
                var annSpawn = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
                yield return annSpawn;
                yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");

                Level level = Arena.Level;
                _level = LevelView.Create(level);
                var cameraObject = new GameObject("Showcase camera");
                _camera = cameraObject.AddComponent<Camera>();
                _camera.orthographic = true;
                _camera.orthographicSize = 7f;
                _camera.transform.position = new Vector3(0f, 0f, -10f);
                _camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
                _camera.clearFlags = CameraClearFlags.SolidColor;
                RenderStyle.Current.ConfigureCamera(_camera);
                // No backdrop: its border sprites would sort over the HUD once Capture moves the canvases onto the camera.

                _hud = MatchHud.Create(Client);
                Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange(2)), Is.True);
                yield return WaitUntil(() => Turns.For(Client) != null && Turns.For(Server).MatchPhase == MatchPhase.Aiming && _hud.Icons.Showing, "the match never opened", 10f);
                Player host = hostSpawn.Result;
                host.ServerAim(60f);
                host.ServerCycleWeapon(1);
                host.ServerUseItem(60f); // an extra bounce on this turn
                yield return new WaitForSeconds(0.6f);
                yield return Capture("hud", "U.RICO-T49");

                // Close in on the current tank: the turn arrow, the guide, the angle and its degree sign, the extra bounce.
                Vector3 keep = _camera.transform.position;
                _camera.orthographicSize = 2.5f;
                _camera.transform.position = new Vector3(host.Position.x, host.Position.y, -10f);
                yield return Capture("hud-tank", "U.RICO-T49");
                _camera.orthographicSize = 7f;
                _camera.transform.position = keep;

                var hover = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                UnityEngine.EventSystems.ExecuteEvents.Execute(_hud.Icons.IconObject((int)Weapon.Vortex), hover, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
                yield return null;
                yield return Capture("hud-hover", "U.RICO-T49");
            }
            finally
            {
                TestMatches.StockReadyCard();
            }
        }

        // U.RICO-T78: the menus in the original's buttons, checkboxes, input box and arrows; the title room; the controls,
        // how-to-play and credits pictures; the pause menu; and in a match the pad's tiny buttons (and U.RICO-T51's portrait).
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TheMenusAndPictures()
        {
            var cameraObject = new GameObject("Showcase camera");
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            GmlMenuSkin.Ensure();

            TitleScreen title = TitleScreen.Show(null, "showcase");
            yield return new WaitForSeconds(0.3f);
            yield return Capture("title", "U.RICO-T78");
            Object.Destroy(title.gameObject);

            MenuStack stack = MenuStack.Create(null, backdrop: true);
            try
            {
                var setup = new MatchSetup();
                stack.Push("Settings", s =>
                {
                    s.AddTextbox("Name", "Ann").Text = "Ann";
                    Menus.AddGameSettings(s, setup);
                });
                yield return new WaitForSeconds(0.3f);
                yield return Capture("settings", "U.RICO-T78");
                stack.Push(MenuFlow.Title(Room.Controls), Menus.BuildControls);
                yield return new WaitForSeconds(0.3f);
                yield return Capture("controls", "U.RICO-T78");
                stack.Push(MenuFlow.Title(Room.HowToPlay), s => Menus.BuildHowToPlay(s));
                yield return new WaitForSeconds(0.3f);
                yield return Capture("howtoplay", "U.RICO-T78");
                stack.Push(MenuFlow.Title(Room.Credits), Menus.BuildCredits);
                yield return new WaitForSeconds(0.3f);
                yield return Capture("credits", "U.RICO-T78");
            }
            finally
            {
                Object.Destroy(stack.transform.root.gameObject);
            }

            TestMatches.WithoutReadyCard();
            try
            {
                yield return StartAndJoinAll();
                Arena arena = Arena.Serve(Server, GamePrefabs.Dummy);
                yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");
                _camera.orthographicSize = 7f;
                _camera.transform.position = new Vector3(0f, 0f, -10f);
                RenderStyle.Current.ConfigureCamera(_camera);
                _level = LevelView.Create(Arena.Level);
                _hud = MatchHud.Create(Client);
                MatchSetup match = TestMatches.OnThePracticeRange(2);
                match.AnnouncerVoice = 0; // Kurt Sly, his own spr_profile_sly
                match.Announcements = 1;
                Assert.That(arena.StartMatch(match), Is.True);
                yield return WaitUntil(() => _hud.Overlay.AnnouncerShowing && _hud.Pad.Showing, "the announcer's card never came in", 10f);
                yield return Capture("match", "U.RICO-T78");
                Assert.That(arena.SetPaused(true), Is.True);
                yield return new WaitForSeconds(0.5f);
                yield return Capture("paused", "U.RICO-T78");
            }
            finally
            {
                TestMatches.StockReadyCard();
            }
        }

        // Renders the camera with every overlay canvas moved onto it, into a 1920x1080 PNG under Screenshots/.
        private IEnumerator Capture(string name, string ticket = "U.RICO-T44")
        {
            yield return null; // WaitForEndOfFrame never resumes in batch mode
            Debug.Log("[Ricochet] Capturing " + name);
            const int Width = 1920, Height = 1080;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Canvas[] overlays = Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            try
            {
                foreach (Canvas canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = _camera;
                    canvas.planeDistance = 1f;
                }

                _camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Screenshots");
                string path = Path.Combine("Screenshots", ticket + "-" + name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.Destroy(image);
                Debug.Log("[Ricochet] Saved " + Path.GetFullPath(path));
            }
            finally
            {
                RenderTexture.active = null;
                _camera.targetTexture = null;
                foreach (Canvas canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }

                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
