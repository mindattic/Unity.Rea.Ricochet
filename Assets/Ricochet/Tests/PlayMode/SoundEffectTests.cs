// U.RICO-T50: the original's recordings and the cues the game chooses, asserted from what GameAudio records (the cue and the
// volume it was given), never by listening. Every test runs muted for the session (Unity.Shared's session mute, as -muted or
// MINDATTIC_CLI_MUTED=1 does) except where it checks the profile's own volume, and then with the listener at 0.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Menu;
using Unity.Shared;
using Unity.Shared.Core;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet.Tests
{
    /// <summary>
    /// Lifts the session mute (a CLI's MINDATTIC_CLI_MUTED=1 included) for a test that checks the profile's own volumes,
    /// holding the listener at 0 so nothing is heard; disposing puts the variable, the mute and the listener back.
    /// </summary>
    internal sealed class SessionMute : System.IDisposable
    {
        private readonly string _env;
        private readonly bool _muted;
        private readonly float _listener;

        private SessionMute()
        {
            _env = System.Environment.GetEnvironmentVariable(Profiles.MutedEnvironmentVariable);
            _muted = Profiles.SessionMuted;
            _listener = AudioListener.volume;
            System.Environment.SetEnvironmentVariable(Profiles.MutedEnvironmentVariable, null);
            Profiles.SessionMuted = false;
            AudioListener.volume = 0f;
        }

        public static SessionMute Lifted() => new SessionMute();

        public void Dispose()
        {
            System.Environment.SetEnvironmentVariable(Profiles.MutedEnvironmentVariable, _env);
            Profiles.SessionMuted = _muted;
            AudioListener.volume = _listener;
        }
    }

    public class SoundEffectTests
    {
        private string _root;
        private GameAudio _audio;
        private float _listenerVolume;
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void AFreshProfile()
        {
            _listenerVolume = AudioListener.volume;
            _root = TempProfiles.Use();
            CommonSettings common = Profiles.Current.Settings.Common;
            common.SfxVolume = 0.45f;
            Profiles.Current.SaveSettings();
            Profiles.SessionMuted = true;
        }

        [TearDown]
        public void PutTheProfilesBack()
        {
            Profiles.SessionMuted = false;
            AudioListener.volume = _listenerVolume;
            foreach (GameObject made in _made)
            {
                if (made != null)
                {
                    Object.Destroy(made);
                }
            }

            _made.Clear();
            if (_audio != null)
            {
                Object.Destroy(_audio.gameObject);
            }

            TempProfiles.Release(_root);
        }

        private IEnumerator Audio()
        {
            _audio = GameAudio.Create();
            Assert.That(_audio, Is.Not.Null);
            yield return null;
        }

        private List<SoundCue> Cues(int from) => _audio.Requests.Skip(from).Select(r => r.Cue).ToList();

        [UnityTest]
        public IEnumerator UnderASessionMute_EveryCuePlaysAtVolumeZero_AndNothingIsSavedToSettings()
        {
            string settings = Profiles.Current.SettingsPath;
            string before = File.ReadAllText(settings);
            System.DateTime written = File.GetLastWriteTimeUtc(settings);
            yield return Audio();

            int played = _audio.Played;
            foreach (SoundCue cue in SoundBook.All)
            {
                GameAudio.Play(cue);
                Assert.That(_audio.LastCue, Is.EqualTo(cue));
                Assert.That(_audio.LastVolume, Is.EqualTo(0f), cue + " is chosen, at volume 0");
                GameAudio.PlayVoice(cue);
                Assert.That(_audio.LastVolume, Is.EqualTo(0f), cue + " on its own voice, at volume 0");
                GameAudio.Stop(cue);
            }

            Assert.That(_audio.Played - played, Is.EqualTo(SoundBook.Count * 2));
            Assert.That(_audio.Effects.volume, Is.EqualTo(0f));
            Assert.That(AudioListener.volume, Is.EqualTo(0f));
            Assert.That(File.ReadAllText(settings), Is.EqualTo(before), "settings.json is untouched");
            Assert.That(File.GetLastWriteTimeUtc(settings), Is.EqualTo(written));
        }

        [UnityTest]
        public IEnumerator EveryCuesRecording_IsTheOriginalsOwn_AndLoads()
        {
            yield return Audio();
            foreach (SoundCue cue in SoundBook.All)
            {
                AudioClip clip = _audio.ClipFor(cue);
                Assert.That(clip, Is.Not.Null, SoundBook.Recording(cue) + " loads from Resources/" + SoundBook.ResourceFolder);
                Assert.That(clip.name, Is.EqualTo(SoundBook.Recording(cue)));
                Assert.That(clip.length, Is.GreaterThan(0f), clip.name);
            }
        }

        [UnityTest]
        public IEnumerator UnmutedACuePlaysAtTheProfilesEffectVolume_AndTheProfilesMuteSilencesIt()
        {
            // The profile's own volume is checked with the session mute lifted, the listener held at 0: nothing is heard.
            using (SessionMute.Lifted())
            {
                yield return Audio();
                Assert.That(GameAudio.SessionMuted, Is.False);
                Assert.That(AudioListener.volume, Is.EqualTo(0f));
                _audio.Effects.mute = true; // and the source itself, so nothing sounds once the listener is restored

                GameAudio.Play(SoundCue.Shatter);
                Assert.That(_audio.LastVolume, Is.EqualTo(0.45f).Within(1e-4));
                AudioMix mix = _audio.Mix;
                mix.MuteSfx = true;
                _audio.Apply(mix); // applied, not saved
                GameAudio.Play(SoundCue.Crash);
                Assert.That(_audio.LastCue, Is.EqualTo(SoundCue.Crash));
                Assert.That(_audio.LastVolume, Is.EqualTo(0f));
                _audio.Effects.Stop();
            }
        }

        [UnityTest]
        public IEnumerator AShotSoundsAsTheOriginalsDid_FireBouncesAndLanding()
        {
            yield return Audio();
            var room = new Level(-3, -3, 3, 3);

            // A shell: its own fire sound, sfx_bounce_cannon off each wall, sfx_explode where it is spent.
            Volley shell = Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 10, 1, 2, 0, room, null);
            int from = _audio.Requests.Count;
            Presentation.Raise(null, new Cmd.ShotFired(1, 0, 1, Weapon.Cannon, 10, 1, 2, 0, shell));
            Presentation.Raise(null, new Cmd.Bounced(1, 1, 1, shell.Main.Contacts[0].Point));
            Presentation.Raise(null, new Cmd.ShotLanded(1, 0, 1, shell, null, false, -1));
            Assert.That(Cues(from), Is.EqualTo(new[] { SoundCue.FireCannon, SoundCue.BounceCannon, SoundCue.Explode }));

            // The shotgun: its pellets' bounces alternate between the two recordings that exist.
            Volley pellets = Arsenal.Fire(Weapon.Shotgun, Vec2.Zero, 10, 1, 2, 0, room, null);
            from = _audio.Requests.Count;
            Presentation.Raise(null, new Cmd.ShotFired(1, 0, 2, Weapon.Shotgun, 10, 1, 2, 0, pellets));
            for (int i = 0; i < 4; i++)
            {
                Presentation.Raise(null, new Cmd.Bounced(1, 2, 1, pellets.Paths[i].Contacts[0].Point));
            }

            List<SoundCue> heard = Cues(from);
            Assert.That(heard[0], Is.EqualTo(SoundCue.FireShotgun));
            Assert.That(heard.Skip(1).Distinct(), Is.EquivalentTo(new[] { SoundCue.BounceShotgun1, SoundCue.BounceShotgun2 }));

            // The laser: sfx_fire_laser, then sfx_loop_laser held until it lands.
            Volley laser = Arsenal.Fire(Weapon.Laser, Vec2.Zero, 10, 1, 2, 0, room, null);
            Presentation.Raise(null, new Cmd.ShotFired(1, 0, 3, Weapon.Laser, 10, 1, 2, 0, laser));
            Assert.That(_audio.LastCue, Is.EqualTo(SoundCue.FireLaser));
            Assert.That(GameAudio.Held(SoundCue.LoopLaser), Is.True);
            Assert.That(GameAudio.IsPlaying(SoundCue.FireLaser), Is.True, "playing, muted or not");
            Assert.That(GameAudio.IsPlaying(SoundCue.LoopLaser), Is.False, "the loop waits for the fire sound to end");
            Presentation.Raise(null, new Cmd.Bounced(3, 3, 1, laser.Main.Contacts[0].Point)); // another player's: no weapon of theirs known
            Presentation.Raise(null, new Cmd.Bounced(1, 3, 1, laser.Main.Contacts[0].Point));
            Assert.That(_audio.LastCue, Is.EqualTo(SoundCue.BounceLaser));
            Presentation.Raise(null, new Cmd.ShotLanded(1, 0, 3, laser, null, false, -1));
            Assert.That(GameAudio.Held(SoundCue.LoopLaser), Is.False);
            Assert.That(GameAudio.IsPlaying(SoundCue.LoopLaser), Is.False, "stopped as it lands");

            // A grenade beeps while it flies, and goes off.
            Volley grenade = Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 90, 0.3, 2, 0, room, null);
            from = _audio.Requests.Count;
            Presentation.Raise(null, new Cmd.ShotFired(1, 0, 4, Weapon.Grenade, 90, 0.3, 2, 0, grenade));
            Assert.That(Cues(from), Is.EqualTo(new[] { SoundCue.FireGrenade, SoundCue.GrenadeBeep }));
            Assert.That(GameAudio.Held(SoundCue.GrenadeBeep), Is.True);
            Presentation.Raise(null, new Cmd.ShotLanded(1, 0, 4, grenade, null, false, -1));
            Assert.That(GameAudio.Held(SoundCue.GrenadeBeep), Is.False);
            Assert.That(_audio.LastCue, Is.EqualTo(SoundCue.Explode));
            Assert.That(_audio.Requests.Skip(from).All(r => r.Volume == 0f), Is.True, "all chosen, none heard");
        }

        [UnityTest]
        public IEnumerator AHeldCue_PlaysAgainEachTimeItEnds_UntilLetGo()
        {
            yield return Audio();
            var holder = new object();
            GameAudio.Hold(SoundCue.Beep, holder);
            Assert.That(GameAudio.IsPlaying(SoundCue.Beep), Is.True);
            int played = _audio.Played;
            float length = GameAudio.Length(SoundCue.Beep);
            Assert.That(length, Is.GreaterThan(0f));
            yield return new WaitForSeconds(length + 0.1f);
            Assert.That(_audio.Played, Is.GreaterThan(played), "sounded again once it ended");
            GameAudio.Release(SoundCue.Beep, holder);
            Assert.That(GameAudio.IsPlaying(SoundCue.Beep), Is.False);
            played = _audio.Played;
            yield return new WaitForSeconds(length + 0.1f);
            Assert.That(_audio.Played, Is.EqualTo(played), "let go: not again");
        }

        [UnityTest]
        public IEnumerator TheKnockOutThatEndsTheRound_IsApplauded_NotSighedAt()
        {
            yield return Audio();
            int from = _audio.Requests.Count;
            GameAudio.PlaySettled(SoundCue.Sigh);
            GameAudio.PlaySettled(SoundCue.Applause);
            yield return null;
            Assert.That(Cues(from), Is.EqualTo(new[] { SoundCue.Applause }));

            from = _audio.Requests.Count;
            GameAudio.PlaySettled(SoundCue.Sigh);
            yield return null;
            Assert.That(Cues(from), Is.EqualTo(new[] { SoundCue.Sigh }), "a knock-out mid-round sighs");
        }

        [UnityTest]
        public IEnumerator TheMenus_Flick_Click_Select_AndRefuseAsTheOriginalsButtonsDid()
        {
            yield return Audio();
            var canvas = new GameObject("Menu canvas", typeof(Canvas));
            _made.Add(canvas);
            Button button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            button.transform.SetParent(canvas.transform, false);
            Slider slider = new GameObject("Slider", typeof(RectTransform), typeof(Slider)).GetComponent<Slider>();
            slider.transform.SetParent(canvas.transform, false);
            Toggle toggle = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle)).GetComponent<Toggle>();
            toggle.transform.SetParent(canvas.transform, false);

            UiSounds ui = _audio.Ui;
            Assert.That(ui, Is.Not.Null);
            int from = _audio.Requests.Count;
            ui.Observe(button, false);
            ui.Observe(button, true);
            ui.Observe(slider, true);
            ui.Observe(toggle, true);
            button.interactable = false;
            ui.Observe(button, true);
            ui.Observe(null, true);
            Assert.That(Cues(from), Is.EqualTo(new[]
            {
                SoundCue.Flick, SoundCue.Click,   // over the button, then pressed
                SoundCue.Flick, SoundCue.Select,  // over the slider (a stepper), pressed
                SoundCue.Flick, SoundCue.Click,   // over the toggle (a checkbox), pressed
                SoundCue.Disabled,                // the button, no longer pressable: no flick, sfx_disabled
            }));
        }
    }
}
