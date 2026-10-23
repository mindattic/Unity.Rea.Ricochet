// A practice dummy: a networked object the server spawns with a key ("dummy:0", "dummy:1", ...) through
// NetSession.Objects.Spawn, for shots to land on before there are turns and opponents (UR-T4). Its prefab carries a
// NetKey beside the NetworkObject, so every peer, late joiners included, finds it by that key
// (session.Objects.TryGet("dummy:0", out Dummy dummy)) instead of by spawn order.
using Unity.Multiplayer;      // NetEntity, NetKey
using Unity.Multiplayer.Core; // [Synced]
using UnityEngine;

namespace Ricochet
{
    public partial class Dummy : NetEntity
    {
        private static readonly Color32 Standing = new Color32(176, 132, 84, 255);
        private static readonly Color32 Knocked = new Color32(176, 132, 84, 70);

        // Placed by the server (Arena); every peer draws it where the server says.
        [Synced] private Vector2 _position;

        // Set by the server when a shot lands on it (Arena.Knock); a knocked dummy is no longer a target.
        [Synced] private bool _down;

        private NetKey _key;
        private SpriteRenderer _body;

        /// <summary>The key the server spawned this dummy with.</summary>
        public string Key
        {
            get
            {
                if (_key == null)
                {
                    _key = GetComponent<NetKey>();
                }

                return _key != null ? _key.Key : null;
            }
        }

        protected override void OnSpawned()
        {
            _body = Shapes.Square(gameObject, Arena.DummyRadius * 2f * 0.9f, -1);
            if (IsServer && Position == Vector2.zero)
            {
                Position = Arena.DummyStart(Arena.DummyIndex(Key));
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            transform.position = Position;
            transform.rotation = Quaternion.Euler(0f, 0f, Down ? 45f : 0f);
            if (_body != null)
            {
                _body.color = Down ? Knocked : Standing;
            }
        }
    }
}
