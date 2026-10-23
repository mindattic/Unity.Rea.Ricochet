// A crate on the wire: one per Core Drop, which the server spawns under a key ("crate:1", "crate:2", ...) when its
// DropField drops one (Arena), and despawns when it bursts, is collected, or the round ends. Every peer draws it falling
// from above the top of the level to its spot at the rules' speed (the original's tick(10) a frame), then sitting there
// with its kind's mark over it (obj_weapondrop's gun icon, obj_itemdrop's, obj_healthdrop's cross, obj_powerup's): W,
// I, + or ?. Only a landed crate can be collected (by a shot passing over it, Arena), so only the landing is synced.
using Ricochet.Core;          // DropKind
using Unity.Multiplayer;      // NetEntity, NetKey
using Unity.Multiplayer.Core; // [Synced]
using UnityEngine;

namespace Ricochet
{
    public partial class Crate : NetEntity
    {
        public const string KeyPrefix = "crate:";

        /// <summary>The crate's square is drawn this wide.</summary>
        public const float Size = 0.6f;

        // Written by the server (Arena) as it spawns the crate, and when it lands.
        [Synced] private int _kind;
        [Synced] private Vector2 _spot;
        [Synced] private bool _landed;

        private SpriteRenderer _body;
        private float _shownY = float.NaN;
        private GUIStyle _markStyle;

        public DropKind DropKind => (DropKind)Mathf.Clamp(Kind, 0, DropRules.KindCount - 1);

        /// <summary>Where this peer draws it now (falling, or at its spot).</summary>
        public Vector2 Shown => new Vector2(Spot.x, float.IsNaN(_shownY) ? Spot.y : _shownY);

        public static string KeyFor(int id) => KeyPrefix + id;

        /// <summary>The colour of a kind's crate.</summary>
        public static Color ColorOf(DropKind kind)
        {
            switch (kind)
            {
                case DropKind.Weapon: return new Color(0.95f, 0.55f, 0.2f);
                case DropKind.Item: return new Color(0.35f, 0.65f, 1f);
                case DropKind.Health: return new Color(0.3f, 0.85f, 0.35f);
                default: return new Color(0.85f, 0.4f, 0.95f);
            }
        }

        /// <summary>The mark drawn over a kind's crate.</summary>
        public static string MarkOf(DropKind kind)
        {
            switch (kind)
            {
                case DropKind.Weapon: return "W";
                case DropKind.Item: return "I";
                case DropKind.Health: return "+";
                default: return "?";
            }
        }

        protected override void OnSpawned()
        {
            _body = Shapes.Square(gameObject, Size, -1);
            _shownY = Landed ? Spot.y : (float)(Arena.Level.MaxY + 1);
            Place();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (Landed || float.IsNaN(_shownY))
            {
                _shownY = Spot.y;
            }
            else
            {
                _shownY = Mathf.Max(Spot.y, _shownY - (float)Arena.DropRules.FallSpeed * Time.deltaTime);
            }

            Place();
        }

        private void Place()
        {
            transform.position = Shown;
            if (_body != null)
            {
                Color color = ColorOf(DropKind);
                color.a = Landed ? 1f : 0.7f;
                _body.color = color;
            }
        }

        private void OnGUI()
        {
            Camera camera = Camera.main;
            if (!IsSpawned || camera == null)
            {
                return;
            }

            if (_markStyle == null)
            {
                _markStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
                _markStyle.normal.textColor = Color.white;
            }

            Vector3 screen = camera.WorldToScreenPoint(transform.position);
            GUI.Label(new Rect(screen.x - 15f, Screen.height - screen.y - 12f, 30f, 24f), MarkOf(DropKind), _markStyle);
        }
    }
}
