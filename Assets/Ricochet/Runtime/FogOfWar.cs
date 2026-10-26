// Fog of war (UR-T26: the original's fogofwar setting and obj_fogscreen, which randomize_level's reset_fogofwar created to
// hide the arena). It is a match rule the host sets (MatchSetup.FogOfWar, synced through Turns), drawn on every peer for
// that peer's own local player: a dark sprite with a clear hole that follows their tank, so each player sees only what the
// fog reveals for them. With the rule off, or no local human tank to centre on, nothing is drawn.
using Ricochet.Core.Match;
using Unity.Multiplayer; // NetSession
using UnityEngine;

namespace Ricochet
{
    public sealed class FogOfWar : MonoBehaviour
    {
        /// <summary>Over the world labels (<see cref="WorldText.Order"/>), under the HUD's canvases.</summary>
        public const int SortingOrder = 11;

        /// <summary>How far, in world units, a tank sees through the fog.</summary>
        public const float SightRadius = 5f;

        private const int TextureSize = 256;
        private const float HoleFraction = 0.12f; // the clear hole's radius as a share of the sprite's half-width
        private const float EdgeFraction = 0.04f; // the soft edge around it

        private MatchHud _hud;
        private SpriteRenderer _renderer;
        private static Sprite _sprite;

        /// <summary>True while the fog is drawn.</summary>
        public bool Active => _renderer != null && _renderer.enabled;

        /// <summary>Where the clear hole is, in world space (the local tank), while the fog is up.</summary>
        public Vector3 Centre => _renderer != null ? _renderer.transform.position : Vector3.zero;

        /// <summary>Adds the fog to <paramref name="hud"/>'s GameObject; it follows whatever match the HUD's session plays.</summary>
        public static FogOfWar Create(MatchHud hud)
        {
            var fog = hud.gameObject.AddComponent<FogOfWar>();
            fog._hud = hud;
            return fog;
        }

        private void OnDestroy()
        {
            if (_renderer != null)
            {
                Destroy(_renderer.gameObject);
            }
        }

        private void Update()
        {
            NetSession session = _hud != null ? _hud.CurrentSession : null;
            Turns turns = session != null && session.IsLive ? Turns.For(session) : null;
            Player local = null;
            if (turns != null && turns.FogOfWar && turns.MatchPhase != MatchPhase.GameOver)
            {
                foreach (Player player in Player.In(session))
                {
                    if (player.IsLocalHuman)
                    {
                        local = player;
                        break;
                    }
                }
            }

            if (local == null)
            {
                if (_renderer != null && _renderer.enabled)
                {
                    _renderer.enabled = false;
                }

                return;
            }

            Build();
            _renderer.enabled = true;
            _renderer.transform.position = new Vector3(local.transform.position.x, local.transform.position.y, 0f);
        }

        private void Build()
        {
            if (_renderer != null)
            {
                return;
            }

            if (_sprite == null)
            {
                _sprite = MakeSprite();
            }

            var go = new GameObject("Fog of war");
            go.transform.SetParent(transform, false);
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = _sprite;
            _renderer.sortingOrder = SortingOrder;
            float world = SightRadius / HoleFraction * 2f; // the sprite's width: large enough to cover the whole arena
            go.transform.localScale = Vector3.one * (world / (TextureSize / _sprite.pixelsPerUnit));
        }

        // Opaque black, clear inside the hole, a soft edge between; the sprite is one world unit wide per 256 pixels' worth.
        private static Sprite MakeSprite()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Fog of war",
            };
            var pixels = new Color32[TextureSize * TextureSize];
            float half = TextureSize / 2f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                    float alpha = Mathf.Clamp01((distance - HoleFraction) / EdgeFraction);
                    pixels[y * TextureSize + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        }
    }
}
