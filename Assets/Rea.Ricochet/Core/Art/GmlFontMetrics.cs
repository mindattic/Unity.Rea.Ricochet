// The one place a GameMaker font's baked glyph table becomes the rebuild's (U.RICO-T44, docs/BIBLE.md "Art and
// fonts"). Engine-free, so the editor's font builder and the Core tests share it; it follows Unity.Hare's GmlFontMetrics
// (U.HARE-T48), with the SDF sampling scale and padding the rebuild's TextMeshPro fonts need on top.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Art
{
    /// <summary>
    /// One glyph of a GameMaker font.gmx: its cell in the baked atlas (x right and y down from the atlas's top left,
    /// exactly as GameMaker stores it), its advance (<c>shift</c>) and its left bearing (<c>offset</c>). GameMaker crops
    /// every cell at the top of the line and at the bottom of the glyph's own ink, so a cell's top is always the top of
    /// the line and only its height varies (taller for descenders).
    /// </summary>
    public readonly struct GmlGlyph
    {
        public readonly int Character, X, Y, Width, Height, Shift, Offset;

        public GmlGlyph(int character, int x, int y, int width, int height, int shift, int offset)
        {
            if (width < 0 || height < 0)
            {
                throw new ArgumentException($"Glyph {character} has a negative size {width}x{height}");
            }

            Character = character; X = x; Y = y; Width = width; Height = height; Shift = shift; Offset = offset;
        }

        /// <summary>The same glyph with its cell moved to (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public GmlGlyph At(int x, int y) => new GmlGlyph(Character, x, y, Width, Height, Shift, Offset);
    }

    /// <summary>A font's glyphs repacked onto a fresh atlas (<see cref="GmlFontMetrics.Pack"/>), and that atlas's size.</summary>
    public readonly struct PackedAtlas
    {
        public readonly int Width, Height;
        public readonly GmlGlyph[] Glyphs;

        public PackedAtlas(int width, int height, GmlGlyph[] glyphs)
        {
            Width = width; Height = height; Glyphs = glyphs;
        }
    }

    /// <summary>
    /// GameMaker has no baseline: <c>draw_text</c> puts every cell's top on the line's top and steps each next character
    /// by the glyph's <c>shift</c>, drawing it <c>offset</c> pixels right of the pen. Lines of a multi-line string are
    /// <c>string_height("M")</c> apart, the height of the <c>M</c> cell, which ends on the baseline because M has no
    /// descender. So the rebuild puts the baseline at the bottom of the M cell: the ascent is that height, every glyph's
    /// top bearing is the ascent (its cell top on the line top, exactly as GameMaker draws it), and the line height is
    /// the same figure.
    /// </summary>
    public static class GmlFontMetrics
    {
        /// <summary>The character whose cell height GameMaker uses for line spacing.</summary>
        public const int LineReferenceCharacter = 'M';

        /// <summary>The largest draw_text_outline size the original calls (2, on its buttons and labels; 1 on the Ready card).</summary>
        public const int MaxOutlinePixels = 2;

        /// <summary>The line height the SDF atlas aims for: small bitmaps are sampled up to about this many texels a line.</summary>
        public const int TargetLineTexels = 64;

        /// <summary>The most a GameMaker atlas is sampled up.</summary>
        public const int MaxSamplingScale = 4;

        /// <summary>
        /// The font's line height: the height of its <c>M</c> cell (GameMaker's <c>string_height("M")</c>), or the tallest
        /// cell when the font has no M.
        /// </summary>
        public static int LineHeight(IReadOnlyList<GmlGlyph> glyphs)
        {
            if (glyphs == null || glyphs.Count == 0)
            {
                throw new ArgumentException("A font needs at least one glyph");
            }

            int tallest = 0;
            foreach (GmlGlyph g in glyphs)
            {
                if (g.Character == LineReferenceCharacter)
                {
                    return g.Height;
                }

                tallest = Math.Max(tallest, g.Height);
            }

            return tallest;
        }

        /// <summary>The tallest cell, reaching furthest below the line's top (the deepest descender).</summary>
        public static int TallestCell(IReadOnlyList<GmlGlyph> glyphs)
        {
            int tallest = 0;
            foreach (GmlGlyph g in glyphs)
            {
                tallest = Math.Max(tallest, g.Height);
            }

            return tallest;
        }

        /// <summary>How far below the baseline the deepest cell reaches, as a non-positive y (TextCore's descent line).</summary>
        public static int Descent(IReadOnlyList<GmlGlyph> glyphs) => Math.Min(0, LineHeight(glyphs) - TallestCell(glyphs));

        /// <summary>
        /// How many atlas texels each GameMaker pixel becomes in the SDF atlas: enough that a line is about
        /// <see cref="TargetLineTexels"/> tall (an 8 pt bitmap's edges are too coarse to carry a distance field at 1:1),
        /// between 1 and <see cref="MaxSamplingScale"/>.
        /// </summary>
        public static int SamplingScale(int lineHeight)
        {
            if (lineHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lineHeight));
            }

            int scale = (TargetLineTexels + lineHeight - 1) / lineHeight;
            return Math.Max(1, Math.Min(MaxSamplingScale, scale));
        }

        /// <summary>
        /// The SDF padding (TextMeshPro's atlas padding, in atlas texels) for a font sampled at <paramref name="scale"/>:
        /// room for a <see cref="MaxOutlinePixels"/> outline both ways (TextMeshPro's outline is centred on the edge and
        /// the face is dilated back by the same amount, <see cref="OutlineWidth"/>), plus two texels of slack.
        /// </summary>
        public static int Padding(int scale) => 2 * MaxOutlinePixels * scale + 2;

        /// <summary>
        /// TextMeshPro's _OutlineWidth (and _FaceDilate) for an outline <paramref name="pixels"/> GameMaker pixels wide:
        /// the outline spans OutlineWidth x padding texels centred on the dilated edge, so dilating the face by the same
        /// amount leaves the white face exactly as drawn and puts the whole black band outside it.
        /// </summary>
        public static float OutlineWidth(int pixels, int scale) => pixels * scale / (float)Padding(scale);

        /// <summary>
        /// Repacks a font's glyph cells into a fresh atlas with <paramref name="gap"/> clear pixels between cells and round
        /// the edge: the narrowest power-of-two square-or-wider atlas they fit on, tallest cells first, in shelves. Returns
        /// the glyphs in their input order with only their cell position changed (x right and y down from the top left),
        /// and the atlas size. Deterministic, so a rebuild repacks identically.
        /// </summary>
        public static PackedAtlas Pack(IReadOnlyList<GmlGlyph> glyphs, int gap)
        {
            if (glyphs == null || glyphs.Count == 0)
            {
                throw new ArgumentException("A font needs at least one glyph");
            }

            if (gap < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gap));
            }

            var order = new int[glyphs.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int byHeight = glyphs[b].Height.CompareTo(glyphs[a].Height);
                return byHeight != 0 ? byHeight : glyphs[a].Character.CompareTo(glyphs[b].Character);
            });

            for (int width = 32; width <= 8192; width *= 2)
            {
                var placed = new GmlGlyph[glyphs.Count];
                int x = gap, y = gap, row = 0;
                bool fits = true;
                foreach (int i in order)
                {
                    GmlGlyph g = glyphs[i];
                    if (g.Width + 2 * gap > width)
                    {
                        fits = false;
                        break;
                    }

                    if (x + g.Width + gap > width)
                    {
                        x = gap;
                        y += row + gap;
                        row = 0;
                    }

                    placed[i] = g.At(x, y);
                    x += g.Width + gap;
                    row = Math.Max(row, g.Height);
                }

                int used = y + row + gap;
                if (!fits || used > width)
                {
                    continue;
                }

                int height = 1;
                while (height < used)
                {
                    height *= 2;
                }

                return new PackedAtlas(width, height, placed);
            }

            throw new ArgumentException("The glyphs do not fit an 8192 px atlas");
        }

        /// <summary>
        /// GameMaker's <c>string_width</c> for one line: the sum of every character's <c>shift</c>; a character the font
        /// has no glyph for adds nothing (GameMaker skips it).
        /// </summary>
        public static int StringWidth(IReadOnlyList<GmlGlyph> glyphs, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var shifts = new Dictionary<int, int>(glyphs.Count);
            foreach (GmlGlyph g in glyphs)
            {
                shifts[g.Character] = g.Shift;
            }

            int width = 0;
            foreach (char c in text)
            {
                if (shifts.TryGetValue(c, out int s))
                {
                    width += s;
                }
            }

            return width;
        }

        /// <summary>
        /// The glyphs of a GameMaker sprite font laid out on a ruled grid (spr_spritefont: ASCII 33 to 126, ten 40x51 cells a
        /// row from 20,20, 1 px rules): each cell inside its rules, advancing its full width with no bearing, as
        /// font_add_sprite draws a non-proportional sprite font.
        /// </summary>
        public static GmlGlyph[] GridGlyphs(int firstCharacter, int count, int columns, int originX, int originY, int cellWidth, int cellHeight, int rule)
        {
            if (count <= 0 || columns <= 0 || cellWidth <= 2 * rule || cellHeight <= 2 * rule)
            {
                throw new ArgumentException("A sprite font grid needs characters, columns and cells larger than their rules");
            }

            var glyphs = new GmlGlyph[count];
            int width = cellWidth - rule, height = cellHeight - rule;
            for (int i = 0; i < count; i++)
            {
                int x = originX + (i % columns) * cellWidth + rule;
                int y = originY + (i / columns) * cellHeight + rule;
                glyphs[i] = new GmlGlyph(firstCharacter + i, x, y, width, height, width, 0);
            }

            return glyphs;
        }
    }
}
