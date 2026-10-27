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
        private readonly AimState _aim = new AimState(StartHeading); // owner only: the local, responsive aim
        private float _aimSentAt = -1f;
        private float _sentHeading = float.NaN;
        private readonly EngineSound _engineSound = new EngineSound(); // owner only: the engine's sounds (U.RICO-T50)
        private double? _snapBackHeading;   // owner only: the heading before the last numpad snap (obj_game's previousAngle)

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

        /// <summary>Solves a cannon shot from this player's spot at <paramref name="heading"/>, as things stand.</summary>
        public ShotPath Solve(float heading)
        {
            return SolveVolley(Weapon.Cannon, heading, Arena.ArsenalRules.DefaultFuse, 0).Main;
        }

        /// <summary>Solves what <paramref name="weapon"/> would do from this player's spot, as things stand.</summary>
        public Volley SolveVolley(Weapon weapon, float heading, double fuse, int extraBounces)
        {
            return Arsenal.Fire(weapon, Arena.ToCore(Position), heading, fuse, extraBounces, ShotLevel, Arena.Targets(Session),
                Arena.Rules, Arena.ArsenalRules);
        }

        /// <summary>The level this player's shots fly in now: the hazards on it, less their own shield walls (UR-T6).</summary>
        public Level ShotLevel => Arena.LevelFor(Session, Seat);

        // Server: a new barrel direction. Never trusted as-is: a NaN heading would be garbage on every peer.
        private void OnAim(float heading) => Run(new Cmd.AimCommand(State.Id, heading), mirror: false);

        /// <summary>Server: turns the barrel for an AI seat (or a test), clamped like a client's Aim.</summary>
        public void ServerAim(float heading)
        {
            if (IsServer)
            {
                OnAim(heading);
            }
        }

        /// <summary>Server: fires for an AI seat (or a test), through the same checks as a client's Fire.</summary>
        public void ServerFire(float heading)
        {
            if (IsServer)
            {
                OnFire(heading);
            }
        }

        // Server: fire. One volley in the air per player, and in a match only on your turn and only with a weapon you have
        // (fire_weapon.gml); the aim is clamped like Aim's, then the authority solves the volley and the shot is drawn
        // and sent from its ShotFired event (Present).
        private void OnFire(float heading) => Run(new Cmd.FireCommand(State.Id, heading));

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

        // Server: moves this player's shot a frame; the frame it lands the authority strikes, scores and collects. The items
        // this player has in the air move too, and the frame one arrives it takes effect (U.RICO-T80).
        private void FlightStep()
        {
            Cmd.PlayerState state = State;
            if (Authority.ItemsInFlight(state.Id) && Authority.AdvanceItems(state.Id, Time.deltaTime).Count > 0)
            {
                SyncFromAuthority(); // a teleporter put its tank down, a shrink ray struck
                Arena.Current?.Mirror();
            }

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
                _aim.Set(AssistHeading);
            }

            // The numpad snaps the aim to an eight-way heading, and 5 puts it back (obj_game's numpad keys, UR-T29).
            bool snapped = false;
            double? snap = GameInput.AimSnap();
            if (snap.HasValue && Turns.MayFire(Session, Seat))
            {
                _snapBackHeading = _aim.Heading;
                _aim.Set(snap.Value);
                snapped = true;
            }
            else if (GameInput.AimSnapBack() && _snapBackHeading.HasValue && Turns.MayFire(Session, Seat))
            {
                _aim.Set(_snapBackHeading.Value);
                snapped = true;
            }

            if (snapped)
            {
                GameAudio.Play(SoundCue.Type); // obj_game's numpad keys
            }

            bool changed = snapped || _aim.Step(GameInput.Turn(), Time.deltaTime, Arena.Rules) || assisted;
            float heading = (float)_aim.Heading;
            if (heading != _sentHeading && Time.unscaledTime - _aimSentAt >= 0.05f)
            {
                _sentHeading = heading;
                _aimSentAt = Time.unscaledTime;
                Aim(heading);
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

            HearDriving(drive != 0, GameInput.Turn() != 0f && mayAct);

            if (GameInput.EndTurn() && mayFire && !ShotInFlight)
            {
                EndTurn();
            }

            int cycleWeapon = GameInput.CycleWeapon();
            if (cycleWeapon != 0 && mayAct)
            {
                GameAudio.Play(SoundCue.Flick); // cycle_weapon_left / _right.gml
                CycleWeapon(cycleWeapon);
            }

            int cycleItem = GameInput.CycleItem();
            if (cycleItem != 0 && mayAct)
            {
                GameAudio.Play(SoundCue.Flick); // cycle_item_left / _right.gml
                CycleItem(cycleItem);
            }

            int fuse = GameInput.Fuse();
            if (fuse != 0 && mayAct && Loadout.Weapon == Weapon.Grenade)
            {
                GameAudio.Play(SoundBook.Fuse(fuse)); // obj_game's keys 1-3: sfx_fuse1-3
                Fuse(fuse);
            }

            // use_item.gml: sfx_disabled with none of the item left, or once the shot is fired (every peer hears the item's
            // own sound when the server uses it).
            bool useItem = GameInput.UseItem();
            if (useItem && mayAct)
            {
                if (!Loadout.Has(Loadout.Item))
                {
                    GameAudio.Play(SoundCue.Disabled);
                }

                UseItem(heading);
            }
            else if (useItem && mayFire)
            {
                GameAudio.Play(SoundCue.Disabled);
            }

            bool fire = GameInput.Fire();
            if (fire && mayAct && !Moved)
            {
                if (!Loadout.Has(Loadout.Weapon))
                {
                    GameAudio.Play(SoundCue.Disabled); // fire_weapon.gml with none of the weapon left
                }

                Fire(heading);
            }
            else if (fire && VortexInFlight)
            {
                Detonate(); // vortex_detonate.gml: fire again to set it off
            }

            // The preview: the whole path of the selected weapon as it would fly now, hidden while the shot is in the air.
            Loadout loadout = Loadout;
            if (changed || _preview == null || Time.frameCount % 10 == 0 || _preview.Weapon != loadout.Weapon)
            {
                _preview = SolveVolley(loadout.Weapon, heading, loadout.Fuse, ExtraBounces);
            }

            if (_previewBeam != null)
            {
                if (ShotInFlight || !mayFire || Moved) // obj_guide: no guide once the tank has driven
                {
                    _previewBeam.Clear();
                }
                else
                {
                    // draw_beam's dotted line from the muzzle (obj_aimassist's Len = 40) along every leg of the path.
                    ShotPath main = _preview.Main;
                    _beamPoints.Clear();
                    for (int i = 0; i < main.Points.Count; i++)
                    {
                        _beamPoints.Add(Arena.ToUnity(main.Points[i]));
                    }

                    _previewBeam.Set(_beamPoints, 1f);
                }
            }
        }

        // The owner's tank as the original sounded it (U.RICO-T50): play_movement_sound.gml's sfx_engine_turnon then sfx_engine
        // while a drive key is held, stop_moving.gml's sfx_engine_shutoff on release; rotate_left / rotate_right.gml's sfx_turn,
        // again each time it ends, while the barrel turns and the tank stands (stopped on the key's release).
        private void HearDriving(bool driving, bool turning)
        {
            switch (_engineSound.Update(driving, GameAudio.IsPlaying(SoundCue.EngineTurnOn), GameAudio.IsPlaying(SoundCue.Engine)))
            {
                case EngineSound.Step.TurnOn:
                    GameAudio.PlayVoice(SoundCue.EngineTurnOn);
                    break;
                case EngineSound.Step.Run:
                    GameAudio.PlayVoice(SoundCue.Engine);
                    break;
                case EngineSound.Step.ShutOff:
                    GameAudio.Stop(SoundCue.Engine);
                    GameAudio.Play(SoundCue.EngineShutoff);
                    break;
            }

            if (turning && !_engineSound.Moving)
            {
                GameAudio.Hold(SoundCue.Turn, this);
            }
            else
            {
                GameAudio.Release(SoundCue.Turn, this);
            }
        }

        // This player's own vortex is in the air and may still be set off.
        private bool VortexInFlight
        {
            get { return ShotInFlight && LastVolley != null && LastVolley.Weapon == Weapon.Vortex && LastVolley.HasBlast && ShotDetonation <= 0f; }
        }
    }
}
