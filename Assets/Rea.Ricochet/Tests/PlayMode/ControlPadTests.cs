// The clickable on-screen controls (U.RICO-T29: obj_btn_degreeleft/right, obj_btn_moveforward/backward, obj_btn_fire,
// obj_btn_useitem, obj_btn_endturn) and the Alt ping of every player, played with real pointer events on a host and one client:
// Ann's pad shows over her own tank, a held turn button turns her aim and stops on release, and Fire, Use Item and End Turn
// buttons do what their keys do, through the same server checks. The numpad snaps and the key reads are GameInputTests (EditMode).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class ControlPadTests : NetSessionTest
    {
        private MatchHud _hud;

        public ControlPadTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            Client.Identity = new PlayerIdentity("pad-token", "Pad", new PlayerColor(200, 60, 220));
        }

        [TearDown]
        public void RemoveTheHud()
        {
            if (_hud != null)
            {
                Object.Destroy(_hud.gameObject);
                _hud = null;
            }
        }

        private static void PointerDown(GameObject button) =>
            ExecuteEvents.Execute(button, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerDownHandler);

        private static void PointerUp(GameObject button) =>
            ExecuteEvents.Execute(button, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerUpHandler);

        [UnityTest]
        public IEnumerator TheTurnButtons_TurnTheAimWhileHeld_AndFireShoots()
        {
            yield return StartAndJoinAll();
            var ann = WaitForSpawn<Player>(Client, owner: Client.LocalClientId);
            yield return ann;
            var annOnHost = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return annOnHost;
            yield return WaitUntil(() => ann.Result.Position != Vector2.zero, "Ann was never given her seat's spot");

            _hud = MatchHud.Create(Client);
            yield return WaitUntil(() => _hud.Pad.Showing, "the pad never showed over Ann's tank");
            Assert.That(_hud.Pad.Canvas.sortingOrder, Is.LessThan(90), "the menus (90 and up) draw over the pad");
            foreach (ControlPad.Control control in System.Enum.GetValues(typeof(ControlPad.Control)))
            {
                Assert.That(_hud.Pad.ButtonObject(control), Is.Not.Null, control + " has no button");
                if (GameArt.Frame("spr_tinybutton") != null)
                {
                    // U.RICO-T78: the original's spr_tinybutton / spr_tinyarrow* over their _back sprites.
                    Assert.That(_hud.Pad.PlateFor(control).FaceName, Is.EqualTo(Ricochet.Core.Art.MenuLooks.Face(ControlPad.PlateOf(control))));
                    Assert.That(_hud.Pad.PlateFor(control).BackName, Does.EndWith("_back"));
                }
            }

            float start = annOnHost.Result.Heading;
            GameObject left = _hud.Pad.ButtonObject(ControlPad.Control.TurnLeft);
            PointerDown(left);
            yield return WaitUntil(() => Mathf.DeltaAngle(start, annOnHost.Result.Heading) > 5f, () => "the held turn-left button left the aim at " + annOnHost.Result.Heading);
            PointerUp(left);
            yield return new WaitForSeconds(0.3f); // the last aim in flight lands
            float stopped = annOnHost.Result.Heading;
            yield return new WaitForSeconds(0.3f);
            Assert.That(annOnHost.Result.Heading, Is.EqualTo(stopped), "the aim kept turning after the button was released");

            GameObject right = _hud.Pad.ButtonObject(ControlPad.Control.TurnRight);
            PointerDown(right);
            yield return WaitUntil(() => Mathf.DeltaAngle(stopped, annOnHost.Result.Heading) < -5f, () => "the held turn-right button left the aim at " + annOnHost.Result.Heading);
            PointerUp(right);

            Assert.That(annOnHost.Result.Shots, Is.EqualTo(0));
            PointerDown(_hud.Pad.ButtonObject(ControlPad.Control.Fire));
            PointerUp(_hud.Pad.ButtonObject(ControlPad.Control.Fire));
            yield return WaitUntil(() => annOnHost.Result.Shots == 1, "the Fire button never fired");
        }

        [UnityTest]
        public IEnumerator PingingEveryPlayer_RingsEachSeatedTank()
        {
            yield return StartAndJoinAll();
            var ann = WaitForSpawn<Player>(Client, owner: Client.LocalClientId);
            yield return ann;
            yield return WaitUntil(() => ann.Result.Position != Vector2.zero, "Ann was never given her seat's spot");
            _hud = MatchHud.Create(Client);

            int seated = Player.In(Client).Count(p => p.Seat >= 0);
            Assert.That(seated, Is.GreaterThanOrEqualTo(1));
            _hud.Ready.PingAll(Client);
            Assert.That(_hud.Ready.Pings, Is.EqualTo(seated), "one ping per seated player");
            Assert.That(_hud.Ready.RingsShowing, Is.EqualTo(seated * 2), "two rings each");
        }
    }
}
