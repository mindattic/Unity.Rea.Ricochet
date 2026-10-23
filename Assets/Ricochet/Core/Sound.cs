// The game's sound, engine-free (UR-T10): the mix the player's MindAttic profile sets (Unity.Shared's common block:
// musicVolume, sfxVolume, muteMusic, muteSfx, the same four every MindAttic game reads), and the short effects the
// game plays, synthesised from a few tones so the repo carries no audio files. The runtime (GameAudio) reads the
// profile into an AudioMix, renders each cue once into an AudioClip and plays it at the mix's effect volume.
using System;

namespace Ricochet.Core
{
    /// <summary>The moments the game makes a sound for.</summary>
    public enum SoundCue
    {
        /// <summary>A shot leaves the barrel (fire_weapon.gml).</summary>
        Fire,

        /// <summary>A projectile glances off a wall, a mirror or a redirector.</summary>
        Bounce,

        /// <summary>A projectile comes to rest: a hit, a stop, or a shot that falls short.</summary>
        Land,

        /// <summary>A grenade or a vortex goes off.</summary>
        Blast,
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

    /// <summary>Renders each <see cref="SoundCue"/> as mono samples in -1..1: a short tone with a quick decay.</summary>
    public static class Tones
    {
        /// <summary>The sample rate the cues are rendered at.</summary>
        public const int SampleRate = 22050;

        /// <summary>How long <paramref name="cue"/> lasts, in seconds.</summary>
        public static double Seconds(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.Fire: return 0.12;
                case SoundCue.Bounce: return 0.05;
                case SoundCue.Land: return 0.18;
                default: return 0.45;
            }
        }

        /// <summary>
        /// The samples for <paramref name="cue"/> at <paramref name="sampleRate"/>: the same every call. Fire is a falling
        /// square chirp, Bounce a high click, Land a low thud, Blast a decaying rumble of noise.
        /// </summary>
        public static float[] Render(SoundCue cue, int sampleRate = SampleRate)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            int count = Math.Max(1, (int)Math.Round(Seconds(cue) * sampleRate));
            var samples = new float[count];
            uint noise = 0x2545F491u; // xorshift: the blast's noise, the same every render
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)count;           // 0..1 through the cue
                double envelope = (1 - t) * (1 - t);     // a quick decay to silence
                double attack = Math.Min(1.0, i / (sampleRate * 0.003)); // 3 ms in, so nothing clicks on
                double value;
                switch (cue)
                {
                    case SoundCue.Fire:
                        phase += (880 - 520 * t) / sampleRate;
                        value = (phase % 1.0) < 0.5 ? 0.6 : -0.6;
                        break;
                    case SoundCue.Bounce:
                        phase += 1760.0 / sampleRate;
                        value = Math.Sin(2 * Math.PI * phase) * 0.5;
                        break;
                    case SoundCue.Land:
                        phase += (180 - 100 * t) / sampleRate;
                        value = Math.Sin(2 * Math.PI * phase) * 0.8;
                        break;
                    default:
                        noise ^= noise << 13;
                        noise ^= noise >> 17;
                        noise ^= noise << 5;
                        phase += 60.0 / sampleRate;
                        value = ((noise / (double)uint.MaxValue) * 2 - 1) * 0.5 + Math.Sin(2 * Math.PI * phase) * 0.35;
                        break;
                }

                samples[i] = (float)Math.Max(-1.0, Math.Min(1.0, value * envelope * attack));
            }

            return samples;
        }
    }
}
