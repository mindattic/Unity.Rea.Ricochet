using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ricochet.Core.Tests
{
    using Ricochet.Core.Art;
    using Ricochet.Core.Menu;

    // U.RICO-T54: the controls room's keyboard and mouse redrawn from the bindings (obj_keyboardlayout's spr_keyboardlayout,
    // obj_mouse's spr_mouse), and the how-to-play slides' pictures on their frame (obj_slide's spr_howtoplay1).
    public class KeyboardPictureTests
    {
        // RicochetControls.inputactions' keyboard and mouse bindings, and a gamepad one the picture leaves out.
        private static List<BoundControl> Defaults() => new List<BoundControl>
        {
            new BoundControl("Aim", "<Keyboard>/d"), new BoundControl("Aim", "<Keyboard>/a"),
            new BoundControl("Aim", "<Keyboard>/rightArrow"), new BoundControl("Aim", "<Keyboard>/leftArrow"),
            new BoundControl("Aim", "<Gamepad>/leftStick/x"),
            new BoundControl("Drive", "<Keyboard>/s"), new BoundControl("Drive", "<Keyboard>/w"),
            new BoundControl("CycleWeapon", "<Keyboard>/q"), new BoundControl("CycleWeapon", "<Keyboard>/e"),
            new BoundControl("CycleItem", "<Keyboard>/z"), new BoundControl("CycleItem", "<Keyboard>/c"),
            new BoundControl("Fire", "<Keyboard>/space"), new BoundControl("Fire", "<Mouse>/leftButton"),
            new BoundControl("Fire", "<Gamepad>/buttonSouth"),
            new BoundControl("EndTurn", "<Keyboard>/f"),
            new BoundControl("UseItem", "<Keyboard>/leftCtrl"), new BoundControl("UseItem", "<Keyboard>/rightCtrl"),
            new BoundControl("UseItem", "<Mouse>/rightButton"),
            new BoundControl("Fuse1", "<Keyboard>/1"), new BoundControl("Fuse2", "<Keyboard>/2"), new BoundControl("Fuse3", "<Keyboard>/3"),
            new BoundControl("Ping", "<Keyboard>/leftAlt"), new BoundControl("Ping", "<Keyboard>/rightAlt"),
            new BoundControl("Pause", "<Keyboard>/p"), new BoundControl("Pause", "<Keyboard>/pause"),
            new BoundControl("Fullscreen", "<Keyboard>/f1"), new BoundControl("Reroll", "<Keyboard>/r"),
            new BoundControl("BorderThinner", "<Keyboard>/pageUp"), new BoundControl("BorderThicker", "<Keyboard>/pageDown"),
        };

        [Test]
        public void EveryKeyIsInsideThePicture_AndNoTwoCapsOverlap()
        {
            IReadOnlyList<KeyCap> keys = KeyboardPicture.Keys;
            Assert.That(keys.Count, Is.GreaterThan(100), "a full keyboard");
            Assert.That(keys.Select(k => k.Control).Distinct().Count(), Is.EqualTo(keys.Count), "each control once");
            for (int i = 0; i < keys.Count; i++)
            {
                Assert.That(keys[i].Rect.Inside(KeyboardPicture.Width, KeyboardPicture.Height), Is.True, keys[i] + " " + keys[i].Rect);
                for (int j = i + 1; j < keys.Count; j++)
                {
                    Assert.That(keys[i].Rect.Overlaps(keys[j].Rect), Is.False, keys[i] + " overlaps " + keys[j]);
                }
            }

            Assert.That(KeyboardPicture.Mouse.Inside(KeyboardPicture.Width, KeyboardPicture.Height), Is.True);
            Assert.That(keys.Any(k => k.Rect.Overlaps(KeyboardPicture.Mouse)), Is.False, "the mouse is clear of the keys");
        }

        [Test]
        public void TheDefaultBindingsColourTheirKeys_AndTheMouseButtons()
        {
            KeyboardDrawing drawing = KeyboardPicture.Draw(Defaults());
            Assert.That(drawing.GroupOf("q"), Is.EqualTo("CycleWeapon"));
            Assert.That(drawing.GroupOf("e"), Is.EqualTo("CycleWeapon"));
            Assert.That(drawing.GroupOf("a"), Is.EqualTo("Aim"));
            Assert.That(drawing.GroupOf("leftArrow"), Is.EqualTo("Aim"));
            Assert.That(drawing.GroupOf("w"), Is.EqualTo("Drive"));
            Assert.That(drawing.GroupOf("space"), Is.EqualTo("Fire"));
            Assert.That(drawing.GroupOf("leftCtrl"), Is.EqualTo("UseItem"));
            Assert.That(drawing.GroupOf("2"), Is.EqualTo("Fuse"), "the three fuses are one callout");
            Assert.That(drawing.GroupOf("pageDown"), Is.EqualTo("Border"));
            Assert.That(drawing.GroupOf("escape"), Is.EqualTo(KeyboardPicture.BackGroup), "Esc is the menus' Back");
            Assert.That(drawing.GroupOf("numpad7"), Is.EqualTo(KeyboardPicture.QuickAnglesGroup));
            Assert.That(drawing.GroupOf("g"), Is.Null);
            Assert.That(drawing.MouseGroupOf("leftButton"), Is.EqualTo("Fire"));
            Assert.That(drawing.MouseGroupOf("rightButton"), Is.EqualTo("UseItem"));
            Assert.That(drawing.MouseGroupOf("middleButton"), Is.Null);
            Assert.That(drawing.MouseCaptions.Select(c => c.Caption), Is.EqualTo(new[] { "Left: Fire weapon", "Right: Use item" }));
            Assert.That(drawing.Callouts.Select(c => c.Group), Is.Unique);
            Assert.That(drawing.Callouts.Count, Is.EqualTo(drawing.Marks.Select(m => m.Group).Distinct().Count()));
            Assert.That(drawing.Callouts.First(c => c.Group == "Fire").Above, Is.False, "the space bar's name is below the keyboard");
            Assert.That(drawing.Callouts.First(c => c.Group == "CycleWeapon").Above, Is.True);
        }

        [Test]
        public void ARebindMovesTheColour_ToTheNewKey()
        {
            List<BoundControl> bindings = Defaults();
            int space = bindings.FindIndex(b => b.Path == "<Keyboard>/space");
            bindings[space] = new BoundControl("Fire", "<Keyboard>/g");
            int left = bindings.FindIndex(b => b.Path == "<Mouse>/leftButton");
            bindings[left] = new BoundControl("Fire", "<Mouse>/middleButton");
            KeyboardDrawing drawing = KeyboardPicture.Draw(bindings);
            Assert.That(drawing.GroupOf("g"), Is.EqualTo("Fire"));
            Assert.That(drawing.GroupOf("space"), Is.Null);
            Assert.That(drawing.Callouts.First(c => c.Group == "Fire").Anchor.Control, Is.EqualTo("g"));
            Assert.That(drawing.MouseGroupOf("leftButton"), Is.Null);
            Assert.That(drawing.MouseGroupOf("middleButton"), Is.EqualTo("Fire"));
        }

        [Test]
        public void TheNamesStandInsideThePicture_ClearOfEachOtherAndOfTheKeys()
        {
            KeyboardDrawing drawing = KeyboardPicture.Draw(Defaults());
            List<PictureRect> labels = drawing.Callouts.Select(c => c.Label).Concat(drawing.MouseCaptions.Select(c => c.Label)).ToList();
            for (int i = 0; i < labels.Count; i++)
            {
                Assert.That(labels[i].Inside(KeyboardPicture.Width, KeyboardPicture.Height), Is.True, labels[i].ToString());
                Assert.That(KeyboardPicture.Keys.Any(k => k.Rect.Overlaps(labels[i])), Is.False, "a name over a key " + labels[i]);
                for (int j = i + 1; j < labels.Count; j++)
                {
                    Assert.That(labels[i].Overlaps(labels[j]), Is.False, labels[i] + " overlaps " + labels[j]);
                }
            }

            // With the default bindings every line reaches its name without running through another's.
            foreach (Callout c in drawing.Callouts)
            {
                double top = System.Math.Min(c.LineFromY, c.LaneY);
                double bottom = System.Math.Max(c.LineFromY, c.LaneY);
                foreach (Callout other in drawing.Callouts.Where(o => o != c))
                {
                    PictureRect them = other.Label;
                    bool crosses = c.LineX >= them.X && c.LineX <= them.Right && them.Bottom > top && them.Y < bottom;
                    Assert.That(crosses, Is.False, c.Group + "'s line runs through " + other.Group + "'s name");
                }
            }
        }

        [Test]
        public void AnotherDevicesPath_ColoursNoKey()
        {
            Assert.That(KeyboardPicture.KeyboardControl("<Keyboard>/leftctrl"), Is.EqualTo("leftCtrl"), "paths are case-blind");
            Assert.That(KeyboardPicture.KeyboardControl("<Gamepad>/buttonSouth"), Is.Null);
            Assert.That(KeyboardPicture.MouseButton("<Mouse>/rightButton"), Is.EqualTo("rightButton"));
            KeyboardDrawing drawing = KeyboardPicture.Draw(new[] { new BoundControl("Fire", "<Gamepad>/buttonSouth") });
            Assert.That(drawing.Marks.Select(m => m.Group).Distinct(), Is.EquivalentTo(new[] { KeyboardPicture.BackGroup, KeyboardPicture.QuickAnglesGroup }));
        }

        [Test]
        public void EverySlideHasPictures_LaidOutOnItsFrame()
        {
            Assert.That(MenuFlow.Slides.All(s => s.Images.Count > 0), Is.True, "every slide in menu.json has images");
            Assert.That(MenuFlow.Slides.SelectMany(s => s.Images).All(n => n.StartsWith("spr_")), Is.True, "GML sprite names");
            for (int count = 1; count <= 8; count++)
            {
                IReadOnlyList<PictureRect> slots = SlidePicture.Slots(count);
                Assert.That(slots.Count, Is.EqualTo(count));
                for (int i = 0; i < slots.Count; i++)
                {
                    Assert.That(slots[i].Inside(SlidePicture.Width, SlidePicture.Height), Is.True, count + ": " + slots[i]);
                    for (int j = i + 1; j < slots.Count; j++)
                    {
                        Assert.That(slots[i].Overlaps(slots[j]), Is.False, count + ": " + slots[i] + " and " + slots[j]);
                    }
                }
            }

            Assert.That(SlidePicture.Slots(0), Is.Empty);
        }
    }
}
