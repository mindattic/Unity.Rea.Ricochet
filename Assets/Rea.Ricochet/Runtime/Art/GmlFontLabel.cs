// Keeps a themed label in its GML font (U.RICO-T44): see GameArt.ApplyFont.
using MindAttic.Components.Unity; // MaTheme, ThemeTokens
using TMPro;
using UnityEngine;

namespace Ricochet
{
    /// <summary>
    /// Keeps a themed label (a MindAttic Components MaText, which restyles itself from the theme on enable and on every
    /// theme change) in its GML font: added after the label, it reapplies the font each time the theme would reset it.
    /// When the catalogue lacks the font it leaves the theme's.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GmlFontLabel : MonoBehaviour
    {
        [SerializeField] private string _font;
        [SerializeField] private int _outline;
        private TMP_Text _text;

        public string FontName => _font;

        public int OutlinePixels => _outline;

        /// <summary>Puts <paramref name="text"/> in GML font <paramref name="font"/> (outlined <paramref name="outline"/> px) and keeps it there.</summary>
        public static GmlFontLabel Attach(TMP_Text text, string font, int outline = 0)
        {
            GmlFontLabel label = text.GetComponent<GmlFontLabel>();
            if (label == null)
            {
                label = text.gameObject.AddComponent<GmlFontLabel>();
            }

            label._text = text;
            label._font = font;
            label._outline = outline;
            label.Apply();
            return label;
        }

        private void OnEnable()
        {
            MaTheme.Changed += OnTheme;
            Apply();
        }

        private void OnDisable()
        {
            MaTheme.Changed -= OnTheme;
        }

        // MaText also restyles itself (font included) whenever its colour is overridden, so check each frame too.
        private void LateUpdate()
        {
            if (_text != null && _text.font != GameArt.Font(_font))
            {
                Apply();
            }
        }

        private void OnTheme(ThemeTokens theme)
        {
            if (this != null)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }

            if (!string.IsNullOrEmpty(_font))
            {
                GameArt.ApplyFont(_text, _font, _outline);
            }
        }
    }
}
