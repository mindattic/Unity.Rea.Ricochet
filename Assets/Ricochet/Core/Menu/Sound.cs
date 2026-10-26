// The game's sound, engine-free: the mix the player's MindAttic profile sets (UR-T10: Unity.Shared's common block's
// musicVolume, sfxVolume, muteMusic, muteSfx, the same four every MindAttic game reads), and (U.RICO-T50) every sound
// effect the original plays, as the original's own recordings. SoundCue names one recording each: the 70 non-voice
// sfx_* files under the original's sound/audio, which Tools/import-gml-sound.ps1 mirrors into
// Resources/Ricochet/Sound (Assets/Ricochet/Audio/gml-sound.json records where each came from). SoundBook says which
// cue each moment of play plays (fire_weapon.gml's shoot_* scripts, the *_step scripts' bounces, use_item.gml, the drops,
// turn_countdown.gml, hit.gml, play_movement_sound.gml / stop_moving.gml, the buttons) and at what volume; the runtime
// (GameAudio) loads the recordings and plays what this says. The announcers' voice lines are U.RICO-T51's and the music
// U.RICO-T27's.
using System;
using System.Collections.Generic;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Commands;

namespace Ricochet.Core.Menu
{
    /// <summary>
    /// The original's sound effects, one per recording (sfx_&lt;name&gt; in its sound folder; <see cref="SoundBook.Recording"/>
    /// gives the file). Where the original played each is on the cue.
    /// </summary>
    public enum SoundCue
    {
        /// <summary>sfx_applause: hit.gml, the knock-out that ends the round.</summary>
        Applause,

        /// <summary>sfx_beep: turn_countdown.gml, each of the turn clock's last four seconds.</summary>
        Beep,

        /// <summary>sfx_bend: a shot sent on by a rail redirector (obj_redirector's collisions, laser_step.gml).</summary>
        Bend,

        /// <summary>sfx_blip: obj_selector appearing.</summary>
        Blip,

        /// <summary>sfx_bounce: in the original's sound folder, played nowhere.</summary>
        Bounce,

        /// <summary>sfx_bounce_cannon: a shell or a vortex off a wall (bullet_step.gml, vortex_step.gml).</summary>
        BounceCannon,

        /// <summary>sfx_bounce_grenade: a grenade off a wall (grenade_step.gml).</summary>
        BounceGrenade,

        /// <summary>sfx_bounce_laser: a laser off a wall (laser_step.gml).</summary>
        BounceLaser,

        /// <summary>sfx_bounce_shotgun: an older copy of sfx_bounce_shotgun1 (the same bytes), played nowhere.</summary>
        BounceShotgun,

        /// <summary>sfx_bounce_shotgun1: a pellet off a wall (pellet_step.gml's choose).</summary>
        BounceShotgun1,

        /// <summary>sfx_bounce_shotgun2: a pellet off a wall (pellet_step.gml's choose).</summary>
        BounceShotgun2,

        /// <summary>sfx_click: a button or checkbox pressed (every obj_btn_* and obj_checkbox_*, unpause.gml).</summary>
        Click,

        /// <summary>sfx_crash: a crate landing (obj_weapondrop, obj_itemdrop, obj_healthdrop, obj_powerup).</summary>
        Crash,

        /// <summary>sfx_disabled: something that cannot be done now (fire_weapon.gml and use_item.gml with none left or after firing, a spent icon clicked).</summary>
        Disabled,

        /// <summary>sfx_dummy: in the original's sound folder, played nowhere.</summary>
        Dummy,

        /// <summary>sfx_engine: the tank driving (play_movement_sound.gml, again each time it ends).</summary>
        Engine,

        /// <summary>sfx_engine_shutoff: the tank stopping (stop_moving.gml).</summary>
        EngineShutoff,

        /// <summary>sfx_engine_turnon: the tank starting to drive (play_movement_sound.gml).</summary>
        EngineTurnOn,

        /// <summary>sfx_explode: obj_boom, obj_boom_normal, obj_explosion, obj_grenade_explosion, obj_playerexplosion, obj_powerup_explosion.</summary>
        Explode,

        /// <summary>sfx_fire: in the original's sound folder, played nowhere.</summary>
        Fire,

        /// <summary>sfx_fire_cannon: shoot_bullet.gml.</summary>
        FireCannon,

        /// <summary>sfx_fire_grenade: shoot_grenade.gml.</summary>
        FireGrenade,

        /// <summary>sfx_fire_laser: shoot_laser.gml.</summary>
        FireLaser,

