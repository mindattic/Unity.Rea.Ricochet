// U.RICO-T55, the credits roll over the credits room's picture (see Core's CreditsRoll): menu.json's credits, each section's
// heading in fnt_mono outlined 2 px as the menu rooms' captions and its lines in fnt_credits outlined 1 px, white and
// centred, climbing up over spr_creditsimage inside the picture's own rect (masked to it) from just under its bottom edge
// until the last line has left its top, then again. The picture is dimmed under the roll so the painted names it already
// carries do not fight the moving text. Each time the screen is shown the roll starts over; it runs on unscaled time.
using System.Collections.Generic;
using Ricochet.Core.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class CreditsRollView : MonoBehaviour
    {
        /// <summary>The names of the objects it adds, for tests.</summary>
        public const string WindowName = "Credits roll";
        public const string ContentName = "Credits roll lines";

        /// <summary>How opaque the picture stays under the roll.</summary>
        public const float PictureAlpha = 0.35f;

        /// <summary>fnt_credits is drawn at this many times its face (Verdana 10) so it reads at the room's size.</summary>
        private const float LineScale = 2f;

        private const float Padding = 16f;

        private RectTransform _window;
        private RectTransform _content;
        private double _seconds;

        /// <summary>Seconds since the roll (re)started.</summary>
        public double Seconds => _seconds;

        /// <summary>The lines' holder, whose top edge climbs the window.</summary>
        public RectTransform Content => _content;

        /// <summary>The lines, in order.</summary>
        public List<TextMeshProUGUI> Lines { get; } = new List<TextMeshProUGUI>();

        /// <summary>Rolls <paramref name="sections"/> over <paramref name="picture"/> (dimming it); the view.</summary>
        public static CreditsRollView Create(Image picture, IEnumerable<CreditsSection> sections)
        {
            Color dim = picture.color;
            dim.a = PictureAlpha;
            picture.color = dim;

            var window = new GameObject(WindowName, typeof(RectTransform)).GetComponent<RectTransform>();
            window.gameObject.layer = picture.gameObject.layer;
            window.SetParent(picture.transform, false);
            window.anchorMin = Vector2.zero;
            window.anchorMax = Vector2.one;
            window.offsetMin = window.offsetMax = Vector2.zero;
            window.gameObject.AddComponent<RectMask2D>();
            var view = window.gameObject.AddComponent<CreditsRollView>();
            view._window = window;

            var content = new GameObject(ContentName, typeof(RectTransform)).GetComponent<RectTransform>();
            content.gameObject.layer = window.gameObject.layer;
            content.SetParent(window, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(Padding, 0f);
            content.offsetMax = new Vector2(-Padding, 0f);
            VerticalLayoutGroup column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            column.spacing = 4f;
            ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view._content = content;

            float headingSize = GameArt.CanvasFontSize(CreditsRoll.HeadingFont, 36f);
            float lineSize = GameArt.CanvasFontSize(CreditsRoll.LineFont, 10f) * LineScale;
            bool first = true;
            foreach (CreditsSection section in sections)
            {
                if (!first)
                {
                    view.Spacer(headingSize * 0.75f);
                }

                first = false;
                if (section.Heading.Length > 0)
                {
                    view.Line(section.Heading, CreditsRoll.HeadingFont, 2, headingSize);
                }

                foreach (string line in section.Lines)
                {
                    view.Line(line, CreditsRoll.LineFont, 1, lineSize);
                }
            }

            view.Place();
            return view;
        }

        private void Line(string text, string font, int outline, float size)
        {
            var rect = new GameObject("Credits line: " + text, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = _content.gameObject.layer;
            rect.SetParent(_content, false);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            GameArt.ApplyFont(label, font, outline);
            label.text = text;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            Lines.Add(label);
        }

        private void Spacer(float height)
        {
            var rect = new GameObject("Credits gap", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = _content.gameObject.layer;
            rect.SetParent(_content, false);
            LayoutElement gap = rect.gameObject.AddComponent<LayoutElement>();
            gap.minHeight = gap.preferredHeight = height;
        }

        private void OnEnable()
        {
            _seconds = 0;
            Place();
        }

        private void Update()
        {
            _seconds += Time.unscaledDeltaTime;
            Place();
        }

        private void Place()
        {
            if (_window == null || _content == null)
            {
                return;
            }

            double window = _window.rect.height;
            double content = _content.rect.height;
            if (content <= 0)
            {
                content = LayoutUtility.GetPreferredHeight(_content);
            }

            double top = CreditsRoll.Top(_seconds, CreditsRoll.Speed, window, content);
            _content.anchoredPosition = new Vector2(0f, -(float)top);
        }
    }
}
