// GameMaker's built-in effects the original threw with effect_create_above (U.RICO-T48): ef_ring, ef_firework, ef_smokeup
// and ef_spark. GameMaker's runner drew these, not the game, so they are drawn here as the rebuild reads them (Core's
// HitLooks.Look: how many particles, how long they live, how big and how fast), each a soft disc or a ring tinted the
// colour the original passed, fading out over its life, above everything else on the level (effect_create_above), at the
// room speed. One pooled component draws them all; nothing here touches the game.
using System.Collections.Generic;
using Ricochet.Core.Art;
using UnityEngine;

namespace Ricochet
{
    public sealed class GmlParticles : MonoBehaviour
    {
        /// <summary>Over the shots and the tanks, under the world labels and the fog's frame.</summary>
        public const int Order = 7;

        private const int TextureSize = 64;

        private static GmlParticles s_instance;
        private static Sprite s_disc;
        private static Sprite s_ring;

        private readonly List<Particle> _live = new List<Particle>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private readonly System.Random _dice = new System.Random();

        /// <summary>Particles drawn now (for tests).</summary>
        public static int Count => s_instance != null ? s_instance._live.Count : 0;

        /// <summary>How many of <paramref name="effect"/> have been thrown since the scene started (for tests).</summary>
        public static int Thrown(GmEffect effect) => s_instance != null ? s_instance._thrown[(int)effect] : 0;

        private readonly int[] _thrown = new int[4];

        /// <summary>
        /// effect_create_above(<paramref name="effect"/>, x, y, <paramref name="size"/>, <paramref name="color"/>) at
        /// <paramref name="at"/> (world units).
        /// </summary>
        public static void Play(GmEffect effect, Vector2 at, int size, Color color)
        {
            Instance()?.Throw(effect, at, size, color);
        }

        /// <summary>Takes every particle away (a test tearing down).</summary>
        public static void Clear()
        {
            if (s_instance == null)
            {
                return;
            }

            foreach (Particle particle in s_instance._live)
            {
                s_instance.Recycle(particle.Renderer);
            }

            s_instance._live.Clear();
        }

        private static GmlParticles Instance()
        {
            if (s_instance == null)
            {
                s_instance = new GameObject("GameMaker effects").AddComponent<GmlParticles>();
            }

            return s_instance;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void Throw(GmEffect effect, Vector2 at, int size, Color color)
        {
            _thrown[(int)effect]++;
            GmEffectLook look = HitLooks.Look(effect, size);
            for (int i = 0; i < look.Count; i++)
            {
                float speed = (float)(look.SpeedMin + _dice.NextDouble() * (look.SpeedMax - look.SpeedMin));
                Vector2 heading = look.Upward ? Vector2.up : Random.insideUnitCircle.normalized;
                if (heading == Vector2.zero)
                {
                    heading = Vector2.up;
                }

                var particle = new Particle
                {
                    Renderer = Take(effect == GmEffect.Ring ? Ring() : Disc()),
                    Position = at,
                    Velocity = heading * speed * Steps.Pixel,
                    Size = (float)look.Size,
                    Growth = (float)look.SizeGrowth,
                    Life = look.LifeMin + _dice.Next(look.LifeMax - look.LifeMin + 1),
                    Alpha = (float)look.Alpha,
                    Color = color,
                };
                Draw(particle);
                _live.Add(particle);
            }
        }

        private void Update()
        {
            float steps = Time.deltaTime * Steps.PerSecond;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Particle particle = _live[i];
                particle.Age += steps;
                if (particle.Age >= particle.Life || particle.Renderer == null)
                {
                    Recycle(particle.Renderer);
                    _live.RemoveAt(i);
                    continue;
                }

                particle.Position += particle.Velocity * steps;
                particle.Size = Mathf.Max(0f, particle.Size + particle.Growth * steps);
                Draw(particle);
            }
        }

        private static void Draw(Particle particle)
        {
            float diameter = particle.Size * Steps.Pixel;
            particle.Renderer.transform.position = new Vector3(particle.Position.x, particle.Position.y, 0f);
            particle.Renderer.transform.localScale = new Vector3(diameter, diameter, 1f);
            Color color = particle.Color;
            color.a = particle.Alpha * Mathf.Clamp01(1f - particle.Age / Mathf.Max(1f, particle.Life));
            particle.Renderer.color = color;
        }

        private SpriteRenderer Take(Sprite sprite)
        {
            SpriteRenderer renderer = _pool.Count > 0 ? _pool.Pop() : null;
            if (renderer == null)
            {
                var go = new GameObject("Particle");
                go.transform.SetParent(transform, false);
                renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = Order;
            }

            renderer.sprite = sprite;
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        private void Recycle(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.gameObject.SetActive(false);
            _pool.Push(renderer);
        }

        // A soft round puff, one unit across: GameMaker's smoke and spark shapes, and a firework's sparks.
        private static Sprite Disc()
        {
            if (s_disc == null)
            {
                s_disc = Make("GameMaker disc", (d) => Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
            }

            return s_disc;
        }

        // A thin bright ring, one unit across: GameMaker's ring shape.
        private static Sprite Ring()
        {
            if (s_ring == null)
            {
                s_ring = Make("GameMaker ring", (d) => Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.1f));
            }

            return s_ring;
        }

        // A white sprite whose alpha is <paramref name="alphaAt"/> of the distance from its centre (0) to its edge (1).
        private static Sprite Make(string name, System.Func<float, float> alphaAt)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            float half = TextureSize / 2f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * alphaAt(d)));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private sealed class Particle
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity; // units a step
            public float Size;       // pixels across
            public float Growth;     // pixels a step
            public float Age;        // steps
            public float Life;       // steps
            public float Alpha;
            public Color Color;
        }
    }
}
