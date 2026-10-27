// The original's menu rooms around the rebuild's menu screens (U.RICO-T84). Every menu room was 1920 x 1080 on bg_titlescreen with
// its controls at the room's own positions and sizes (Core's RoomLayout holds the table), so each MindAttic Components screen
// the skin sweeps up gets:
//   the room's background under it (bg_titlescreen, in place of the live arena or the kit's dimmed backdrop);
//   its card dissolved: the title at (32, 32), the back button at (128, 1024), the room's way on (the Primary button) at
//     (1792, 1024), Reset to defaults at (1792, 928), a screen of buttons only (the main menu) as one column at x 960, 96 apart,
//     the controls with no slot of their own flowing down the room as the players room's rows did (up to four columns);
//   the pictures where their rooms put them (the keyboard layout, the credits image, the how-to-play slide with its counter
//     above, its text below and Last / Next as arrows either side).
// A screen too long even for four columns (the settings) keeps the kit's card, on the new background. The title screen, the
// pause menu's vote stack and the kit's in-game and endgame menus (over the arena, as the original's pause was) are not
// taken: they carry a <see cref="GmlNoRoom"/> or are a stack's closable root.
using System.Collections.Generic;
using MindAttic.Components.Unity; // MenuScreen, MenuStack, MaButton, MaFormRow, MaToggle, MaTextbox, MaText, MaRow
using MindAttic.Components.Unity.Core; // ButtonVariant
using Ricochet.Core.Art;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet
{
    /// <summary>Put on a menu stack's object to keep the room look off its screens.</summary>
    public sealed class GmlNoRoom : MonoBehaviour
    {
    }

    /// <summary>Marks a button the room draws as an arrow (the how-to-play slide's Last / Next).</summary>
    public sealed class GmlSlideArrow : MonoBehaviour
    {
        /// <summary>True for Last (spr_arrowleft), false for Next.</summary>
        public bool Last;
    }

    [DisallowMultipleComponent]
    public sealed class GmlRoomLayout : MonoBehaviour
    {
        /// <summary>The names of the objects it adds, for tests.</summary>
        public const string BackgroundName = "GML room background";
        public const string BackName = "GML back";

        /// <summary>The widest the flowing items are made.</summary>
        private const double Margin = 32;

        private MenuScreen _screen;
        private Image _background;
        private bool _taken;
        private bool _laid;
        private int _signature;
        private readonly RectState _panel = new RectState();
        private readonly RectState _header = new RectState();
        private readonly RectState _subtitle = new RectState();
        private readonly RectState _items = new RectState();

        /// <summary>True while the controls stand at the room's places (false: the kit's card is kept).</summary>
        public bool Taken => _taken;

        /// <summary>The background image.</summary>
        public Image Background => _background;

        /// <summary>True when the room look applies to <paramref name="screen"/>.</summary>
        public static bool Eligible(MenuScreen screen)
        {
            if (screen == null)
            {
                return false;
            }

            MenuStack stack = screen.Stack != null ? screen.Stack : screen.GetComponentInParent<MenuStack>(true);
            if (stack != null)
            {
                if (stack.GetComponent<GmlNoRoom>() != null)
                {
                    return false;
                }

                if (stack.AllowCloseRoot && stack.Root == screen)
                {
                    return false; // the kit's in-game menu and the endgame recap stay over the arena
                }
            }

            return true;
        }

        /// <summary>Gives <paramref name="screen"/> the room look; null when it is not eligible or the art is missing.</summary>
        public static GmlRoomLayout Attach(MenuScreen screen)
        {
            if (!Eligible(screen))
            {
                return null;
            }

            GmlRoomLayout existing = screen.GetComponent<GmlRoomLayout>();
            if (existing != null)
            {
                return existing;
            }

            Sprite sprite = GameArt.Background(MenuLooks.RoomBackground);
            if (sprite == null)
            {
                return null;
            }

            var layout = screen.gameObject.AddComponent<GmlRoomLayout>();
            layout._screen = screen;
            var rect = new GameObject(BackgroundName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = screen.gameObject.layer;
            rect.SetParent(screen.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            layout._background = rect.gameObject.AddComponent<Image>();
            layout._background.sprite = sprite;
            layout._background.raycastTarget = false;
            return layout;
        }

        private void LateUpdate()
        {
            if (_screen == null)
            {
                return;
            }

            int signature = Signature();
            if (_laid && signature == _signature)
            {
                return;
            }

            Layout();
            _laid = true;
            _signature = Signature();
        }

        private bool CanBack
        {
            get
            {
                MenuStack stack = _screen.Stack;
                return stack != null && stack.Current == _screen && stack.CanGoBack;
            }
        }

        private int Signature()
        {
            int h = 17;
            Transform items = _screen.Items;
            for (int i = 0; i < items.childCount; i++)
            {
                Transform child = items.GetChild(i);
                h = h * 31 + child.GetInstanceID();
                h = h * 31 + (child.gameObject.activeSelf ? 1 : 0);
                h = h * 31 + child.childCount;
                h = h * 31 + (child.GetComponent<GmlControlSkin>() != null ? 1 : 0);
                MaText text = child.GetComponent<MaText>();
                if (text != null && text.Tmp != null)
                {
                    h = h * 31 + (text.Tmp.text != null ? text.Tmp.text.GetHashCode() : 0);
                }
            }

            h = h * 31 + (CanBack ? 1 : 0);
            h = h * 31 + (_screen.Subtitle != null ? _screen.Subtitle.GetHashCode() : 0);
            h = h * 31 + (_screen.Title != null ? _screen.Title.GetHashCode() : 0);
            return h;
        }

        private enum Kind
        {
            Button,
            Row,
            Toggle,
            Text,
            Picture,
            Other,
            Arrow,
        }

        private sealed class Entry
        {
            public RectTransform Rect;
            public Kind Kind;
            public double Height;
            public double Width;
            public string Sprite;
            public MaText Text;
            public MaButton Button;
        }

        private void Layout()
        {
            // The how-to-play slide's Last / Next first: they become arrows of their own, out of their row.
            ExtractSlideArrows();

            bool canBack = CanBack;
            MaButton back = FindBack();
            if (canBack && back == null)
            {
                back = _screen.AddButton("Back", _screen.GoBack);
                back.name = BackName;
            }
            else if (!canBack && back != null)
            {
                Destroy(back.gameObject);
                back = null;
            }

            var entries = new List<Entry>();
            Transform items = _screen.Items;
            for (int i = 0; i < items.childCount; i++)
            {
                Transform child = items.GetChild(i);
                if (!child.gameObject.activeSelf || child.name == BackName)
                {
                    continue;
                }

                entries.Add(Describe((RectTransform)child));
            }

            // Pictures decide where the rest may go.
            Entry picture = entries.Find(e => e.Kind == Kind.Picture);
            bool slide = picture != null && picture.Sprite == MenuLooks.HowToPlaySlide;
            RoomRect pictureRect = picture != null ? PictureRect(picture.Sprite) : default;
            bool pictureCovers = picture != null && !slide && pictureRect.Left < 300;

            var column = new List<Entry>();
            var flow = new List<Entry>();
            Entry primary = null;
            Entry reset = null;
            bool buttonsOnly = entries.Count > 0 && entries.Count <= 10 && picture == null && entries.TrueForAll(e => e.Kind == Kind.Button);
            foreach (Entry entry in entries)
            {
                if (entry.Kind == Kind.Picture || entry.Kind == Kind.Arrow)
                {
                    continue;
                }

                if (buttonsOnly)
                {
                    column.Add(entry);
                }
                else if (entry.Kind == Kind.Button && entry.Button.LabelText != null && entry.Button.Label == Menus.ResetSetupLabel && reset == null)
                {
                    reset = entry;
                }
                else if (entry.Kind == Kind.Button && entry.Button.Variant == ButtonVariant.Primary && primary == null)
                {
                    primary = entry;
                }
                else
                {
                    flow.Add(entry);
                }
            }

            // How to play: counter above the slide, text under it; nothing flows.
            var slideTexts = new List<Entry>();
            Entry counter = null;
            if (slide)
            {
                foreach (Entry entry in flow)
                {
                    if (entry.Kind == Kind.Text)
                    {
                        if (counter == null && entry.Rect.GetSiblingIndex() < picture.Rect.GetSiblingIndex())
                        {
                            counter = entry;
                        }
                        else
                        {
                            slideTexts.Add(entry);
                        }
                    }
                }

                flow.RemoveAll(e => e == counter || slideTexts.Contains(e));
            }

            // The flow region: the room's players rows' area, left of a picture that does not cover the room.
            double top = RoomLayout.FlowTop;
            double bottom = reset != null ? RoomLayout.FlowBottom : 960;
            double left = 96;
            double right = 1824;
            if (picture != null && !slide && !pictureCovers)
            {
                left = Margin;
                right = pictureRect.Left - Margin;
            }

            double subtitleBottom = RoomLayout.Border + 72;
            FlowPlan plan = null;
            double[] widths = null;
            bool hide = pictureCovers;
            if (!hide && !buttonsOnly && flow.Count > 0)
            {
                for (int columns = 1; columns <= 4 && plan == null; columns++)
                {
                    double itemWidth = (right - left) / columns - Margin;
                    var heights = new List<double>();
                    foreach (Entry entry in flow)
                    {
                        Measure(entry, itemWidth);
                        heights.Add(entry.Height);
                    }

                    plan = RoomLayout.Flow(heights, left, System.Math.Max(top, subtitleBottom), right, bottom, columns);
                    widths = new[] { itemWidth };
                }
            }

            if (!hide && !buttonsOnly && flow.Count > 0 && plan == null)
            {
                KeepCard(back); // too long even for four columns
                return;
            }

            Take();

            // The title and the line under it.
            Transform header = _screen.Panel.Find("Header");
            if (header != null)
            {
                var rect = (RectTransform)header;
                float width = Mathf.Min(1400f, LayoutUtility.GetPreferredWidth(rect));
                float height = LayoutUtility.GetPreferredHeight(rect);
                PlaceTopLeft(rect, RoomLayout.Border, RoomLayout.Border, width, height);
                subtitleBottom = RoomLayout.Border + height;
            }

            MaText subtitle = SubtitleText();
            if (subtitle != null && subtitle.gameObject.activeSelf)
            {
                var rect = (RectTransform)subtitle.transform;
                rect.sizeDelta = new Vector2(1100f, rect.sizeDelta.y);
                float height = LayoutUtility.GetPreferredHeight(rect);
                PlaceTopLeft(rect, RoomLayout.Border, subtitleBottom + 8, 1100f, height);
                subtitleBottom += 8 + height;
            }

            if (back != null)
            {
                PlaceCentre((RectTransform)back.transform, RoomLayout.Back);
            }

            if (primary != null)
            {
                PlaceCentre(primary.Rect, new RoomRect(RoomLayout.Primary.X, RoomLayout.Primary.Y, ButtonWidth(primary), RoomLayout.ButtonHeight));
            }

            if (reset != null)
            {
                PlaceCentre(reset.Rect, new RoomRect(RoomLayout.Reset.X, RoomLayout.Reset.Y, ButtonWidth(reset), RoomLayout.ButtonHeight));
            }

            for (int i = 0; i < column.Count; i++)
            {
                RoomRect slot = RoomLayout.ButtonColumn(i, column.Count);
                PlaceCentre(column[i].Rect, new RoomRect(slot.X, slot.Y, ButtonWidth(column[i]), slot.Height));
            }

            foreach (Entry entry in entries)
            {
                if (entry.Kind == Kind.Picture)
                {
                    PlaceCentre(entry.Rect, PictureRect(entry.Sprite));
                }
                else if (entry.Kind == Kind.Arrow)
                {
                    bool last = entry.Rect.GetComponent<GmlSlideArrow>().Last;
                    PlaceCentre(entry.Rect, last ? RoomLayout.SlideLast : RoomLayout.SlideNext);
                }
            }

            if (slide)
            {
                if (counter != null)
                {
                    counter.Rect.sizeDelta = new Vector2(640f, counter.Rect.sizeDelta.y);
                    float height = LayoutUtility.GetPreferredHeight(counter.Rect);
                    PlaceTopLeft(counter.Rect, RoomLayout.Slide.Left, RoomLayout.Height / 2.0 - RoomLayout.SlideCounterRise - height / 2.0, 640f, height);
                }

                double y = RoomLayout.Height / 2.0 + RoomLayout.SlideTextDrop;
                foreach (Entry entry in slideTexts)
                {
                    entry.Rect.sizeDelta = new Vector2(1100f, entry.Rect.sizeDelta.y);
                    float height = LayoutUtility.GetPreferredHeight(entry.Rect);
                    PlaceTopLeft(entry.Rect, RoomLayout.Slide.Left, y, 1100f, height);
                    y += height + 8;
                }
            }

            for (int i = 0; plan != null && i < flow.Count; i++)
            {
                Entry entry = flow[i];
                FlowSlot slot = plan.Slots[i];
                double centre = plan.ColumnCentres[slot.Column];
                PlaceCentre(entry.Rect, new RoomRect(centre, slot.Top + entry.Height / 2.0, entry.Width, entry.Height));
            }

            // Lines a covering picture already shows are kept but not drawn.
            foreach (Entry entry in entries)
            {
                if (entry.Kind == Kind.Text || entry.Kind == Kind.Other)
                {
                    SetHidden(entry.Rect, hide && flow.Contains(entry));
                }
            }

            _ = widths;
            SizeParts(true);
        }

        private static double ButtonWidth(Entry entry)
        {
            float preferred = LayoutUtility.GetPreferredWidth(entry.Rect);
            return System.Math.Min(420.0, System.Math.Max(RoomLayout.ButtonWidth, preferred));
        }

        private void Measure(Entry entry, double itemWidth)
        {
            entry.Width = itemWidth;
            switch (entry.Kind)
            {
                case Kind.Button:
                    entry.Width = System.Math.Min(itemWidth, ButtonWidth(entry));
                    entry.Height = RoomLayout.ButtonHeight;
                    break;
                case Kind.Row:
                case Kind.Toggle:
                    entry.Height = RoomLayout.ButtonHeight;
                    break;
                default:
                    entry.Rect.sizeDelta = new Vector2((float)itemWidth, entry.Rect.sizeDelta.y);
                    entry.Height = System.Math.Max(0, LayoutUtility.GetPreferredHeight(entry.Rect));
                    if (entry.Kind == Kind.Other && entry.Rect.GetComponent<MaTextbox>() != null)
                    {
                        entry.Width = System.Math.Min(itemWidth, 460);
                    }

                    break;
            }
        }

        private Entry Describe(RectTransform rect)
        {
            var entry = new Entry { Rect = rect, Kind = Kind.Other };
            if (rect.name.StartsWith(Menus.PictureName))
            {
                entry.Kind = Kind.Picture;
                int colon = rect.name.IndexOf(": ");
                entry.Sprite = colon >= 0 ? rect.name.Substring(colon + 2) : string.Empty;
            }
            else if (rect.GetComponent<GmlSlideArrow>() != null)
            {
                entry.Kind = Kind.Arrow;
            }
            else if ((entry.Button = rect.GetComponent<MaButton>()) != null)
            {
                entry.Kind = Kind.Button;
            }
            else if (rect.GetComponent<MaFormRow>() != null)
            {
                entry.Kind = Kind.Row;
            }
            else if (rect.GetComponent<MaToggle>() != null)
            {
                entry.Kind = Kind.Toggle;
            }
            else if ((entry.Text = rect.GetComponent<MaText>()) != null)
            {
                entry.Kind = Kind.Text;
            }

            return entry;
        }

        private static RoomRect PictureRect(string sprite)
        {
            if (sprite == MenuLooks.KeyboardLayout)
            {
                return RoomLayout.Keyboard;
            }

            if (sprite == MenuLooks.CreditsImage)
            {
                return RoomLayout.Credits;
            }

            return RoomLayout.Slide;
        }

        // ---- the slide's arrows ------------------------------------------------------------------------------------------

        private void ExtractSlideArrows()
        {
            Transform items = _screen.Items;
            bool slide = false;
            for (int i = 0; i < items.childCount; i++)
            {
                if (items.GetChild(i).name == Menus.PictureName + ": " + MenuLooks.HowToPlaySlide)
                {
                    slide = true;
                }
            }

            if (!slide)
            {
                return;
            }

            for (int i = items.childCount - 1; i >= 0; i--)
            {
                Transform child = items.GetChild(i);
                MaRow row = child.GetComponent<MaRow>();
                if (row == null)
                {
                    continue;
                }

                MaButton[] buttons = row.GetComponentsInChildren<MaButton>(true);
                if (buttons.Length != 2)
                {
                    continue;
                }

                for (int b = 0; b < 2; b++)
                {
                    buttons[b].transform.SetParent(items, false);
                    GmlMenuSkin.MakeArrow(buttons[b], b == 0);
                }

                Destroy(row.gameObject);
                row.gameObject.SetActive(false);
                _screen.MarkNavigationDirty();
            }
        }

        // ---- the card ------------------------------------------------------------------------------------------------

        private MaButton FindBack()
        {
            Transform back = _screen.Items.Find(BackName);
            return back != null ? back.GetComponent<MaButton>() : null;
        }

        private MaText SubtitleText()
        {
            foreach (Transform child in _screen.Panel)
            {
                MaText text = child.GetComponent<MaText>();
                if (text != null)
                {
                    return text;
                }
            }

            return null;
        }

        private IEnumerable<Graphic> CardGraphics()
        {
            foreach (string name in new[] { "Shadow", "Fill", "Border" })
            {
                Transform child = _screen.Panel.Find(name);
                if (child != null && child.GetComponent<Graphic>() != null)
                {
                    yield return child.GetComponent<Graphic>();
                }
            }
        }

        private void KeepCard(MaButton back)
        {
            if (_taken)
            {
                _panel.Restore((RectTransform)_screen.Panel);
                _header.Restore((RectTransform)_screen.Panel.Find("Header"));
                _items.Restore(_screen.Items);
                MaText subtitle = SubtitleText();
                if (subtitle != null)
                {
                    _subtitle.Restore((RectTransform)subtitle.transform);
                }

                SetCardGroups(true);
                foreach (Graphic graphic in CardGraphics())
                {
                    graphic.canvasRenderer.SetAlpha(1f);
                }

                Transform rule = _screen.Panel.Find("Rule");
                if (rule != null)
                {
                    rule.gameObject.SetActive(true);
                }

                _taken = false;
            }

            if (back != null)
            {
                Destroy(back.gameObject);
            }

            _screen.ShowBackButton = true;
            SizeParts(false);
        }

        private void SetCardGroups(bool on)
        {
            foreach (LayoutGroup group in new[] { _screen.Panel.GetComponent<LayoutGroup>(), _screen.Items.GetComponent<LayoutGroup>() })
            {
                if (group != null)
                {
                    group.enabled = on;
                }
            }

            ContentSizeFitter fitter = _screen.Panel.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = on;
            }
        }

        private void Take()
        {
            if (_taken)
            {
                return;
            }

            _panel.Save((RectTransform)_screen.Panel);
            _header.Save((RectTransform)_screen.Panel.Find("Header"));
            _items.Save(_screen.Items);
            MaText subtitle = SubtitleText();
            if (subtitle != null)
            {
                _subtitle.Save((RectTransform)subtitle.transform);
            }

            SetCardGroups(false);
            _screen.ShowBackButton = false; // the room's own Back button (obj_btn_back_*) stands in the corner
            RectTransform panel = _screen.Panel;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            RectTransform items = _screen.Items;
            items.anchorMin = Vector2.zero;
            items.anchorMax = Vector2.one;
            items.offsetMin = items.offsetMax = Vector2.zero;
            foreach (Graphic graphic in CardGraphics())
            {
                graphic.canvasRenderer.SetAlpha(0f);
            }

            Transform rule = panel.Find("Rule");
            if (rule != null)
            {
                rule.gameObject.SetActive(false);
            }

            _taken = true;
        }

        // The arrows beside a row's slider are the sprite's own size in the room, half of it on the kit's card; a switch's
        // checkbox is 64 px and stands before its text.
        private void SizeParts(bool room)
        {
            foreach (Transform child in _screen.Items)
            {
                foreach (GmlStepper stepper in child.GetComponentsInChildren<GmlStepper>(true))
                {
                    LayoutElement size = stepper.GetComponent<LayoutElement>();
                    if (size != null)
                    {
                        Vector2 want = room ? new Vector2((float)RoomLayout.ArrowWidth, (float)RoomLayout.ArrowHeight) : GmlStepper.Size;
                        size.minWidth = size.preferredWidth = want.x;
                        size.minHeight = size.preferredHeight = want.y;
                    }
                }

                MaToggle toggle = child.GetComponent<MaToggle>();
                if (room && toggle != null && toggle.GetComponent<GmlControlSkin>() != null)
                {
                    Transform track = null;
                    foreach (Transform t in toggle.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "Track")
                        {
                            track = t;
                            break;
                        }
                    }

                    if (track != null)
                    {
                        LayoutElement size = track.GetComponent<LayoutElement>();
                        if (size != null)
                        {
                            size.minWidth = size.preferredWidth = size.minHeight = size.preferredHeight = (float)RoomLayout.CheckboxSize;
                        }

                        track.SetSiblingIndex(0); // obj_checkbox_*: the box, then its text
                    }
                }
            }
        }

        private static void SetHidden(RectTransform rect, bool hidden)
        {
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null && !hidden)
            {
                return;
            }

            if (group == null)
            {
                group = rect.gameObject.AddComponent<CanvasGroup>();
            }

            group.alpha = hidden ? 0f : 1f;
            group.blocksRaycasts = !hidden;
        }

        // ---- placing ---------------------------------------------------------------------------------------------------

        /// <summary>Puts <paramref name="rect"/> where <paramref name="room"/> says (room pixels, y down, from the top left).</summary>
        public static void PlaceCentre(RectTransform rect, RoomRect room)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2((float)room.Width, (float)room.Height);
            rect.anchoredPosition = new Vector2((float)room.X, -(float)room.Y);
        }

        private static void PlaceTopLeft(RectTransform rect, double left, double top, double width, double height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2((float)width, (float)height);
            rect.anchoredPosition = new Vector2((float)left, -(float)top);
        }

        private sealed class RectState
        {
            private Vector2 _min;
            private Vector2 _max;
            private Vector2 _pivot;
            private Vector2 _size;
            private Vector2 _position;

            public void Save(RectTransform rect)
            {
                if (rect == null)
                {
                    return;
                }

                _min = rect.anchorMin;
                _max = rect.anchorMax;
                _pivot = rect.pivot;
                _size = rect.sizeDelta;
                _position = rect.anchoredPosition;
            }

            public void Restore(RectTransform rect)
            {
                if (rect == null)
                {
                    return;
                }

                rect.anchorMin = _min;
                rect.anchorMax = _max;
                rect.pivot = _pivot;
                rect.sizeDelta = _size;
                rect.anchoredPosition = _position;
            }
        }
    }
}
