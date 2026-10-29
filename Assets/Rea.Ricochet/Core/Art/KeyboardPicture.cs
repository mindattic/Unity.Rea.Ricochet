// U.RICO-T54, the controls room's picture redrawn for this game's controls. The original's obj_keyboardlayout drew
// spr_keyboardlayout (1814 x 972): a whole keyboard with the keys it used coloured, each colour led by a line to its name
// above or below the keyboard ("Go Back", "Move", "Cycle Weapons", "Fire Weapon" ...), and its obj_mouse drew spr_mouse.
// Here the same picture is drawn from the Input System's bindings as they are now (after the rebind screen), so a key
// moved there moves here too:
//   Keys      - every key of a full keyboard (the main block, the navigation block and the number pad) as the Input System
//               names it ("q", "leftCtrl", "numpad7"), with its cap's label and its place in the 1814 x 972 picture.
//   Draw      - the bindings (action, path) in; out come the coloured keys (a key goes to the first action bound to it),
//               one callout per action (the line from its leftmost key to its name, in lanes above or below the keyboard
//               that never put one name on another) and what each mouse button does.
//   Groups    - the three grenade fuses are one callout, as are the border's thinner and thicker; Esc (the menus' Back,
//               the kit's) and the number pad's quick angles (GameInput reads them directly) are drawn though no
//               binding names them.
// Engine-free (UR-LAW-1): Runtime's KeyboardPictureView draws it; Tests/Core.Tests/KeyboardPictureTests checks it.
using System;
using System.Collections.Generic;

namespace Ricochet.Core.Art
{
    /// <summary>A rectangle in a picture's pixels, y down.</summary>
    public readonly struct PictureRect
    {
        public PictureRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => X + Width;
        public double Bottom => Y + Height;
        public double CentreX => X + Width / 2.0;
        public double CentreY => Y + Height / 2.0;

        public bool Overlaps(PictureRect other) =>
            X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

        public bool Inside(double width, double height) => X >= 0 && Y >= 0 && Right <= width && Bottom <= height;

        public override string ToString() => "(" + X + ", " + Y + ", " + Width + " x " + Height + ")";
    }

    /// <summary>One key of the keyboard: its Input System control name, the label on its cap, and where it is.</summary>
    public sealed class KeyCap
    {
        internal KeyCap(string control, string label, PictureRect rect)
        {
            Control = control;
            Label = label;
            Rect = rect;
        }

        public string Control { get; }
        public string Label { get; }
        public PictureRect Rect { get; }

        public override string ToString() => Control;
    }

    /// <summary>A binding as the Input System has it now: the action's name and its control's effective path.</summary>
    public readonly struct BoundControl
    {
        public BoundControl(string action, string path)
        {
            Action = action;
            Path = path;
        }

        public string Action { get; }
        public string Path { get; }
    }

    /// <summary>A key coloured for the callout group <see cref="Group"/>.</summary>
    public sealed class KeyMark
    {
        internal KeyMark(KeyCap key, string group)
        {
            Key = key;
            Group = group;
        }

        public KeyCap Key { get; }
        public string Group { get; }
        public uint Colour => KeyboardPicture.Colour(Group);
    }

    /// <summary>
    /// One group's name and the line to it: down (or up) from its leftmost key's edge at <see cref="LineX"/> to its lane,
    /// then across to the name, which starts <see cref="KeyboardPicture.LabelGap"/> past the line (or ends that far before
    /// it, when <see cref="RightAligned"/>).
    /// </summary>
    public sealed class Callout
    {
        internal Callout(string group, KeyCap anchor, bool above)
        {
            Group = group;
            Anchor = anchor;
            Above = above;
        }

