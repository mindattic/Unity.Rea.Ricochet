// Fog of war (UR-T26, ported exactly in UR-T77 from obj_fogscreen, obj_fog and obj_fogofwar_emitter): a match rule the host
// sets (MatchSetup.FogOfWar, synced through Turns). Like the original's fogscreen, each peer keeps one small surface (the
// arena's size over 16, Core's FogSurface), black at its start, that light sources are stamped into every 10 steps and that
// is never cleared in between, so what a light has reached stays revealed; the surface is drawn over the arena scaled up
// (point filtered, the original's draw_surface_ext). The lights are the original's: every tank, every projectile in the
// air (bullet, pellet, laser, grenade, rail swirl, vortex), and every landed crate. A new level (or round) makes a new,
// fully black surface, as reset_fogofwar's new obj_fogscreen did. Every peer draws its own; nothing about it is sent.
using System.Collections.Generic;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Unity.Multiplayer; // NetSession
using UnityEngine;

namespace Ricochet
{
    public sealed class FogOfWar : MonoBehaviour
    {
        /// <summary>Over the world labels (<see cref="WorldText.Order"/>), under the HUD's canvases.</summary>
        public const int SortingOrder = 11;

        private readonly List<(Vector2 Spot, double Size)> _lights = new List<(Vector2 Spot, double Size)>();
        private MatchHud _hud;
        private SpriteRenderer _renderer;
        private Texture2D _texture;
        private Color32[] _pixels;
        private FogSurface _surface;
        private Level _surfaceLevel;
        private int _surfaceRound = -1;
        private float _nextUpdate;

        /// <summary>True while the fog is drawn.</summary>
        public bool Active => _renderer != null && _renderer.enabled;

        /// <summary>The surface being drawn (null until the fog has been up).</summary>
        public FogSurface Surface => _surface;

        /// <summary>The fog's opacity over an arena point on this peer: 0 revealed, 1 hidden (1 while there is no fog).</summary>
        public double OpacityAt(Vector2 spot) => Active && _surface != null ? _surface.OpacityAt(spot.x, spot.y) : 1.0;

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

            if (_texture != null)
            {
                Destroy(_texture);
            }
        }

        private void Update()
        {
            NetSession session = _hud != null ? _hud.CurrentSession : null;
            Turns turns = session != null && session.IsLive ? Turns.For(session) : null;
            if (turns == null || !turns.FogOfWar || turns.MatchPhase == MatchPhase.GameOver)
            {
                if (_renderer != null && _renderer.enabled)
                {
                    _renderer.enabled = false;
                    _surface = null; // reset_fogofwar: the next time the fog comes up it starts black
                }

                return;
            }

            Level level = Arena.Level;
            bool fresh = _surface == null || !ReferenceEquals(level, _surfaceLevel) || turns.Round != _surfaceRound;
            if (fresh)
            {
                Reset(level, turns.Round);
            }

            if (fresh || Time.time >= _nextUpdate)
            {
                _nextUpdate = Time.time + (float)FogSurface.UpdateSeconds;
                Light(session);
                Upload();
            }

            _renderer.enabled = true;
        }

        // obj_fogscreen's Create: a new surface at the level's size over 16 (obj_fogofwar_emitter's per-cell obj_fog is the
        // older, retired way of doing the same).
        private void Reset(Level level, int round)
        {
            _surfaceLevel = level;
            _surfaceRound = round;
            _surface = new FogSurface(level.MinX, level.MinY, level.MaxX, level.MaxY, ShapeLibrary.UnitsPerPixel);
            if (_texture != null)
            {
                Destroy(_texture);
            }

            _texture = new Texture2D(_surface.Width, _surface.Height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                name = "Fog of war",
            };
            _pixels = new Color32[_surface.Width * _surface.Height];
            if (_renderer == null)
            {
                var go = new GameObject("Fog of war");
                go.transform.SetParent(transform, false);
                _renderer = go.AddComponent<SpriteRenderer>();
                _renderer.sortingOrder = SortingOrder;
            }

            // One texel per cell, the sprite one texel per unit before scaling by a cell's size, anchored at the surface's corner.
            _renderer.sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), Vector2.zero, 1f);
            _renderer.transform.position = new Vector3((float)_surface.MinX, (float)_surface.MinY, 0f);
            _renderer.transform.localScale = new Vector3((float)_surface.CellSize, (float)_surface.CellSize, 1f);
        }

        // The Draw event's lights: obj_player, obj_bullet, obj_pellet, obj_laser, obj_grenade, obj_rail_swirl, obj_vortex, and
        // the landed obj_weapondrop, obj_itemdrop and obj_healthdrop, each at its lightSize (1; obj_powerup is 0.5 but is
        // never lit by the fogscreen) and lightStrength 1.
        private void Light(NetSession session)
        {
            foreach (Player player in Player.In(session))
            {
                _surface.Light(player.transform.position.x, player.transform.position.y, 1.0);
                _lights.Clear();
                player.CollectShotLights(_lights);
                foreach ((Vector2 spot, double size) in _lights)
                {
                    _surface.Light(spot.x, spot.y, size);
                }
            }

            foreach (Crate crate in FindObjectsByType<Crate>(FindObjectsSortMode.None))
            {
                if (crate.Landed && crate.DropKind != Ricochet.Core.Arsenal.DropKind.Powerup)
                {
                    _surface.Light(crate.Shown.x, crate.Shown.y, 1.0);
                }
            }
        }

        private void Upload()
        {
            for (int y = 0; y < _surface.Height; y++)
            {
                for (int x = 0; x < _surface.Width; x++)
                {
                    _pixels[y * _surface.Width + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt((float)_surface.Opacity(x, y) * 255f));
                }
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }
    }
}
