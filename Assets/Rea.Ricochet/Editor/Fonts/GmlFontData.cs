using System;
using System.Collections.Generic;
using System.IO;
using Ricochet.Core.Art;
using UnityEngine;

namespace Ricochet.Tooling
{
    /// <summary>
    /// Fonts/gml-fonts.json, the file Tools/import-gml-fonts.ps1 writes from the original project (U.RICO-T44): the nine
    /// GameMaker font resources (face, size, glyph table, baked atlas, original path, who draws with them), the sprite
    /// font's grid, the outline sizes draw_text_outline is called with, and what could not be imported. The editor reads
    /// it and never writes it.
    /// </summary>
    [Serializable]
    public sealed class GmlFontData
    {
        public const string FontFolder = "Assets/Rea.Ricochet/Fonts";
        public const string DataPath = FontFolder + "/gml-fonts.json";

        public string source;
        public OutlineData outline;
        public FontData[] fonts;
        public SpriteFontData[] spriteFonts;
        public SkippedData[] skipped;

        [Serializable]
        public sealed class OutlineData
        {
            public string script;
            public int[] sizes;
        }

        [Serializable]
        public sealed class GlyphData
        {
            public int character, x, y, w, h, shift, offset;
        }

        [Serializable]
        public sealed class FontData
        {
            public string name;
            public bool inResourceTree;
            public string originalPath, originalImage;
            public string family;
            public int size;
            public bool bold, italic;
            public int antialias;
            public string atlas;
            public int atlasWidth, atlasHeight;
            public string[] usedBy;
            public string[] notes;
            public GlyphData[] glyphs;

            /// <summary>The glyph table in Core's terms, in the file's (character) order.</summary>
            public GmlGlyph[] Glyphs()
            {
                var result = new GmlGlyph[glyphs.Length];
                for (int i = 0; i < glyphs.Length; i++)
                {
                    GlyphData g = glyphs[i];
                    result[i] = new GmlGlyph(g.character, g.x, g.y, g.w, g.h, g.shift, g.offset);
                }

                return result;
            }
        }

        [Serializable]
        public sealed class SpriteFontData
        {
            public string name;
            public bool inResourceTree;
            public string originalPath, originalImage;
            public string atlas;
            public int atlasWidth, atlasHeight;
            public int firstCharacter, characterCount, columns, originX, originY, cellWidth, cellHeight, rule;

            /// <summary>The sheet's cells as glyphs (Core's <see cref="GmlFontMetrics.GridGlyphs"/>).</summary>
            public GmlGlyph[] Glyphs() => GmlFontMetrics.GridGlyphs(firstCharacter, characterCount, columns, originX, originY, cellWidth, cellHeight, rule);
        }

        [Serializable]
        public sealed class SkippedData
        {
            public string name, kind, reason;
        }

        /// <summary>The project's gml-fonts.json, or null if the tool has not run.</summary>
        public static GmlFontData Load() => File.Exists(DataPath) ? Parse(File.ReadAllText(DataPath)) : null;

        /// <summary>Parses and validates the file, so a malformed one fails loudly here rather than as a missing font.</summary>
        public static GmlFontData Parse(string json)
        {
            GmlFontData data = JsonUtility.FromJson<GmlFontData>(json);
            if (data == null)
            {
                throw new InvalidDataException(DataPath + " is not valid JSON");
            }

            data.fonts ??= Array.Empty<FontData>();
            data.spriteFonts ??= Array.Empty<SpriteFontData>();
            data.skipped ??= Array.Empty<SkippedData>();
            if (data.outline == null || data.outline.sizes == null || data.outline.sizes.Length == 0)
            {
                throw new InvalidDataException(DataPath + ": the draw_text_outline sizes are missing");
            }

            foreach (int size in data.outline.sizes)
            {
                if (size < 1 || size > GmlFontMetrics.MaxOutlinePixels)
                {
                    throw new InvalidDataException(DataPath + $": outline size {size} is outside 1..{GmlFontMetrics.MaxOutlinePixels}");
                }
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (FontData f in data.fonts)
            {
                if (string.IsNullOrEmpty(f.name) || !names.Add(f.name))
                {
                    throw new InvalidDataException(DataPath + ": font name missing or repeated: " + f.name);
                }

                if (f.size <= 0 || f.glyphs == null || f.glyphs.Length == 0)
                {
                    throw new InvalidDataException(DataPath + ": " + f.name + " has no size or no glyphs");
                }

                if (string.IsNullOrEmpty(f.originalPath) || string.IsNullOrEmpty(f.atlas))
                {
                    throw new InvalidDataException(DataPath + ": " + f.name + " does not record its original file or atlas");
                }
            }

            foreach (SpriteFontData s in data.spriteFonts)
            {
                if (string.IsNullOrEmpty(s.name) || !names.Add(s.name))
                {
                    throw new InvalidDataException(DataPath + ": sprite font name missing or repeated: " + s.name);
                }
            }

            return data;
        }
    }
}
