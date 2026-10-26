// Player, weapons and items: the loadout (the authority's, or the synced copy on a peer), its mirror to the synced ints, the
// weapon/item/fuse commands and the pickup text. (U.RICO-T39: split out of Player.cs, no behaviour change.)
using System.Collections.Generic;
using Ricochet.Core.Ai;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Ricochet.Core.Net;
using Unity.Multiplayer;      // NetEntity, NetPlayer, NetRoster, NetBots
using Unity.Multiplayer.Core; // [ToServer], [Synced]
using UnityEngine;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet
{
    public partial class Player : NetEntity
    {
        private Loadout _shownLoadout;            // every other peer: the synced one, unpacked
        private int _shownWeapons = -1;
        private int _shownItems = -1;

        /// <summary>
        /// What this player carries: on the server the real loadout; on every other peer the synced copy (read-only in
        /// spirit: changing it changes nothing but this peer's view).
        /// </summary>
        public Loadout Loadout
        {
            get
            {
                if (IsServer)
                {
                    return State.Loadout;
                }

                if (_shownLoadout == null || Weapons != _shownWeapons || Items != _shownItems)
                {
                    _shownLoadout = _shownLoadout ?? new Loadout(Arena.ArsenalRules);
                    if (Weapons == 0)
                    {
                        _shownLoadout.Reset();
                    }
                    else
                    {
                        _shownLoadout.UnpackFrom(Weapons, Items);
                    }

                    _shownWeapons = Weapons;
                    _shownItems = Items;
                }

                return _shownLoadout;
            }
        }

        /// <summary>Server: copies the loadout out to the synced ints every peer reads.</summary>
        public void MirrorLoadout()
        {
            if (!IsServer)
            {
                return;
            }

            Loadout loadout = State.Loadout;
            int weapons = loadout.PackWeapons();
            int items = loadout.PackItems();
            if (Weapons != weapons)
            {
                Weapons = weapons;
            }

            if (Items != items)
            {
                Items = items;
            }
        }

        /// <summary>
        /// add_random_weapon / item / health's floating text for a pickup code: "Grenade x 1 collected",
        /// "Teleporter x 1 collected", "+7".
        /// </summary>
        public static string PickupText(int code)
        {
            var kind = (DropKind)Mathf.Clamp(code / 1000, 0, DropRules.KindCount - 1);
            int value = code % 1000;
            switch (kind)
            {
                case DropKind.Health:
                    return "+" + value;
                case DropKind.Item:
                    return ArsenalRules.Name((Item)Mathf.Clamp(value, 0, ArsenalRules.Kinds - 1)) + " x 1 collected";
                default:
                    return ArsenalRules.Name((Weapon)Mathf.Clamp(value, 0, ArsenalRules.Kinds - 1)) + " x 1 collected";
            }
        }

        /// <summary>
        /// Server: cycles right until <paramref name="weapon"/> is selected, for an AI seat (or a test). False when the
        /// player has none of it (or may not cycle now); the selection is then wherever the cycling stopped.
        /// </summary>
        public bool ServerSelectWeapon(Weapon weapon)
        {
            if (!IsServer)
            {
                return false;
            }

            for (int i = 0; i < ArsenalRules.Kinds && State.Loadout.Weapon != weapon; i++)
            {
                OnCycleWeapon(1);
            }

            return State.Loadout.Weapon == weapon;
        }

        /// <summary>Server: cycles the weapon for an AI seat (or a test), through the same checks as a client's.</summary>
        public void ServerCycleWeapon(int direction)
        {
            if (IsServer)
            {
                OnCycleWeapon(direction);
            }
        }

        /// <summary>Server: sets off this player's vortex for a test, through the same checks as a client's.</summary>
        public void ServerDetonate()
        {
            if (IsServer)
            {
                OnDetonate();
            }
        }

        /// <summary>Server: uses the selected item for an AI seat (or a test), through the same checks as a client's.</summary>
        public void ServerUseItem(float heading, float power)
        {
            if (IsServer)
            {
                OnUseItem(heading, power);
            }
        }

        private void OnCycleWeapon(int direction) => Run(new Cmd.CycleWeaponCommand(State.Id, direction), mirror: false);

        private void OnCycleItem(int direction) => Run(new Cmd.CycleItemCommand(State.Id, direction), mirror: false);

        private void OnFuse(int change) => Run(new Cmd.FuseCommand(State.Id, change), mirror: false);

        // Server: use_item.gml. The selected item, if carried and in play, before this turn's shot.
        private void OnUseItem(float heading, float power) => Run(new Cmd.UseItemCommand(State.Id, heading, power), mirror: false);
    }
}
