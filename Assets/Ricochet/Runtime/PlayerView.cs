// Player, what it looks like: the aim-assist beam, pickup and damage figures, world labels, the shot in flight as this peer sees it,
// and the aim readout and vortex hint MatchHud reads. (U.RICO-T39: split out of Player.cs, no behaviour change.) U.RICO-T48: the
// figures are obj_float_text's (FloatText), and a shot's result also floats its points and blows up whoever it knocked out.
using System.Collections.Generic;
using Ricochet.Core.Ai;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Art;
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
        /// <summary>Seconds an aim-assist beam takes to fade (obj_aimassist lost 0.003 of its alpha a frame at 60).</summary>
        public const float AssistSeconds = 5.5f;

        private int _seenAssists;        // every peer: the last assist whose beam it drew
        private int _adoptedAssists;     // owner: the last assist its local aim followed
        private float _assistShownUntil = -1f;
        private DottedBeam _assistBeam;
        private readonly List<Vector3> _beamPoints = new List<Vector3>();
        private int _seenPickups;
        private int _seenExtraBounces;
        private PlayerLabels _labels;           // the world labels (name, bar), UR-T38

        private ShotOutcome _pendingOutcome;  // a client: the server's result for the shot this peer is still flying

        // The player's own scale (its code-drawn square's size; the tank's art undoes it to draw at the original's pixels).
        private const float BodyScale = 0.8f;

        private TankView _tank;          // the original's turret, hull and tracks (U.RICO-T45); null without the art
        private SpriteRenderer _body;    // the code-drawn square and barrel when the art is missing
        private SpriteRenderer _barrel;
        private SpriteRenderer _turnMarker;
        private bool _turnArrowArt;

        /// <summary>The tank drawn from the original's art, or null when it fell back to squares.</summary>
        public TankView Tank => _tank;

        /// <summary>The aim this peer shows: the owner's own, responsive one, everyone else's synced one (draw_angle reads it).</summary>
        public float ShownHeading => LocallyControlled && IsSpawned ? (float)_aim.Heading : Heading;

        // Every peer: the tank as the synced state has it (the owner's turret follows its own, responsive aim).
        private void DrawTank()
        {
            float shownHeading = ShownHeading;

            // Out of the round (no health left, or waiting for the next one): drawn faint.
            Color32 color = Color;
            color.a = Turns.For(Session) != null && !InRound ? (byte)60 : (byte)255;
            if (_tank != null)
            {
                _tank.Show(shownHeading, TankMoving, color, LevelVersion, Time.deltaTime);
            }
            else
            {
                _barrel.transform.localRotation = Quaternion.Euler(0f, 0f, shownHeading);
                _barrel.transform.localPosition = Quaternion.Euler(0f, 0f, shownHeading) * new Vector3(0.45f, 0f, 0f);
                _body.color = color;
                _barrel.color = color;
            }

            _turnMarker.enabled = HasTurn;
            if (_turnArrowArt && _turnMarker.enabled)
            {
                // obj_turnarrow: spr_turnarrow's six frames at image_speed 0.2.
                Sprite frame = GameArt.FrameAt(GmlSprites.TurnArrow, Time.time, true, (float)HudLooks.TurnArrowImageSpeed);
                if (frame != null && _turnMarker.sprite != frame)
                {
                    _turnMarker.sprite = frame;
                }
            }
        }

        /// <summary>True while the turn marker over this tank is drawn (obj_turnarrow's spr_turnarrow when the art is there).</summary>
        public bool TurnMarkerShowing => _turnMarker != null && _turnMarker.enabled;

        /// <summary>True when the turn marker is the original's spr_turnarrow rather than the code-drawn diamond.</summary>
        public bool TurnMarkerFromArt => _turnArrowArt;

        private DottedBeam _previewBeam;
        private ShotView _shotView;
        private readonly List<Vector3> _assistPoints = new List<Vector3>();

        /// <summary>True while this peer is drawing an aim-assist beam for this player.</summary>
        public bool AssistShown => _assistShownUntil > Time.time;

        /// <summary>The owner's preview of the shot in draw_beam's dotted style (null on other peers).</summary>
        public DottedBeam PreviewBeam => _previewBeam;

        // Every peer: obj_aimassist's beam along the assisted path in draw_beam's dotted style, fading out (its alpha lost
        // 0.003 a step), gone once the shot is fired.
        private void DrawAssist()
        {
            if (_assistBeam == null)
            {
                return;
            }

            if (Assists != _seenAssists)
            {
                _seenAssists = Assists;
                _assistShownUntil = Time.time + AssistSeconds;
                ShotPath path = SolveVolley(Weapon.Cannon, AssistHeading, Arena.ArsenalRules.DefaultFuse, ExtraBounces).Main;
                _assistPoints.Clear();
                for (int i = 0; i < path.Points.Count; i++)
                {
                    _assistPoints.Add(Arena.ToUnity(path.Points[i]));
                }
            }

            if (!AssistShown)
            {
                _assistBeam.Clear();
                return;
            }

            _assistBeam.Set(_assistPoints, Mathf.Clamp01((_assistShownUntil - Time.time) / AssistSeconds));
        }

        // Every peer: draw a volley this player fired: its flight, the fire sound, and the aim-assist beam gone with it.
        private void Draw(int shot, Volley volley)
        {
            _assistShownUntil = -1f; // fire_weapon.gml: the aim-assist beam goes with the shot
            _pendingOutcome = null;
            LastVolley = volley;
            LastVolleyShot = shot;
            // The view draws it and GameAudio plays the fire sound (UR-T10), each from the feed.
            Presentation.Raise(this, new Cmd.ShotFired(PlayerId, Seat, shot, volley.Weapon, ShotHeading, ShotFuse, ShotExtraBounces, volley));
        }

        // Every peer, once its own flight of the shot lands (hit.gml, U.RICO-T48): "+N pts" flashing over the shooter, the damage
        // in red over each player it struck, and obj_playerexplosion on each it knocked out.
        private void ShowOutcome(ShotOutcome outcome)
        {
            if (outcome.Points > 0)
            {
                FloatText.Show(transform.position, HitLooks.PointsText(outcome.Points));
            }

            foreach (Player player in In(Session))
            {
                if (player.Seat < 0)
                {
                    continue;
                }

                int damage = outcome.DamageTo(player.Seat);
                if (damage > 0)
                {
                    FloatText.Show(player.transform.position, HitLooks.DamageText(damage), false, HitLooks.FloatQuickFadePerStep, FloatText.Of(HitLooks.Red));
                }

                foreach (int seat in outcome.KnockedOut)
                {
                    if (seat == player.Seat)
                    {
                        FieldEffects.KnockOut(player.transform.position);
                    }
                }
            }
        }

        // Every peer: add_random_weapon / item / health's text over whoever collected a crate (white, health in green).
        private void ShowPickup(int code)
        {
            bool health = code / 1000 == (int)DropKind.Health;
            FloatText.Show(transform.position, PickupText(code), false, HitLooks.FloatQuickFadePerStep, FloatText.Of(health ? HitLooks.Green : HitLooks.White));
        }

        /// <summary>
        /// The number of bounces this player's shot has made so far on this peer while it flies (draw_potential's combo
        /// grows with them), or -1 when nothing of theirs is in the air.
        /// </summary>
        public int BouncesInFlight => ShotInFlight && _shotView != null && _shotView.Flight != null && _shotView.Flight.Flights.Count > 0
            ? _shotView.Flight.Flights[0].Contacts
            : -1;

        /// <summary>Seconds this player's shot has been in the air on this peer (0 when nothing is).</summary>
        public double SecondsInFlight => ShotInFlight && _shotView != null && _shotView.Flight != null && _shotView.Flight.Flights.Count > 0 && LastVolley != null
            ? _shotView.Flight.Flights[0].Travelled / LastVolley.Speed
            : 0;

        // The name under the square, the pickup text, the damage figure and the small health bar are PlayerLabels (world
        // TextMeshPro, UR-T38); the owner's draw_bounceinfo line and the vortex hint are MatchHud's, read from here.
        // Labels live in a root of their own, so they follow the player's active state by hand.
        private void OnEnable()
        {
            _labels?.SetVisible(true);
        }

        private void OnDisable()
        {
            _labels?.SetVisible(false);
        }

        private void RefreshLabels()
        {
            if (!IsSpawned)
            {
                return; // a despawned player must not rebuild labels nothing would destroy
            }

            if (_labels == null)
            {
                _labels = PlayerLabels.Create(name, gameObject);
            }

            // In a match, a small health bar under the name (the roster's bars along the top are MatchHud's). The pickup text
            // and the damage figures float on their own (FloatText, U.RICO-T48).
            bool bar = Turns.For(Session) != null && Seat >= 0;
            _labels.Refresh(transform.position, DisplayName + (LocallyControlled ? " (you)" : Away ? " (reconnecting...)" : string.Empty),
                bar, Health / (float)Arena.MatchRules.MaxHealth, Color, InRound);
        }

        // Every peer: what this player's synced counts say happened since the last frame: a crate collected (its text) and an
        // extra bounce used (use_item.gml's "Bounce +1", flashing).
        private void ShowSyncedFloats()
        {
            if (PickupCount != _seenPickups)
            {
                _seenPickups = PickupCount;
                ShowPickup(LastPickup);
            }

            if (ExtraBounces > _seenExtraBounces)
            {
                FloatText.Show(transform.position, HitLooks.ExtraBounceText);
            }

            _seenExtraBounces = ExtraBounces;
        }

        /// <summary>The owner's draw_bounceinfo line (angle, "Bounces: n/max" and where the shot ends), or null for anyone else.</summary>
        public string AimReadout
        {
            get
            {
                if (!IsSpawned || !LocallyControlled)
                {
                    return null;
                }

                bool flying = ShotInFlight && _shotView != null && _shotView.Flight != null;
                ShotPath shown = flying ? _shotView.Flight.Volley.Main : _preview != null ? _preview.Main : null;
                int bounces = flying ? _shotView.Flight.Flights[0].Contacts : shown != null ? shown.Contacts.Count : 0;
                string end = shown == null ? string.Empty
                    : shown.End == ShotEndKind.Absorbed ? "  -> blocked"
                    : shown.End == ShotEndKind.Escaped ? "  -> out through the breach"
                    : _preview != null && _preview.HasBlast ? string.Empty
                    : shown.End == ShotEndKind.Target ? "  -> HIT"
                    : shown.End == ShotEndKind.Spent ? "  -> falls short" : string.Empty;
                return "Angle " + Mathf.RoundToInt((float)_aim.Heading) + "°   Bounces: "
                    + bounces + "/" + (Arena.Rules.MaxBounces + ExtraBounces) + end + "   (A/D aim, W/S drive, Space fire, F end turn)";
            }
        }

        /// <summary>draw_vortexdetonate.gml: how the owner sets the vortex off, or null when it does not apply.</summary>
        public string VortexHint
        {
            get
            {
                if (!IsSpawned || !LocallyControlled)
                {
                    return null;
                }

                if ((Loadout.Weapon == Weapon.Vortex && Turns.MayFire(Session, Seat)) || VortexInFlight)
                {
                    return VortexInFlight ? "Press [SPACE] to detonate the Vortex Device" : "After you fire, press [SPACE] to detonate the Vortex Device";
                }

                return null;
            }
        }

        /// <summary>
        /// draw_grenadefuse.gml: how the owner sets the grenade's fuse, while they hold the grenade on their turn and have
        /// not driven (the original's keys 1-6; here 1/2/3), or null when it does not apply (U.RICO-T47).
        /// </summary>
        public string GrenadeHint
        {
            get
            {
                if (!IsSpawned || !LocallyControlled || ShotInFlight || Moved)
                {
                    return null;
                }

                return Loadout.Weapon == Weapon.Grenade && Turns.MayFire(Session, Seat) ? "You can adjust the grenade fuse by pressing [1-3]" : null;
            }
        }

        /// <summary>
        /// The seconds left on this player's grenade in the air on this peer (draw_weaponinfo's fuseLeft counting down,
        /// grenade_countdown.gml), or -1 when no grenade of theirs is flying.
        /// </summary>
        public double FuseLeft => ShotInFlight && LastVolley != null && LastVolley.Weapon == Weapon.Grenade
            ? System.Math.Max(0, ShotFuse - SecondsInFlight)
            : -1;
    }
}