        /// <summary>sfx_fire_railgun: shoot_railgun.gml.</summary>
        FireRailgun,

        /// <summary>sfx_fire_shotgun: shoot_shotgun.gml.</summary>
        FireShotgun,

        /// <summary>sfx_flick: the pointer over a button, a weapon or item cycled or picked (cycle_weapon.gml, obj_icon_*).</summary>
        Flick,

        /// <summary>sfx_flyover: a crate falling (the drops' Create, stopped when it lands).</summary>
        Flyover,

        /// <summary>sfx_fuse1: obj_game's key 1, a grenade's fuse shortened.</summary>
        Fuse1,

        /// <summary>sfx_fuse2: obj_game's key 2, a grenade's fuse lengthened.</summary>
        Fuse2,

        /// <summary>sfx_fuse3: obj_game's key 3, a grenade's fuse at its longest.</summary>
        Fuse3,

        /// <summary>sfx_fuse4: for the original's keys 4-6, whose code it never wrote.</summary>
        Fuse4,

        /// <summary>sfx_fuse5: for the original's keys 4-6, whose code it never wrote.</summary>
        Fuse5,

        /// <summary>sfx_fuse6: for the original's keys 4-6, whose code it never wrote.</summary>
        Fuse6,

        /// <summary>sfx_grenade_beep: a grenade in the air, again each time it ends (grenade_step.gml).</summary>
        GrenadeBeep,

        /// <summary>sfx_hacktivated: obj_game's debug key turning debug on.</summary>
        Hacktivated,

        /// <summary>sfx_hologram: obj_shieldwall going up.</summary>
        Hologram,

        /// <summary>sfx_hologram_pass: in obj_shieldwall's collisions.</summary>
        HologramPass,

        /// <summary>sfx_loop_laser: a laser in the air once sfx_fire_laser has ended, again each time it ends (laser_step.gml).</summary>
        LoopLaser,

        /// <summary>sfx_powerup: a crate collected (add_random_weapon / item / health).</summary>
        Powerup,

        /// <summary>sfx_powerup_activate: an Extra Bounce used (use_item.gml).</summary>
        PowerupActivate,

        /// <summary>sfx_redirector_grow: a redirector stopping and opening out (redirector_detonate.gml).</summary>
        RedirectorGrow,

        /// <summary>sfx_redirector_shoot: a redirector launched (shoot_redirector.gml).</summary>
        RedirectorShoot,

        /// <summary>sfx_select: a setting stepped (every obj_btn_*_less / _more / _last / _next).</summary>
        Select,

        /// <summary>sfx_shatter: a shield wall broken by a shot (obj_shieldwall).</summary>
        Shatter,

        /// <summary>sfx_shoot_vortex: in the original's sound folder, played nowhere (shoot_vortex.gml plays sfx_vortex_shoot).</summary>
        ShootVortex,

        /// <summary>sfx_shrink: a shrink ray reaching a tank (obj_shrinkray).</summary>
        Shrink,

        /// <summary>sfx_shrinkray_shoot: shoot_shrinkray.gml.</summary>
        ShrinkrayShoot,

        /// <summary>sfx_sigh: a turn timed out (turn_countdown.gml) or a tank knocked out that does not end the round (hit.gml).</summary>
        Sigh,

        /// <summary>sfx_sonar_ping: the tank whose turn it is, pinged (ping_player.gml).</summary>
        SonarPing,

        /// <summary>sfx_startup: the splash's lotus card (obj_lotus_card_picture).</summary>
        Startup,

        /// <summary>sfx_static: in the original's sound folder, played nowhere (the splash plays sfx_static1-6).</summary>
        Static,

        /// <summary>sfx_static1: the splash's lotus card (obj_lotus_card_picture).</summary>
        Static1,

        /// <summary>sfx_static2: the splash's lotus card (obj_lotus_card_picture).</summary>
        Static2,

        /// <summary>sfx_static3: the splash's lotus card (obj_lotus_card_picture).</summary>
        Static3,

        /// <summary>sfx_static4: the splash's lotus card (obj_lotus_card_picture).</summary>
        Static4,

        /// <summary>sfx_static5: the splash's lotus card (obj_lotus_card_picture).</summary>
        Static5,

        /// <summary>sfx_static6: the splash's lotus card (obj_lotus_card_picture).</summary>
        Static6,

        /// <summary>sfx_switch: a turn's Ready card going up (obj_ready).</summary>
        Switch,

        /// <summary>sfx_tech: the splash screen (object_splashscreen).</summary>
        Tech,

