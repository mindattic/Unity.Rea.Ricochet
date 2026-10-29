// Plain coloured squares, made in code, so the template needs no art, no materials and works in any render pipeline.
// Replace them with your own sprites or models.
using UnityEngine;

namespace Ricochet
{
    internal static class Shapes
    {
        private static Sprite s_square;

        /// <summary>The one-unit white square every code-drawn shape is (the fallback where the original's art is missing).</summary>
        public static Sprite SquareSprite
        {
            get
            {
                if (s_square == null)
                {
                    Texture2D white = Texture2D.whiteTexture;
                    s_square = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
                }

                return s_square;
            }
        }

        /// <summary>Gives <paramref name="owner"/> a SpriteRenderer drawing a square <paramref name="size"/> units wide.</summary>
        public static SpriteRenderer Square(GameObject owner, float size, int sortingOrder)
        {
            SpriteRenderer renderer = owner.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = owner.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = SquareSprite;
            renderer.sortingOrder = sortingOrder;
            owner.transform.localScale = new Vector3(size, size, 1f);
            return renderer;
        }
    }
}