        public string Group { get; }
        public string Caption => KeyboardPicture.Caption(Group);
        public uint Colour => KeyboardPicture.Colour(Group);
        public KeyCap Anchor { get; }
        public bool Above { get; }
        public int Lane { get; internal set; }
        public bool RightAligned { get; internal set; }
        public double LineX => Anchor.Rect.CentreX;
        public double LineFromY => Above ? Anchor.Rect.Y : Anchor.Rect.Bottom;
        public double LaneY => KeyboardPicture.LaneY(Above, Lane);

        /// <summary>The name's box.</summary>
        public PictureRect Label
        {
            get
            {
                double width = KeyboardPicture.LabelWidth(Caption);
                double left = RightAligned ? LineX - KeyboardPicture.LabelGap - width : LineX + KeyboardPicture.LabelGap;
                return new PictureRect(left, LaneY - KeyboardPicture.LabelHeight / 2.0, width, KeyboardPicture.LabelHeight);
            }
        }
    }

    /// <summary>What a mouse button does, with its name's box beside the mouse.</summary>
    public sealed class MouseCaption
    {
        internal MouseCaption(string button, string group, PictureRect label)
        {
            Button = button;
            Group = group;
            Label = label;
        }

        /// <summary>The Input System's name for the button ("leftButton", "rightButton", "middleButton").</summary>
        public string Button { get; }
        public string Group { get; }
        public string Caption => KeyboardPicture.ButtonName(Button) + ": " + KeyboardPicture.Caption(Group);
        public uint Colour => KeyboardPicture.Colour(Group);
        public PictureRect Label { get; }
    }

    /// <summary>The picture for one set of bindings.</summary>
    public sealed class KeyboardDrawing
    {
        private readonly Dictionary<string, KeyMark> _marks;

        internal KeyboardDrawing(Dictionary<string, KeyMark> marks, List<KeyMark> ordered, List<Callout> callouts, List<MouseCaption> mouse,
            Dictionary<string, string> buttons)
        {
            _marks = marks;
            Marks = ordered;
            Callouts = callouts;
            MouseCaptions = mouse;
            Buttons = buttons;
        }

        /// <summary>The coloured keys, in the keyboard's order.</summary>
        public IReadOnlyList<KeyMark> Marks { get; }

        public IReadOnlyList<Callout> Callouts { get; }

        public IReadOnlyList<MouseCaption> MouseCaptions { get; }

        private Dictionary<string, string> Buttons { get; }

        /// <summary>The group key <paramref name="control"/> is coloured for, or null.</summary>
        public string GroupOf(string control) =>
            control != null && _marks.TryGetValue(control, out KeyMark mark) ? mark.Group : null;

        /// <summary>The group the mouse button <paramref name="button"/> ("leftButton" ...) does, or null.</summary>
        public string MouseGroupOf(string button) => button != null && Buttons.TryGetValue(button, out string group) ? group : null;
    }

    public static class KeyboardPicture
    {
        /// <summary>The picture's size: spr_keyboardlayout's.</summary>
        public const double Width = 1814;
        public const double Height = 972;

        /// <summary>A key's pitch, and its cap (the pitch less the gap between caps).</summary>
        public const double Pitch = 78;
        public const double Cap = 74;

        /// <summary>The keyboard's left edge, and the top of each of its six rows (the original's rows).</summary>
        public const double Left = 8;
        public static readonly double[] RowTops = { 262, 378, 466, 553, 641, 729 };

        /// <summary>A name's height, the gap between its line and it, and the room one character takes.</summary>
        public const double LabelHeight = 32;
        public const double LabelGap = 10;
        public const double CharWidth = 16;

        /// <summary>The lanes the names stand in: seven above the keyboard, three below (the first the farthest from it).</summary>
        public static readonly double[] LanesAbove = { 22, 56, 90, 124, 158, 192, 226 };
        public static readonly double[] LanesBelow = { 940, 896, 852 };

        /// <summary>The mouse (obj_mouse's spr_mouse, drawn large): its body and its two buttons, right of the number pad's foot.</summary>
        public static readonly PictureRect Mouse = new PictureRect(1640, 828, 92, 136);
        public static readonly PictureRect MouseLeft = new PictureRect(1640, 828, 46, 56);
        public static readonly PictureRect MouseRight = new PictureRect(1686, 828, 46, 56);