        /// <summary>sfx_teleport: a teleporter putting its tank down (teleport_player.gml).</summary>
        Teleport,

        /// <summary>sfx_teleporter_shoot: shoot_teleporter.gml.</summary>
        TeleporterShoot,

        /// <summary>sfx_turn: the barrel turning while the tank stands (rotate_left / rotate_right.gml, stopped on release).</summary>
        Turn,

        /// <summary>sfx_type: a key typed into a name box (obj_inputbox*), or obj_game's numpad snapping the aim.</summary>
        Type,

        /// <summary>sfx_type_quiet: the splash's typewriter (obj_typewriter).</summary>
        TypeQuiet,

        /// <summary>sfx_vortex_blow: a vortex blowing out (vortex_step.gml's "blowing").</summary>
        VortexBlow,

        /// <summary>sfx_vortex_shoot: shoot_vortex.gml.</summary>
        VortexShoot,

        /// <summary>sfx_vortex_suck: a vortex pulling in (vortex_step.gml's "sucking", stopped when it blows).</summary>
        VortexSuck,

        /// <summary>sfx_whoosh: the splash's lotus card (obj_lotus_card_picture).</summary>
        Whoosh,

        /// <summary>sfx_woot: in the original's sound folder, played nowhere (the endgame podium's, U.RICO-T56).</summary>
        Woot,

        /// <summary>sfx_zoom: an Aim Assist used (use_item.gml).</summary>
        Zoom,
    }

    /// <summary>
    /// The player's audio settings as the game applies them: two volumes, 0-1, and a mute each. Values outside 0-1 (or
    /// NaN) are clamped, the way Unity.Shared's CommonSettings clamps them.
    /// </summary>
    public sealed class AudioMix
    {
        /// <summary>Unity.Shared's defaults (CommonSettings.DefaultMusicVolume / DefaultSfxVolume).</summary>
        public const float DefaultMusicVolume = 0.6f;
        public const float DefaultSfxVolume = 0.85f;

        private float _musicVolume = DefaultMusicVolume;
        private float _sfxVolume = DefaultSfxVolume;

        public float MusicVolume
        {
            get { return _musicVolume; }
            set { _musicVolume = Clamp01(value); }
        }

        public float SfxVolume
        {
            get { return _sfxVolume; }
            set { _sfxVolume = Clamp01(value); }
        }

        public bool MuteMusic { get; set; }

        public bool MuteSfx { get; set; }

        /// <summary>The volume music plays at: 0 when muted.</summary>
        public float EffectiveMusic => MuteMusic ? 0f : MusicVolume;

        /// <summary>The volume sound effects play at: 0 when muted.</summary>
        public float EffectiveSfx => MuteSfx ? 0f : SfxVolume;

        public AudioMix Copy()
        {
            return new AudioMix { MusicVolume = MusicVolume, SfxVolume = SfxVolume, MuteMusic = MuteMusic, MuteSfx = MuteSfx };
        }

        public override string ToString()
        {
            return "music " + MusicVolume.ToString("0.00") + (MuteMusic ? " (muted)" : string.Empty)
                + ", effects " + SfxVolume.ToString("0.00") + (MuteSfx ? " (muted)" : string.Empty);
        }

        private static float Clamp01(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
        }
    }

    /// <summary>
    /// Which recording each <see cref="SoundCue"/> is, which cue each moment of play makes, and the volume it plays at.
    /// </summary>
    public static class SoundBook
    {
        /// <summary>The Resources folder the recordings are imported into (Tools/import-gml-sound.ps1).</summary>
        public const string ResourceFolder = "Ricochet/Sound";

        /// <summary>How many cues (recordings) there are.</summary>
        public static readonly int Count = Enum.GetValues(typeof(SoundCue)).Length;

        private static readonly string[] Names = BuildNames();

        /// <summary>Every cue, in order.</summary>
        public static IEnumerable<SoundCue> All
        {
            get
            {
                for (int i = 0; i < Count; i++)
                {
                    yield return (SoundCue)i;
                }
            }
        }

        /// <summary>The original's name for <paramref name="cue"/>'s recording: sfx_fire_cannon for FireCannon.</summary>
        public static string Recording(SoundCue cue)
        {
            int i = (int)cue;
            if (i < 0 || i >= Names.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(cue));
            }

            return Names[i];
        }

        /// <summary>The Resources path GameAudio loads <paramref name="cue"/>'s recording from.</summary>
        public static string ResourcePath(SoundCue cue) => ResourceFolder + "/" + Recording(cue);

