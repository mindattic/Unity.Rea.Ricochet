// U.RICO-T44: looks at the original's art in a running match and saves what the camera and the HUD show to
// Screenshots/ (gitignored): the Ready card in fnt_impact and fnt_mono with draw_text_outline's outlines, the four
// crates with their icons, a shell in flight, its spr_smallexplosion, and a grenade's spr_bigexplosion. Explicit: it
// needs graphics (Tools/unity.ps1 -Graphics), so it runs only when asked for by name:
//   powershell -File Tools/unity.ps1 -Graphics -Log art-showcase -Extra "-runTests -testPlatform PlayMode -testFilter Ricochet.Tests.ArtShowcaseTests -testResults Logs/TestResults-ArtShowcase.xml"
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
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
    [Explicit("Needs graphics; saves screenshots of the imported art in a match (U.RICO-T44)")]
    public class ArtShowcaseTests : NetSessionTest
    {
        private MatchHud _hud;
        private Camera _camera;
        private LevelView _level;

        public ArtShowcaseTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
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
            current.ServerFire(30f, 1f);
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
            other.ServerFire(200f, 1f);
            ShotView grenade = null;
            yield return WaitUntil(() => (grenade = Object.FindObjectsByType<ShotView>().FirstOrDefault(v => v.Flying && v.Flight.Volley.Weapon == Weapon.Grenade)) != null, "the grenade never flew", 5f);
            yield return WaitUntil(() => !grenade.Flying, "the grenade never went off", 10f);
            yield return new WaitForSeconds(0.4f);
            yield return Capture("grenade-explosion");
        }

        // Renders the camera with every overlay canvas moved onto it, into a 1920x1080 PNG under Screenshots/.
        private IEnumerator Capture(string name)
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
                string path = Path.Combine("Screenshots", "U.RICO-T44-" + name + ".png");
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
