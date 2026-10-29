// The original's music, engine-free (U.RICO-T27): the eight music_* tracks that have a recording in its sound folder (music_spacefighter names none and is never played) (Tools/import-gml-sound.ps1
// mirrors them into Resources/Ricochet/Music, streamed, with Assets/Rea.Ricochet/Audio/gml-sound.json's "music" entries naming
// where each came from), which of them a round or turn picks (play_random_music.gml: irandom_range(1,5) over the five
// below, called from randomize_level.gml for a new round and next_player.gml for each turn, and by obj_jukebox's create
// event), and the volume they play at. Plain data and rules; GameAudio loads the recordings and plays what this says.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Menu
{
    /// <summary>The original's music_* recordings, one per track.</summary>
    public enum MusicTrack
    {
        /// <summary>music_cutandrun: the fifth of play_random_music.gml's tracks.</summary>
        CutAndRun,

        /// <summary>music_gocart: the second of play_random_music.gml's tracks.</summary>
        GoCart,

        /// <summary>music_inaheartbeat: in the original's sound folder, played nowhere.</summary>
        InAHeartbeat,

        /// <summary>music_latinindustries: the fourth of play_random_music.gml's tracks.</summary>
        LatinIndustries,

        /// <summary>music_phatsketch: the first of play_random_music.gml's tracks (globals.gml's first current_track).</summary>
        PhatSketch,

        /// <summary>music_rocket: the third of play_random_music.gml's tracks.</summary>
        Rocket,

        /// <summary>music_victory: the endgame podium's (U.RICO-T56), not a random pick.</summary>
        Victory,

        /// <summary>music_windsong: in the original's sound folder, played nowhere.</summary>
        WindSong,
    }

    /// <summary>Which recording each <see cref="MusicTrack"/> is, which a round or turn picks, and the volume music plays at.</summary>
    public static class MusicBook
    {
        /// <summary>The Resources folder the tracks are imported into (Tools/import-gml-sound.ps1).</summary>
        public const string ResourceFolder = "Ricochet/Music";

        /// <summary>The tracks play_random_music.gml picks among, in its order (irandom_range(1,5) = 1 to 5).</summary>
        public static readonly IReadOnlyList<MusicTrack> Rotation = new[]
        {
            MusicTrack.PhatSketch,
            MusicTrack.GoCart,
            MusicTrack.Rocket,
            MusicTrack.LatinIndustries,
            MusicTrack.CutAndRun,
        };

        /// <summary>Every track.</summary>
        public static IEnumerable<MusicTrack> All => (MusicTrack[])Enum.GetValues(typeof(MusicTrack));

        /// <summary>The original's name for <paramref name="track"/>'s recording: music_gocart for GoCart.</summary>
        public static string Recording(MusicTrack track) => "music_" + track.ToString().ToLowerInvariant();

        /// <summary>The Resources path GameAudio loads <paramref name="track"/> from.</summary>
        public static string ResourcePath(MusicTrack track) => ResourceFolder + "/" + Recording(track);

        /// <summary>
        /// play_random_music.gml's choice for <paramref name="roll"/>, a whole number from 1 to 5 (irandom_range(1,5)); a
        /// roll outside that is wrapped into it.
        /// </summary>
        public static MusicTrack Pick(int roll)
        {
            int n = Rotation.Count;
            return Rotation[(((roll - 1) % n) + n) % n];
        }

        /// <summary>
        /// Whether a turn or round change since the last look is a moment the original called play_random_music: a new round
        /// (randomize_level.gml) or a new turn (next_player.gml), only while a match is on.
        /// </summary>
        public static bool IsNewMoment(bool matchOn, int lastRound, int lastTurn, int round, int turn)
        {
            return matchOn && (round != lastRound || turn != lastTurn);
        }

        /// <summary>
        /// The volume music plays at, 0-1: the profile's music volume, 0 when the player muted music or the session is muted
        /// (U.RICO-T71: -muted or MINDATTIC_CLI_MUTED=1). Every track's GameMaker volume is 1, so none is scaled.
        /// </summary>
        public static float Volume(AudioMix mix, bool sessionMuted)
        {
            return sessionMuted || mix == null ? 0f : mix.EffectiveMusic;
        }
    }
}