        /// <summary>The cue whose recording is <paramref name="recording"/> (sfx_fire_cannon), or null.</summary>
        public static SoundCue? FromRecording(string recording)
        {
            for (int i = 0; i < Names.Length; i++)
            {
                if (string.Equals(Names[i], recording, StringComparison.Ordinal))
                {
                    return (SoundCue)i;
                }
            }

            return null;
        }

        /// <summary>
        /// The volume a cue plays at, 0-1: the profile's effect volume, 0 when the player muted effects or the session is
        /// muted (U.RICO-T71: -muted or MINDATTIC_CLI_MUTED=1). Every recording's GameMaker volume is 1, so none is scaled.
        /// </summary>
        public static float Volume(AudioMix mix, bool sessionMuted)
        {
            return sessionMuted || mix == null ? 0f : mix.EffectiveSfx;
        }

        /// <summary>fire_weapon.gml: the sound <paramref name="weapon"/> makes leaving the barrel (shoot_*.gml).</summary>
        public static SoundCue Fire(Weapon weapon)
        {
            switch (weapon)
            {
                case Weapon.Laser: return SoundCue.FireLaser;
                case Weapon.Shotgun: return SoundCue.FireShotgun;
                case Weapon.Grenade: return SoundCue.FireGrenade;
                case Weapon.Railgun: return SoundCue.FireRailgun;
                case Weapon.Vortex: return SoundCue.VortexShoot;
                default: return SoundCue.FireCannon;
            }
        }

        /// <summary>
        /// The sound a projectile of <paramref name="weapon"/> makes passing a contact of <paramref name="kind"/>: a rail
        /// redirector bends it (sfx_bend), a wall or the border bounces it (the weapon's own bounce). A pellet plays one of
        /// two recordings, picked by <paramref name="pick"/> (any number: even is the first). A rail passes straight through,
        /// silently (railgun_step.gml plays nothing): null.
        /// </summary>
        public static SoundCue? Bounce(Weapon weapon, ContactKind kind, int pick)
        {
            if (kind == ContactKind.Redirector)
            {
                return SoundCue.Bend;
            }

            switch (weapon)
            {
                case Weapon.Cannon:
                case Weapon.Vortex: return SoundCue.BounceCannon;
                case Weapon.Laser: return SoundCue.BounceLaser;
                case Weapon.Grenade: return SoundCue.BounceGrenade;
                case Weapon.Shotgun: return (pick & 1) == 0 ? SoundCue.BounceShotgun1 : SoundCue.BounceShotgun2;
                default: return null;
            }
        }

        /// <summary>
        /// The kind of the <paramref name="bounces"/>th contact (1 for the first) that one of <paramref name="volley"/>'s paths
        /// made at <paramref name="point"/> (the Bounced event's), a wall when none is found.
        /// </summary>
        public static ContactKind ContactAt(Volley volley, int bounces, Vec2 point)
        {
            if (volley != null && bounces > 0)
            {
                foreach (ShotPath path in volley.Paths)
                {
                    if (bounces <= path.Contacts.Count)
                    {
                        Contact contact = path.Contacts[bounces - 1];
                        if (Vec2.Distance(contact.Point, point) < 1e-6)
                        {
                            return contact.Kind;
                        }
                    }
                }
            }

            return ContactKind.Wall;
        }

        /// <summary>
        /// What a landed volley sets off: sfx_explode for obj_boom_normal on whoever it struck (hit.gml), a shell's
        /// obj_explosion where its last bounce is spent (bullet_step.gml), a grenade's obj_grenade_explosion and a vortex's
        /// obj_boom or obj_explosion; null for a laser, a pellet, a rail or a shot a barrier absorbed that struck nobody.
        /// </summary>
        public static SoundCue? Landing(Volley volley)
        {
            if (volley == null)
            {
                return null;
            }

            if (volley.Strikes.Count > 0 || volley.HasBlast)
            {
                return SoundCue.Explode;
            }

            if (volley.Weapon == Weapon.Cannon && (volley.Main.End == ShotEndKind.Spent || volley.Main.End == ShotEndKind.BounceLimit))
            {
                return SoundCue.Explode;
            }

            return null;
        }

        /// <summary>use_item.gml: the sound <paramref name="item"/> makes as it is used.</summary>
        public static SoundCue Use(Item item)
        {
            switch (item)
            {
                case Item.ExtraBounce: return SoundCue.PowerupActivate;
                case Item.ShieldWall: return SoundCue.Hologram; // spawn_shieldwall's obj_shieldwall
                case Item.ShrinkRay: return SoundCue.ShrinkrayShoot;
                case Item.Teleporter: return SoundCue.TeleporterShoot;
                case Item.AimAssist: return SoundCue.Zoom;
                default: return SoundCue.RedirectorShoot;
            }
        }

