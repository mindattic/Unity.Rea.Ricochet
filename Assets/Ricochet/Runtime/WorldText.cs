// Text in the world, as TextMeshPro objects (UR-T38; it was IMGUI drawn at WorldToScreenPoint): the name under a player,
// the pickup and damage figures floating over one, a crate's mark. They live in the scene, so they scale with the camera,
// sort with the sprites, take the theme's font and never draw over the kit's menus.
using MindAttic.Components.Unity; // MaTheme
using TMPro;
using UnityEngine;

namespace Ricochet
{
    internal static class WorldText
    {
        /// <summary>Sorting order of world labels: over the squares (0 to 2).</summary>
        public const int Order = 10;

        /// <summary>
        /// A centred label under <paramref name="parent"/>. <paramref name="height"/> is the glyph height in world units
        /// (the old IMGUI labels were about 0.25 at the main camera's 7-unit half-height on 720 lines).
        /// </summary>
        public static TextMeshPro Create(Transform parent, string name, float height, Color color, bool bold = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            TMP_FontAsset font = MaTheme.Font;
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = height * 10f; // TextMeshPro's world scale: one point is a tenth of a unit
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.sortingOrder = Order;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Sets the text only when it changed.</summary>
        public static void Set(TMP_Text text, string value)
        {
            if (text.text != value)
            {
                text.text = value;
            }
        }
    }
}