        /// <summary>The mouse's captions end here, left of the mouse.</summary>
        public const double MouseCaptionRight = 1626;

        /// <summary>The group Esc is drawn for: the menus' Back (the kit's, so no binding names it).</summary>
        public const string BackGroup = "Back";

        /// <summary>The group the number pad's 1 ... 9 are drawn for: the quick angles GameInput reads directly.</summary>
        public const string QuickAnglesGroup = "QuickAngles";

        private static readonly List<KeyCap> s_keys = BuildKeys();
        private static readonly Dictionary<string, KeyCap> s_byControl = Index(s_keys);

        // Each group's name and colour (0xRRGGBB), close to the original's where it had the control.
        private static readonly Dictionary<string, (string caption, uint colour)> s_groups = new Dictionary<string, (string, uint)>
        {
            [BackGroup] = ("Go back", 0x8C8C8C),
            ["Fullscreen"] = ("Fullscreen", 0xE6E6E6),
            ["Drive"] = ("Move", 0xF7780F),
            ["CycleWeapon"] = ("Cycle weapons", 0xA8E61D),
            ["Aim"] = ("Rotate", 0x4F6BED),
            ["CycleItem"] = ("Cycle items", 0xFFC20E),
            ["Fire"] = ("Fire weapon", 0xED1C24),
            ["UseItem"] = ("Use item", 0xE040C0),
            ["EndTurn"] = ("End turn", 0xFF9EC8),
            ["Ping"] = ("Ping players", 0x9F6BFF),
            ["Fuse"] = ("Grenade fuse", 0x22C7C7),
            ["Pause"] = ("Pause", 0x6FCFFF),
            ["Reroll"] = ("Reroll level", 0xC08040),
            ["Border"] = ("Border width", 0x22B14C),
            [QuickAnglesGroup] = ("Quick angles", 0xA0A0A0),
        };

        /// <summary>Every key, the main block's rows first, then the navigation block, then the number pad.</summary>
        public static IReadOnlyList<KeyCap> Keys => s_keys;

        /// <summary>The key the Input System calls <paramref name="control"/> ("q", "leftCtrl" ...), or null.</summary>
        public static KeyCap Key(string control) =>
            control != null && s_byControl.TryGetValue(control, out KeyCap key) ? key : null;

        /// <summary>The control a keyboard path names ("&lt;Keyboard&gt;/q" is "q"), or null for any other device.</summary>
        public static string KeyboardControl(string path) => ControlOf(path, "<Keyboard>/");

        /// <summary>The button a mouse path names ("&lt;Mouse&gt;/leftButton" is "leftButton"), or null.</summary>
        public static string MouseButton(string path) => ControlOf(path, "<Mouse>/");

        /// <summary>The callout group an action is drawn under: the fuses are one, the border's two are one.</summary>
        public static string Group(string action)
        {
            if (action == null)
            {
                return null;
            }

            if (action.StartsWith("Fuse", StringComparison.Ordinal))
            {
                return "Fuse";
            }

            return action.StartsWith("Border", StringComparison.Ordinal) ? "Border" : action;
        }

        /// <summary>A group's name in the picture (an unknown one's own name).</summary>
        public static string Caption(string group) => group != null && s_groups.TryGetValue(group, out var g) ? g.caption : group ?? string.Empty;

        /// <summary>A group's colour, 0xRRGGBB (white for an unknown one).</summary>
        public static uint Colour(string group) => group != null && s_groups.TryGetValue(group, out var g) ? g.colour : 0xFFFFFF;

        /// <summary>A mouse button's name in its caption.</summary>
        public static string ButtonName(string button)
        {
            switch (button)
            {
                case "leftButton": return "Left";
                case "rightButton": return "Right";
                case "middleButton": return "Middle";
                default: return button;
            }
        }

