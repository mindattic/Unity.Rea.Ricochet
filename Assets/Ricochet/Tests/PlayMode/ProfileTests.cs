// The MindAttic profile (UR-T10): a player's name, colour and audio settings come from the active Unity.Shared profile
// instead of being typed in every session. Each test points Unity.Shared at a fresh profiles folder in the temp
// directory (never the real one), writes a profile's settings.json, and checks the game reads it: the front end's
// identity source is the profile, the name and colour it carries reach every peer's Player, and GameAudio plays at the
// profile's volumes and writes a change back to settings.json, where the next session reads it.
// The mix and the synthesised effects are tested without Unity: dotnet test Core.slnx (Tests/Core.Tests/SoundTests.cs).
using System.Collections;
using System.IO;
using NUnit.Framework;
using Ricochet.Core.Menu;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using Unity.Multiplayer.UI;
using Unity.Shared;
using Unity.Shared.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    internal static class TempProfiles
    {
        /// <summary>Points Unity.Shared at a new, empty profiles folder; returns it.</summary>
        public static string Use()
        {
            string root = Path.Combine(Path.GetTempPath(), "ricochet-profiles-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Profiles.UseRoot(root);
            return root;
        }

        /// <summary>Back to the real profiles folder, and the temp one deleted.</summary>
        public static void Release(string root)
        {
            Profiles.Reset();
            ProfilesBootstrap.Initialize();
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                    // a file still open: the temp directory is cleaned up later anyway
                }
            }
        }
    }

    /// <summary>The name and colour the profile holds are the ones every peer sees on the player.</summary>
    public class ProfileIdentityTests : NetSessionTest
    {
        private string _root;

        public ProfileIdentityTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            _root = TempProfiles.Use();
            CommonSettings common = Profiles.Current.Settings.Common;
            common.DisplayName = "Bank Shot Betty";
            common.Color = new RgbColor(46, 204, 113);
            Profiles.Current.SaveSettings();

            // What NetFrontEnd does when the player hosts or joins: the identity is read from its source, the profile.
            IPlayerIdentitySource source = PlayerIdentitySources.Default;
            Client.Identity = new PlayerIdentity("betty-token", PlayerIdentitySources.CleanName(source.DisplayName),
                PlayerIdentitySources.ToPlayerColor(source.Color));
        }

        [TearDown]
        public void PutTheProfilesBack()
        {
            TempProfiles.Release(_root);
        }

        [UnityTest]
        public IEnumerator TheProfilesNameAndColour_AreThePlayersOnEveryPeer()
        {
            Assert.That(PlayerIdentitySources.ProfilesAvailable, Is.True, "Unity.Shared is installed, so the kit reads the profile");
            Assert.That(PlayerIdentitySources.Default, Is.InstanceOf<ProfileIdentitySource>());
            Assert.That(PlayerIdentitySources.Default.StoredIn, Does.Contain(Profiles.Current.Name));

            yield return StartAndJoinAll();
            var onHost = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return onHost;
            var onClient = WaitForSpawn<Player>(Client, owner: Client.LocalClientId);
            yield return onClient;

            yield return WaitUntil(() => onHost.Result.DisplayName == "Bank Shot Betty", () => "the host sees her as " + onHost.Result.DisplayName);
            yield return WaitUntil(() => onClient.Result.DisplayName == "Bank Shot Betty", () => "her client sees her as " + onClient.Result.DisplayName);
            Assert.That(onHost.Result.Color, Is.EqualTo(new Color32(46, 204, 113, 255)));
            Assert.That(onClient.Result.Color, Is.EqualTo(new Color32(46, 204, 113, 255)));
        }
    }

    /// <summary>GameAudio plays at the profile's volumes and keeps a change in the profile for the next session.</summary>
    public class ProfileAudioTests
    {
        private string _root;
        private GameAudio _audio;

        [SetUp]
        public void AFreshProfile()
        {
            _root = TempProfiles.Use();
            CommonSettings common = Profiles.Current.Settings.Common;
            common.SfxVolume = 0.3f;
            common.MusicVolume = 0.2f;
            common.MuteMusic = true;
            Profiles.Current.SaveSettings();
        }

        [TearDown]
        public void PutTheProfilesBack()
        {
            if (_audio != null)
            {
                Object.Destroy(_audio.gameObject);
            }

            TempProfiles.Release(_root);
        }

        [UnityTest]
        public IEnumerator TheProfilesVolumes_AreApplied_AndAChangeIsSavedForTheNextSession()
        {
            _audio = GameAudio.Create();
            Assert.That(_audio, Is.Not.Null);
            Assert.That(GameAudio.Instance, Is.SameAs(_audio));
            yield return null;

            Assert.That(_audio.Mix.SfxVolume, Is.EqualTo(0.3f).Within(1e-4));
            Assert.That(_audio.Effects.volume, Is.EqualTo(0.3f).Within(1e-4), "effects at the profile's sfxVolume");
            Assert.That(_audio.Music.volume, Is.EqualTo(0f), "music muted by the profile");
            Assert.That(_audio.Mix.MusicVolume, Is.EqualTo(0.2f).Within(1e-4), "the volume to come back to is kept");

            GameAudio.Play(SoundCue.Fire);
            Assert.That(_audio.LastCue, Is.EqualTo(SoundCue.Fire));
            Assert.That(_audio.Played, Is.EqualTo(1));
            AudioClip clip = _audio.ClipFor(SoundCue.Blast);
            Assert.That(clip.samples, Is.EqualTo(Tones.Render(SoundCue.Blast).Length));
            Assert.That(clip.frequency, Is.EqualTo(Tones.SampleRate));

            AudioMix mix = _audio.Mix;
            mix.SfxVolume = 0.65f;
            mix.MuteSfx = true;
            mix.MuteMusic = false;
            Assert.That(_audio.Save(mix), Is.True, "written to the profile");
            Assert.That(_audio.Effects.volume, Is.EqualTo(0f), "muted at once");
            Assert.That(_audio.Music.volume, Is.EqualTo(0.2f).Within(1e-4), "unmuted at once");

            // The next session: settings.json read again from disk.
            CommonSettings reread = Profiles.Current.ReloadSettings().Common;
            Assert.That(reread.SfxVolume, Is.EqualTo(0.65f).Within(1e-4));
            Assert.That(reread.MuteSfx, Is.True);
            Assert.That(reread.MuteMusic, Is.False);
            Assert.That(File.ReadAllText(Profiles.Current.SettingsPath), Does.Contain("\"sfxVolume\""));

            // Another MindAttic game changed the profile while this one was in the background: Reload picks it up.
            reread.SfxVolume = 0.1f;
            reread.MuteSfx = false;
            Profiles.Current.SaveSettings();
            _audio.Reload();
            Assert.That(_audio.Effects.volume, Is.EqualTo(0.1f).Within(1e-4));
        }
    }

    /// <summary>
    /// U.RICO-T71: launched with -muted or MINDATTIC_CLI_MUTED=1 (Unity.Shared's session mute), nothing is heard, and a
    /// setting changed during the muted run saves the player's real values, never the mute's zeros.
    /// </summary>
    public class SessionMuteAudioTests
    {
        private string _root;
        private GameAudio _audio;
        private float _listenerVolume;

        [SetUp]
        public void AFreshProfile_MutedForTheSession()
        {
            _listenerVolume = AudioListener.volume;
            _root = TempProfiles.Use();
            CommonSettings common = Profiles.Current.Settings.Common;
            common.SfxVolume = 0.4f;
            common.MusicVolume = 0.5f;
            Profiles.Current.SaveSettings();
            Profiles.SessionMuted = true;
        }

        [TearDown]
        public void PutTheProfilesBack()
        {
            Profiles.SessionMuted = false;
            AudioListener.volume = _listenerVolume;
            if (_audio != null)
            {
                Object.Destroy(_audio.gameObject);
            }

            TempProfiles.Release(_root);
        }

        [UnityTest]
        public IEnumerator ASessionMute_SilencesEverything_AndLeavesTheSavedSettingsAlone()
        {
            _audio = GameAudio.Create();
            yield return null;

            Assert.That(GameAudio.SessionMuted, Is.True);
            Assert.That(AudioListener.volume, Is.EqualTo(0f), "the listener is silenced for this run");
            Assert.That(_audio.Effects.volume, Is.EqualTo(0f));
            Assert.That(_audio.Music.volume, Is.EqualTo(0f));
            Assert.That(_audio.Mix.SfxVolume, Is.EqualTo(0.4f).Within(1e-4), "the mix holds the saved value, not the mute's 0");

            GameAudio.Play(SoundCue.Fire);
            Assert.That(_audio.Played, Is.EqualTo(1), "the cue is counted but not heard");

            // The player raises the music during the muted run: still silent, and only that change is saved.
            AudioMix mix = _audio.Mix;
            mix.MusicVolume = 0.7f;
            Assert.That(_audio.Save(mix), Is.True);
            Assert.That(_audio.Music.volume, Is.EqualTo(0f), "still silent this session");

            Profiles.SessionMuted = false;
            CommonSettings reread = Profiles.Current.ReloadSettings().Common;
            Assert.That(reread.MusicVolume, Is.EqualTo(0.7f).Within(1e-4));
            Assert.That(reread.SfxVolume, Is.EqualTo(0.4f).Within(1e-4), "not saved as the mute's 0");
            Assert.That(reread.MuteSfx, Is.False, "not saved as the mute's true");
            Assert.That(reread.MuteMusic, Is.False);
        }
    }
}
