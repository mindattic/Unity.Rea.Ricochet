using System;
using System.Collections.Generic;
using System.IO;
using Ricochet.Core.Art;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace Ricochet.Tooling
{
    /// <summary>
    /// Turns Fonts/gml-fonts.json (written by Tools/import-gml-fonts.ps1) into the rebuild's fonts (U.RICO-T44): one static
    /// TextMeshPro font per GML font at Resources/Ricochet/Fonts/&lt;name&gt;.asset, built from GameMaker's own baked glyph
    /// atlas and glyph table, so text draws with the glyphs the original drew and no typeface file (Impact, Verdana,
    /// Monotype Corsiva, Arial: not redistributable) is ever needed. Each is recorded in the <see cref="ArtCatalogue"/>.
    ///
    /// TextMeshPro outlines text through a signed distance field, so the builder makes one from the bitmap: the glyph
    /// cells are copied out of GameMaker's atlas onto a repacked one (clear gaps so no field reaches a neighbour), sampled
    /// up (<see cref="GmlFontMetrics.SamplingScale"/>: an 8 pt bitmap is too coarse for a field at 1:1), thresholded at half
    /// coverage and turned into a field (<see cref="SignedDistanceField"/>), saved as an Alpha8 texture beside the font.
    /// The face's point size is the font.gmx size times that scale, with every metric scaled to match, so a label at
    /// <c>fontSize</c> equal to the font.gmx size draws at the original's pixel size. Each glyph's metrics follow Core's
    /// <see cref="GmlFontMetrics"/> (cell top on the line top, GameMaker's <c>shift</c> and <c>offset</c> as advance and
    /// bearing, <c>string_height("M")</c> lines).
    ///
    /// Beside each font's own material sit draw_text_outline's: white text with a black outline 1 and 2 GameMaker pixels
    /// wide (<see cref="GmlFontMetrics.OutlineWidth"/>, the face dilated back so the white face is exactly the glyph).
    ///
    /// The sprite font (spr_spritefont, coloured glyphs) is built the same way but keeps its colours: its cells are
    /// repacked onto an RGBA atlas drawn with TextMeshPro's Sprite shader, no field and no outline.
    ///
    /// A rebuild rewrites the existing assets in place, so their GUIDs hold and the same data writes the same bytes.
    ///
    /// Batch: Tools/unity.ps1 -Method Ricochet.Tooling.GmlFontBuilder.BuildAll (Tools/import-gml-fonts.ps1 runs it).
    /// </summary>
    public static class GmlFontBuilder
    {
        public const string OutputFolder = "Assets/Rea.Ricochet/Resources/Ricochet/Fonts/";
        public const string DistanceFieldShader = "TextMeshPro/Distance Field";
        public const string SpriteShader = "TextMeshPro/Sprite";

        /// <summary>The TMP Essentials font the template asset is made from (its glyphs are all replaced).</summary>
        private const string TemplateFont = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

        /// <summary>Clear pixels between a sprite font's repacked cells (no field to keep apart, just filtering).</summary>
        private const int SpriteFontGap = 4;

        public static string AssetPath(string fontName) => OutputFolder + fontName + ".asset";

        public static string AtlasPath(string fontName) => OutputFolder + fontName + " Atlas.png";

        [MenuItem("Ricochet/Build Fonts From gml-fonts.json")]
        public static void BuildAll()
        {
            GmlFontData data = GmlFontData.Load() ?? throw new FileNotFoundException(
                GmlFontData.DataPath + " is missing; run Tools/import-gml-fonts.ps1 first", GmlFontData.DataPath);

            GmlArtBuilder.EnsureFolder(OutputFolder);
            var entries = new List<ArtCatalogue.FontEntry>();
            foreach (GmlFontData.FontData font in data.fonts)
            {
                entries.Add(Build(font, data.outline.sizes));
            }

            foreach (GmlFontData.SpriteFontData sheet in data.spriteFonts)
            {
                entries.Add(Build(sheet));
            }

            ArtCatalogue catalogue = GmlArtBuilder.LoadOrCreateCatalogue();
            catalogue.SetFonts(entries);
            EditorUtility.SetDirty(catalogue);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Ricochet] GML fonts built: {data.fonts.Length} fonts, {data.spriteFonts.Length} sprite font, {data.skipped.Length} skipped");
            foreach (ArtCatalogue.FontEntry e in entries)
            {
                Debug.Log($"[Ricochet] Font {e.name}: {e.family} {e.size}, {e.font.glyphTable.Count} glyphs, atlas {e.font.atlasWidth}x{e.font.atlasHeight}, padding {e.font.atlasPadding}");
            }

            foreach (GmlFontData.SkippedData s in data.skipped)
            {
                Debug.Log($"[Ricochet] Not imported: {s.kind} {s.name}: {s.reason}");
            }
        }

        /// <summary>An image file's pixels, bottom row first, read from the file itself (not through its importer).</summary>
        public static Color32[] ReadImage(string path, int width, int height)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(path + " is missing; rerun Tools/import-gml-fonts.ps1", path);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    throw new InvalidDataException(path + " is not a readable image");
                }

                if (texture.width != width || texture.height != height)
                {
                    throw new InvalidDataException($"{path} is {texture.width}x{texture.height}, not the manifest's {width}x{height}");
                }

                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Builds (or rebuilds in place) one GML font's SDF font asset and its outline materials.</summary>
        public static ArtCatalogue.FontEntry Build(GmlFontData.FontData font, int[] outlineSizes)
        {
            Color32[] source = ReadImage(font.atlas, font.atlasWidth, font.atlasHeight);
            GmlGlyph[] glyphs = font.Glyphs();
            int line = GmlFontMetrics.LineHeight(glyphs);
            int descent = GmlFontMetrics.Descent(glyphs);
            int scale = GmlFontMetrics.SamplingScale(line);
            int padding = GmlFontMetrics.Padding(scale);
            // Cells sit far enough apart that neither's field (padding texels round it) reaches the other.
            int gap = (2 * padding + 2 + scale - 1) / scale;
            PackedAtlas packed = GmlFontMetrics.Pack(glyphs, gap);

            // GameMaker's coverage (the atlas's alpha) on the repacked layout, top row first.
            var coverage = new byte[packed.Width * packed.Height];
            for (int i = 0; i < glyphs.Length; i++)
            {
                GmlGlyph from = glyphs[i], to = packed.Glyphs[i];
                for (int row = 0; row < from.Height; row++)
                {
                    for (int col = 0; col < from.Width; col++)
                    {
                        coverage[(to.Y + row) * packed.Width + to.X + col] =
                            source[(font.atlasHeight - 1 - (from.Y + row)) * font.atlasWidth + from.X + col].a;
                    }
                }
            }

            int width = packed.Width * scale, height = packed.Height * scale;
            byte[] field = SignedDistanceField.Encode(SignedDistanceField.Upsample(coverage, packed.Width, packed.Height, scale), width, height, padding + 1);
            Texture2D atlas = WriteAtlas(AtlasPath(font.name), width, height, (x, y) => new Color32(255, 255, 255, field[y * width + x]), distanceField: true);

            string path = AssetPath(font.name);
            TMP_FontAsset asset = LoadOrCreate(path, font.size * scale, padding, GlyphRenderMode.SDFAA, width, height);
            FaceInfo face = Face(font.family, font.bold, font.italic, font.size * scale, line * scale, descent * scale, Array.Find(glyphs, g => g.Character == ' ').Shift * scale);
            FillGlyphs(asset, face, packed, height, scale, line);

            Material material = asset.material;
            material.name = font.name + " Material";
            material.shader = FindShader(DistanceFieldShader);
            SetUp(material, atlas, padding + 1, width, height);

            var outlines = new Material[GmlFontMetrics.MaxOutlinePixels + 1];
            foreach (int pixels in outlineSizes)
            {
                Material outline = SubMaterial(path, font.name + " Outline " + pixels, material);
                float w = GmlFontMetrics.OutlineWidth(pixels, scale);
                outline.SetFloat(ShaderUtilities.ID_OutlineWidth, w);
                outline.SetFloat(ShaderUtilities.ID_FaceDilate, w);
                outline.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                outline.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
                outline.EnableKeyword("OUTLINE_ON");
                ShaderUtilities.UpdateShaderRatios(outline);
                EditorUtility.SetDirty(outline);
                outlines[pixels] = outline;
            }

            Finish(asset, padding, GlyphRenderMode.SDFAA, width, height);
            return new ArtCatalogue.FontEntry
            {
                name = font.name,
                family = font.family,
                size = font.size,
                inProject = font.inResourceTree,
                originalPath = font.originalPath,
                originalImage = font.originalImage,
                spriteFont = false,
                font = asset,
                outline1 = outlines[1],
                outline2 = outlines.Length > 2 ? outlines[2] : null,
            };
        }

        /// <summary>Builds (or rebuilds in place) the sprite font: its coloured cells repacked onto an RGBA atlas.</summary>
        public static ArtCatalogue.FontEntry Build(GmlFontData.SpriteFontData sheet)
        {
            Color32[] source = ReadImage(sheet.atlas, sheet.atlasWidth, sheet.atlasHeight);
            var glyphs = new List<GmlGlyph>(sheet.Glyphs());
            int cell = glyphs[0].Width;
            glyphs.Insert(0, new GmlGlyph(' ', 0, 0, 0, glyphs[0].Height, cell / 2, 0)); // the sheet has no space; half a cell
            PackedAtlas packed = GmlFontMetrics.Pack(glyphs, SpriteFontGap);
            var pixels = new Color32[packed.Width * packed.Height];
            for (int i = 0; i < glyphs.Count; i++)
            {
                GmlGlyph from = glyphs[i], to = packed.Glyphs[i];
                for (int row = 0; row < from.Height && from.Width > 0; row++)
                {
                    for (int col = 0; col < from.Width; col++)
                    {
                        pixels[(to.Y + row) * packed.Width + to.X + col] =
                            source[(sheet.atlasHeight - 1 - (from.Y + row)) * sheet.atlasWidth + from.X + col];
                    }
                }
            }

            int width = packed.Width, height = packed.Height;
            Texture2D atlas = WriteAtlas(AtlasPath(sheet.name), width, height, (x, y) => pixels[y * width + x], distanceField: false);
            int line = glyphs[1].Height;
            string path = AssetPath(sheet.name);
            TMP_FontAsset asset = LoadOrCreate(path, line, 0, GlyphRenderMode.COLOR, width, height);
            FillGlyphs(asset, Face(sheet.name, false, false, line, line, 0, cell / 2), packed, height, 1, line);
            Material material = asset.material;
            material.name = sheet.name + " Material";
            material.shader = FindShader(SpriteShader);
            SetUp(material, atlas, 1f, width, height);
            Finish(asset, 0, GlyphRenderMode.COLOR, width, height);
            return new ArtCatalogue.FontEntry
            {
                name = sheet.name,
                family = sheet.name,
                size = line,
                inProject = sheet.inResourceTree,
                originalPath = sheet.originalPath,
                originalImage = sheet.originalImage,
                spriteFont = true,
                font = asset,
            };
        }

        private static FaceInfo Face(string family, bool bold, bool italic, float pointSize, int line, int descent, int space)
        {
            return new FaceInfo
            {
                familyName = family,
                styleName = bold ? (italic ? "Bold Italic" : "Bold") : (italic ? "Italic" : "Regular"),
                pointSize = pointSize,
                scale = 1f,
                lineHeight = line,
                ascentLine = line,
                capLine = line,
                meanLine = line / 2f,
                baseline = 0f,
                descentLine = descent,
                superscriptOffset = line,
                superscriptSize = 0.5f,
                subscriptOffset = descent,
                subscriptSize = 0.5f,
                underlineOffset = descent / 2f,
                underlineThickness = Mathf.Max(1f, line / 15f),
                strikethroughOffset = line / 3f,
                strikethroughThickness = Mathf.Max(1f, line / 15f),
                tabWidth = space,
            };
        }

        /// <summary>The glyph and character tables: cell top on the line top, offset as bearing, shift as advance, all at the atlas's scale.</summary>
        private static void FillGlyphs(TMP_FontAsset asset, FaceInfo face, PackedAtlas packed, int atlasHeight, int scale, int line)
        {
            asset.faceInfo = face;
            asset.glyphTable.Clear();
            asset.characterTable.Clear();
            foreach (GmlGlyph g in packed.Glyphs)
            {
                int w = g.Width * scale, h = g.Height * scale;
                var glyph = new Glyph((uint)g.Character,
                    new GlyphMetrics(w, h, g.Offset * scale, line * scale, g.Shift * scale),
                    new GlyphRect(g.X * scale, atlasHeight - g.Y * scale - h, w, h), 1f, 0);
                asset.glyphTable.Add(glyph);
                asset.characterTable.Add(new TMP_Character((uint)g.Character, asset, glyph));
            }
        }

        /// <summary>Writes the atlas image and imports it: Alpha8 for a field, RGBA for coloured glyphs; never compressed or mipmapped.</summary>
        private static Texture2D WriteAtlas(string path, int width, int height, Func<int, int, Color32> pixelAt, bool distanceField)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        pixels[(height - 1 - y) * width + x] = pixelAt(x, y); // pixelAt reads top row first
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                byte[] png = texture.EncodeToPNG();
                if (!File.Exists(path) || !SameBytes(File.ReadAllBytes(path), png))
                {
                    File.WriteAllBytes(path, png);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = distanceField ? TextureImporterType.SingleChannel : TextureImporterType.Default;
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Alpha;
            settings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.sRGBTexture = !distanceField;
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = !distanceField;
            settings.npotScale = TextureImporterNPOTScale.None;
            settings.readable = false;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 8192;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException(path + " did not import as a texture");
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The font asset at <paramref name="path"/>, or a new one from TextMeshPro's own template (so its serialized layout and
        /// material are whatever this version expects) with its in-memory atlas dropped and its material stored inside it.
        /// </summary>
        private static TMP_FontAsset LoadOrCreate(string path, int pointSize, int padding, GlyphRenderMode mode, int width, int height)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
            {
                return existing;
            }

            Font template = AssetDatabase.LoadAssetAtPath<Font>(TemplateFont)
                ?? throw new FileNotFoundException(TemplateFont + " (TMP Essentials) is missing", TemplateFont);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(template, Mathf.Max(1, pointSize), padding, mode, width, height, AtlasPopulationMode.Static, false)
                ?? throw new InvalidOperationException("TextMeshPro could not create a font asset template for " + path);
            if (asset.atlasTextures != null)
            {
                foreach (Texture2D t in asset.atlasTextures)
                {
                    if (t != null)
                    {
                        UnityEngine.Object.DestroyImmediate(t);
                    }
                }
            }

            asset.atlasTextures = Array.Empty<Texture2D>();
            asset.hideFlags = HideFlags.None;
            asset.material.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            return asset;
        }

        /// <summary>A material stored inside the font asset at <paramref name="path"/>, named <paramref name="name"/>, copied from <paramref name="from"/>.</summary>
        private static Material SubMaterial(string path, string name, Material from)
        {
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Material m && m.name == name)
                {
                    m.shader = from.shader;
                    m.CopyPropertiesFromMaterial(from);
                    m.name = name;
                    return m;
                }
            }

            var material = new Material(from) { name = name, hideFlags = HideFlags.None };
            AssetDatabase.AddObjectToAsset(material, path);
            return material;
        }

        private static void SetUp(Material material, Texture2D atlas, float gradientScale, int width, int height)
        {
            material.hideFlags = HideFlags.None;
            material.mainTexture = atlas;
            material.SetFloat(ShaderUtilities.ID_GradientScale, gradientScale);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, width);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, height);
            material.SetFloat(ShaderUtilities.ID_WeightNormal, 0f);
            material.SetFloat(ShaderUtilities.ID_WeightBold, 0f); // GameMaker baked the face; bold must not thicken it
            if (material.HasProperty(ShaderUtilities.ID_GradientScale))
            {
                ShaderUtilities.UpdateShaderRatios(material);
            }

            EditorUtility.SetDirty(material);
        }

        /// <summary>Everything TextMeshPro would otherwise take from a source font file: none, static, this atlas, no fallbacks.</summary>
        private static void Finish(TMP_FontAsset asset, int padding, GlyphRenderMode mode, int width, int height)
        {
            asset.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(asset));
            asset.hideFlags = HideFlags.None;
            asset.atlasTextures = new[] { (Texture2D)asset.material.mainTexture };

            var so = new SerializedObject(asset);
            ClearReference(so, "m_SourceFontFile");
            ClearReference(so, "m_SourceFontFile_EditorRef");
            SetString(so, "m_SourceFontFileGUID", string.Empty);
            SetString(so, "m_SourceFontFilePath", string.Empty);
            SetString(so, "m_CreationSettings.sourceFontFileGUID", string.Empty);
            SetString(so, "m_CreationSettings.sourceFontFileName", string.Empty);
            SetInt(so, "m_AtlasPopulationMode", (int)AtlasPopulationMode.Static);
            SetBool(so, "InternalDynamicOS", false);
            SetBool(so, "m_ClearDynamicDataOnBuild", false);
            SetBool(so, "m_IsMultiAtlasTexturesEnabled", false);
            SetBool(so, "m_GetFontFeatures", false);
            SetInt(so, "m_AtlasWidth", width);
            SetInt(so, "m_AtlasHeight", height);
            SetInt(so, "m_AtlasPadding", padding);
            SetInt(so, "m_AtlasRenderMode", (int)mode);
            SetInt(so, "m_AtlasTextureIndex", 0);
            ClearArray(so, "m_FallbackFontAssetTable");
            ClearArray(so, "m_UsedGlyphRects");
            ClearArray(so, "m_FreeGlyphRects");
            so.ApplyModifiedPropertiesWithoutUndo();

            asset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(asset.material);
            EditorUtility.SetDirty(asset);
        }

        private static Shader FindShader(string name) =>
            Shader.Find(name) ?? throw new InvalidOperationException("Shader " + name + " (TMP Essentials) is missing");

        private static void ClearReference(SerializedObject so, string property)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            {
                p.objectReferenceValue = null;
            }
        }

        private static void SetString(SerializedObject so, string property, string value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null && p.propertyType == SerializedPropertyType.String)
            {
                p.stringValue = value;
            }
        }

        private static void SetInt(SerializedObject so, string property, int value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
            {
                p.intValue = value;
            }
        }

        private static void SetBool(SerializedObject so, string property, bool value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null && p.propertyType == SerializedPropertyType.Boolean)
            {
                p.boolValue = value;
            }
        }

        private static void ClearArray(SerializedObject so, string property)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null && p.isArray)
            {
                p.ClearArray();
            }
        }
    }
}