        /// <summary>The room a name of <paramref name="caption"/> takes.</summary>
        public static double LabelWidth(string caption) => (caption ?? string.Empty).Length * CharWidth + 8;

        /// <summary>The middle of lane <paramref name="lane"/> above or below the keyboard.</summary>
        public static double LaneY(bool above, int lane)
        {
            double[] lanes = above ? LanesAbove : LanesBelow;
            return lanes[Math.Max(0, Math.Min(lanes.Length - 1, lane))];
        }

        /// <summary>The picture for <paramref name="bindings"/> (gamepad and other devices' bindings are left out).</summary>
        public static KeyboardDrawing Draw(IEnumerable<BoundControl> bindings)
        {
            var marks = new Dictionary<string, KeyMark>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, string>(StringComparer.Ordinal);
            var all = new List<BoundControl>();
            if (bindings != null)
            {
                all.AddRange(bindings);
            }

            all.Add(new BoundControl(BackGroup, "<Keyboard>/escape"));
            for (int n = 1; n <= 9; n++)
            {
                all.Add(new BoundControl(QuickAnglesGroup, "<Keyboard>/numpad" + n));
            }

            foreach (BoundControl binding in all)
            {
                string group = Group(binding.Action);
                if (string.IsNullOrEmpty(group))
                {
                    continue;
                }

                KeyCap key = Key(KeyboardControl(binding.Path));
                if (key != null)
                {
                    if (!marks.ContainsKey(key.Control))
                    {
                        marks[key.Control] = new KeyMark(key, group);
                    }

                    continue;
                }

                string button = MouseButton(binding.Path);
                if (button != null && !buttons.ContainsKey(button))
                {
                    buttons[button] = group;
                }
            }

            var ordered = new List<KeyMark>();
            foreach (KeyCap key in s_keys)
            {
                if (marks.TryGetValue(key.Control, out KeyMark mark))
                {
                    ordered.Add(mark);
                }
            }

            return new KeyboardDrawing(marks, ordered, PlaceCallouts(ordered), MouseCaptions(buttons), buttons);
        }

        // One callout per group, from its leftmost key (the topmost of those): above the keyboard for a key in its top
        // four rows, below for one in its bottom two.
        private static List<Callout> PlaceCallouts(List<KeyMark> marks)
        {
            var anchors = new Dictionary<string, KeyCap>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (KeyMark mark in marks)
            {
                if (!anchors.TryGetValue(mark.Group, out KeyCap anchor))
                {
                    anchors[mark.Group] = mark.Key;
                    order.Add(mark.Group);
                }
                else if (mark.Key.Rect.X < anchor.Rect.X || mark.Key.Rect.X == anchor.Rect.X && mark.Key.Rect.Y < anchor.Rect.Y)
                {
                    anchors[mark.Group] = mark.Key;
                }
            }

            var callouts = new List<Callout>();
            foreach (string group in order)
            {
                KeyCap anchor = anchors[group];
                callouts.Add(new Callout(group, anchor, anchor.Rect.Y < RowTops[4]));
            }

            callouts.Sort((a, b) => a.LineX.CompareTo(b.LineX));
            Lay(callouts.FindAll(c => c.Above), LanesAbove.Length);
            Lay(callouts.FindAll(c => !c.Above), LanesBelow.Length);
            return callouts;
        }

