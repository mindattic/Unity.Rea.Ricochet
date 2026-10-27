// U.RICO-T45: the original's tank on every peer: spr_tank_top over spr_tank_bottom in the player's colour, the turret on
// the aim, the hull turning after it a degree a step while the tank drives, and spr_tracks prints at 0.7 behind it, laid
// only by the tank that drives (the original's global isMoving had every tank lay them).
using System.Collections;
using NUnit.Framework;
using Ricochet.Core.Art;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ricochet.Tests
{
    public class TankTests : NetSessionTest
    {
        public TankTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(GamePrefabs.Player);
            RegisterPrefab(GamePrefabs.Dummy);
            Client.Identity = new PlayerIdentity("tank-token", "Ann", new PlayerColor(200, 60, 220));
        }

        [UnityTest]
        public IEnumerator TheTankIsTheOriginalsArt_InTheSeatColour_TurretOnTheAim_HullAndTracksFollowingTheDrive()
        {
            yield return StartAndJoinAll();
            Arena.Serve(Server, GamePrefabs.Dummy);
            var annSpawn = WaitForSpawn<Player>(Client, owner: Client.LocalClientId);
            yield return annSpawn;
            var annOnHostSpawn = WaitForSpawn<Player>(Server, owner: Client.LocalClientId);
            yield return annOnHostSpawn;
            var hostSpawn = WaitForSpawn<Player>(Server, owner: Server.LocalClientId);
            yield return hostSpawn;
            Player ann = annSpawn.Result, annOnHost = annOnHostSpawn.Result, host = hostSpawn.Result;
            yield return WaitUntil(() => ann.Position != Vector2.zero, "Ann was never given her seat's spot");

            TankView tank = annOnHost.Tank;
            Assert.That(tank, Is.Not.Null, "the tank fell back to squares: the art catalogue lacks spr_tank_top / spr_tank_bottom");
            Assert.That(tank.Turret.sprite, Is.SameAs(GameArt.Frame(GmlSprites.TankTop)));
            Assert.That(tank.Hull.sprite, Is.SameAs(GameArt.Frame(GmlSprites.TankBottom)));
            yield return WaitUntil(() => ((Color32)tank.Turret.color).Equals(new Color32(200, 60, 220, 255)), () => "her turret is " + tank.Turret.color);
            Assert.That((Color32)tank.Hull.color, Is.EqualTo(new Color32(200, 60, 220, 255)), "the hull is tinted as the turret is");
            Assert.That(tank.TrackCount, Is.Zero, "a standing tank lays no tracks");

            // The turret follows her aim on another peer; the hull stays where it was until she drives.
            float hullBefore = tank.HullAngle;
            ann.Aim(30f);
            yield return WaitUntil(() => Mathf.Abs(Mathf.DeltaAngle(tank.TurretAngle, 30f)) < 0.01f, () => "her turret is at " + tank.TurretAngle);
            Assert.That(tank.HullAngle, Is.EqualTo(hullBefore), "the hull only turns while the tank drives");

            // Driving: the hull turns toward the turret a degree a step, and prints are laid behind at 0.7.
            float until = Time.time + 1f;
            while (Time.time < until)
            {
                ann.Drive(1);
                yield return new WaitForSeconds(0.1f);
            }

            ann.Drive(0);
            Assert.That(tank.TrackCount, Is.GreaterThanOrEqualTo(2), "driving a second laid " + tank.TrackCount + " prints");
            Assert.That(tank.NewestTrackAlpha, Is.EqualTo((float)TankMotion.TrackAlpha).Within(0.001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(tank.HullAngle, 30f)), Is.LessThan(Mathf.Abs(Mathf.DeltaAngle(hullBefore, 30f))),
                "the hull turned toward the turret (" + hullBefore + " -> " + tank.HullAngle + ")");
            Assert.That(ann.Tank.TrackCount, Is.GreaterThanOrEqualTo(2), "her own client draws her tracks too");
            Assert.That(host.Tank.TrackCount, Is.Zero, "a tank that did not drive laid no tracks");

            yield return WaitUntil(() => !annOnHost.TankMoving, "the tank never stopped");
            int laid = tank.TrackCount;
            yield return new WaitForSeconds(0.3f);
            Assert.That(tank.TrackCount, Is.EqualTo(laid), "a stopped tank lays no more prints");
        }
    }
}
