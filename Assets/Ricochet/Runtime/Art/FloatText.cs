// obj_float_text (U.RICO-T48), made by show_float_text, show_float_text_ext and show_float_text_color: a line of fnt_score
// outlined 1 px in black (draw_text_outline_color), drawn 10 px over where it was made, rising a pixel a step and fading by
// its fadespeed until it is gone; while flash is on its colour moves on every step through Core's HitLooks.FlashColorAt
// (white, red, yellow, blue, blue). hit.gml floats "+N pts" over the shooter (flashing) and "-N" in red over whoever was
// struck; the crates float what they gave (white, or green for health) and use_item floats "Bounce +1" (flashing). Every
// peer makes its own from what the server sent; it holds no game state and goes with the round (clear_objects).
using System.Collections.Generic;
using Ricochet.Core.Art;
using TMPro;
using UnityEngine;

namespace Ricochet
{
    public sealed class FloatText : MonoBehaviour
    {
        private static readonly List<FloatText> s_live = new List<FloatText>();

        private TextMeshPro _text;
        private Vector3 _origin;
        private float _steps;
        private double _fade;
        private bool _flash;
        private Color _color;

        /// <summary>Every float text showing now (for tests).</summary>
        public static IReadOnlyList<FloatText> Live => s_live;

        /// <summary>What it says.</summary>
        public string Text => _text != null ? _text.text : string.Empty;

        /// <summary>The colour it is drawn in now (its alpha is how faded it is).</summary>
        public Color Color => _text != null ? _text.color : Color.clear;

        /// <summary>True when its colour cycles (show_float_text's flash).</summary>
        public bool Flashing => _flash;

        /// <summary>
        /// show_float_text_color(x, y, <paramref name="text"/>, <paramref name="flash"/>, <paramref name="fadePerStep"/>, 1,
        /// <paramref name="color"/>, c_black) at <paramref name="at"/>.
        /// </summary>
        public static FloatText Show(Vector2 at, string text, bool flash, double fadePerStep, Color color)
        {
            var go = new GameObject("Float text: " + text);
            var floating = go.AddComponent<FloatText>();
            floating._text = WorldText.Create(go.transform, "Text", GmlFonts.Score, HitLooks.FloatOutlinePixels, color, 0.27f);
            floating._text.text = text;
            floating._origin = new Vector3(at.x, at.y, 0f);
            floating._fade = fadePerStep;
            floating._flash = flash;
            floating._color = color;
            floating.Place();
            return floating;
        }

        /// <summary>show_float_text(x, y, <paramref name="text"/>): flashing, at the default fade.</summary>
        public static FloatText Show(Vector2 at, string text) => Show(at, text, true, HitLooks.FloatFadePerStep, Color.white);

        /// <summary>Every float text goes (the round is swept).</summary>
        public static void ClearAll()
        {
            for (int i = s_live.Count - 1; i >= 0; i--)
            {
                if (s_live[i] != null)
                {
                    Destroy(s_live[i].gameObject);
                }
            }

            s_live.Clear();
        }

        /// <summary>A GameMaker colour as Unity's, at full alpha.</summary>
        public static Color Of(GmlColor color) => Steps.Of(color, 1f);

        private void OnEnable()
        {
            s_live.Add(this);
        }

        private void OnDisable()
        {
            s_live.Remove(this);
        }

        private void Update()
        {
            _steps += Time.deltaTime * Steps.PerSecond;
            if (HitLooks.FloatAlphaAt(_steps, _fade) <= 0)
            {
                Destroy(gameObject);
                return;
            }

            Place();
        }

        private void Place()
        {
            float rise = (float)(HitLooks.FloatDrawnAbove + HitLooks.FloatRisePerStep * _steps) * Steps.Pixel;
            transform.position = _origin + new Vector3(0f, rise, 0f);
            Color color = _flash ? Of(HitLooks.FlashColorAt(Mathf.FloorToInt(_steps) + 1)) : _color;
            color.a = (float)HitLooks.FloatAlphaAt(_steps, _fade);
            _text.color = color;
        }
    }
}
