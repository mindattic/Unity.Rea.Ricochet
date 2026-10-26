using UnityEditor;
using UnityEngine;

namespace Ricochet.Tooling
{
    /// <summary>
    /// The importer preset for the art Tools/import-gml-art.ps1 mirrors into Assets/Ricochet/Art (U.RICO-T44), read from
    /// Art/gml-art.json so nothing is set by hand in the Inspector and every .meta is generated:
    ///
    /// Sprite frames (Art/Sprites): single sprites at the manifest's pixels per unit (the original's 1920 px room is the
    /// rebuild's 22 units, ShapeLibrary.UnitsPerPixel), the pivot on the GML origin, a full-rect mesh (GameMaker draws the
    /// whole frame), bilinear, clamped unless the sprite tiles, uncompressed and without mipmaps.
    ///
    /// Backgrounds (Art/Backgrounds): single sprites pivoted on the top left, where GameMaker draws a background from,
    /// repeating along any axis the background tiles, compressed at high quality.
    ///
    /// The import depends on gml-art.json, so a changed origin or tiling reimports the affected art.
    /// </summary>
    public class GmlArtImporter : AssetPostprocessor
    {
        /// <summary>Raise when the settings below change, so Unity reimports the art with them.</summary>
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool sprite = path.StartsWith(GmlArtData.SpritesFolder);
            if (!sprite && !path.StartsWith(GmlArtData.BackgroundsFolder))
            {
                return;
            }

            context.DependsOnSourceAsset(GmlArtData.DataPath);
            GmlArtData data = GmlArtData.Load();
            if (data == null)
            {
                Debug.LogWarning("[Ricochet] " + path + " has no " + GmlArtData.DataPath + "; run Tools/import-gml-art.ps1");
                return;
            }

            var importer = (TextureImporter)assetImporter;
            if (sprite)
            {
                GmlArtData.SpriteData entry = data.SpriteForFrame(path);
                if (entry == null)
                {
                    Debug.LogWarning("[Ricochet] " + path + " is not a frame in " + GmlArtData.DataPath);
                }
                else
                {
                    Configure(importer, entry, data.pixelsPerUnit);
                }
            }
            else
            {
                GmlArtData.BackgroundData entry = data.BackgroundForTexture(path);
                if (entry == null)
                {
                    Debug.LogWarning("[Ricochet] " + path + " is not a background in " + GmlArtData.DataPath);
                }
                else
                {
                    Configure(importer, entry, data.pixelsPerUnit);
                }
            }
        }

        /// <summary>A GML origin (x right, y down from the top left) as Unity's normalised pivot (y up).</summary>
        public static Vector2 Pivot(int xorig, int yorig, int width, int height) =>
            new Vector2(xorig / (float)width, 1f - yorig / (float)height);

        /// <summary>Applies a sprite frame's settings; true if anything changed.</summary>
        public static bool Configure(TextureImporter importer, GmlArtData.SpriteData sprite, float pixelsPerUnit)
        {
            return Apply(importer, pixelsPerUnit, Pivot(sprite.xorig, sprite.yorig, sprite.width, sprite.height),
                WrapFor(sprite.tileHorizontal), WrapFor(sprite.tileVertical), TextureImporterCompression.Uncompressed);
        }

        /// <summary>Applies a background's settings; true if anything changed.</summary>
        public static bool Configure(TextureImporter importer, GmlArtData.BackgroundData background, float pixelsPerUnit)
        {
            return Apply(importer, pixelsPerUnit, new Vector2(0f, 1f),
                WrapFor(background.tileHorizontal), WrapFor(background.tileVertical), TextureImporterCompression.CompressedHQ);
        }

        private static TextureWrapMode WrapFor(bool tiles) => tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        private static bool Apply(TextureImporter importer, float pixelsPerUnit, Vector2 pivot, TextureWrapMode wrapU,
            TextureWrapMode wrapV, TextureImporterCompression compression)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed =
                settings.textureType != TextureImporterType.Sprite ||
                settings.spriteMode != (int)SpriteImportMode.Single ||
                !Mathf.Approximately(settings.spritePixelsPerUnit, pixelsPerUnit) ||
                settings.spriteAlignment != (int)SpriteAlignment.Custom ||
                (settings.spritePivot - pivot).sqrMagnitude > 1e-12f ||
                settings.spriteMeshType != SpriteMeshType.FullRect ||
                settings.spriteGenerateFallbackPhysicsShape ||
                settings.mipmapEnabled ||
                !settings.alphaIsTransparency ||
                settings.filterMode != FilterMode.Bilinear ||
                settings.wrapModeU != wrapU || settings.wrapModeV != wrapV ||
                importer.textureCompression != compression ||
                importer.maxTextureSize != 2048;
            if (!changed)
            {
                return false;
            }

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = pixelsPerUnit;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false; // collisions are Core's, never the sprite's
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = true;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = wrapU;
            settings.wrapModeU = wrapU;
            settings.wrapModeV = wrapV;
            importer.SetTextureSettings(settings);
            importer.textureCompression = compression;
            // Every image in the original is at most 1920 px on a side, so 2048 never downscales one.
            importer.maxTextureSize = 2048;
            return true;
        }
    }
}
