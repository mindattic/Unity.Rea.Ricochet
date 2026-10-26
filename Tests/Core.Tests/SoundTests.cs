using NUnit.Framework;
using Ricochet.Core.Menu;

namespace Ricochet.Core.Tests
{
    // UR-T10: the profile's audio mix and the synthesised effects.
    public class SoundTests
    {
        [Test]
        public void ANewMix_HasUnitySharedsDefaults()
        {
            var mix = new AudioMix();
            Assert.That(mix.MusicVolume, Is.EqualTo(0.6f));
            Assert.That(mix.SfxVolume, Is.EqualTo(0.85f));
            Assert.That(mix.MuteMusic, Is.False);
            Assert.That(mix.MuteSfx, Is.False);
        }

        [Test]
        public void Volumes_AreClampedToZeroOne_AndNaNIsSilence()
        {
            var mix = new AudioMix { MusicVolume = 3f, SfxVolume = -1f };
            Assert.That(mix.MusicVolume, Is.EqualTo(1f));
            Assert.That(mix.SfxVolume, Is.EqualTo(0f));
            mix.MusicVolume = float.NaN;
            Assert.That(mix.MusicVolume, Is.EqualTo(0f));
        }

        [Test]
        public void AMute_SilencesOnlyItsOwnChannel()
        {
            var mix = new AudioMix { MusicVolume = 0.4f, SfxVolume = 0.7f, MuteSfx = true };
            Assert.That(mix.EffectiveSfx, Is.EqualTo(0f));
            Assert.That(mix.EffectiveMusic, Is.EqualTo(0.4f));
            mix.MuteSfx = false;
            mix.MuteMusic = true;
            Assert.That(mix.EffectiveSfx, Is.EqualTo(0.7f));
            Assert.That(mix.EffectiveMusic, Is.EqualTo(0f));
            Assert.That(mix.SfxVolume, Is.EqualTo(0.7f), "a mute keeps the volume to come back to");
        }

        [Test]
        public void ACopy_IsIndependent()
        {
            var mix = new AudioMix { SfxVolume = 0.3f, MuteMusic = true };
            AudioMix copy = mix.Copy();
            copy.SfxVolume = 0.9f;
            Assert.That(mix.SfxVolume, Is.EqualTo(0.3f));
            Assert.That(copy.MuteMusic, Is.True);
        }

        [TestCase(SoundCue.Fire)]
        [TestCase(SoundCue.Bounce)]
        [TestCase(SoundCue.Land)]
        [TestCase(SoundCue.Blast)]
        [TestCase(SoundCue.Switch)]
        [TestCase(SoundCue.SonarPing)]
        public void EveryCue_RendersItsLength_InRange_AudibleAndTheSameEveryTime(SoundCue cue)
        {
            float[] samples = Tones.Render(cue);
            Assert.That(samples.Length, Is.EqualTo((int)System.Math.Round(Tones.Seconds(cue) * Tones.SampleRate)));
            float peak = 0f;
            foreach (float s in samples)
            {
                Assert.That(s, Is.InRange(-1f, 1f));
                peak = System.Math.Max(peak, System.Math.Abs(s));
            }

            Assert.That(peak, Is.GreaterThan(0.1f), "audible");
            Assert.That(System.Math.Abs(samples[0]), Is.LessThan(0.01f), "starts from silence, no click");
            Assert.That(Tones.Render(cue), Is.EqualTo(samples), "deterministic");
        }

        [Test]
        public void ABadSampleRate_IsRefused()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Tones.Render(SoundCue.Fire, 0));
        }
    }
}
