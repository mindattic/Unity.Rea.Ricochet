// The game's sound and the player's audio settings (UR-T10). The volumes and mutes come from the active MindAttic
// profile (Unity.Shared: Profiles.Current.Settings.Common's musicVolume, sfxVolume, muteMusic, muteSfx), the same four
// every MindAttic game reads, so they are set once and kept from session to session and from game to game; the
// Settings screen's sliders and toggles (Game adds them beside the kit's name and colour, which the kit already reads
// from and saves to the same profile) write straight back to it. The effects are Core's Tones rendered into clips once;
// a player's shot plays them as it fires, bounces and lands, heard from the Presentation feed's ShotFired, Bounced and
// ShotLanded events (UR-T65), not by being called from the player. Music plays from the music source at the music volume
// when the game gives it a clip. Nothing is created on a headless peer (a dedicated server or a bot).
using System.Collections.Generic;
using Ricochet.Core.Commands;
using Ricochet.Core.Menu;
using Unity.Shared;
using Unity.Shared.Core;
using UnityEngine;

namespace Ricochet
{
    public sealed class GameAudio : MonoBehaviour
    {
        private readonly Dictionary<SoundCue, AudioClip> _clips = new Dictionary<SoundCue, AudioClip>();
        private AudioSource _effects;
        private AudioSource _music;
        private AudioMix _mix = new AudioMix();

        /// <summary>The one in the scene (null on a headless peer, or before Game creates it).</summary>
        public static GameAudio Instance { get; private set; }

        /// <summary>The mix being applied now (a copy: change it through <see cref="Save"/>).</summary>
        public AudioMix Mix => _mix.Copy();

        /// <summary>The source sound effects play from (its volume is the mix's effect volume).</summary>
        public AudioSource Effects => _effects;

        /// <summary>The source music plays from (its volume is the mix's music volume).</summary>
        public AudioSource Music => _music;

        /// <summary>The cue played last, and how many have played (for tests and the log).</summary>
        public SoundCue LastCue { get; private set; }

        public int Played { get; private set; }

        /// <summary>
        /// The scene's audio, created once and read from the profile; null on a headless peer, which has no one to hear it.
        /// </summary>
        public static GameAudio Create()
        {
            if (Instance != null)
            {
                return Instance;
            }

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null && !Application.isEditor)
            {
                return null;
            }

            return new GameObject("Game audio").AddComponent<GameAudio>();
        }

        /// <summary>
        /// True while Unity.Shared's session mute is on (U.RICO-T71): the game was launched with -muted or
        /// MINDATTIC_CLI_MUTED=1 (tools/play.ps1 does both unless given -Sound). Nothing is heard for this run, and the
        /// player's saved settings are left exactly as they were.
        /// </summary>
        public static bool SessionMuted => Profiles.SessionMuted;

        /// <summary>
        /// The profile's audio settings (Unity.Shared's common block) as a mix: the values stored in settings.json, read
        /// past the session mute (which makes CommonSettings report 0 and muted), so a setting the player changes during a
        /// muted run saves the other three as they were, not as the mute shows them. <see cref="Apply"/> does the silencing.
        /// </summary>
        public static AudioMix ReadProfile()
        {
            CommonSettings common = Common;
            if (!Profiles.SessionMuted)
            {
                return new AudioMix
                {
                    MusicVolume = common.MusicVolume,
                    SfxVolume = common.SfxVolume,
                    MuteMusic = common.MuteMusic,
                    MuteSfx = common.MuteSfx,
                };
            }

            return new AudioMix
            {
                MusicVolume = Mathf.Clamp01(common.Data.GetFloat(CommonSettings.Keys.MusicVolume, CommonSettings.DefaultMusicVolume)),
                SfxVolume = Mathf.Clamp01(common.Data.GetFloat(CommonSettings.Keys.SfxVolume, CommonSettings.DefaultSfxVolume)),
                MuteMusic = common.Data.GetBool(CommonSettings.Keys.MuteMusic, false),
                MuteSfx = common.Data.GetBool(CommonSettings.Keys.MuteSfx, false),
            };
        }

        /// <summary>Writes <paramref name="mix"/> into the profile's settings.json. False when the file could not be written.</summary>
        public static bool WriteProfile(AudioMix mix)
        {
            CommonSettings common = Common;
            common.MusicVolume = mix.MusicVolume;
            common.SfxVolume = mix.SfxVolume;
            common.MuteMusic = mix.MuteMusic;
            common.MuteSfx = mix.MuteSfx;
            return Profiles.Current.TrySaveSettings();
        }

        private static CommonSettings Common
        {
            get
            {
                ProfilesBootstrap.Initialize();
                return Profiles.Current.Settings.Common;
            }
        }

