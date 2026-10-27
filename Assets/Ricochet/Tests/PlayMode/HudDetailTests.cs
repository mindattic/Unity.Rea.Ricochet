// U.RICO-T49: the HUD details in a match on a host and one client, read on Ann's client: the weapon and item icons show
// the current player's loadout as setup_weapons / setup_items did (the cannon selected and unlimited under spr_infinity, the
// rest carried with their counts), a click on one selects it through the server on her own turn, hovering says its name
// and what it does; each seat's slot has obj_healthbar's "shown/max" and the current one obj_turnarrow; the current tank
// has the turn arrow over it, obj_guide's ring and draw_angle's readout with its degree sign; the pad shows her angle.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
using Ricochet.Core.Match;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class HudDetailTests : NetSessionTest
    {
        private MatchHud _hud;
        private double _endTurnSeconds;

        public HudDetailTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            RegisterPrefab(GamePrefabs.Opponent);
            RegisterPrefab(GamePrefabs.Turns);
            RegisterPrefab(GamePrefabs.Crate);
            Client.Identity = new PlayerIdentity("icons-token", "Ann", new PlayerColor(230, 80, 60));
        }

        [SetUp]
        public void KeepTheRules()
        {
            _endTurnSeconds = Arena.MatchRules.EndTurnSeconds;
            TestMatches.WithoutReadyCard();
        }

        [TearDown]
        public void PutTheRulesBack()
        {
            Arena.MatchRules.EndTurnSeconds = _endTurnSeconds;
            TestMatches.StockReadyCard();
            if (_hud != null)
            {
                Object.Destroy(_hud.gameObject);
                _hud = null;
            }
        }

        private static void Send<T>(GameObject target, ExecuteEvents.EventFunction<T> handler) where T : IEventSystemHandler =>
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, handler);

        [UnityTest]
        public IEnumerator TheIconsShowTheCurrentLoadout_AClickSelects_TheHoverExplains_AndTheTankHasItsGuide()
        {
            Arena.MatchRules.EndTurnSeconds = 0.2;
            yield return StartAndJoinAll();
            Arena arena = Arena.Serve(Server, GamePrefabs.Dummy);
            var hostSpawn = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
            yield return hostSpawn;
            var annSpawn = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return annSpawn;
            Player host = hostSpawn.Result, ann = annSpawn.Result;
            yield return WaitUntil(() => Player.In(Client).Count() == 2, "the client never saw both players");

            _hud = MatchHud.Create(Client);
            Assert.That(arena.StartMatch(TestMatches.OnThePracticeRange()), Is.True);
            yield return WaitUntil(() => Turns.For(Client) != null && Turns.For(Client).MatchPhase == MatchPhase.Aiming, "the match never opened", 10f);
            Player hostOnClient = Player.In(Client).First(p => p.Seat == host.Seat);
            yield return WaitUntil(() => _hud.Icons.Subject == hostOnClient && hostOnClient.Weapons != 0, "the icons never showed the host's loadout", 5f);

            // setup_weapons / setup_items: the cannon and extra bounce in hand, everything else carried.
            yield return null;
            Assert.That(_hud.Icons.Canvas.sortingOrder, Is.LessThan(90), "the menus (90 and up) draw over the icons");
            Assert.That(_hud.Icons.SpriteOf(Weapon.Cannon), Is.EqualTo("spr_icon_weapon_cannon_selected"));
            Assert.That(_hud.Icons.SpriteOf(Weapon.Laser), Is.EqualTo("spr_icon_weapon_laser"));
            Assert.That(_hud.Icons.SpriteOf(Item.ExtraBounce), Is.EqualTo("spr_item_extrabounce_selected"));
            Assert.That(_hud.Icons.InfinityOn(Weapon.Cannon), Is.True, "an unlimited cannon draws spr_infinity");
            Assert.That(_hud.Icons.CountOf(Weapon.Grenade), Is.EqualTo("3"));
            Assert.That(_hud.Icons.CountOf(Item.Teleporter), Is.EqualTo("3"));
            Assert.That(_hud.Icons.AlphaOf(Weapon.Laser), Is.EqualTo(1f));

            // The slots: obj_healthbar's text, obj_turnarrow beside the current seat only.
            int hostSlot = host.Seat, annSlot = ann.Seat; // seats 0 and 1 are slots 0 and 1
            Assert.That(_hud.HealthTextOf(hostSlot), Is.EqualTo(Arena.MatchRules.MaxHealth + "/" + Arena.MatchRules.MaxHealth));
            Assert.That(_hud.BarFromArtAt(hostSlot), Is.True, "the bar is spr_healthbar's");
            Assert.That(_hud.ArrowShowingAt(hostSlot), Is.True);
            Assert.That(_hud.ArrowShowingAt(annSlot), Is.False);

            // The current tank: the turn arrow over it, obj_guide's ring, the angle with its degree sign.
            Assert.That(hostOnClient.TurnMarkerShowing && hostOnClient.TurnMarkerFromArt, Is.True, "spr_turnarrow over the host's tank");
            Assert.That(_hud.Guide.RingShowing, Is.True);
            Assert.That(_hud.Guide.AngleText, Is.EqualTo(HudLooks.AngleText(hostOnClient.ShownHeading)));
            Assert.That(_hud.Guide.DegreeShowing, Is.True);
            Assert.That(_hud.Guide.ExtraBounceShowing, Is.False);

            // A click on someone else's turn does nothing.
            Send(_hud.Icons.IconObject((int)Weapon.Grenade), ExecuteEvents.pointerDownHandler);
            yield return new WaitForSeconds(0.3f);
            Assert.That(host.Loadout.Weapon, Is.EqualTo(Weapon.Cannon), "Ann's click changed the host's weapon");

            // Ann's turn: her icons, and a click on the grenade selects it on the server.
            host.ServerFire(0f);
            Player annOnClient = Player.In(Client).First(p => p.IsLocalHuman);
            yield return WaitUntil(() => Turns.For(Client).Current == ann.Seat && Turns.For(Client).MatchPhase == MatchPhase.Aiming && _hud.Icons.Subject == annOnClient,
                "the turn never passed to Ann", 10f);
            Assert.That(_hud.Pad.AngleText, Is.EqualTo(HudLooks.AngleText(annOnClient.ShownHeading)), "obj_degree_gui's readout at the turn buttons");
            Assert.That(_hud.Pad.DegreeShowing, Is.True);

            Send(_hud.Icons.IconObject((int)Weapon.Grenade), ExecuteEvents.pointerDownHandler);
            yield return WaitUntil(() => ann.Loadout.Weapon == Weapon.Grenade, "the click on the grenade never selected it");
            yield return WaitUntil(() => _hud.Icons.SpriteOf(Weapon.Grenade) == "spr_icon_weapon_grenade_selected", "the grenade's icon never showed it selected");
            Assert.That(_hud.Icons.SpriteOf(Weapon.Cannon), Is.EqualTo("spr_icon_weapon_cannon"));

            Send(_hud.Icons.IconObject(ArsenalRules.Kinds + (int)Item.ShieldWall), ExecuteEvents.pointerDownHandler);
            yield return WaitUntil(() => ann.Loadout.Item == Item.ShieldWall, "the click on the shield wall never selected it");

            // Hovering says what it is and does.
            Send(_hud.Icons.IconObject((int)Weapon.Railgun), ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(_hud.Icons.TooltipText, Is.EqualTo(ArsenalRules.Name(Weapon.Railgun) + ": " + ArsenalRules.About(Weapon.Railgun)));
            Send(_hud.Icons.IconObject((int)Weapon.Railgun), ExecuteEvents.pointerExitHandler);
            yield return null;
            Assert.That(_hud.Icons.TooltipText, Is.Empty);
        }
    }
}
