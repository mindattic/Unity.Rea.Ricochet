// The game's sound and the player's audio settings (UR-T10). The volumes and mutes come from the active MindAttic
// profile (Unity.Shared: Profiles.Current.Settings.Common's musicVolume, sfxVolume, muteMusic, muteSfx), the same four
// every MindAttic game reads, so they are set once and kept from session to session and from game to game; the
// Settings screen's sliders and toggles (Game adds them beside the kit's name and colour, which the kit already reads
// from and saves to the same profile) write straight back to it. Music plays from the music source at the music volume
// when the game gives it a clip. Nothing is created on a headless peer (a dedicated server or a bot).
//
// U.RICO-T50: the effects are the original's own recordings (Resources/Ricochet/Sound, Tools/import-gml-sound.ps1), one per
// Core SoundCue; Core's SoundBook says which cue each moment plays and at what volume. From the Presentation feed: a shot's
// fire sound per weapon (ShotFired), its bounces per weapon and sfx_bend off a redirector (Bounced), sfx_explode where it
// lands (ShotLanded), the laser's loop once its fire sound ends and the grenade's beep while each flies. The views play the
// rest where they draw it (crates, the vortex, the Ready card, the HUD, the owner's keys), and the server's flourish
// carries the events no view draws (items, a shrink, a teleport, a pickup, the countdown, a knock-out) to every peer.
//
// Sounds the original checked with audio_is_playing (the engine, the turn, the loops, the sonar) play on a voice of their
// own whose length is kept here, so "is it still playing" answers the same muted (U.RICO-T71) as heard. Every cue chosen
// is recorded with the volume it was given (Requests, LastCue, LastVolume), so tests see what would be heard without
// anything being heard.
//
// U.RICO-T27: the music is the original's eight music_* tracks (Resources/Ricochet/Music, streamed). Each new round
// (randomize_level.gml) and each new turn (next_player.gml) of a match picks one of play_random_music.gml's five at random
// and plays it on the music source, which carries the mix's music volume and mute (0 under a session mute, where it still
// plays but is never heard and nothing is saved). Tests read the choice (MusicRequests, CurrentTrack, Music.volume).
//
// U.RICO-T51: the announcer speaks. When HudOverlay slides a card in, Announce plays the recording Core's Announcer picked for
// the line it drew (Resources/Ricochet/Voice/sfx_<key>_<kind><n>) on a source of its own at the effect volume, cutting off
// any line still playing, and says how long it is heard so the card holds while it plays (obj_announcer). Muted, nothing is
// heard and the written caption carries the line alone. Tests read the choice (AnnouncementRequests).
using System.Collections.Generic;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Unity.Shared;
using Unity.Shared.Core;
using UnityEngine;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet
{
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>How many recent requests <see cref="Requests"/> keeps.</summary>
        public const int RequestsKept = 128;

        private readonly Dictionary<SoundCue, AudioClip> _clips = new Dictionary<SoundCue, AudioClip>();
        private readonly Dictionary<SoundCue, AudioSource> _voices = new Dictionary<SoundCue, AudioSource>();
        private readonly Dictionary<SoundCue, float> _busyUntil = new Dictionary<SoundCue, float>();
        private readonly Dictionary<SoundCue, HashSet<object>> _holds = new Dictionary<SoundCue, HashSet<object>>();
        private readonly Dictionary<SoundCue, SoundCue?> _holdAfter = new Dictionary<SoundCue, SoundCue?>();
        private readonly List<SoundCue> _pending = new List<SoundCue>();
        private readonly List<SoundRequest> _requests = new List<SoundRequest>();
        private readonly Dictionary<int, Weapon> _shotWeapons = new Dictionary<int, Weapon>();
        private readonly Dictionary<int, Volley> _shotVolleys = new Dictionary<int, Volley>();
        private AudioSource _effects;
        private AudioSource _music;
        private AudioSource _announcer;
        private readonly Dictionary<string, AudioClip> _voiceLines = new Dictionary<string, AudioClip>();
        private readonly List<AnnouncementRequest> _announcementRequests = new List<AnnouncementRequest>();
        private AudioMix _mix = new AudioMix();
        private int _pellets;
        private readonly List<MusicRequest> _musicRequests = new List<MusicRequest>();
        private readonly Dictionary<MusicTrack, AudioClip> _tracks = new Dictionary<MusicTrack, AudioClip>();
        private Turns _turns;
        private bool _musicSeen;
        private float _nextTurnsSearch;
        private int _lastRound = -1;
        private int _lastTurn = -1;
        private float _jukeboxWait;

        /// <summary>The menus' sounds, beside this.</summary>
        public UiSounds Ui => GetComponent<UiSounds>();

        /// <summary>A cue the game chose, and the volume it was given (0 when muted or session-muted).</summary>
        public readonly struct SoundRequest
        {
            public SoundRequest(SoundCue cue, float volume)
            {
                Cue = cue;
                Volume = volume;
            }

            public SoundCue Cue { get; }

            public float Volume { get; }

            public override string ToString() => Cue + " @ " + Volume.ToString("0.00");
        }

        /// <summary>A track the game chose, and the volume it was given (0 when muted or session-muted).</summary>
        public readonly struct MusicRequest
        {
            public MusicRequest(MusicTrack track, float volume)
            {
                Track = track;
                Volume = volume;
            }

            public MusicTrack Track { get; }

            public float Volume { get; }

            public override string ToString() => Track + " @ " + Volume.ToString("0.00");
        }

        /// <summary>The track chosen last (play_random_music.gml's current_track), or null before any.</summary>
        public MusicTrack? CurrentTrack { get; private set; }

        /// <summary>The most recent tracks chosen, oldest first (at most <see cref="RequestsKept"/>).</summary>
        public IReadOnlyList<MusicRequest> MusicRequests => _musicRequests;

        /// <summary>The one in the scene (null on a headless peer, or before Game creates it).</summary>
        public static GameAudio Instance { get; private set; }

        /// <summary>The mix being applied now (a copy: change it through <see cref="Save"/>).</summary>
        public AudioMix Mix => _mix.Copy();

        /// <summary>The source one-shot sound effects play from (its volume is the mix's effect volume).</summary>
        public AudioSource Effects => _effects;

        /// <summary>The source music plays from (its volume is the mix's music volume).</summary>
        public AudioSource Music => _music;

        /// <summary>The cue played last, the volume it was given, and how many have played (for tests and the log).</summary>
        public SoundCue LastCue { get; private set; }

        public float LastVolume { get; private set; }

        public int Played { get; private set; }

        /// <summary>The most recent cues chosen, oldest first (at most <see cref="RequestsKept"/>).</summary>
        public IReadOnlyList<SoundRequest> Requests => _requests;

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

        /// <summary>The volume the music is given now (MusicBook.Volume: the mix's music volume, 0 when muted or session-muted).</summary>
        public float MusicLevel => MusicBook.Volume(_mix, Profiles.SessionMuted);

        /// <summary>The recording <paramref name="track"/> plays (loaded, streamed, from Resources/Ricochet/Music the first time).</summary>
        public AudioClip TrackFor(MusicTrack track)
        {
            if (!_tracks.TryGetValue(track, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(MusicBook.ResourcePath(track));
                if (clip == null)
                {
                    Debug.LogWarning("[GameAudio] The track " + MusicBook.Recording(track) + " is missing from Resources/" + MusicBook.ResourceFolder + "; it plays nothing.");
                }

                _tracks[track] = clip;
            }

            return clip;
        }

        /// <summary>
        /// play_random_music.gml: picks one of the five tracks at random and starts it (replacing the one playing), at the
        /// mix's music volume (0 when muted or session-muted, when it is chosen all the same and heard by no one).
        /// </summary>
        public MusicTrack PlayRandomMusic(bool loop = true)
        {
            MusicTrack track = MusicBook.Pick(Random.Range(1, MusicBook.Rotation.Count + 1));
            PlayMusic(track, loop);
            return track;
        }

        /// <summary>True while a track is on the music source (heard or not: a muted track still plays, at volume 0).</summary>
        public bool MusicPlaying => _music != null && _music.isPlaying;

        /// <summary>Stops the track playing, if any (obj_titlescreen's audio_stop_music as the title opens).</summary>
        public void StopMusic()
        {
            if (_music != null)
            {
                _music.Stop();
            }

            CurrentTrack = null;
            _jukeboxWait = 0f;
        }

        /// <summary>
        /// U.RICO-T52, obj_jukebox on the titlescreen and mainmenu rooms: each step, when no track plays, play_random_music
        /// starts a random one of the five, once through (audio_play_music(track, 0): unlooped), so the menus run through track
        /// after track. A track that cannot be heard because its recording is missing is not asked for again for a second.
        /// </summary>
        public void Jukebox()
        {
            if (_music == null || _music.isPlaying || Time.unscaledTime < _jukeboxWait)
            {
                return;
            }

            PlayRandomMusic(loop: false);
            if (!_music.isPlaying)
            {
                _jukeboxWait = Time.unscaledTime + 1f;
            }
        }

        /// <summary>Starts <paramref name="track"/> on the music source, looping, at the music volume now in force.</summary>
        public void PlayMusic(MusicTrack track) => PlayMusic(track, true);

        /// <summary>
        /// Starts <paramref name="track"/> on the music source at the music volume now in force, looping or (the jukebox's)
        /// once through.
        /// </summary>
        public void PlayMusic(MusicTrack track, bool loop)
        {
            float volume = MusicLevel;
            CurrentTrack = track;
            _musicRequests.Add(new MusicRequest(track, volume));
            if (_musicRequests.Count > RequestsKept)
            {
                _musicRequests.RemoveAt(0);
            }

            if (_music == null)
            {
                return;
            }

            AudioClip clip = TrackFor(track);
            Apply(_mix); // the source's volume and mute are the mix's
            if (clip == null)
            {
                return;
            }

            _music.clip = clip;
            _music.loop = loop;
            _music.Play();
        }

        /// <summary>The volume a cue is given now (SoundBook.Volume: the mix's effect volume, 0 when muted or session-muted).</summary>
        public float CueVolume => SoundBook.Volume(_mix, Profiles.SessionMuted);

        // --- the announcer's voice (U.RICO-T51) ------------------------------------------------------------------------

        /// <summary>A voice line the game chose, and the volume it was given (0 when muted or session-muted).</summary>
        public readonly struct AnnouncementRequest
        {
            public AnnouncementRequest(string recording, float volume, float seconds)
            {
                Recording = recording;
                Volume = volume;
                Seconds = seconds;
            }

            /// <summary>The recording's name (sfx_sly_win3).</summary>
            public string Recording { get; }

            public float Volume { get; }

            /// <summary>How long it is heard for (0 when it is not: muted, or the recording is missing).</summary>
            public float Seconds { get; }

            public override string ToString() => Recording + " @ " + Volume.ToString("0.00");
        }

        /// <summary>The most recent voice lines chosen, oldest first (at most <see cref="RequestsKept"/>).</summary>
        public IReadOnlyList<AnnouncementRequest> AnnouncementRequests => _announcementRequests;

        /// <summary>The source the announcer speaks from (one line at a time).</summary>
        public AudioSource AnnouncerSource => _announcer;

        /// <summary>
        /// The announcer says <paramref name="announcement"/> in <paramref name="voice"/>: the recording Core's Announcer picked
        /// for the line it drew, at the effect volume (every voice line's GameMaker volume is 1). A line still playing is cut
        /// off first (play_random_announcement's audio_stop_sound(current_announcement)). Returns the seconds it is heard
        /// for, 0 when nobody hears it (muted, session-muted, no audio, or the recording is missing), so the card knows how
        /// long to hold its caption.
        /// </summary>
        public static float Announce(AnnouncerVoice voice, Announcement announcement)
        {
            return Instance != null && voice != null ? Instance.Speak(voice, announcement) : 0f;
        }

        /// <summary>The recording for <paramref name="voice"/>'s <paramref name="announcement"/> (loaded from Resources/Ricochet/Voice), or null.</summary>
        public AudioClip VoiceClip(AnnouncerVoice voice, Announcement announcement)
        {
            string recording = Announcer.Recording(voice, announcement);
            if (!_voiceLines.TryGetValue(recording, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(Announcer.ResourcePath(voice, announcement));
                if (clip == null)
                {
                    Debug.LogWarning("[GameAudio] The voice line " + recording + " is missing from Resources/" + Announcer.VoiceFolder + "; its caption stands alone.");
                }

                _voiceLines[recording] = clip;
            }

            return clip;
        }

        private float Speak(AnnouncerVoice voice, Announcement announcement)
        {
            float volume = CueVolume;
            AudioClip clip = VoiceClip(voice, announcement);
            float seconds = clip != null && volume > 0f ? clip.length : 0f;
            _announcementRequests.Add(new AnnouncementRequest(Announcer.Recording(voice, announcement), volume, seconds));
            if (_announcementRequests.Count > RequestsKept)
            {
                _announcementRequests.RemoveAt(0);
            }

            if (_announcer == null)
            {
                return 0f;
            }

            _announcer.Stop();
            if (seconds <= 0f)
            {
                return 0f;
            }

            _announcer.clip = clip;
            _announcer.volume = volume;
            _announcer.mute = false;
            _announcer.Play();
            return seconds;
        }

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

            foreach (AudioSource voice in _voices.Values)
            {
                if (voice != null)
                {
                    voice.volume = CueVolume;
                    voice.mute = session || _mix.MuteSfx;
                }
            }

            if (_announcer != null)
            {
                _announcer.volume = CueVolume;
                _announcer.mute = session || _mix.MuteSfx;
            }

            if (_music != null)
            {
                _music.volume = session ? 0f : _mix.EffectiveMusic;
                _music.mute = session || _mix.MuteMusic;
            }
        }

        // --- the static calls the views make (each does nothing when there is no audio: a headless peer, a test) ---------

        /// <summary>Plays <paramref name="cue"/> once on the scene's audio, if there is one.</summary>
        public static void Play(SoundCue cue)
        {
            Instance?.PlayCue(cue);
        }

        /// <summary>Plays <paramref name="cue"/>, if any.</summary>
        public static void Play(SoundCue? cue)
        {
            if (cue.HasValue)
            {
                Play(cue.Value);
            }
        }

        /// <summary>
        /// Plays <paramref name="cue"/> once, at the end of this frame, settled with the others raised in the same moment
        /// (SoundBook.Settle: the knock-out that ends a round is applauded, not sighed at).
        /// </summary>
        public static void PlaySettled(SoundCue cue)
        {
            Instance?._pending.Add(cue);
        }

        /// <summary>Plays <paramref name="cue"/> on a voice of its own, so <see cref="IsPlaying"/> knows when it ends.</summary>
        public static void PlayVoice(SoundCue cue)
        {
            Instance?.StartVoice(cue);
        }

        /// <summary>The original's audio_is_playing: true until <paramref name="cue"/>'s voice has played its length (muted or not).</summary>
        public static bool IsPlaying(SoundCue cue)
        {
            return Instance != null && Instance.Busy(cue);
        }

        /// <summary>The original's audio_stop_sound for a cue played on its own voice.</summary>
        public static void Stop(SoundCue cue)
        {
            Instance?.StopVoice(cue);
        }

        /// <summary>
        /// Keeps <paramref name="cue"/> sounding for <paramref name="holder"/>: played again each time it ends (the original's
        /// "if (!audio_is_playing(x)) audio_play_sound(x)" every step), but not while <paramref name="after"/> is still playing
        /// (the laser's loop waits for its fire sound). Held until every holder lets go.
        /// </summary>
        public static void Hold(SoundCue cue, object holder, SoundCue? after = null)
        {
            Instance?.AddHold(cue, holder, after);
        }

        /// <summary>
        /// <paramref name="holder"/> lets go of <paramref name="cue"/>; once nobody holds it, it is stopped when
        /// <paramref name="stop"/> (a crate's flyover, the laser's loop) or left to play out (the grenade's beep).
        /// </summary>
        public static void Release(SoundCue cue, object holder, bool stop = true)
        {
            Instance?.RemoveHold(cue, holder, stop);
        }

        /// <summary>True while anyone holds <paramref name="cue"/>.</summary>
        public static bool Held(SoundCue cue)
        {
            return Instance != null && Instance._holds.TryGetValue(cue, out HashSet<object> holders) && holders.Count > 0;
        }

        /// <summary>The length of <paramref name="cue"/>'s recording in seconds (0 when it is missing or there is no audio).</summary>
        public static float Length(SoundCue cue)
        {
            AudioClip clip = Instance != null ? Instance.ClipFor(cue) : null;
            return clip != null ? clip.length : 0f;
        }

        /// <summary>The recording <paramref name="cue"/> plays (loaded from Resources/Ricochet/Sound the first time it is asked for).</summary>
        public AudioClip ClipFor(SoundCue cue)
        {
            if (!_clips.TryGetValue(cue, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(SoundBook.ResourcePath(cue));
                if (clip == null)
                {
                    Debug.LogWarning("[GameAudio] The recording " + SoundBook.Recording(cue) + " is missing from Resources/" + SoundBook.ResourceFolder + "; it plays nothing.");
                }

                _clips[cue] = clip;
            }

            return clip;
        }

        private void Record(SoundCue cue)
        {
            float volume = CueVolume;
            LastCue = cue;
            LastVolume = volume;
            Played++;
            _requests.Add(new SoundRequest(cue, volume));
            if (_requests.Count > RequestsKept)
            {
                _requests.RemoveAt(0);
            }
        }

        private void PlayCue(SoundCue cue)
        {
            Record(cue);
            if (_effects != null && LastVolume > 0f)
            {
                AudioClip clip = ClipFor(cue);
                if (clip != null)
                {
                    _effects.PlayOneShot(clip);
                }
            }
        }

        private void StartVoice(SoundCue cue)
        {
            Record(cue);
            AudioClip clip = ClipFor(cue);
            _busyUntil[cue] = Time.time + (clip != null ? clip.length : 0f);
            if (clip == null)
            {
                return;
            }

            AudioSource voice = VoiceFor(cue);
            voice.clip = clip;
            voice.volume = LastVolume;
            voice.mute = LastVolume <= 0f;
            if (LastVolume > 0f)
            {
                voice.Play();
            }
            else
            {
                voice.Stop();
            }
        }

        private bool Busy(SoundCue cue)
        {
            return _busyUntil.TryGetValue(cue, out float until) && Time.time < until;
        }

        private void StopVoice(SoundCue cue)
        {
            _busyUntil.Remove(cue);
            if (_voices.TryGetValue(cue, out AudioSource voice) && voice != null)
            {
                voice.Stop();
            }
        }

        private AudioSource VoiceFor(SoundCue cue)
        {
            if (!_voices.TryGetValue(cue, out AudioSource voice) || voice == null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                _voices[cue] = voice;
            }

            return voice;
        }

        private void AddHold(SoundCue cue, object holder, SoundCue? after)
        {
            if (!_holds.TryGetValue(cue, out HashSet<object> holders))
            {
                holders = new HashSet<object>();
                _holds[cue] = holders;
            }

            holders.Add(holder ?? this);
            _holdAfter[cue] = after;
            KeepHeld(cue);
        }

        private void RemoveHold(SoundCue cue, object holder, bool stop)
        {
            if (!_holds.TryGetValue(cue, out HashSet<object> holders) || !holders.Remove(holder ?? this) || holders.Count > 0)
            {
                return;
            }

            if (stop)
            {
                StopVoice(cue);
            }
        }

        private void KeepHeld(SoundCue cue)
        {
            if (Busy(cue))
            {
                return;
            }

            if (_holdAfter.TryGetValue(cue, out SoundCue? after) && after.HasValue && Busy(after.Value))
            {
                return;
            }

            StartVoice(cue);
        }

        private void Update()
        {
            WatchMatch();
            foreach (KeyValuePair<SoundCue, HashSet<object>> hold in _holds)
            {
                // A holder that is gone (a player who left, a view destroyed) holds nothing any more.
                if (hold.Value.RemoveWhere(h => h is Object unity && unity == null) > 0 && hold.Value.Count == 0)
                {
                    StopVoice(hold.Key);
                }

                if (hold.Value.Count > 0 && !PausedFor(hold.Value))
                {
                    KeepHeld(hold.Key);
                }
            }
        }

        // A new round or turn of a match, as the synced turn state shows it on every peer: the original's randomize_level.gml
        // and next_player.gml each call play_random_music. The first look only notes where the match is (joining mid-match
        // starts music for the turn in progress, as a fresh round would).
        private void WatchMatch()
        {
            if ((_turns == null || !_turns.IsSpawned) && Time.unscaledTime >= _nextTurnsSearch)
            {
                _nextTurnsSearch = Time.unscaledTime + 1f; // a scene search, so about once a second
                _turns = FindAnyObjectByType<Turns>();
            }

            bool on = _turns != null && _turns.IsSpawned && _turns.MatchPhase != MatchPhase.Practice;
            if (!on)
            {
                if (_musicSeen)
                {
                    // The match ended (back to practice, or the session was left): the track ends with it.
                    if (_music != null)
                    {
                        _music.Stop();
                    }

                    CurrentTrack = null;
                }

                _musicSeen = false;
                return;
            }

            int round = _turns.Round;
            int turn = _turns.Current;
            if (!_musicSeen || MusicBook.IsNewMoment(true, _lastRound, _lastTurn, round, turn))
            {
                PlayRandomMusic();
            }

            _musicSeen = true;
            _lastRound = round;
            _lastTurn = turn;
        }

        // Paused (UR-T43): a shot in the air hangs, and so does its loop.
        private static bool PausedFor(HashSet<object> holders)
        {
            foreach (object holder in holders)
            {
                if (holder is Player player && player != null && Turns.PausedIn(player.Session))
                {
                    return true;
                }
            }

            return false;
        }

        // The moment's settled cues (a knock-out's sigh dropped for the round's applause).
        private void LateUpdate()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            List<SoundCue> settled = SoundBook.Settle(_pending);
            _pending.Clear();
            foreach (SoundCue cue in settled)
            {
                PlayCue(cue);
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
            _announcer = gameObject.AddComponent<AudioSource>();
            _announcer.playOnAwake = false;
            _announcer.spatialBlend = 0f;
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }

            gameObject.AddComponent<UiSounds>(); // the menus' flick, click, select and disabled
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

        // A shot as every peer sees it: fired, passing each contact, landing.
        private void OnEvent(Player who, Cmd.GameEvent e)
        {
            switch (e)
            {
                case Cmd.ShotFired fired:
                    _shotWeapons[fired.PlayerId] = fired.Weapon;
                    _shotVolleys[fired.PlayerId] = fired.Volley;
                    if (fired.Weapon == Weapon.Laser)
                    {
                        PlayVoice(SoundCue.FireLaser); // shoot_laser.gml, then laser_step.gml's loop once it ends
                        Hold(SoundCue.LoopLaser, who != null ? (object)who : this, SoundCue.FireLaser);
                    }
                    else
                    {
                        PlayCue(SoundBook.Fire(fired.Weapon));
                    }

                    if (fired.Weapon == Weapon.Grenade)
                    {
                        Hold(SoundCue.GrenadeBeep, who != null ? (object)who : this); // grenade_step.gml, every step it flies
                    }

                    break;
                case Cmd.ShotCut cut:
                    _shotVolleys[cut.PlayerId] = cut.Volley;
                    break;
                case Cmd.Bounced bounced when _shotWeapons.TryGetValue(bounced.PlayerId, out Weapon weapon):
                    _shotVolleys.TryGetValue(bounced.PlayerId, out Volley volley);
                    Play(SoundBook.Bounce(weapon, SoundBook.ContactAt(volley, bounced.Bounces, bounced.Point), _pellets++));
                    break;
                case Cmd.ShotLanded landed:
                    object holder = who != null ? (object)who : this;
                    Release(SoundCue.LoopLaser, holder);
                    Release(SoundCue.GrenadeBeep, holder, stop: false);
                    Play(SoundBook.Landing(landed.Volley));
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

            _tracks.Clear();
            _voiceLines.Clear();
            _clips.Clear(); // the recordings are assets: Resources keeps them, nothing here destroys them
        }
    }
}