        // Left to right, each name goes in the farthest lane where it is clear of the other names and no line runs through
        // it (nor its own line through another's): the staircase the original's names stood in.
        private static void Lay(List<Callout> band, int lanes)
        {
            var placed = new List<Callout>();
            foreach (Callout c in band)
            {
                c.RightAligned = c.LineX + LabelGap + LabelWidth(c.Caption) > Width;
                int best = -1;
                int bestCrossings = int.MaxValue;
                for (int lane = 0; lane < lanes; lane++)
                {
                    c.Lane = lane;
                    PictureRect label = c.Label;
                    bool clear = true;
                    int crossings = 0;
                    foreach (Callout other in placed)
                    {
                        PictureRect them = other.Label;
                        if (other.Lane == lane)
                        {
                            if (label.X < them.Right + LabelGap && them.X < label.Right + LabelGap)
                            {
                                clear = false;
                                break;
                            }
                        }
                        else if (other.Lane > lane)
                        {
                            // theirs is nearer the keyboard: my line runs down through their lane
                            if (c.LineX >= them.X && c.LineX <= them.Right)
                            {
                                crossings++;
                            }
                        }
                        else if (other.LineX >= label.X && other.LineX <= label.Right)
                        {
                            crossings++; // theirs is farther: their line runs through my lane
                        }
                    }

                    if (clear && crossings < bestCrossings)
                    {
                        best = lane;
                        bestCrossings = crossings;
                        if (crossings == 0)
                        {
                            break;
                        }
                    }
                }

                c.Lane = best >= 0 ? best : lanes - 1;
                placed.Add(c);
            }
        }

        private static List<MouseCaption> MouseCaptions(Dictionary<string, string> buttons)
        {
            var captions = new List<MouseCaption>();
            int line = 0;
            foreach (string button in new[] { "leftButton", "rightButton", "middleButton" })
            {
                if (!buttons.TryGetValue(button, out string group))
                {
                    continue;
                }

                string text = ButtonName(button) + ": " + Caption(group);
                double width = LabelWidth(text);
                double y = LanesBelow[LanesBelow.Length - 1 - line] - LabelHeight / 2.0;
                captions.Add(new MouseCaption(button, group, new PictureRect(MouseCaptionRight - width, y, width, LabelHeight)));
                line++;
            }

            return captions;
        }

