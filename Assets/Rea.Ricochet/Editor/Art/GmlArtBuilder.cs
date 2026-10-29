using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Ricochet.Tooling
{
    /// <summary>
    /// Turns Art/gml-art.json (written by Tools/import-gml-art.ps1) into the Unity side of the original's art (U.RICO-T44):
    /// makes sure every frame and background carries <see cref="GmlArtImporter"/>'s preset, writes a Sprite Atlas V2 per
    /// top sprite group (Art/Atlases), and fills the runtime <see cref="ArtCatalogue"/> (Resources/Ricochet/ArtCatalogue)
    /// mapping every GML sprite and background name to its sprites and the original file it came from. Stale atlases are
    /// deleted. Rerunning it on the same data rewrites the same files.
    ///
    /// Batch: Tools/unity.ps1 -Method Ricochet.Tooling.GmlArtBuilder.BuildAll (Tools/import-gml-art.ps1 runs it).
    /// </summary>
    public static class GmlArtBuilder
    {
        public const string CatalogueFolder = "Assets/Rea.Ricochet/Resources/Ricochet";
        public const string CataloguePath = "Assets/Rea.Ricochet/Resources/" + ArtCatalogue.ResourcePath + ".asset";

        /// <summary>Atlas pages are at most this big; a sprite over gml-art.json's atlasMaxSide is never packed.</summary>
        public const int AtlasMaxTextureSize = 4096;

        [MenuItem("Ricochet/Build Art From gml-art.json")]
        public static void BuildAll()
        {
            GmlArtData data = GmlArtData.Load() ?? throw new FileNotFoundException(
                GmlArtData.DataPath + " is missing; run Tools/import-gml-art.ps1 first", GmlArtData.DataPath);

            // Sprite Atlas V2, packed for player builds (the editor draws the source textures).
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2Build)
            {
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2Build;
            }

            AssetDatabase.Refresh();
            int reimported = EnsureImportSettings(data);
            int atlases = BuildAtlases(data);
            BuildCatalogue(data);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Ricochet] GML art built: {data.sprites.Length} sprites ({data.sprites.Count(s => s.inProject)} in the project), " +
                      $"{data.backgrounds.Length} backgrounds ({data.backgrounds.Count(b => b.inProject)} in the project), " +
                      $"{atlases} atlases, {reimported} textures reimported, {data.skipped.Length} skipped");
            foreach (GmlArtData.SkippedData s in data.skipped)
            {
                Debug.Log($"[Ricochet] Not imported: {s.kind} {s.name}: {s.reason}");
            }
        }

        /// <summary>Reimports any texture whose settings are not yet the preset's (e.g. imported before it compiled).</summary>
        private static int EnsureImportSettings(GmlArtData data)
        {
            var stale = new List<string>();
            foreach (GmlArtData.SpriteData s in data.sprites)
            {
                foreach (string frame in s.frames)
                {
                    if (GmlArtImporter.Configure(Importer(frame), s, data.pixelsPerUnit))
                    {
                        stale.Add(frame);
                    }
                }
            }

            foreach (GmlArtData.BackgroundData b in data.backgrounds)
            {
                if (GmlArtImporter.Configure(Importer(b.texture), b, data.pixelsPerUnit))
                {
                    stale.Add(b.texture);
                }
            }

            if (stale.Count == 0)
            {
                return 0;
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in stale)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return stale.Count;
        }

        private static TextureImporter Importer(string path) =>
            AssetImporter.GetAtPath(path) as TextureImporter
            ?? throw new FileNotFoundException(path + " is in " + GmlArtData.DataPath + " but not imported; rerun Tools/import-gml-art.ps1", path);

        private static Sprite[] Frames(GmlArtData.SpriteData s) =>
            s.frames.Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f)
                ?? throw new InvalidOperationException(f + " did not import as a sprite")).ToArray();

        /// <summary>One Sprite Atlas V2 per atlas path in gml-art.json, packing every frame of the sprites that name it.</summary>
        private static int BuildAtlases(GmlArtData data)
        {
            IOrderedEnumerable<IGrouping<string, GmlArtData.SpriteData>> groups = data.sprites
                .Where(s => !string.IsNullOrEmpty(s.atlas))
                .GroupBy(s => s.atlas, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Ordinal);
            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string, GmlArtData.SpriteData> group in groups)
            {
                string path = group.Key;
                wanted.Add(path);
                EnsureFolder(Path.GetDirectoryName(path));
                var asset = new SpriteAtlasAsset();
                UnityEngine.Object[] textures = group.OrderBy(s => s.name, StringComparer.Ordinal)
                    .SelectMany(s => s.frames).Select(f => (UnityEngine.Object)AssetDatabase.LoadAssetAtPath<Texture2D>(f)).ToArray();
                asset.Add(textures);
                SpriteAtlasAsset.Save(asset, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                importer.includeInBuild = true;
                importer.packingSettings = new SpriteAtlasPackingSettings
                {
                    blockOffset = 1,
                    padding = 4,                // bilinear filtering samples a neighbour; keep frames from bleeding
                    enableRotation = false,
                    enableTightPacking = false, // full-rect sprites
                    enableAlphaDilation = true,
                };
                importer.textureSettings = new SpriteAtlasTextureSettings
                {
                    readable = false,
                    generateMipMaps = false,
                    sRGB = true,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 0,
                };
                TextureImporterPlatformSettings platform = importer.GetPlatformSettings("DefaultTexturePlatform");
                platform.maxTextureSize = AtlasMaxTextureSize;
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                platform.format = TextureImporterFormat.Automatic;
                importer.SetPlatformSettings(platform);
                importer.SaveAndReimport();
            }

            DeleteStale(GmlArtData.AtlasesFolder, "*.spriteatlasv2", wanted);
            return wanted.Count;
        }

        /// <summary>The runtime catalogue: every GML name to its sprites and original files, in gml-art.json's order.</summary>
        private static void BuildCatalogue(GmlArtData data)
        {
            var sprites = new List<ArtCatalogue.SpriteEntry>(data.sprites.Length);
            foreach (GmlArtData.SpriteData s in data.sprites)
            {
                sprites.Add(new ArtCatalogue.SpriteEntry
                {
                    name = s.name,
                    group = s.group,
                    inProject = s.inProject,
                    originalPath = s.originalPath,
                    originalFrames = s.originalFrames,
                    width = s.width,
                    height = s.height,
                    xorig = s.xorig,
                    yorig = s.yorig,
                    imageSpeed = s.imageSpeed,
                    frames = Frames(s),
                });
            }

            var backgrounds = new List<ArtCatalogue.BackgroundEntry>(data.backgrounds.Length);
            foreach (GmlArtData.BackgroundData b in data.backgrounds)
            {
                backgrounds.Add(new ArtCatalogue.BackgroundEntry
                {
                    name = b.name,
                    group = b.group,
                    inProject = b.inProject,
                    originalPath = b.originalPath,
                    originalImage = b.originalImage,
                    width = b.width,
                    height = b.height,
                    tileHorizontal = b.tileHorizontal,
                    tileVertical = b.tileVertical,
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(b.texture)
                        ?? throw new InvalidOperationException(b.texture + " did not import as a sprite"),
                });
            }

            ArtCatalogue catalogue = LoadOrCreateCatalogue();
            catalogue.SetArt(data.pixelsPerUnit, data.roomSpeed, sprites, backgrounds);
            EditorUtility.SetDirty(catalogue);
        }

        /// <summary>The catalogue asset, created on first use (the font builder fills its fonts).</summary>
        public static ArtCatalogue LoadOrCreateCatalogue()
        {
            ArtCatalogue catalogue = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(CataloguePath);
            if (catalogue == null)
            {
                EnsureFolder(CatalogueFolder);
                catalogue = ScriptableObject.CreateInstance<ArtCatalogue>();
                AssetDatabase.CreateAsset(catalogue, CataloguePath);
            }

            return catalogue;
        }

        private static void DeleteStale(string folder, string pattern, HashSet<string> wanted)
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(folder, pattern, SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (!wanted.Contains(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        public static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
