// The marks hits and knock-outs leave on the level (U.RICO-T48, docs/BIBLE.md "Hit and knock-out effects"), on every peer:
//   Scorch marks   obj_scorchmark_small under every obj_boom_normal (a hit), obj_explosion (a shell spent) and obj_boom (a
//                  vortex set off), obj_scorchmark under every obj_grenade_explosion, obj_playerexplosion and
//                  obj_powerup_explosion; each loses 0.0001 of its alpha a step and all go with the round.
//   Knock-outs     obj_playerexplosion: spr_bigexplosion at image_speed 0.5 holding its last frame, sfx_explode, a big scorch
//                  mark, and 30 steps in obj_flame's smoke (ef_smokeup in a random dark grey every step) until the round goes.
//   Crate bursts   obj_powerup_explosion: spr_bigexplosion at 0.5, gone at its end, sfx_explode, a big scorch mark.
//   Crates taken   the collision events' ef_ring 25 px over where the crate stood.
//   Shield walls   obj_shieldwall breaking: ef_ring and ef_firework in white and spawn_glass's 20-30 shards (spr_glass1-5)
//                  flying, turning and fading 0.02 a step, with sfx_shatter when a shot broke it.
// The level going (a new round, a reroll, a session ending: Arena's level version, map or seed changing) sweeps it all away,
// as the original's clear_objects did, float texts included. Sounds go through GameAudio, which honours the session mute.
// What to draw comes from the Presentation feed (ShieldWallShattered and CrateGone, which the server sends every peer) and
// from the views that see a shot land (ShotView) or a player knocked out (Player), so every peer draws the same thing.
using System.Collections.Generic;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
using Ricochet.Core.Menu;
using UnityEngine;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet
{
    public sealed class FieldEffects : MonoBehaviour
    {
        /// <summary>Scorch marks lie on the floor and the shapes' shadows, under the tracks and the tanks (depth 2).</summary>
        public const int ScorchOrder = -5;

        /// <summary>The glass flies over the tanks (depth 0, drawn after them).</summary>
        public const int GlassOrder = 6;

        /// <summary>obj_playerexplosion's depth -1002: over the shots.</summary>
        public const int KnockOutOrder = 7;

        public const string ScorchSprite = "spr_scorchmark";
        public const string SmallScorchSprite = "spr_scorchmark_small";

        private static FieldEffects s_instance;

        private readonly List<Scorch> _scorches = new List<Scorch>();
        private readonly List<Blast> _knockOuts = new List<Blast>();
        private readonly List<Shard> _shards = new List<Shard>();
        private readonly System.Random _dice = new System.Random();
        private (int Version, int Map, int Seed) _level;

        /// <summary>Scorch marks on the level now (for tests).</summary>
        public static int Scorches => s_instance != null ? s_instance._scorches.Count : 0;

        /// <summary>Knocked-out tanks' explosions held on the level now (for tests).</summary>
        public static int KnockOuts => s_instance != null ? s_instance._knockOuts.Count : 0;

        /// <summary>Of those, the ones smoking (obj_flame) now.</summary>
        public static int Flames
        {
            get
            {
                int count = 0;
                if (s_instance != null)
                {
                    foreach (Blast blast in s_instance._knockOuts)
                    {
                        count += blast.Smoking ? 1 : 0;
                    }
                }

                return count;
            }
        }

        /// <summary>Glass shards in the air now (for tests).</summary>
        public static int Shards => s_instance != null ? s_instance._shards.Count : 0;

        /// <summary>The scene's effects, made the first time something asks.</summary>
        public static FieldEffects Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new GameObject("Field effects").AddComponent<FieldEffects>();
                }

                return s_instance;
            }
        }

        /// <summary>
        /// Makes sure the effects are listening to the feed (the game calls it at start, so a shield wall breaking or a
        /// crate going is drawn even before anything else has asked).
        /// </summary>
        public static void Ensure() => _ = Instance;

        /// <summary>obj_scorchmark (<paramref name="big"/>) or obj_scorchmark_small at <paramref name="at"/>.</summary>
        public static void ScorchAt(Vector2 at, bool big) => Instance.AddScorch(at, big);

        /// <summary>obj_playerexplosion where a tank was knocked out.</summary>
        public static void KnockOut(Vector2 at) => Instance.AddKnockOut(at);

        /// <summary>obj_powerup_explosion where a crate burst.</summary>
        public static void CrateBurst(Vector2 at)
        {
            SpriteBurst.Play(GmlSprites.BigExplosion, at, (float)HitLooks.CrateBurstImageSpeed, KnockOutOrder);
            GameAudio.Play(SoundCue.Blast); // sfx_explode
            ScorchAt(at, true);
        }

        /// <summary>A shield wall breaking at <paramref name="at"/>: its ring, firework and glass, and sfx_shatter when <paramref name="struck"/>.</summary>
        public static void Shatter(Vector2 at, bool struck)
        {
            GmlParticles.Play(GmEffect.Ring, at, 1, Color.white);
            GmlParticles.Play(GmEffect.Firework, at, 1, Color.white);
            Instance.SpawnGlass(at);
            if (struck)
            {
                GameAudio.Play(SoundCue.Shatter);
            }
        }

        /// <summary>Sweeps the level clean (clear_objects): scorch marks, held explosions and their smoke, glass and float texts.</summary>
        public static void Clear()
        {
            if (s_instance != null)
            {
                s_instance.ClearAll();
            }

            FloatText.ClearAll();
        }

        private void OnEnable()
        {
            Presentation.Raised += OnEvent;
            _level = (Arena.LevelVersion, Arena.MapIndex, Arena.LevelSeed);
        }

        private void OnDisable()
        {
            Presentation.Raised -= OnEvent;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        // What the server sent every peer: a shield wall broke, a crate was taken or burst.
        private void OnEvent(Player who, Cmd.GameEvent e)
        {
            switch (e)
            {
                case Cmd.ShieldWallShattered wall:
                    Shatter(Arena.ToUnity(wall.Center), wall.Struck);
                    break;
                case Cmd.CrateGone crate when crate.End == DropEnd.Burst:
                    CrateBurst(Arena.ToUnity(crate.Spot));
                    break;
                case Cmd.CrateGone crate when crate.End == DropEnd.Collected:
                    GmlParticles.Play(GmEffect.Ring, Arena.ToUnity(crate.Spot) + Vector2.up * ((float)HitLooks.CratePickupAbove * Steps.Pixel), 1, Color.white);
                    break;
            }
        }

        private void Update()
        {
            var level = (Arena.LevelVersion, Arena.MapIndex, Arena.LevelSeed);
            if (level != _level)
            {
                _level = level;
                Clear(); // a new round or level: clear_objects
            }

            float steps = Time.deltaTime * Steps.PerSecond;
            for (int i = _scorches.Count - 1; i >= 0; i--)
            {
                Scorch scorch = _scorches[i];
                scorch.Steps += steps;
                float alpha = (float)HitLooks.ScorchAlphaAt(scorch.Steps);
                if (alpha <= 0f || scorch.Renderer == null)
                {
                    Remove(scorch.Renderer);
                    _scorches.RemoveAt(i);
                    continue;
                }

                scorch.Renderer.color = new Color(1f, 1f, 1f, alpha);
            }

            foreach (Blast blast in _knockOuts)
            {
                StepKnockOut(blast, steps);
            }

            for (int i = _shards.Count - 1; i >= 0; i--)
            {
                Shard shard = _shards[i];
                shard.Steps += steps;
                float alpha = 1f - (float)HitLooks.GlassFadePerStep * shard.Steps;
                if (alpha <= 0f || shard.Renderer == null)
                {
                    Remove(shard.Renderer);
                    _shards.RemoveAt(i);
                    continue;
                }

                Transform t = shard.Renderer.transform;
                t.position += (Vector3)(shard.Velocity * steps);
                t.rotation = Quaternion.Euler(0f, 0f, t.eulerAngles.z + shard.Spin * steps);
                shard.Renderer.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        private void StepKnockOut(Blast blast, float steps)
        {
            blast.Steps += steps;
            if (blast.Renderer != null)
            {
                // Animation End: image_index = 19, the last frame held until the round is swept.
                Sprite[] frames = GameArt.Frames(GmlSprites.BigExplosion);
                if (frames != null)
                {
                    int frame = Mathf.Min(Mathf.FloorToInt(blast.Steps * (float)HitLooks.KnockOutImageSpeed), Mathf.Min(HitLooks.KnockOutHoldFrame, frames.Length - 1));
                    blast.Renderer.sprite = frames[frame];
                }
            }

            if (blast.Steps < HitLooks.FlameDelaySteps)
            {
                return;
            }

            // obj_flame: an ef_smokeup every step, each in its own dark grey, 5 px over the spot.
            blast.Smoking = true;
            blast.SmokeClock += steps;
            Vector2 at = blast.At + Vector2.up * ((float)HitLooks.FlameSmokeAbove * Steps.Pixel);
            while (blast.SmokeClock >= 1f)
            {
                blast.SmokeClock -= 1f;
                GmlParticles.Play(GmEffect.SmokeUp, at, 0, Steps.Of(HitLooks.FlameColor(_dice), 1f));
            }
        }

        private void AddScorch(Vector2 at, bool big)
        {
            Sprite sprite = GameArt.Frame(big ? ScorchSprite : SmallScorchSprite);
            if (sprite == null)
            {
                return;
            }

            SpriteRenderer renderer = Steps.Sprite(transform, big ? "Scorch mark" : "Small scorch mark", ScorchOrder);
            renderer.sprite = sprite;
            renderer.transform.position = new Vector3(at.x, at.y, 0f);
            _scorches.Add(new Scorch { Renderer = renderer });
        }

        private void AddKnockOut(Vector2 at)
        {
            GameAudio.Play(SoundCue.Blast); // sfx_explode
            AddScorch(at, true);
            SpriteRenderer renderer = null;
            Sprite first = GameArt.Frame(GmlSprites.BigExplosion);
            if (first != null)
            {
                renderer = Steps.Sprite(transform, "Knock-out", KnockOutOrder);
                renderer.sprite = first;
                renderer.transform.position = new Vector3(at.x, at.y, 0f);
            }

            _knockOuts.Add(new Blast { Renderer = renderer, At = at });
        }

        private void SpawnGlass(Vector2 at)
        {
            foreach (GlassShard shard in HitLooks.SpawnGlass(_dice))
            {
                Sprite sprite = GameArt.Frame("spr_glass" + (shard.Sprite + 1));
                if (sprite == null)
                {
                    continue;
                }

                SpriteRenderer renderer = Steps.Sprite(transform, "Glass", GlassOrder);
                renderer.sprite = sprite;
                // GameMaker's y grows downward: its offsets and speeds turn over for Unity's.
                renderer.transform.position = new Vector3(at.x + shard.OffsetX * Steps.Pixel, at.y - shard.OffsetY * Steps.Pixel, 0f);
                _shards.Add(new Shard
                {
                    Renderer = renderer,
                    Velocity = new Vector2(shard.SpeedX, -shard.SpeedY) * Steps.Pixel,
                    Spin = shard.Spin, // image_angle and Unity's z both turn anticlockwise on screen
                });
            }
        }

        private void ClearAll()
        {
            foreach (Scorch scorch in _scorches)
            {
                Remove(scorch.Renderer);
            }

            foreach (Blast blast in _knockOuts)
            {
                Remove(blast.Renderer);
            }

            foreach (Shard shard in _shards)
            {
                Remove(shard.Renderer);
            }

            _scorches.Clear();
            _knockOuts.Clear();
            _shards.Clear();
        }

        private static void Remove(SpriteRenderer renderer)
        {
            if (renderer != null)
            {
                Destroy(renderer.gameObject);
            }
        }

        private sealed class Scorch
        {
            public SpriteRenderer Renderer;
            public float Steps;
        }

        private sealed class Blast
        {
            public SpriteRenderer Renderer;
            public Vector2 At;
            public float Steps;
            public float SmokeClock;
            public bool Smoking;
        }

        private sealed class Shard
        {
            public SpriteRenderer Renderer;
            public Vector2 Velocity; // units a step
            public float Spin;       // degrees a step
            public float Steps;
        }
    }
}