        /// <summary>obj_game's fuse keys: -1 shorter (sfx_fuse1), +1 longer (sfx_fuse2), +2 the longest (sfx_fuse3); null for 0.</summary>
        public static SoundCue? Fuse(int change)
        {
            switch (change)
            {
                case -1: return SoundCue.Fuse1;
                case 1: return SoundCue.Fuse2;
                case 2: return SoundCue.Fuse3;
                default: return null;
            }
        }

        /// <summary>
        /// The sound a Core event makes on every peer, for the events no view plays: an item used (its own), a shrink ray
        /// reaching a tank (sfx_shrink), a teleporter landing (sfx_teleport), a crate collected (sfx_powerup), the turn
        /// clock's last seconds (sfx_beep), a turn that ran out (sfx_sigh), a tank knocked out (sfx_sigh) and the knock-out
        /// that ends the round (sfx_applause, which <see cref="Settle"/> keeps in place of that sigh). Null for the rest.
        /// </summary>
        public static SoundCue? ForEvent(GameEvent e)
        {
            switch (e)
            {
                case ItemUsed used: return Use(used.Item);
                case Shrunk _: return SoundCue.Shrink;
                case Moved moved when moved.Cause == MoveCause.Teleported: return SoundCue.Teleport;
                case CratePickedUp _: return SoundCue.Powerup;
                case Countdown _: return SoundCue.Beep;
                case TurnPassed passed when passed.Reason == TurnPassReason.TimedOut: return SoundCue.Sigh;
                case Eliminated _: return SoundCue.Sigh;
                case RoundOver over when over.Winner >= 0: return SoundCue.Applause;
                default: return null;
            }
        }

        /// <summary>
        /// The cues one moment raised, as they should be heard: hit.gml plays sfx_applause in place of sfx_sigh for the
        /// knock-out that ends the round, so applause in the same moment drops the sighs. The rest keep their order.
        /// </summary>
        public static List<SoundCue> Settle(IEnumerable<SoundCue> cues)
        {
            var settled = new List<SoundCue>(cues ?? Array.Empty<SoundCue>());
            if (settled.Contains(SoundCue.Applause))
            {
                settled.RemoveAll(c => c == SoundCue.Sigh);
            }

            return settled;
        }

        private static string[] BuildNames()
        {
            var names = new string[Count];
            for (int i = 0; i < Count; i++)
            {
                names[i] = "sfx_" + Snake(((SoundCue)i).ToString());
            }

            // The names the PascalCase rule does not give.
            names[(int)SoundCue.EngineTurnOn] = "sfx_engine_turnon";
            names[(int)SoundCue.ShrinkrayShoot] = "sfx_shrinkray_shoot";
            return names;
        }

        // FireCannon -> fire_cannon, BounceShotgun1 -> bounce_shotgun1, Static4 -> static4.
        private static string Snake(string pascal)
        {
            var text = new System.Text.StringBuilder(pascal.Length + 8);
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        text.Append('_');
                    }

                    text.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    text.Append(c);
                }
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// play_movement_sound.gml and stop_moving.gml: the engine as the tank drives. Ask it every frame with whether the tank
    /// is driving and which of the two engine recordings is still playing; it says what to do.
    /// </summary>
    public sealed class EngineSound
    {
        /// <summary>What the engine does this frame.</summary>
        public enum Step
        {
            /// <summary>Nothing new.</summary>
            None,

            /// <summary>sfx_engine_turnon: the tank began to drive.</summary>
            TurnOn,

            /// <summary>sfx_engine: the turn-on (or the last run of the engine) has ended while it still drives.</summary>
            Run,

            /// <summary>Stop sfx_engine and play sfx_engine_shutoff: it stopped while the engine ran.</summary>
            ShutOff,
        }

        /// <summary>True while the tank is driving (isMoving).</summary>
        public bool Moving { get; private set; }

        public Step Update(bool driving, bool turnOnPlaying, bool enginePlaying)
        {
            if (driving)
            {
                if (!Moving)
                {
                    Moving = true;
                    return turnOnPlaying ? Step.None : Step.TurnOn;
                }

                return !enginePlaying && !turnOnPlaying ? Step.Run : Step.None;
            }

            if (!Moving)
            {
                return Step.None;
            }

            Moving = false;
            return enginePlaying ? Step.ShutOff : Step.None; // stopped during the turn-on: it just plays out
        }
    }
}
