// A GML sprite played once where something happened, then gone (U.RICO-T47): the original's one-shot effect objects,
// which play their sprite at their image_speed and destroy themselves at its last frame (their Animation End event),
// such as obj_boom_normal's spr_smallexplosion where a hit lands (hit.gml) and obj_boom's where a vortex is set off.
using UnityEngine;

namespace Ricochet
{
    public sealed class SpriteBurst : MonoBehaviour
    {
        private string _sprite;
        private float _imageSpeed;
        private float _age;
        private SpriteRenderer _renderer;

        /// <summary>Every burst playing now (for tests and the showcase).</summary>
        public static int Playing { get; private set; }

        /// <summary>The GML sprite it plays.</summary>
        public string Sprite => _sprite;

        /// <summary>
        /// Plays GML sprite <paramref name="sprite"/> once at <paramref name="at"/> at <paramref name="imageSpeed"/> frames a
        /// step; null (nothing drawn) when the catalogue lacks it.
        /// </summary>
        public static SpriteBurst Play(string sprite, Vector2 at, float imageSpeed, int sortingOrder = 6, float scale = 1f, float alpha = 1f)
        {
            Sprite first = GameArt.Frame(sprite);
            if (first == null || GameArt.Duration(sprite, imageSpeed) <= 0f)
            {
                return null;
            }

            var go = new GameObject("Burst (" + sprite + ")");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var burst = go.AddComponent<SpriteBurst>();
            burst._sprite = sprite;
            burst._imageSpeed = imageSpeed;
            burst._renderer = go.AddComponent<SpriteRenderer>();
            burst._renderer.sprite = first;
            burst._renderer.color = new Color(1f, 1f, 1f, alpha);
            burst._renderer.sortingOrder = sortingOrder;
            return burst;
        }

        private void OnEnable()
        {
            Playing++;
        }

        private void OnDisable()
        {
            Playing--;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= GameArt.Duration(_sprite, _imageSpeed))
            {
                Destroy(gameObject);
                return;
            }

            _renderer.sprite = GameArt.FrameAt(_sprite, _age, false, _imageSpeed);
        }
    }
}