        private static string ControlOf(string path, string device)
        {
            if (path == null || !path.StartsWith(device, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string control = path.Substring(device.Length);
            if (control.Length == 0)
            {
                return null;
            }

            foreach (KeyCap key in s_keys)
            {
                if (string.Equals(key.Control, control, StringComparison.OrdinalIgnoreCase))
                {
                    return key.Control;
                }
            }

            return control;
        }

        private static Dictionary<string, KeyCap> Index(List<KeyCap> keys)
        {
            var index = new Dictionary<string, KeyCap>(StringComparer.Ordinal);
            foreach (KeyCap key in keys)
            {
                index[key.Control] = key;
            }

            return index;
        }

        // ---- the keyboard ------------------------------------------------------------------------------------------------

        // A key: its control, its label, the column it starts at and how many keys wide it is, in pitches.
        private readonly struct K
        {
            public K(string control, string label, double units = 1)
            {
                Control = control;
                Label = label;
                Units = units;
            }

            public string Control { get; }
            public string Label { get; }
            public double Units { get; }
        }

        private static K Space(double units) => new K(null, null, units);

        private static List<KeyCap> BuildKeys()
        {
            var keys = new List<KeyCap>();

            // The main block: fifteen pitches wide, as the original's.
            Row(keys, 0, 0, new K("escape", "Esc"), Space(1), new K("f1", "F1"), new K("f2", "F2"), new K("f3", "F3"), new K("f4", "F4"), Space(0.5),
                new K("f5", "F5"), new K("f6", "F6"), new K("f7", "F7"), new K("f8", "F8"), Space(0.5),
                new K("f9", "F9"), new K("f10", "F10"), new K("f11", "F11"), new K("f12", "F12"));
            Row(keys, 1, 0, new K("backquote", "`"), new K("1", "1"), new K("2", "2"), new K("3", "3"), new K("4", "4"), new K("5", "5"),
                new K("6", "6"), new K("7", "7"), new K("8", "8"), new K("9", "9"), new K("0", "0"), new K("minus", "-"), new K("equals", "="),
                new K("backspace", "Backspace", 2));
            Row(keys, 2, 0, new K("tab", "Tab", 1.5), Letters("qwertyuiop"), new K("leftBracket", "["), new K("rightBracket", "]"),
                new K("backslash", "\\", 1.5));
            Row(keys, 3, 0, new K("capsLock", "Caps Lock", 1.75), Letters("asdfghjkl"), new K("semicolon", ";"), new K("quote", "'"),
                new K("enter", "Enter", 2.25));
            Row(keys, 4, 0, new K("leftShift", "Shift", 2.25), Letters("zxcvbnm"), new K("comma", ","), new K("period", "."), new K("slash", "/"),
                new K("rightShift", "Shift", 2.75));
            Row(keys, 5, 0, new K("leftCtrl", "Ctrl", 1.25), new K("leftMeta", "Win", 1.25), new K("leftAlt", "Alt", 1.25), new K("space", "Space", 6.25),
                new K("rightAlt", "Alt Gr", 1.25), new K("rightMeta", "Win", 1.25), new K("contextMenu", "Menu", 1.25), new K("rightCtrl", "Ctrl", 1.25));

            // The navigation block.
            const double nav = 15.5;
            Row(keys, 0, nav, new K("printScreen", "PrtSc"), new K("scrollLock", "ScrLk"), new K("pause", "Pause"));
            Row(keys, 1, nav, new K("insert", "Ins"), new K("home", "Home"), new K("pageUp", "PgUp"));
            Row(keys, 2, nav, new K("delete", "Del"), new K("end", "End"), new K("pageDown", "PgDn"));
            Row(keys, 4, nav, Space(1), new K("upArrow", "Up"));
            Row(keys, 5, nav, new K("leftArrow", "Left"), new K("downArrow", "Down"), new K("rightArrow", "Right"));

            // The number pad: + and Enter two rows tall.
            const double pad = nav + 3.5;
            Row(keys, 1, pad, new K("numLock", "Num"), new K("numpadDivide", "/"), new K("numpadMultiply", "*"), new K("numpadMinus", "-"));
            Row(keys, 2, pad, new K("numpad7", "7"), new K("numpad8", "8"), new K("numpad9", "9"));
            Tall(keys, 2, pad + 3, "numpadPlus", "+");
            Row(keys, 3, pad, new K("numpad4", "4"), new K("numpad5", "5"), new K("numpad6", "6"));
            Row(keys, 4, pad, new K("numpad1", "1"), new K("numpad2", "2"), new K("numpad3", "3"));
            Tall(keys, 4, pad + 3, "numpadEnter", "Enter");
            Row(keys, 5, pad, new K("numpad0", "0", 2), new K("numpadPeriod", "."));
            return keys;
        }

        private static K[] Letters(string letters)
        {
            var row = new K[letters.Length];
            for (int i = 0; i < letters.Length; i++)
            {
                row[i] = new K(letters[i].ToString(), letters[i].ToString().ToUpperInvariant());
            }

            return row;
        }

        private static void Row(List<KeyCap> keys, int row, double column, params object[] items)
        {
            foreach (object item in items)
            {
                if (item is K[] many)
                {
                    foreach (K k in many)
                    {
                        column = Add(keys, row, column, k);
                    }
                }
                else
                {
                    column = Add(keys, row, column, (K)item);
                }
            }
        }

        private static double Add(List<KeyCap> keys, int row, double column, K k)
        {
            if (k.Control != null)
            {
                keys.Add(new KeyCap(k.Control, k.Label, new PictureRect(Left + column * Pitch, RowTops[row], k.Units * Pitch - (Pitch - Cap), Cap)));
            }

            return column + k.Units;
        }

        private static void Tall(List<KeyCap> keys, int row, double column, string control, string label)
        {
            double top = RowTops[row];
            keys.Add(new KeyCap(control, label, new PictureRect(Left + column * Pitch, top, Cap, RowTops[row + 1] + Cap - top)));
        }
    }
}
