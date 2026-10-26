// Player, aim and fire: the owner's local aim and input, the preview, the server-side Aim/Fire/Drive/EndTurn/Reroll commands,
// the drive and flight steps, and the shot solving. (U.RICO-T39: split out of Player.cs, no behaviour change.)
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
        private readonly AimState _aim = new AimState(StartHeading, StartPower); // owner only: the local, responsive aim
        private float _aimSentAt = -1f;
        private float _sentHeading = float.NaN;
        private float _sentPower = float.NaN;

        private Volley _preview;            // owner only: the shot as it would fly right now

        /// <summary>This player is the seat on turn and has driven this turn, so it cannot shoot (draw_movewarning.gml).</summary>
        public bool Moved
        {
            get
            {
                Turns turns = Turns.For(Session);
                return turns != null && turns.MatchPhase == MatchPhase.Aiming && turns.Current == Seat && turns.HasMoved;
            }
        }

        /// <summary>True while this player's last shot is still in the air on this peer.</summary>
        public bool ShotInFlight
        {
            get { return IsServer ? State.InFlight : _shotView != null && _shotView.Flying; }
        }

        /// <summary>Server: how far the shot in the air (its first projectile) has flown from the muzzle; 0 with none.</summary>
        public double ShotTravelled
        {
            get { return IsServer && State.Flight != null && State.Flight.Flights.Count > 0 ? State.Flight.Flights[0].Travelled : 0; }
        }

        /// <summary>The main path of the shot this peer last saw fired (null before the first).</summary>
        public ShotPath LastShot => LastVolley != null ? LastVolley.Main : null;

        /// <summary>The whole volley this peer last saw fired (null before the first).</summary>
        public Volley LastVolley { get; private set; }

        /// <summary>The shot number <see cref="LastVolley"/> is (the server's <see cref="Shots"/> count when it was fired).</summary>
        public int LastVolleyShot { get; private set; }

        /// <summary>What this player's last landed shot did in a match, as the server sent it (null before the first).</summary>
        public ShotOutcome LastOutcome { get; private set; }

        /// <summary>The shot number <see cref="LastOutcome"/> is about.</summary>
        public int LastOutcomeShot { get; private set; }

        /// <summary>The owner's current preview: what would fly if they fired now (null on other peers).</summary>
        public Volley Preview => _preview;

        /// <summary>Solves a cannon shot from this player's spot at <paramref name="heading"/> and <paramref name="power"/>, as things stand.</summary>
        public ShotPath Solve(float heading, float power)
        {
            return SolveVolley(Weapon.Cannon, heading, power, Arena.ArsenalRules.DefaultFuse, 0).Main;
        }

        /// <summary>Solves what <paramref name="weapon"/> would do from this player's spot, as things stand.</summary>
        public Volley SolveVolley(Weapon weapon, float heading, float power, double fuse, int extraBounces)
        {
            return Arsenal.Fire(weapon, Arena.ToCore(Position), heading, power, fuse, extraBounces, ShotLevel, Arena.Targets(Session),
                Arena.Rules, Arena.ArsenalRules);
        }

        /// <summary>The level this player's shots fly in now: the hazards on it, less their own shield walls (UR-T6).</summary>
        public Level ShotLevel => Arena.LevelFor(Session, Seat);

        // Server: a new barrel direction. Never trusted as-is: NaN or an out-of-range power would be garbage on every peer.
        private void OnAim(float heading, float power)
        {
            int id = State.Id;
            Run(new Cmd.AimCommand(id, heading), mirror: false);
            Run(new Cmd.SetPowerCommand(id, power), mirror: false);
        }

        /// <summary>Server: turns the barrel for an AI seat (or a test), clamped like a client's Aim.</summary>
        public void ServerAim(float heading, float power)
        {
            if (IsServer)
            {
                OnAim(heading, power);
            }
        }

        /// <summary>Server: fires for an AI seat (or a test), through the same checks as a client's Fire.</summary>
        public void ServerFire(float heading, float power)
        {
            if (IsServer)
            {
                OnFire(heading, power);
            }
        }

        // Server: fire. One volley in the air per player, and in a match only on your turn and only with a weapon you have
        // (fire_weapon.gml); the aim is clamped like Aim's, then the authority solves the volley and the shot is drawn
        // and sent from its ShotFired event (Present).
        private void OnFire(float heading, float power) => Run(new Cmd.FireCommand(State.Id, heading, power));

        // Server: R / PageUp / PageDown in practice: a new random level, everyone to fresh spots. Ignored in a match.
        private void OnReroll() => Run(new Cmd.RerollCommand(State.Id));

        // Server: vortex_detonate.gml. Only this player's own vortex, only while it is still in the air, and only once: the
        // authority cuts the volley short where it is now and lands it at once; ShotCut tells every client.
        private void OnDetonate() => Run(new Cmd.DetonateCommand(State.Id));

        // Server: the owner holds a drive key. Only on their own turn with nothing of theirs in the air, never for NaN; the
        // tank then moves in Update until the key stops arriving.
        private void OnDrive(int direction) => Run(new Cmd.DriveCommand(State.Id, direction), mirror: false);

        // Server: F / End Turn. Only on the seat's own turn, before firing.
        private void OnEndTurn() => Run(new Cmd.EndTurnCommand(State.Id));

        // Server: drives the tank a frame along its barrel while a drive key is held.
        private void DriveStep()
        {
            Cmd.PlayerState state = State;
            if (state.DriveDirection == 0)
            {
                return;
            }

            Authority.AdvanceDrive(state.Id, Time.deltaTime);
            SyncFromAuthority();
        }

        // Server: moves this player's shot a frame; the frame it lands the authority strikes, scores and collects.
        private void FlightStep()
        {
            Cmd.PlayerState state = State;
            if (!state.InFlight)
            {
                return;
            }

            Authority.AdvanceFlight(state.Id, Time.deltaTime);
            if (!state.InFlight) // it landed: the dummies are down, the players hurt, the crates collected, the turn over
            {
                SyncFromAuthority();
                Arena.Current?.Mirror();
            }
        }

        private void OwnerInput()
        {
            if (Turns.PausedIn(Session))
            {
                return; // paused (UR-T43): no turning, charging, driving, firing or ending the turn until it resumes
            }

            if (GameInput.Reroll())
            {
                Reroll(); // R / PageUp / PageDown: the server only obeys it in practice
            }

            // An aim assist the server just applied: the local aim follows it.
            bool assisted = Assists != _adoptedAssists;
            if (assisted)
            {
                _adoptedAssists = Assists;
                _aim.Set(AssistHeading, AssistPower);
            }

            bool changed = _aim.Step(GameInput.Turn(), GameInput.Charge(), Time.deltaTime, Arena.Rules) || assisted;
            float heading = (float)_aim.Heading;
            float power = (float)_aim.Power;
            if ((heading != _sentHeading || power != _sentPower) && Time.unscaledTime - _aimSentAt >= 0.05f)
            {
                _sentHeading = heading;
                _sentPower = power;
                _aimSentAt = Time.unscaledTime;
                Aim(heading, power);
            }

            bool mayFire = Turns.MayFire(Session, Seat);
            bool mayAct = mayFire && !ShotInFlight;
            // W / S drive (obj_game's keyboard events): a held key is re-sent every tenth of a second, a released one once.
            int drive = mayAct ? GameInput.Drive() : 0;
            if (drive != 0 && Time.unscaledTime - _driveSentAt >= 0.1f)
            {
                _driveSentAt = Time.unscaledTime;
                Drive(drive);
            }
            else if (drive == 0 && _driveSentAt > 0f)
            {
                _driveSentAt = 0f;
                Drive(0); // stop_moving.gml on key release
            }

            if (GameInput.EndTurn() && mayFire && !ShotInFlight)
            {
                EndTurn();
            }

            int cycleWeapon = GameInput.CycleWeapon();
            if (cycleWeapon != 0 && mayAct)
            {
                CycleWeapon(cycleWeapon);
            }

            int cycleItem = GameInput.CycleItem();
            if (cycleItem != 0 && mayAct)
            {
                CycleItem(cycleItem);
            }

            int fuse = GameInput.Fuse();
            if (fuse != 0 && mayAct && Loadout.Weapon == Weapon.Grenade)
            {
                Fuse(fuse);
            }

            if (GameInput.UseItem() && mayAct)
            {
                UseItem(heading, power);
            }

            if (GameInput.Fire() && mayAct && !Moved)
            {
                Fire(heading, power);
            }
            else if (GameInput.Fire() && VortexInFlight)
            {
                Detonate(); // vortex_detonate.gml: fire again to set it off
            }

            // The preview: the whole path of the selected weapon as it would fly now, hidden while the shot is in the air.
            Loadout loadout = Loadout;
            if (changed || _preview == null || Time.frameCount % 10 == 0 || _preview.Weapon != loadout.Weapon)
            {
                _preview = SolveVolley(loadout.Weapon, heading, power, loadout.Fuse, ExtraBounces);
            }

            if (_previewLine != null)
            {
                if (ShotInFlight || !mayFire || Moved) // obj_guide: no guide once the tank has driven
                {
                    _previewLine.positionCount = 0;
                }
                else
                {
                    ShotPath main = _preview.Main;
                    _previewLine.positionCount = main.Points.Count + 1;
                    _previewLine.SetPosition(0, Position);
                    for (int i = 0; i < main.Points.Count; i++)
                    {
                        _previewLine.SetPosition(i + 1, Arena.ToUnity(main.Points[i]));
                    }
                }
            }
        }

        // This player's own vortex is in the air and may still be set off.
        private bool VortexInFlight
        {
            get { return ShotInFlight && LastVolley != null && LastVolley.Weapon == Weapon.Vortex && LastVolley.HasBlast && ShotDetonation <= 0f; }
        }
    }
}
