// U.RICO-T78: the original's menu art on the rebuild's screens, no session needed: GmlMenuSkin dresses a button as obj_btn_*
// (spr_button over spr_button_back, spr_button_over under the pointer, the label in fnt_mono outlined 2 px), a switch as
// obj_checkbox_* (ticked as it is set), a text field as obj_inputbox, a slider row with the less / more arrows that step it
// and wrap, and the titles in fnt_impact; the title room is bg_titlescreen with RICOCHET's letters; the controls, how-to-play
// and credits rooms show their pictures. U.RICO-T84: the screens stand on the rooms' background with their controls at the
// rooms' places.
using System.Collections;
using System.Linq;
using MindAttic.Components.Unity;
using NUnit.Framework;
using Ricochet.Core.Art;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Ricochet.Tests
{
    public class MenuArtTests
    {
        private MenuStack _stack;
        private TitleScreen _title;

        [TearDown]
        public void RemoveTheScreens()
        {
            if (_stack != null)
            {
                Object.Destroy(_stack.transform.root.gameObject);
                _stack = null;
            }

            if (_title != null)
            {
                Object.Destroy(_title.gameObject);
                _title = null;
            }
        }

        [UnityTest]
        public IEnumerator TheMenusControlsAreDressedInTheOriginalsArt()
        {
            Assume.That(GameArt.Frame("spr_button"), Is.Not.Null, "the art is not built");
            _stack = MenuStack.Create();
            float volume = 0;
            bool on = false;
            MenuScreen s = _stack.Push("Art", screen =>
            {
                screen.AddSection("Section");
                screen.AddButton("First", () => { }); // the screen's first control has the keyboard's focus, drawn as if hovered
                screen.AddButton("Resume", () => { });
                screen.AddToggle("Laser", false, v => on = v);
                screen.AddTextbox("Name", "Ann");
                screen.AddRange("Bounces", 1, 20, 20, v => volume = v, wholeNumbers: true);
            });
            yield return null;
            GmlMenuSkin.Sweep(); // (a watcher another test left behind may have dressed them already)
            yield return null;

            MaButton button = s.GetComponentsInChildren<MaButton>().First(b => b.Label == "Resume");
            GmlControlSkin skin = button.GetComponent<GmlControlSkin>();
            Assert.That(skin, Is.Not.Null, "the button was not dressed");
            Assert.That(skin.Plate.FaceName, Is.EqualTo("spr_button"));
            Assert.That(skin.Plate.BackName, Is.EqualTo("spr_button_back"));
            Assert.That(skin.Plate.Back.color.a, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(skin.Plate.Face.sprite, Is.Not.Null);
            Assert.That(button.Surface.Fill.canvasRenderer.GetAlpha(), Is.EqualTo(0f), "the theme's fill still shows");
            Assert.That(button.LabelText.font, Is.SameAs(GameArt.Font(MenuLooks.ButtonFont)));
            Assert.That(button.LabelText.fontSharedMaterial, Is.SameAs(GameArt.Outline(MenuLooks.ButtonFont, 2)));
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(skin.Plate.BackName, Is.EqualTo("spr_button_over"), "the pointer on it shows spr_button_over");

            MaToggle toggle = s.GetComponentInChildren<MaToggle>();
            GmlControlSkin box = toggle.GetComponent<GmlControlSkin>();
            Assert.That(box.Plate.FaceName, Is.EqualTo("spr_checkbox_unchecked"));
            toggle.Value = true;
            yield return null;
            Assert.That(on, Is.True);
            Assert.That(box.Plate.FaceName, Is.EqualTo("spr_checkbox_checked"));

            MaTextbox textbox = s.GetComponentInChildren<MaTextbox>();
            Assert.That(textbox.GetComponent<GmlControlSkin>().Plate.FaceName, Is.EqualTo("spr_inputbox"));
            Assert.That(textbox.GetComponent<GmlControlSkin>().Plate.BackName, Is.EqualTo("spr_inputbox_back"));

            // obj_btn_bounces_more: 20 wraps to 1; _less: 1 wraps to 20.
            GmlStepper[] arrows = s.GetComponentsInChildren<GmlStepper>();
            Assert.That(arrows.Select(a => a.Direction), Is.EquivalentTo(new[] { -1, 1 }));
            GmlStepper more = arrows.First(a => a.Direction > 0), less = arrows.First(a => a.Direction < 0);
            Assert.That(more.GetComponentInChildren<GmlPlate>().FaceName, Is.EqualTo("spr_arrowright"));
            Assert.That(less.GetComponentInChildren<GmlPlate>().FaceName, Is.EqualTo("spr_arrowleft"));
            more.Button.onClick.Invoke();
            Assert.That(volume, Is.EqualTo(1f));
            less.Button.onClick.Invoke();
            Assert.That(volume, Is.EqualTo(20f));
            less.Button.onClick.Invoke();
            Assert.That(volume, Is.EqualTo(19f));
            Assert.That(UiSounds.PressCue(more.Button), Is.EqualTo(SoundCue.Select), "the arrows play sfx_select");

            Assert.That(GmlMenuSkin.TitleOf(s).font, Is.SameAs(GameArt.Font(MenuLooks.TitleFont)));
            Assert.That(GmlMenuSkin.Sweep(), Is.EqualTo(0), "a dressed control is dressed once");
        }

        [UnityTest]
        public IEnumerator TheTitleIsOnItsBackground_WithRicochetsLetters()
        {
            Assume.That(GameArt.Background(MenuLooks.RoomBackground), Is.Not.Null, "the art is not built");
            _title = TitleScreen.Show(null, "test");
            yield return null;
            Assert.That(_title.RoomCanvas, Is.Not.Null);
            Assert.That(_title.RoomCanvas.sortingOrder, Is.LessThan(TitleScreen.TitleSortingOrder), "the card is over the room");
            Assert.That(_title.Letters.Select(l => l.sprite.name.Replace("spr_letter_", string.Empty).Substring(0, 1)), Is.EqualTo("RICOCHET".Select(c => c.ToString())));
            Assert.That(_title.Letters[0].rectTransform.anchoredPosition, Is.EqualTo(new Vector2(64f, -576f)));
            Assert.That(_title.Letters[7].rectTransform.anchoredPosition, Is.EqualTo(new Vector2(512f, -352f)));
        }

        [UnityTest]
        public IEnumerator TheControlsHowToPlayAndCreditsRoomsShowTheirPictures()
        {
            Assume.That(GameArt.Frame(MenuLooks.KeyboardLayout), Is.Not.Null, "the art is not built");
            _stack = MenuStack.Create();
            MenuScreen controls = _stack.Push(MenuFlow.Title(Room.Controls), Menus.BuildControls);
            MenuScreen how = _stack.Push(MenuFlow.Title(Room.HowToPlay), s => Menus.BuildHowToPlay(s));
            MenuScreen credits = _stack.Push(MenuFlow.Title(Room.Credits), Menus.BuildCredits);
            yield return null;
            Assert.That(Picture(controls), Is.EqualTo(MenuLooks.KeyboardLayout));
            Assert.That(Picture(how), Is.EqualTo(MenuLooks.HowToPlaySlide));
            Assert.That(Picture(credits), Is.EqualTo(MenuLooks.CreditsImage));
        }

        // U.RICO-T84: every menu room was 1920 x 1080 on bg_titlescreen, its controls at the room's own places (rooms/*.gmx):
        // the main menu's buttons down x 960 from y 352, 96 apart; Back at (128, 1024), the way on at (1792, 1024), Default at
        // (1792, 928); the keyboard layout at (960, 512) over the controls room's lines; the slide at (896, 576) with Last / Next
        // as spr_arrowleft / right at x 512 and 1280.
        [UnityTest]
        public IEnumerator TheMenuScreensStandOnTheRoomsBackground_WithTheirControlsWhereTheRoomsPutThem()
        {
            Assume.That(GameArt.Background(MenuLooks.RoomBackground), Is.Not.Null, "the art is not built");
            Assume.That(GameArt.Frame(MenuLooks.KeyboardLayout), Is.Not.Null, "the art is not built");
            _stack = MenuStack.Create();
            string[] labels = { "Settings", "How to play", "Controls", "Credits", "Quit" };
            MenuScreen main = _stack.Push("Main menu", s =>
            {
                foreach (string label in labels)
                {
                    s.AddButton(label, () => { });
                }
            });
            yield return Laid();
            GmlRoomLayout room = main.GetComponent<GmlRoomLayout>();
            Assert.That(room, Is.Not.Null, "the main menu has no room");
            Assert.That(room.Taken, Is.True, "the main menu kept the kit's card");
            Assert.That(room.Background.sprite, Is.SameAs(GameArt.Background(MenuLooks.RoomBackground)));
            for (int i = 0; i < labels.Length; i++)
            {
                Assert.That(Rect(main, labels[i]).anchoredPosition, Is.EqualTo(new Vector2(960f, -(352f + 96f * i))), labels[i]);
            }

            MenuScreen setup = _stack.Push("Setup", s =>
            {
                s.AddToggle("Laser", true, _ => { });
                s.AddButton(Menus.ResetSetupLabel, () => { });
                s.AddButton("Start", () => { }, MindAttic.Components.Unity.Core.ButtonVariant.Primary);
            });
            yield return Laid();
            Assert.That(setup.GetComponent<GmlRoomLayout>().Taken, Is.True);
            Assert.That(setup.ShowBackButton, Is.False, "the kit's back icon is hidden: the room's Back stands in the corner");
            Assert.That(((RectTransform)setup.Items.Find(GmlRoomLayout.BackName)).anchoredPosition, Is.EqualTo(new Vector2(128f, -1024f)));
            Assert.That(Rect(setup, "Start").anchoredPosition, Is.EqualTo(new Vector2(1792f, -1024f)));
            Assert.That(Rect(setup, Menus.ResetSetupLabel).anchoredPosition, Is.EqualTo(new Vector2(1792f, -928f)));

            MenuScreen controls = _stack.Push(MenuFlow.Title(Room.Controls), Menus.BuildControls);
            yield return Laid();
            RectTransform keyboard = PictureRect(controls);
            Assert.That(keyboard.anchoredPosition, Is.EqualTo(new Vector2(960f, -512f)));
            Assert.That(keyboard.sizeDelta, Is.EqualTo(new Vector2(1814f, 972f)));
            MaText line = controls.Items.Cast<Transform>().Select(t => t.GetComponent<MaText>()).First(t => t != null);
            Assert.That(line.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f), "the binding lines are under the keyboard picture");

            MenuScreen how = _stack.Push(MenuFlow.Title(Room.HowToPlay), s => Menus.BuildHowToPlay(s));
            yield return Laid();
            Assert.That(PictureRect(how).anchoredPosition, Is.EqualTo(new Vector2(896f, -576f)));
            foreach ((string label, float x, string face) in new[] { ("Last", 512f, "spr_arrowleft"), ("Next", 1280f, "spr_arrowright") })
            {
                RectTransform arrow = Rect(how, label);
                Assert.That(arrow.GetComponent<GmlSlideArrow>(), Is.Not.Null, label + " is not an arrow");
                Assert.That(arrow.GetComponent<GmlControlSkin>().Plate.FaceName, Is.EqualTo(face));
                Assert.That(arrow.anchoredPosition, Is.EqualTo(new Vector2(x, -576f)), label);
            }
        }

        // The skin's sweep, then a frame for the rooms to lay their screens out.
        private static IEnumerator Laid()
        {
            yield return null;
            GmlMenuSkin.Sweep();
            yield return null;
            GmlMenuSkin.Sweep();
            yield return null;
        }

        private static RectTransform Rect(MenuScreen s, string label) =>
            s.GetComponentsInChildren<MaButton>().First(b => b.Label == label).RectTransform;

        private static RectTransform PictureRect(MenuScreen s) =>
            (RectTransform)s.Items.GetComponentsInChildren<Image>(true).First(i => i.name.StartsWith(Menus.PictureName)).transform;

        private static string Picture(MenuScreen s)
        {
            Image image = s.Items.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name.StartsWith(Menus.PictureName));
            return image != null && image.sprite != null ? image.name.Substring(Menus.PictureName.Length + 2) : null;
        }
    }
}
