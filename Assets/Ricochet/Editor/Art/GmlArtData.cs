using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ricochet.Tooling
{
    /// <summary>
    /// Art/gml-art.json, the file Tools/import-gml-art.ps1 writes from the original project (U.RICO-T44): every GML
    /// sprite and background on disk, whether the project lists it, the original files it came from and the Unity assets
    /// it becomes, plus what could not be imported and why. The editor reads it and never writes it; to change the art,
    /// rerun the tool.
    /// </summary>
    [Serializable]
    public sealed class GmlArtData
    {
        public const string ArtFolder = "Assets/Ricochet/Art";
        public const string DataPath = ArtFolder + "/gml-art.json";
        public const string SpritesFolder = ArtFolder + "/Sprites/";
        public const string BackgroundsFolder = ArtFolder + "/Backgrounds/";
        public const string AtlasesFolder = ArtFolder + "/Atlases/";

        public string source;
        public float pixelsPerUnit;
        public int roomSpeed;
        public int atlasMaxSide;
        public SpriteData[] sprites;
        public BackgroundData[] backgrounds;
        public SkippedData[] skipped;

        [Serializable]
        public sealed class SpriteData
        {
            public string name, group;
            public bool inProject;
            public string originalPath;
            public string[] originalFrames;
            public int width, height, xorig, yorig;
            /// <summary>0 precise, 1 rectangle, 2 ellipse, 3 diamond.</summary>
            public int colkind;
            public bool sepmasks;
            public int bboxLeft, bboxTop, bboxRight, bboxBottom;
            public bool tileHorizontal, tileVertical;
            public float imageSpeed;
            public string imageSpeedSource;
            public string[] objects;
            public string[] frames;
            public string atlas;
            public string[] notes;
        }

        [Serializable]
        public sealed class BackgroundData
        {
            public string name, group;
            public bool inProject;
            public string originalPath, originalImage;
            public int width, height;
            public bool tileHorizontal, tileVertical, tileset;
            public int tileWidth, tileHeight;
            public string texture;
            public string[] notes;
        }

        [Serializable]
        public sealed class SkippedData
        {
            public string name, kind, reason;
        }

        [NonSerialized] private Dictionary<string, SpriteData> _spriteByFrame;
        [NonSerialized] private Dictionary<string, BackgroundData> _backgroundByTexture;

        /// <summary>The sprite one of whose frames is at <paramref name="assetPath"/>, or null.</summary>
        public SpriteData SpriteForFrame(string assetPath)
        {
            if (_spriteByFrame == null)
            {
                _spriteByFrame = new Dictionary<string, SpriteData>(StringComparer.OrdinalIgnoreCase);
                foreach (SpriteData s in sprites)
                {
                    foreach (string f in s.frames)
                    {
                        _spriteByFrame[f] = s;
                    }
                }
            }

            return _spriteByFrame.TryGetValue(assetPath, out SpriteData found) ? found : null;
        }

        /// <summary>The background whose image is at <paramref name="assetPath"/>, or null.</summary>
        public BackgroundData BackgroundForTexture(string assetPath)
        {
            if (_backgroundByTexture == null)
            {
                _backgroundByTexture = new Dictionary<string, BackgroundData>(StringComparer.OrdinalIgnoreCase);
                foreach (BackgroundData b in backgrounds)
                {
                    _backgroundByTexture[b.texture] = b;
                }
            }

            return _backgroundByTexture.TryGetValue(assetPath, out BackgroundData found) ? found : null;
        }

        private static GmlArtData s_cached;
        private static DateTime s_cachedStamp;

        /// <summary>The project's gml-art.json, re-read whenever the file changes; null if the tool has not run.</summary>
        public static GmlArtData Load()
        {
            if (!File.Exists(DataPath))
            {
                return null;
            }

            DateTime stamp = File.GetLastWriteTimeUtc(DataPath);
            if (s_cached == null || stamp != s_cachedStamp)
            {
                s_cached = Parse(File.ReadAllText(DataPath));
                s_cachedStamp = stamp;
            }

            return s_cached;
        }

        /// <summary>Parses and validates the file, so a malformed one fails loudly here rather than as missing art.</summary>
        public static GmlArtData Parse(string json)
        {
            GmlArtData data = JsonUtility.FromJson<GmlArtData>(json);
            if (data == null)
            {
                throw new InvalidDataException(DataPath + " is not valid JSON");
            }

            if (data.pixelsPerUnit <= 0f)
            {
                throw new InvalidDataException(DataPath + ": pixelsPerUnit must be positive");
            }

            if (data.roomSpeed <= 0)
            {
                throw new InvalidDataException(DataPath + ": roomSpeed must be positive");
            }

            data.sprites ??= Array.Empty<SpriteData>();
            data.backgrounds ??= Array.Empty<BackgroundData>();
            data.skipped ??= Array.Empty<SkippedData>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpriteData s in data.sprites)
            {
                if (string.IsNullOrEmpty(s.name) || !names.Add(s.name))
                {
                    throw new InvalidDataException(DataPath + ": sprite name missing or repeated: " + s.name);
                }

                if (s.width <= 0 || s.height <= 0)
                {
                    throw new InvalidDataException(DataPath + ": " + s.name + " has no size");
                }

                if (s.frames == null || s.frames.Length == 0)
                {
                    throw new InvalidDataException(DataPath + ": " + s.name + " has no frames");
                }

                if (s.originalFrames == null || s.originalFrames.Length != s.frames.Length || string.IsNullOrEmpty(s.originalPath))
                {
                    throw new InvalidDataException(DataPath + ": " + s.name + " does not record its original files");
                }

                if (s.imageSpeed < 0f)
                {
                    throw new InvalidDataException(DataPath + ": " + s.name + " has a negative image_speed");
                }

                foreach (string f in s.frames)
                {
                    if (!f.StartsWith(SpritesFolder, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(DataPath + ": " + s.name + " frame outside " + SpritesFolder + ": " + f);
                    }
                }
            }

            foreach (BackgroundData b in data.backgrounds)
            {
                if (string.IsNullOrEmpty(b.name) || !names.Add(b.name))
                {
                    throw new InvalidDataException(DataPath + ": background name missing or repeated: " + b.name);
                }

                if (b.texture == null || !b.texture.StartsWith(BackgroundsFolder, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(DataPath + ": " + b.name + " texture outside " + BackgroundsFolder);
                }

                if (string.IsNullOrEmpty(b.originalPath) || string.IsNullOrEmpty(b.originalImage))
                {
                    throw new InvalidDataException(DataPath + ": " + b.name + " does not record its original files");
                }
            }

            return data;
        }
    }
}
