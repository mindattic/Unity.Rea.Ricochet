// The original's art as the game finds it at run time (U.RICO-T44): every GML sprite, background and font by its GML
// name, with the file in the 2013 project it came from. Written by the editor's GmlArtBuilder and GmlFontBuilder from
// Art/gml-art.json and Fonts/gml-fonts.json (Tools/import-gml-art.ps1, import-gml-fonts.ps1) into
// Resources/Ricochet/ArtCatalogue; never edited by hand. GameArt reads it; nothing else loads art.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Ricochet
{
    public sealed class ArtCatalogue : ScriptableObject
    {
        public const string ResourcePath = "Ricochet/ArtCatalogue";

        [Serializable]
        public sealed class SpriteEntry
        {
            public string name;
            public string group;
            /// <summary>Listed in Ricochet.project.gmx (false: on disk only, never built into the original).</summary>
            public bool inProject;
            /// <summary>The sprite's .sprite.gmx, relative to the original project folder.</summary>
            public string originalPath;
            /// <summary>Each frame's image in the original project, in frame order.</summary>
            public string[] originalFrames;
            public int width, height, xorig, yorig;
            /// <summary>The image_speed its object's Create sets (GameMaker's default 1 when none does).</summary>
            public float imageSpeed;
            public Sprite[] frames;
        }

        [Serializable]
        public sealed class BackgroundEntry
        {
            public string name;
            public string group;
            public bool inProject;
            public string originalPath;
            public string originalImage;
            public int width, height;
            public bool tileHorizontal, tileVertical;
            public Sprite sprite;
        }

        [Serializable]
        public sealed class FontEntry
        {
            public string name;
            /// <summary>The typeface GameMaker rasterised it from (never shipped; only its glyph atlas is).</summary>
            public string family;
            /// <summary>The font.gmx point size: a label at this font size draws the glyphs at the original's pixel size.</summary>
            public int size;
            public bool inProject;
            public string originalPath;
            public string originalImage;
            /// <summary>A sheet of coloured glyphs (spr_spritefont) rather than a font resource.</summary>
            public bool spriteFont;
            public TMP_FontAsset font;
            /// <summary>draw_text_outline's look: white face, black outline 1 and 2 pixels wide (null for the sprite font).</summary>
            public Material outline1, outline2;
        }

        [SerializeField] private float _pixelsPerUnit = 1920f / 22f;
        [SerializeField] private int _roomSpeed = 60;
        [SerializeField] private List<SpriteEntry> _sprites = new List<SpriteEntry>();
        [SerializeField] private List<BackgroundEntry> _backgrounds = new List<BackgroundEntry>();
        [SerializeField] private List<FontEntry> _fonts = new List<FontEntry>();

        [NonSerialized] private Dictionary<string, SpriteEntry> _spriteByName;
        [NonSerialized] private Dictionary<string, BackgroundEntry> _backgroundByName;
        [NonSerialized] private Dictionary<string, FontEntry> _fontByName;

        /// <summary>GML pixels per Unity unit: the original's 1920 px room is the rebuild's 22 units.</summary>
        public float PixelsPerUnit => _pixelsPerUnit;

        /// <summary>The room speed animations play at (normalFPS).</summary>
        public int RoomSpeed => _roomSpeed;

        public IReadOnlyList<SpriteEntry> Sprites => _sprites;

        public IReadOnlyList<BackgroundEntry> Backgrounds => _backgrounds;

        public IReadOnlyList<FontEntry> Fonts => _fonts;

        public SpriteEntry Sprite(string name)
        {
            _spriteByName ??= Index(_sprites, s => s.name);
            return name != null && _spriteByName.TryGetValue(name, out SpriteEntry found) ? found : null;
        }

        public BackgroundEntry Background(string name)
        {
            _backgroundByName ??= Index(_backgrounds, b => b.name);
            return name != null && _backgroundByName.TryGetValue(name, out BackgroundEntry found) ? found : null;
        }

        public FontEntry Font(string name)
        {
            _fontByName ??= Index(_fonts, f => f.name);
            return name != null && _fontByName.TryGetValue(name, out FontEntry found) ? found : null;
        }

#if UNITY_EDITOR
        /// <summary>Replaces the sprites and backgrounds (GmlArtBuilder), keeping the fonts.</summary>
        public void SetArt(float pixelsPerUnit, int roomSpeed, List<SpriteEntry> sprites, List<BackgroundEntry> backgrounds)
        {
            _pixelsPerUnit = pixelsPerUnit;
            _roomSpeed = roomSpeed;
            _sprites = sprites;
            _backgrounds = backgrounds;
            _spriteByName = null;
            _backgroundByName = null;
        }

        /// <summary>Replaces the fonts (GmlFontBuilder), keeping the art.</summary>
        public void SetFonts(List<FontEntry> fonts)
        {
            _fonts = fonts;
            _fontByName = null;
        }
#endif

        private static Dictionary<string, T> Index<T>(List<T> items, Func<T, string> key)
        {
            var index = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T item in items)
            {
                if (item != null && key(item) != null)
                {
                    index[key(item)] = item;
                }
            }

            return index;
        }
    }
}
