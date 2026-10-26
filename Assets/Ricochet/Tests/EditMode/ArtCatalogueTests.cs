// U.RICO-T44: the original's art is imported whole, catalogued with where each piece came from, and the views' GML
// names all resolve to it; the fonts draw at the original's metrics with draw_text_outline's outline.
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
using Ricochet.Tooling;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Ricochet.EditorTests
{
    public class ArtCatalogueTests
    {
        private static ArtCatalogue Catalogue()
        {
            ArtCatalogue catalogue = GameArt.Catalogue;
            Assert.That(catalogue, Is.Not.Null, "Resources/" + ArtCatalogue.ResourcePath + " is missing; run Tools/import-gml-art.ps1 and import-gml-fonts.ps1");
            return catalogue;
        }

        [Test]
        public void EverySpriteAndBackgroundOnDiskIsCataloguedWithItsOriginalFiles()
        {
            ArtCatalogue catalogue = Catalogue();
            GmlArtData data = GmlArtData.Load();
            Assert.That(data, Is.Not.Null);

            // The original carries 314 sprites (275 in Ricochet.project.gmx) and 63 backgrounds (50 listed), one of which
            // (bg_redslate) names an image the project never had.
            Assert.That(catalogue.Sprites.Count, Is.EqualTo(314));
            Assert.That(catalogue.Sprites.Count(s => s.inProject), Is.EqualTo(275));
            Assert.That(catalogue.Backgrounds.Count, Is.EqualTo(62));
            Assert.That(catalogue.Backgrounds.Count(b => b.inProject), Is.EqualTo(50));
            Assert.That(data.skipped.Select(s => s.name), Is.EquivalentTo(new[] { "bg_redslate" }));

            foreach (ArtCatalogue.SpriteEntry s in catalogue.Sprites)
            {
                Assert.That(s.originalPath, Is.EqualTo("sprites\\" + s.name + ".sprite.gmx"), s.name);
                Assert.That(s.frames, Is.Not.Empty, s.name);
                Assert.That(s.frames.All(f => f != null), Is.True, s.name + " has a frame that did not import");
                Assert.That(s.originalFrames.Length, Is.EqualTo(s.frames.Length), s.name);
                Assert.That(s.group == "Unlisted", Is.EqualTo(!s.inProject), s.name);
            }

            foreach (ArtCatalogue.BackgroundEntry b in catalogue.Backgrounds)
            {
                Assert.That(b.originalPath, Is.EqualTo("background\\" + b.name + ".background.gmx"), b.name);
                Assert.That(b.sprite, Is.Not.Null, b.name);
                Assert.That(b.originalImage, Does.StartWith("background\\images\\"), b.name);
            }
        }

        [Test]
        public void ASpriteImportsAtItsGmlOriginAndTheRoomsScale()
        {
            ArtCatalogue.SpriteEntry crate = Catalogue().Sprite(GmlSprites.WeaponLand);
            Sprite sprite = crate.frames[0];
            // 1920 GML pixels are 22 units, and the pivot is the GML origin (27, 36 of 54x72: the middle).
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(1920f / 22f).Within(1e-3f));
            Assert.That(sprite.pivot.x, Is.EqualTo(27f).Within(1e-3f));
            Assert.That(sprite.pivot.y, Is.EqualTo(72f - 36f).Within(1e-3f));
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Bilinear));

            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.mipmapEnabled, Is.False);
        }

        [Test]
        public void EverySpriteTheViewsDrawResolves()
        {
            foreach (Weapon weapon in System.Enum.GetValues(typeof(Weapon)))
            {
                Assert.That(GameArt.Frame(ShotView.SpriteOf(weapon)), Is.Not.Null, weapon.ToString());
            }

            foreach (DropKind kind in System.Enum.GetValues(typeof(DropKind)))
            {
                Assert.That(GameArt.Frame(Crate.SpriteOf(kind, false)), Is.Not.Null, kind + " falling");
                Assert.That(GameArt.Frame(Crate.SpriteOf(kind, true)), Is.Not.Null, kind + " landed");
                string icon = Crate.IconOf(kind);
                if (icon != null)
                {
                    Assert.That(GameArt.Frame(icon), Is.Not.Null, kind + " icon");
                }
            }

            // obj_explosion's spr_smallexplosion at image_speed 0.5 and obj_grenade_explosion's spr_bigexplosion at 0.2,
            // both at normalFPS (60): 17 frames in 0.57 s, 20 frames in 1.67 s.
            Assert.That(GameArt.Frames(GmlSprites.SmallExplosion).Length, Is.EqualTo(17));
            Assert.That(GameArt.FramesPerSecond(GmlSprites.SmallExplosion, ShotView.SmallExplosionSpeed), Is.EqualTo(30f).Within(1e-4f));
            Assert.That(GameArt.Duration(GmlSprites.BigExplosion, ShotView.BigExplosionSpeed), Is.EqualTo(20f / 12f).Within(1e-4f));
            Assert.That(GameArt.FrameAt(GmlSprites.SmallExplosion, 10f, false, ShotView.SmallExplosionSpeed), Is.SameAs(GameArt.Frames(GmlSprites.SmallExplosion)[16]));
        }

        [Test]
        public void TheNineFontsAndTheSpriteFontAreTextMeshProFontsWithTheOutlines()
        {
            string[] names =
            {
                GmlFonts.Impact, GmlFonts.Mono, GmlFonts.Score, GmlFonts.ScoreShadow, GmlFonts.Count,
                GmlFonts.Small, GmlFonts.Credits, GmlFonts.Font4, GmlFonts.Font5,
            };
            ArtCatalogue catalogue = Catalogue();
            Assert.That(catalogue.Fonts.Count, Is.EqualTo(10));
            foreach (string name in names)
            {
                ArtCatalogue.FontEntry entry = catalogue.Font(name);
                Assert.That(entry, Is.Not.Null, name);
                Assert.That(entry.originalPath, Is.EqualTo("fonts\\" + name + ".font.gmx"));
                Assert.That(entry.font.glyphTable.Count, Is.EqualTo(96), name);
                Assert.That(entry.font.atlasRenderMode, Is.EqualTo(UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA), name);
                Assert.That(entry.font.atlasTexture, Is.Not.Null, name);
                foreach (int pixels in new[] { 1, 2 })
                {
                    Material outline = GameArt.Outline(name, pixels);
                    Assert.That(outline, Is.Not.Null, name + " outline " + pixels);
                    Assert.That(outline.mainTexture, Is.SameAs(entry.font.atlasTexture));
                    Assert.That(outline.GetColor(ShaderUtilities.ID_OutlineColor), Is.EqualTo(Color.black));
                    // The band is exactly `pixels` GameMaker pixels wide in atlas texels, outside an undilated face.
                    float padding = outline.GetFloat(ShaderUtilities.ID_GradientScale) - 1f;
                    float scale = entry.font.faceInfo.pointSize / entry.size;
                    Assert.That(outline.GetFloat(ShaderUtilities.ID_OutlineWidth) * padding, Is.EqualTo(pixels * scale).Within(1e-3f), name);
                    Assert.That(outline.GetFloat(ShaderUtilities.ID_FaceDilate), Is.EqualTo(outline.GetFloat(ShaderUtilities.ID_OutlineWidth)), name);
                }
            }

            Assert.That(catalogue.Font(GmlFonts.Impact).inProject, Is.True);
            Assert.That(catalogue.Font(GmlFonts.Credits).inProject, Is.False);
            ArtCatalogue.FontEntry sheet = catalogue.Font(GmlFonts.SpriteFont);
            Assert.That(sheet.spriteFont, Is.True);
            Assert.That(sheet.font.glyphTable.Count, Is.EqualTo(95)); // ASCII 33 to 126, and a space
            Assert.That(sheet.originalPath, Is.EqualTo("sprites\\spr_spritefont.sprite.gmx"));
        }

        [Test]
        public void TextInAGmlFontIsAsWideAsGameMakerDrawsIt()
        {
            var go = new GameObject("GML text");
            try
            {
                TextMeshPro text = go.AddComponent<TextMeshPro>();
                Assert.That(GameArt.ApplyFont(text, GmlFonts.Score, 0), Is.True);
                ArtCatalogue.FontEntry entry = Catalogue().Font(GmlFonts.Score);
                text.fontSize = entry.size;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                const string Sample = "Player 1: 12";
                GmlFontData.FontData font = GmlFontData.Load().fonts.First(f => f.name == GmlFonts.Score);
                int gml = GmlFontMetrics.StringWidth(font.Glyphs(), Sample);
                // A world point is a tenth of a unit, so the font.gmx size draws a GameMaker pixel as 0.1 units.
                Assert.That(text.GetPreferredValues(Sample).x * 10f, Is.EqualTo(gml).Within(1.5f));
                Assert.That(text.font.faceInfo.lineHeight / (entry.font.faceInfo.pointSize / entry.size),
                    Is.EqualTo(GmlFontMetrics.LineHeight(font.Glyphs())).Within(1e-3f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
