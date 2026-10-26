// Player, what it looks like: the aim-assist beam, pickup and damage figures, world labels, the shot in flight as this peer sees it,
// and the aim readout and vortex hint MatchHud reads. (U.RICO-T39: split out of Player.cs, no behaviour change.)
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
        private const float PickupSeconds = 2.5f;

        /// <summary>Seconds an aim-assist beam takes to fade (obj_aimassist lost 0.003 of its alpha a frame at 60).</summary>
        public const float AssistSeconds = 5.5f;

        private int _seenAssists;        // every peer: the last assist whose beam it drew
        private int _adoptedAssists;     // owner: the last assist its local aim followed
        private float _assistShownUntil = -1f;
        private LineRenderer _assistLine;
        private int _seenPickups;
        private float _pickupShownUntil = -1f;
        private PlayerLabels _labels;           // the world labels (name, bar, floating figures), UR-T38

        private const float HurtSeconds = 1.6f;
        private ShotOutcome _pendingOutcome;  // a client: the server's result for the shot this peer is still flying
        private int _hurt;                    // every peer: the damage the last shot that struck this player did
        private float _hurtShownUntil = -1f;

        private SpriteRenderer _body;
        private SpriteRenderer _barrel;
        private SpriteRenderer _turnMarker;

        private LineRenderer _previewLine;
        private ShotView _shotView;

        /// <summary>True while this peer is drawing an aim-assist beam for this player.</summary>
        public bool AssistShown => _assistShownUntil > Time.time;

        // Every peer: obj_aimassist's beam along the assisted path, fading out, gone once the shot is fired.
        private void DrawAssist()
        {
            if (_assistLine == null)
            {
                return;
            }

            if (Assists != _seenAssists)
            {
                _seenAssists = Assists;
                _assistShownUntil = Time.time + AssistSeconds;
                ShotPath path = SolveVolley(Weapon.Cannon, AssistHeading, AssistPower, Arena.ArsenalRules.DefaultFuse, ExtraBounces).Main;
                _assistLine.positionCount = path.Points.Count + 1;
                _assistLine.SetPosition(0, Position);
                for (int i = 0; i < path.Points.Count; i++)
                {
                    _assistLine.SetPosition(i + 1, Arena.ToUnity(path.Points[i]));
                }
            }

            if (!AssistShown)
            {
                _assistLine.positionCount = 0;
                return;
            }

            float alpha = Mathf.Clamp01((_assistShownUntil - Time.time) / AssistSeconds);
            var color = new UnityEngine.Color(0.55f, 0.95f, 1f, alpha);
            _assistLine.startColor = color;
            _assistLine.endColor = color;
        }

        // Every peer: draw a volley this player fired: its flight, the fire sound, and the aim-assist beam gone with it.
        private void Draw(int shot, Volley volley)
        {
            _assistShownUntil = -1f; // fire_weapon.gml: the aim-assist beam goes with the shot
            _pendingOutcome = null;
            LastVolley = volley;
            LastVolleyShot = shot;
            // The view draws it and GameAudio plays the fire sound (UR-T10), each from the feed.
            Presentation.Raise(this, new Cmd.ShotFired(PlayerId, Seat, shot, volley.Weapon, ShotHeading, ShotPower, ShotFuse, ShotExtraBounces, volley));
        }

        // Every peer: each player the shot struck floats the damage it took.
        private void ShowOutcome(ShotOutcome outcome)
        {
            foreach (Player player in In(Session))
            {
                int damage = player.Seat >= 0 ? outcome.DamageTo(player.Seat) : 0;
                if (damage > 0)
                {
                    player.ShowHurt(damage);
                }
            }
        }

        private void ShowHurt(int damage)
        {
            _hurt = damage;
            _hurtShownUntil = Time.time + HurtSeconds;
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

            string pickup = null;
            float pickupProgress = 0f;
            if (Time.time < _pickupShownUntil)
            {
                // show_float_text: what the last crate gave, rising over the player for a moment.
                pickup = PickupText(LastPickup);
                pickupProgress = 1f - (_pickupShownUntil - Time.time) / PickupSeconds;
            }

            // The server's result for a shot that struck this player (UR-T12): the damage it took, rising and fading.
            string hurt = null;
            float hurtLeft = 0f;
            if (Time.time < _hurtShownUntil)
            {
                hurt = "-" + _hurt;
                hurtLeft = (_hurtShownUntil - Time.time) / HurtSeconds;
            }

            // In a match, a small health bar under the name (the roster's bars along the top are MatchHud's).
            bool bar = Turns.For(Session) != null && Seat >= 0;
            _labels.Refresh(transform.position, DisplayName + (LocallyControlled ? " (you)" : Away ? " (reconnecting...)" : string.Empty),
                pickup, pickupProgress, hurt, hurtLeft, bar, Health / (float)Arena.MatchRules.MaxHealth, Color, InRound);
        }

        /// <summary>The owner's draw_bounceinfo line (angle, power, "Bounces: n/max" and where the shot ends), or null for anyone else.</summary>
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
                return "Angle " + Mathf.RoundToInt((float)_aim.Heading) + "°   Power " + Mathf.RoundToInt((float)_aim.Power * 100f) + "%   Bounces: "
                    + bounces + "/" + (Arena.Rules.MaxBounces + ExtraBounces) + end + "   (A/D aim, Up/Down power, W/S drive, Space fire, F end turn)";
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
    }
}
