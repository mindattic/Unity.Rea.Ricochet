using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Aim;
    using Ricochet.Core.Arsenal;
    using Ricochet.Core.Commands;
    using Ricochet.Core.Level;
    using Ricochet.Core.Menu;

    // UR-T10: the profile's audio mix. U.RICO-T50: the original's recordings, one cue each, and which cue each moment plays.
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

        [Test]
        public void ACuePlaysAtTheProfilesEffectVolume_AndNotAtAllWhenMutedOrSessionMuted()
        {
            var mix = new AudioMix { SfxVolume = 0.35f };
            Assert.That(SoundBook.Volume(mix, false), Is.EqualTo(0.35f));
            Assert.That(SoundBook.Volume(mix, true), Is.EqualTo(0f), "the session mute (U.RICO-T71) silences every cue");
            mix.MuteSfx = true;
            Assert.That(SoundBook.Volume(mix, false), Is.EqualTo(0f), "the profile's mute");
            Assert.That(SoundBook.Volume(null, false), Is.EqualTo(0f));
        }

        // --- the recordings --------------------------------------------------------------------------------------

        [Test]
        public void ThereAreSeventyCues_EachItsOwnRecording()
        {
            Assert.That(SoundBook.Count, Is.EqualTo(70), "the original's 71 non-voice sfx less sfx_bounce_shotgun3, which names no file");
            string[] names = SoundBook.All.Select(SoundBook.Recording).ToArray();
            Assert.That(names, Is.Unique);
            Assert.That(names.All(n => n.StartsWith("sfx_", StringComparison.Ordinal) && n == n.ToLowerInvariant()), Is.True);
            Assert.That(SoundBook.Recording(SoundCue.FireCannon), Is.EqualTo("sfx_fire_cannon"));
            Assert.That(SoundBook.Recording(SoundCue.EngineTurnOn), Is.EqualTo("sfx_engine_turnon"));
            Assert.That(SoundBook.Recording(SoundCue.ShrinkrayShoot), Is.EqualTo("sfx_shrinkray_shoot"));
            Assert.That(SoundBook.Recording(SoundCue.BounceShotgun1), Is.EqualTo("sfx_bounce_shotgun1"));
            Assert.That(SoundBook.ResourcePath(SoundCue.Static4), Is.EqualTo("Ricochet/Sound/sfx_static4"));
            foreach (SoundCue cue in SoundBook.All)
            {
                Assert.That(SoundBook.FromRecording(SoundBook.Recording(cue)), Is.EqualTo(cue));
            }

            Assert.That(SoundBook.FromRecording("sfx_bounce_shotgun3"), Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => SoundBook.Recording((SoundCue)999));
        }

        [Test]
        public void EveryCuesRecordingIsImported_AndNothingElseIs()
        {
            string folder = FindUp(Path.Combine("Assets", "Ricochet", "Resources", "Ricochet", "Sound"));
            string[] imported = Directory.GetFiles(folder).Where(f => !f.EndsWith(".meta", StringComparison.Ordinal))
                .Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            string[] cues = SoundBook.All.Select(SoundBook.Recording).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.That(imported, Is.EqualTo(cues));

            string manifest = File.ReadAllText(FindUp(Path.Combine("Assets", "Ricochet", "Audio", "gml-sound.json")));
            foreach (string name in cues)
            {
                Assert.That(manifest, Does.Contain("\"originalPath\": \"sound\\\\" + name + ".sound.gmx\""), name + " records where it came from");
            }

            Assert.That(manifest, Does.Contain("\"name\": \"sfx_bounce_shotgun3\""), "the empty definition is listed as skipped");
        }

        // --- which cue each moment plays --------------------------------------------------------------------------

        [TestCase(Weapon.Cannon, SoundCue.FireCannon)]
        [TestCase(Weapon.Laser, SoundCue.FireLaser)]
        [TestCase(Weapon.Shotgun, SoundCue.FireShotgun)]
        [TestCase(Weapon.Grenade, SoundCue.FireGrenade)]
        [TestCase(Weapon.Railgun, SoundCue.FireRailgun)]
        [TestCase(Weapon.Vortex, SoundCue.VortexShoot)]
        public void EveryWeapon_FiresWithItsOwnRecording(Weapon weapon, SoundCue cue)
        {
            Assert.That(SoundBook.Fire(weapon), Is.EqualTo(cue));
        }

        [TestCase(Item.ExtraBounce, SoundCue.PowerupActivate)]
        [TestCase(Item.ShieldWall, SoundCue.Hologram)]
        [TestCase(Item.ShrinkRay, SoundCue.ShrinkrayShoot)]
        [TestCase(Item.Teleporter, SoundCue.TeleporterShoot)]
        [TestCase(Item.AimAssist, SoundCue.Zoom)]
        [TestCase(Item.Redirector, SoundCue.RedirectorShoot)]
        public void EveryItem_IsUsedWithItsOwnRecording(Item item, SoundCue cue)
        {
            Assert.That(SoundBook.Use(item), Is.EqualTo(cue));
            Assert.That(SoundBook.ForEvent(new ItemUsed(1, item)), Is.EqualTo(cue), "every peer hears it from the event");
        }

        [Test]
        public void EveryWeapon_BouncesWithItsOwnRecording_AndARedirectorBendsThemAll()
        {
            Assert.That(SoundBook.Bounce(Weapon.Cannon, ContactKind.Wall, 0), Is.EqualTo(SoundCue.BounceCannon));
            Assert.That(SoundBook.Bounce(Weapon.Vortex, ContactKind.Border, 0), Is.EqualTo(SoundCue.BounceCannon), "vortex_step.gml");
            Assert.That(SoundBook.Bounce(Weapon.Laser, ContactKind.Wall, 0), Is.EqualTo(SoundCue.BounceLaser));
            Assert.That(SoundBook.Bounce(Weapon.Grenade, ContactKind.Border, 0), Is.EqualTo(SoundCue.BounceGrenade));
            Assert.That(SoundBook.Bounce(Weapon.Railgun, ContactKind.Wall, 0), Is.Null, "railgun_step.gml makes no sound");
            foreach (Weapon weapon in Enum.GetValues(typeof(Weapon)))
            {
                Assert.That(SoundBook.Bounce(weapon, ContactKind.Redirector, 3), Is.EqualTo(SoundCue.Bend));
            }
        }

        [Test]
        public void APelletBounce_IsAlwaysOneOfTheTwoRecordings_NeverTheEmptyThird()
        {
            // pellet_step.gml chose among sfx_bounce_shotgun1, 2 and 3, and 3 names no file: a third of the bounces were
            // silent. The rebuild picks between the two that exist (a fixed defect).
            var heard = Enumerable.Range(-4, 9).Select(pick => SoundBook.Bounce(Weapon.Shotgun, ContactKind.Wall, pick)).Distinct().ToArray();
            Assert.That(heard, Is.EquivalentTo(new SoundCue?[] { SoundCue.BounceShotgun1, SoundCue.BounceShotgun2 }));
        }

        [Test]
        public void ABouncesContactKind_IsFoundOnTheVolleysPaths()
        {
            var level = new Level(-3, -3, 3, 3);
            Volley volley = Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 10, 1, 2, 0, level, null);
            Contact first = volley.Main.Contacts[0];
            Assert.That(SoundBook.ContactAt(volley, 1, first.Point), Is.EqualTo(first.Kind));
            Assert.That(SoundBook.ContactAt(volley, 1, new Vec2(99, 99)), Is.EqualTo(ContactKind.Wall), "not found: a wall");
            Assert.That(SoundBook.ContactAt(null, 1, first.Point), Is.EqualTo(ContactKind.Wall));
        }

        [Test]
        public void ALanding_ExplodesWhereTheOriginalDid()
        {
            var open = new Level(-10, -5, 10, 5);
            var me = new Target("me", Vec2.Zero, 0.45);
            var ann = new Target("ann", new Vec2(5, 0), 0.45);
            Assert.That(SoundBook.Landing(Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 0, 1, 2, 0, open, new[] { me, ann }.ToList())),
                Is.EqualTo(SoundCue.Explode), "obj_boom_normal on whoever it struck");
            Assert.That(SoundBook.Landing(Arsenal.Fire(Weapon.Cannon, Vec2.Zero, 90, 0, 2, 0, open, null)),
                Is.EqualTo(SoundCue.Explode), "a shell spent: obj_explosion");
            Assert.That(SoundBook.Landing(Arsenal.Fire(Weapon.Grenade, Vec2.Zero, 90, 0.3, 2, 0, open, null)),
                Is.EqualTo(SoundCue.Explode), "obj_grenade_explosion");
            Assert.That(SoundBook.Landing(Arsenal.Fire(Weapon.Laser, Vec2.Zero, 90, 1, 2, 0, open, null)), Is.Null, "the laser's end makes none");
            Assert.That(SoundBook.Landing(null), Is.Null);
        }

        [Test]
        public void TheFuseKeys_EachHaveTheirOwnRecording()
        {
            Assert.That(SoundBook.Fuse(-1), Is.EqualTo(SoundCue.Fuse1));
            Assert.That(SoundBook.Fuse(1), Is.EqualTo(SoundCue.Fuse2));
            Assert.That(SoundBook.Fuse(2), Is.EqualTo(SoundCue.Fuse3));
            Assert.That(SoundBook.Fuse(0), Is.Null);
        }

        [Test]
        public void TheEventsNoViewPlays_SoundAsTheOriginalsScriptsDid()
        {
            Assert.That(SoundBook.ForEvent(new Shrunk(2, 0, 0.5)), Is.EqualTo(SoundCue.Shrink));
            Assert.That(SoundBook.ForEvent(new Moved(1, Vec2.Zero, new Vec2(1, 1), MoveCause.Teleported)), Is.EqualTo(SoundCue.Teleport));
            Assert.That(SoundBook.ForEvent(new Moved(1, Vec2.Zero, new Vec2(1, 1), MoveCause.Drove)), Is.Null);
            Assert.That(SoundBook.ForEvent(new CratePickedUp(1, DropKind.Health, 20)), Is.EqualTo(SoundCue.Powerup));
            Assert.That(SoundBook.ForEvent(new Countdown(3)), Is.EqualTo(SoundCue.Beep));
            Assert.That(SoundBook.ForEvent(new TurnPassed(0, TurnPassReason.TimedOut)), Is.EqualTo(SoundCue.Sigh));
            Assert.That(SoundBook.ForEvent(new TurnPassed(0, TurnPassReason.EndedEarly)), Is.Null);
            Assert.That(SoundBook.ForEvent(new Eliminated(1)), Is.EqualTo(SoundCue.Sigh));
            Assert.That(SoundBook.ForEvent(new RoundOver(0)), Is.EqualTo(SoundCue.Applause));
            Assert.That(SoundBook.ForEvent(new RoundOver(-1)), Is.Null, "nobody survived: nobody to applaud");
            Assert.That(SoundBook.ForEvent(new TurnStarted(0)), Is.Null);
        }

        [Test]
        public void TheKnockOutThatEndsTheRound_IsApplauded_NotSighedAt()
        {
            Assert.That(SoundBook.Settle(new[] { SoundCue.Explode, SoundCue.Sigh, SoundCue.Applause }),
                Is.EqualTo(new[] { SoundCue.Explode, SoundCue.Applause }));
            Assert.That(SoundBook.Settle(new[] { SoundCue.Sigh, SoundCue.Beep }), Is.EqualTo(new[] { SoundCue.Sigh, SoundCue.Beep }));
            Assert.That(SoundBook.Settle(null), Is.Empty);
        }

        [Test]
        public void TheEngine_TurnsOn_Runs_AndShutsOffAsPlayMovementSoundAndStopMovingDid()
        {
            var engine = new EngineSound();
            Assert.That(engine.Update(false, false, false), Is.EqualTo(EngineSound.Step.None), "standing still");
            Assert.That(engine.Update(true, false, false), Is.EqualTo(EngineSound.Step.TurnOn));
            Assert.That(engine.Moving, Is.True);
            Assert.That(engine.Update(true, true, false), Is.EqualTo(EngineSound.Step.None), "while the turn-on plays");
            Assert.That(engine.Update(true, false, false), Is.EqualTo(EngineSound.Step.Run), "then the engine");
            Assert.That(engine.Update(true, false, true), Is.EqualTo(EngineSound.Step.None));
            Assert.That(engine.Update(false, false, true), Is.EqualTo(EngineSound.Step.ShutOff));
            Assert.That(engine.Moving, Is.False);

            Assert.That(engine.Update(true, false, false), Is.EqualTo(EngineSound.Step.TurnOn));
            Assert.That(engine.Update(false, true, false), Is.EqualTo(EngineSound.Step.None), "stopped during the turn-on: no shut-off");
        }

        // U.RICO-T27: the music.
        [Test]
        public void PlayRandomMusic_PicksAmongTheOriginalsFiveTracks_ByItsOneToFiveRoll()
        {
            Assert.That(MusicBook.Pick(1), Is.EqualTo(MusicTrack.PhatSketch));
            Assert.That(MusicBook.Pick(2), Is.EqualTo(MusicTrack.GoCart));
            Assert.That(MusicBook.Pick(3), Is.EqualTo(MusicTrack.Rocket));
            Assert.That(MusicBook.Pick(4), Is.EqualTo(MusicTrack.LatinIndustries));
            Assert.That(MusicBook.Pick(5), Is.EqualTo(MusicTrack.CutAndRun));
            Assert.That(MusicBook.Pick(0), Is.EqualTo(MusicTrack.CutAndRun), "wrapped, never out of range");
            Assert.That(MusicBook.Pick(6), Is.EqualTo(MusicTrack.PhatSketch));
        }

        [Test]
        public void EveryTrackIsTheOriginalsRecording_ImportedIntoTheMusicFolder()
        {
            string folder = FindUp("Assets/Ricochet/Resources/" + MusicBook.ResourceFolder);
            foreach (MusicTrack track in MusicBook.All)
            {
                Assert.That(MusicBook.Recording(track), Does.StartWith("music_"));
                Assert.That(Directory.GetFiles(folder, MusicBook.Recording(track) + ".*").Any(f => !f.EndsWith(".meta")), Is.True, track + " is imported");
            }

            Assert.That(MusicBook.ResourcePath(MusicTrack.GoCart), Is.EqualTo("Ricochet/Music/music_gocart"));
        }

        [Test]
        public void Music_PlaysAtTheMixsMusicVolume_ZeroWhenMutedOrSessionMuted()
        {
            var mix = new AudioMix { MusicVolume = 0.4f, SfxVolume = 0.9f };
            Assert.That(MusicBook.Volume(mix, false), Is.EqualTo(0.4f).Within(1e-6));
            mix.MuteMusic = true;
            Assert.That(MusicBook.Volume(mix, false), Is.EqualTo(0f));
            mix.MuteMusic = false;
            Assert.That(MusicBook.Volume(mix, true), Is.EqualTo(0f));
            Assert.That(MusicBook.Volume(null, false), Is.EqualTo(0f));
        }

        [Test]
        public void ANewRoundOrTurnOfAMatch_IsAMomentTheOriginalChangedTrack()
        {
            Assert.That(MusicBook.IsNewMoment(true, 1, 0, 1, 0), Is.False, "nothing changed");
            Assert.That(MusicBook.IsNewMoment(true, 1, 0, 1, 1), Is.True, "next_player.gml");
            Assert.That(MusicBook.IsNewMoment(true, 1, 1, 2, 1), Is.True, "randomize_level.gml");
            Assert.That(MusicBook.IsNewMoment(false, 1, 0, 2, 1), Is.False, "no match, no track change");
        }

        private static string FindUp(string relative)
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, relative);
                if (Directory.Exists(candidate) || File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            Assert.Fail(relative + " not found above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
