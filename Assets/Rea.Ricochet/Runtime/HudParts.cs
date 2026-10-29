// The small uGUI building blocks the match HUD is made of (UR-T38): a rect, a solid image, a MindAttic Components label
// fitted to its box. All built in code, never touching the pointer, so the HUD scales with the canvas (the kit's 1920x1080
// reference), takes the theme's font, and sorts under the kit's menus by its canvas order instead of IMGUI's always-on-top.
using MindAttic.Components.Unity; // MaText, TextStyle
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet
{
    internal static class HudParts
    {
        private const int UiLayer = 5;

        /// <summary>An empty RectTransform under <paramref name="parent"/>, anchored and pivoted at <paramref name="anchor"/>.</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = UiLayer;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        /// <summary>Makes <paramref name="rect"/> fill its parent, inset by <paramref name="inset"/> on every side.</summary>
        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>A solid-colour image (no sprite) that never takes the pointer.</summary>
        public static Image Solid(string name, Transform parent, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = UiLayer;
            rect.SetParent(parent, false);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A themed label that shrinks to fit its box between <paramref name="minSize"/> and <paramref name="maxSize"/>, drawn in
        /// the original's GML font <paramref name="gmlFont"/> when one is named (U.RICO-T44; outlined <paramref name="outline"/>
        /// pixels as draw_text_outline does), so the theme keeps its layout but not its face.
        /// </summary>
        public static MaText Label(Transform parent, TextStyle style, Color color, float minSize, float maxSize, TextAlignmentOptions alignment,
            bool bold = false, string gmlFont = null, int outline = 0)
        {
            MaText text = MaText.Create(parent, string.Empty, style, alignment).WithColor(color);
            TextMeshProUGUI tmp = text.Tmp;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;
            tmp.fontSize = maxSize;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            if (bold)
            {
                tmp.fontStyle |= FontStyles.Bold;
            }

            if (!string.IsNullOrEmpty(gmlFont))
            {
                GmlFontLabel.Attach(tmp, gmlFont, outline);
            }

            return text;
        }

        /// <summary>Sets a label's text only when it changed (MaText.Text also renames the object, which allocates).</summary>
        public static void Set(MaText text, string value)
        {
            if (text.Tmp.text != value)
            {
                text.Tmp.text = value;
            }
        }

        /// <summary>Shows or hides <paramref name="item"/> without touching it when it already is.</summary>
        public static void Show(Component item, bool visible)
        {
            if (item.gameObject.activeSelf != visible)
            {
                item.gameObject.SetActive(visible);
            }
        }
    }
}
