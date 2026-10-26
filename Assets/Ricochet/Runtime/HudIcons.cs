// The weapon and item icons along the bottom left (U.RICO-T49): obj_icon_weapon_cannon..vortex on the upper row and
// obj_item_extrabounce..redirector on the lower, at the original's pixels on the HUD canvas's 1920x1080 reference (the
// original room). Each shows the current player's count as setup_weapons / setup_items did: its _off sprite at a fifth
// with none, its plain sprite carried, its _selected sprite for the one in hand, and over it the count in fnt_count
// outlined 2 px, or spr_infinity for an unlimited one (obj_infinity). In practice they show the local player's own. A
// click selects that weapon or item for the local player on their own turn (obj_icon_weapon_*'s Left Pressed), through the
// server's checks like a key's; hovering one says its name and what it does (the rebuild's addition, in
// mouseover_message_draw's white fnt_score, from arsenal.json's about). Read from the synced state only. A canvas of its own
// that takes the pointer (the match HUD's never does), sorting with the HUD under the announcer cards and every menu.
using MindAttic.Components.Unity; // MaCanvas, MaText, TextStyle
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using TMPro;
using Unity.Multiplayer;          // NetSession
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ricochet
{
    public sealed class HudIcons : MonoBehaviour
    {
        private const int InfinitySize = 11;

        /// <summary>One icon: reports its click and hover to the bar.</summary>
        private sealed class Icon : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public HudIcons Bar;
            public int Index;      // 0-5 the weapons, 6-11 the items
            public Image Face;
            public MaText Count;
            public Image Infinity;
            public string Sprite;

            public void OnPointerDown(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    Bar.Click(Index);
                }
            }

            public void OnPointerEnter(PointerEventData eventData) => Bar._hovered = Index;

            public void OnPointerExit(PointerEventData eventData)
            {
                if (Bar._hovered == Index)
                {
                    Bar._hovered = -1;
                }
            }

            private void OnDisable()
            {
                if (Bar != null && Bar._hovered == Index)
                {
                    Bar._hovered = -1;
                }
            }
        }

        private readonly Icon[] _icons = new Icon[ArsenalRules.Kinds * 2];
        private MatchHud _hud;
        private Canvas _canvas;
        private MaText _tooltip;
        private Player _subject;
        private int _hovered = -1;

        /// <summary>Adds the icons to <paramref name="hud"/>'s GameObject.</summary>
        public static HudIcons Create(MatchHud hud)
        {
            var icons = hud.gameObject.AddComponent<HudIcons>();
            icons._hud = hud;
            return icons;
        }

        /// <summary>True while the icons are on screen.</summary>
        public bool Showing => _canvas != null && _canvas.gameObject.activeSelf;

        /// <summary>The icons' canvas (null until they first show).</summary>
        public Canvas Canvas => _canvas;

        /// <summary>Whose loadout the icons show now (null when hidden).</summary>
        public Player Subject => Showing ? _subject : null;

        /// <summary>The GML sprite <paramref name="weapon"/>'s icon is meant to show now ("" before the icons are built).</summary>
        public string SpriteOf(Weapon weapon) => _icons[(int)weapon] != null ? _icons[(int)weapon].Sprite : string.Empty;

        /// <summary>The GML sprite <paramref name="item"/>'s icon is meant to show now.</summary>
        public string SpriteOf(Item item) => _icons[ArsenalRules.Kinds + (int)item] != null ? _icons[ArsenalRules.Kinds + (int)item].Sprite : string.Empty;

        /// <summary>The figure over <paramref name="weapon"/>'s icon ("" for none or an unlimited one).</summary>
        public string CountOf(Weapon weapon) => Text(_icons[(int)weapon]);

        /// <summary>The figure over <paramref name="item"/>'s icon.</summary>
        public string CountOf(Item item) => Text(_icons[ArsenalRules.Kinds + (int)item]);

        /// <summary>True while spr_infinity stands over <paramref name="weapon"/>'s icon.</summary>
        public bool InfinityOn(Weapon weapon) => _icons[(int)weapon] != null && _icons[(int)weapon].Infinity.gameObject.activeSelf;

        /// <summary>The icon's alpha now (0.2 with none carried).</summary>
        public float AlphaOf(Weapon weapon) => _icons[(int)weapon] != null ? _icons[(int)weapon].Face.color.a : 0f;

        /// <summary>The button object of an icon (0-5 the weapons, 6-11 the items), for tests that click or hover it.</summary>
        public GameObject IconObject(int index) => _icons[index] != null ? _icons[index].gameObject : null;

        /// <summary>The hover text showing now ("" when none).</summary>
        public string TooltipText => _tooltip != null && _tooltip.gameObject.activeSelf ? _tooltip.Tmp.text : string.Empty;

        private static string Text(Icon icon) => icon != null && icon.Count.gameObject.activeSelf ? icon.Count.Tmp.text : string.Empty;

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }

        private void Update()
        {
            NetSession session = _hud != null ? _hud.CurrentSession : null;
            _subject = session != null && session.IsLive ? FindSubject(session) : null;
            if (_subject != null && _canvas == null)
            {
                Build();
            }

            if (_canvas == null)
            {
                return;
            }

            if (_canvas.gameObject.activeSelf != (_subject != null))
            {
                _canvas.gameObject.SetActive(_subject != null);
            }

            if (_subject == null)
            {
                return;
            }

            Loadout loadout = _subject.Loadout;
            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                var weapon = (Weapon)i;
                var item = (Item)i;
                int weapons = loadout.Count(weapon);
                int items = loadout.Count(item);
                Show(_icons[i], HudLooks.WeaponIcon(weapon, HudLooks.State(weapons, loadout.Weapon == weapon)), weapons);
                Show(_icons[ArsenalRules.Kinds + i], HudLooks.ItemIcon(item, HudLooks.State(items, loadout.Item == item)), items);
            }

            string tip = string.Empty;
            if (_hovered >= 0)
            {
                tip = _hovered < ArsenalRules.Kinds
                    ? HudLooks.Tooltip(ArsenalRules.Name((Weapon)_hovered), ArsenalRules.About((Weapon)_hovered))
                    : HudLooks.Tooltip(ArsenalRules.Name((Item)(_hovered - ArsenalRules.Kinds)), ArsenalRules.About((Item)(_hovered - ArsenalRules.Kinds)));
            }

            HudParts.Show(_tooltip, tip.Length > 0);
            if (tip.Length > 0)
            {
                HudParts.Set(_tooltip, tip);
            }
        }

        // The current player in a match (the original showed player[current]'s), or the local player in practice.
        private static Player FindSubject(NetSession session)
        {
            Turns turns = Turns.For(session);
            foreach (Player player in Player.In(session))
            {
                if (turns == null ? player.IsLocalHuman
                    : turns.MatchPhase != MatchPhase.RoundOver && turns.MatchPhase != MatchPhase.GameOver && turns.MatchPhase != MatchPhase.Practice
                      && player.Seat >= 0 && player.Seat == turns.Current)
                {
                    return player;
                }
            }

            return null;
        }

        private static void Show(Icon icon, string sprite, int count)
        {
            icon.Sprite = sprite;
            Sprite art = GameArt.Frame(sprite);
            if (art != null && icon.Face.sprite != art)
            {
                icon.Face.sprite = art;
            }

            Color face = art != null ? Color.white : new Color(0.3f, 0.3f, 0.3f); // a grey square when the art is missing
            face.a = (float)HudLooks.Alpha(count);
            icon.Face.color = face;

            bool infinity = HudLooks.ShowsInfinity(count) && GameArt.Frame(GmlSprites.Infinity) != null;
            string figure = infinity ? string.Empty : HudLooks.ShowsInfinity(count) ? ArsenalRules.CountLabel(count) : HudLooks.CountText(count);
            HudParts.Show(icon.Infinity, infinity);
            HudParts.Show(icon.Count, figure.Length > 0);
            if (figure.Length > 0)
            {
                HudParts.Set(icon.Count, figure);
            }
        }

        // obj_icon_weapon_* / obj_item_*'s Left Pressed, for the local player on their own turn; the server checks it again.
        private void Click(int index)
        {
            Player player = _subject;
            if (player == null || !player.IsLocalHuman || !Turns.MayFire(_hud.CurrentSession, player.Seat) || player.ShotInFlight)
            {
                return;
            }

            // U.RICO-T50: sfx_flick for one the player carries, sfx_disabled for one they have none of.
            Loadout loadout = player.Loadout;
            bool carried = index < ArsenalRules.Kinds ? loadout.Count((Weapon)index) > 0 : loadout.Count((Item)(index - ArsenalRules.Kinds)) > 0;
            GameAudio.Play(carried ? SoundCue.Flick : SoundCue.Disabled);
            if (!carried)
            {
                return;
            }

            if (index < ArsenalRules.Kinds)
            {
                player.SelectWeapon(index);
            }
            else
            {
                player.SelectItem(index - ArsenalRules.Kinds);
            }
        }

        private void Build()
        {
            _canvas = MaCanvas.Create("HUD icons canvas", MatchHud.SortingOrder);
            _canvas.transform.SetParent(transform, false);

            for (int i = 0; i < ArsenalRules.Kinds; i++)
            {
                _icons[i] = Add(i, HudLooks.IconX(i), HudLooks.WeaponRowY, "Weapon " + (Weapon)i);
                _icons[ArsenalRules.Kinds + i] = Add(ArsenalRules.Kinds + i, HudLooks.IconX(i), HudLooks.ItemRowY, "Item " + (Item)i);
            }

            // The hover text, over the icons (the original's mouseover line sat at room_height - 30, where the rebuild's aim
            // line runs), never taking the pointer itself.
            _tooltip = HudParts.Label(_canvas.transform, TextStyle.Body, Color.white, 10f, GameArt.CanvasFontSize(GmlFonts.Score, 24f),
                TextAlignmentOptions.BottomLeft, gmlFont: GmlFonts.Score, outline: 1);
            RectTransform tip = _tooltip.RectTransform;
            tip.anchorMin = tip.anchorMax = tip.pivot = Vector2.zero;
            tip.sizeDelta = new Vector2(1100f, 32f);
            tip.anchoredPosition = new Vector2(HudLooks.Border, HudLooks.RoomHeight - HudLooks.WeaponRowY + 6f);
            _tooltip.gameObject.SetActive(false);
        }

        // An icon at (x, y) in the original's pixels (top left origin, y down): placed from the canvas's bottom left.
        private Icon Add(int index, int x, int y, string name)
        {
            float bottom = HudLooks.RoomHeight - y - HudLooks.IconSize;
            RectTransform rect = HudParts.Rect(name, _canvas.transform, Vector2.zero, new Vector2(HudLooks.IconSize, HudLooks.IconSize), new Vector2(x, bottom));
            Image face = rect.gameObject.AddComponent<Image>();
            face.raycastTarget = true;
            Icon icon = rect.gameObject.AddComponent<Icon>();
            icon.Bar = this;
            icon.Index = index;
            icon.Face = face;

            // The count: fnt_count outlined 2 px with its top left at x + sprite_width - 4, y + 15.
            icon.Count = HudParts.Label(rect, TextStyle.Body, Color.white, 8f, GameArt.CanvasFontSize(GmlFonts.Count, 18f), TextAlignmentOptions.TopLeft,
                gmlFont: GmlFonts.Count, outline: HudLooks.CountOutline);
            RectTransform count = icon.Count.RectTransform;
            count.anchorMin = count.anchorMax = count.pivot = new Vector2(0f, 1f);
            count.sizeDelta = new Vector2(30f, 30f);
            count.anchoredPosition = new Vector2(HudLooks.CountOffsetX, -HudLooks.CountOffsetY);
            icon.Count.Tmp.overflowMode = TextOverflowModes.Overflow;

            // spr_infinity (11x11, origin at its centre) at x + sprite_width - 4, y + 24.
            icon.Infinity = HudParts.Solid("Infinity", rect, Color.white);
            icon.Infinity.sprite = GameArt.Frame(GmlSprites.Infinity);
            RectTransform sign = icon.Infinity.rectTransform;
            sign.anchorMin = sign.anchorMax = new Vector2(0f, 1f);
            sign.pivot = new Vector2(0.5f, 0.5f);
            sign.sizeDelta = new Vector2(InfinitySize, InfinitySize);
            sign.anchoredPosition = new Vector2(HudLooks.CountOffsetX, -HudLooks.InfinityOffsetY);
            icon.Infinity.gameObject.SetActive(false);
            icon.Count.gameObject.SetActive(false);
            return icon;
        }
    }
}