        /// <summary>Re-reads the profile (another game, or another profile, may have changed it) and applies it.</summary>
        public void Reload()
        {
            try
            {
                Apply(ReadProfile());
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[GameAudio] Could not read the profile's audio settings (" + ex.Message + "); keeping " + _mix + ".");
            }
        }

        /// <summary>Applies <paramref name="mix"/> now and keeps it in the profile for every session after.</summary>
        public bool Save(AudioMix mix)
        {
            Apply(mix);
            bool saved;
            try
            {
                saved = WriteProfile(_mix);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[GameAudio] Could not save the audio settings: " + ex.Message);
                saved = false;
            }

            if (!saved)
            {
                Debug.LogWarning("[GameAudio] The audio settings last until the game closes: the profile could not be written.");
            }

            return saved;
        }

        /// <summary>
        /// The pause menu's Music toggle (UR-T43, obj_btn_music): the player's profile's muteMusic, set and saved, the other
        /// three values as settings.json holds them (re-read first, past any session mute). During a session mute (U.RICO-T71)
        /// the game stays silent whatever the toggle says, and the mute's forced values are never what gets saved.
        /// </summary>
        public bool SetMusicMuted(bool muted)
        {
            Reload();
            AudioMix mix = Mix;
            mix.MuteMusic = muted;
            return Save(mix);
        }

        /// <summary>The player's own music setting (the profile's, not the session mute): false when they muted it.</summary>
        public bool MusicOn => !_mix.MuteMusic;

        /// <summary>
        /// Applies <paramref name="mix"/> to the sources without saving it. During a session mute the sources and the
        /// listener stay silent whatever the mix says (AudioListener.volume is never saved, so this lasts for this run only).
        /// </summary>
        public void Apply(AudioMix mix)
        {
            _mix = (mix ?? new AudioMix()).Copy();
            bool session = Profiles.SessionMuted;
            if (session)
            {
                AudioListener.volume = 0f;
            }

            if (_effects != null)
            {
                _effects.volume = session ? 0f : _mix.EffectiveSfx;
                _effects.mute = session || _mix.MuteSfx;
            }

            if (_music != null)
            {
                _music.volume = session ? 0f : _mix.EffectiveMusic;
                _music.mute = session || _mix.MuteMusic;
            }
        }

        /// <summary>Plays <paramref name="cue"/> on the scene's audio, if there is one.</summary>
        public static void Play(SoundCue cue)
        {
            if (Instance != null)
            {
                Instance.PlayCue(cue);
            }
        }

        /// <summary>The clip <paramref name="cue"/> plays (rendered from Core's Tones the first time it is asked for).</summary>
        public AudioClip ClipFor(SoundCue cue)
        {
            if (!_clips.TryGetValue(cue, out AudioClip clip) || clip == null)
            {
                float[] samples = Tones.Render(cue);
                clip = AudioClip.Create("Ricochet " + cue, samples.Length, 1, Tones.SampleRate, false);
                clip.SetData(samples, 0);
                _clips[cue] = clip;
            }

            return clip;
        }

        private void PlayCue(SoundCue cue)
        {
            LastCue = cue;
            Played++;
            if (_effects != null && !Profiles.SessionMuted && !_mix.MuteSfx && _mix.SfxVolume > 0f)
            {
                _effects.PlayOneShot(ClipFor(cue));
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Presentation.Raised += OnEvent;
            _effects = gameObject.AddComponent<AudioSource>();
            _effects.playOnAwake = false;
            _effects.spatialBlend = 0f;
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }

            Reload();
        }

        // Back from another window (the player may have changed the profile in another MindAttic game): pick it up.
        private void OnApplicationFocus(bool focused)
        {
            if (focused && Instance == this)
            {
                Reload();
            }
        }

        // What the game shows and says: a shot fired, a contact passed, a volley landed (with a blast or without).
        private void OnEvent(Player who, GameEvent e)
        {
            switch (e)
            {
                case ShotFired _:
                    PlayCue(SoundCue.Fire); // UR-T10: at the profile's effect volume
                    break;
                case Bounced _:
                    PlayCue(SoundCue.Bounce);
                    break;
                case ShotLanded landed when landed.Volley != null:
                    PlayCue(landed.Volley.HasBlast ? SoundCue.Blast : SoundCue.Land);
                    break;
            }
        }

        private void OnDestroy()
        {
            Presentation.Raised -= OnEvent;
            if (Instance == this)
            {
                Instance = null;
            }

            foreach (AudioClip clip in _clips.Values)
            {
                if (clip != null)
                {
                    Destroy(clip);
                }
            }

            _clips.Clear();
        }
    }
}
