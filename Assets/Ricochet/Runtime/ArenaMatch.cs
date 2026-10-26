// Arena, the match: starting it, seats (arrivals, drops, reconnects), the clock and the AI seats' turns, the votes, the announcer and the
// mirror of the authority's state out to every peer. (U.RICO-T39: split out of Arena.cs, no behaviour change.)
using System;
using System.Collections.Generic;
using Ricochet.Core.Ai;
using Ricochet.Core.Aim;
using Ricochet.Core.Arsenal;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Ricochet.Core.Menu;
using Ricochet.Core.Net;
using Unity.Multiplayer;      // NetSession, NetKey
using Unity.Multiplayer.Core; // VoteKind
using UnityEngine;
using Cmd = Ricochet.Core.Commands;

namespace Ricochet
{
    public sealed partial class Arena
    {
        /// <summary>Starts a match with <paramref name="aiCount"/> AI opponents and the default skill and aim assists.</summary>
        public bool StartMatch(int aiCount) => StartMatch(new MatchSetup { AiCount = aiCount });

        /// <summary>
        /// Starts a match (server only): every connected player takes a seat in join order, then the setup's AI opponents
        /// fill the next seats, up to sixteen in all; everyone starts each round with the setup's aim assists; the
        /// practice dummies are cleared away and seat 0 takes the first turn. A match already running starts over.
        /// </summary>
        /// <returns>False (and nothing changes) when fewer than two seats would play.</returns>
        public bool StartMatch(MatchSetup setup)
        {
            setup = setup != null ? setup.Clone() : new MatchSetup();
            int aiCount = setup.AiCount;
            var humans = new List<Player>();
            foreach (Player player in Player.In(_session))
            {
                if (!player.IsAi)
                {
                    humans.Add(player);
                }
            }

            humans.Sort((a, b) => a.KitSeat.CompareTo(b.KitSeat));
            // The lobby's rule (UR-T11): the people first, then as many of the AI as still fit; two seats at least.
            if (!Lobby.CanStart(humans.Count, aiCount))
            {
                return false;
            }

            int seated = Lobby.HumanSeats(humans.Count);
            aiCount = Lobby.AiSeats(humans.Count, aiCount);

            RemoveAi();
            if (_dummiesUp)
            {
                RemoveDummies();
            }

            setup.AiCount = aiCount;
            _setup = setup;
            _setup.ApplyTo(ArsenalRules);
            _aiTurn = null;
            _authority.BeginMatch(_setup.Map, _setup.Goal); // the map room's choice; a random map's level is rolled as round one starts
            _humanSeats.Clear();
            for (int i = 0; i < humans.Count; i++)
            {
                Player human = humans[i];
                int seat = i < seated ? i : -1; // a seventeenth human watches
                human.AssignSeat(seat);
                if (seat >= 0)
                {
                    JoinSeat(human, seat, false);
                    _humanSeats[human.KitSeat] = seat;
                    human.Place(SpawnPoint(seat));
                    if (human.Away)
                    {
                        _authority.Apply(new Cmd.DropSeatCommand(human.PlayerId, HoldLeft(human.KitSeat))); // dropped before the match: their seat waits for them too
                    }
                }
            }

            GameObject opponentPrefab = OpponentPrefab();
            for (int i = 0; i < aiCount; i++)
            {
                int seat = seated + i;
                GameObject spawned = _session.Objects.Spawn(opponentPrefab, AiKeyPrefix + (i + 1), SpawnPoint(seat));
                Player ai = spawned.GetComponent<Player>();
                ai.AssignSeat(seat);
                ai.Place(SpawnPoint(seat));
                JoinSeat(ai, seat, true);
            }

            _turns = Turns.For(_session);
            if (_turns == null)
            {
                _turns = _session.Objects.Spawn(TurnsPrefab(), Turns.Key).GetComponent<Turns>();
            }

            _turns.Arena = this;
            _turns.MirrorMap(MapIndex);
            _announcer = new Announcer(_setup.AnnouncerVoice, _setup.Announcements, _announcerDice);
            _turns.MirrorVoice(_announcer.Voice);
            _authority.StartMatch();
            Mirror();
            Say(_announcer.Welcome()); // obj_game's create event: random_announcement_welcome
            return true;
        }

        // Seats a player through the authority (AssignSeat has registered them already); a join it refuses is said, not hidden.
        private void JoinSeat(Player player, int seat, bool isAi)
        {
            if (_authority.Apply(new Cmd.JoinSeatCommand(player.PlayerId, seat, isAi, player.DisplayName)).Count == 0)
            {
                Debug.LogWarning("[Ricochet] " + player.DisplayName + " was not seated in " + seat + ": the authority refused the join.");
            }
        }

        /// <summary>Server: may <paramref name="player"/> fire now? Always in practice; in a match, only on their turn.</summary>
        public bool MayFire(Player player)
        {
            if (_match == null || !_match.IsRunning)
            {
                return true;
            }

            return player != null && player.Seat >= 0 && _match.MayFire(player.Seat);
        }

        // The announcer cheers a knock-out (random_announcement_win) or heckles a shot that hit nobody
        // (random_announcement_lose, obj_explosion).
        private void Announce(Cmd.ShotLanded landed)
        {
            if (landed.Outcome == null || _announcer == null)
            {
                return;
            }

            if (landed.KnockedOut >= 0)
            {
                Say(_announcer.Win(landed.Seat, landed.KnockedOut));
            }
            else if (!landed.StruckAPlayer)
            {
                Say(_announcer.Lose(landed.Seat));
            }
        }

        /// <summary>True while a match is being played (server only).</summary>
        public bool MatchRunning => _match != null && _match.IsRunning;

        /// <summary>
        /// Server: a player arrived while a match is running: they take the lowest free seat and play from the next
        /// round (a seventeenth watches). Before a match, Player seats itself by its connection's seat.
        /// </summary>
        public void Arrived(Player player)
        {
            if (!MatchRunning || player == null || player.IsAi)
            {
                return;
            }

            int seat = _match.FreeSeat();
            player.AssignSeat(seat);
            if (seat >= 0)
            {
                JoinSeat(player, seat, false);
                _humanSeats[player.KitSeat] = seat;
                player.Place(SpawnPoint(seat));
            }

            Mirror();
        }

        /// <summary>
        /// Server: a player's connection dropped and the kit holds their seat for the reconnect grace (UR-T13): their
        /// Player waits, Away (nobody plays it, not even the host it now belongs to), and in a match their seat keeps its
        /// health and score while the turn clock waits on their turn.
        /// </summary>
        public void SeatHeld(int kitSeat)
        {
            Player player = HumanAt(kitSeat);
            if (player != null)
            {
                player.Away = true;
            }

            if (player != null && _humanSeats.ContainsKey(kitSeat))
            {
                _authority.Apply(new Cmd.DropSeatCommand(player.PlayerId, HoldLeft(kitSeat)));
            }

            Mirror();
        }

        /// <summary>
        /// Server: a dropped player reconnected within the grace and the kit gave them their seat and Player back
        /// (UR-T13): in a match they resume where they were, on the same turn if it was theirs.
        /// </summary>
        public void SeatReclaimed(int kitSeat)
        {
            Player player = HumanAt(kitSeat);
            if (player != null)
            {
                player.Away = false;
            }

            if (player != null && _humanSeats.ContainsKey(kitSeat))
            {
                _authority.Apply(new Cmd.RejoinSeatCommand(player.PlayerId));
            }

            Mirror();
        }

        // The human Player of a kit seat (held ones included), or null.
        private Player HumanAt(int kitSeat)
        {
            foreach (Player player in Player.In(_session))
            {
                if (!player.IsAi && player.KitSeat == kitSeat)
                {
                    return player;
                }
            }

            return null;
        }

        // Seconds left before the kit gives up a held seat (the whole grace when it is not held yet).
        private double HoldLeft(int kitSeat)
        {
            foreach (Unity.Multiplayer.Core.Seat seat in _session.Seats)
            {
                if (seat.Index == kitSeat && seat.Status == SeatStatus.Held)
                {
                    return Math.Max(0, seat.HeldUntil - Time.realtimeSinceStartupAsDouble);
                }
            }

            NetConfig config = _session.Config;
            return config != null ? config.ReconnectGraceSeconds : NetConfig.DefaultReconnectGraceSeconds;
        }

        /// <summary>
        /// Server: the kit released a connection's seat (the player left, or did not come back in time): their match seat
        /// drops out, passing the turn if it was theirs. A dropped player whose seat is only held keeps it.
        /// </summary>
        public void SeatReleased(int kitSeat)
        {
            if (_match != null && _humanSeats.TryGetValue(kitSeat, out int seat))
            {
                _humanSeats.Remove(kitSeat);
                Player player = HumanAt(kitSeat); // already gone with the connection, perhaps: the command names the seat too
                _authority.Apply(new Cmd.LeaveSeatCommand(player != null ? player.PlayerId : -1, seat));
                Mirror();
            }
        }

        /// <summary>Server: runs the match clock (called by Turns each frame) and plays the AI seats' turns.</summary>
        public void Tick(float seconds)
        {
            if (_match == null)
            {
                return;
            }

            _authority.Tick(seconds);

            PlayAi(seconds);
            Mirror();
        }

        // obj_CheckAI: a new turn; an AI seat starts thinking, with one of check_ai's thirty lines.
        private void BeginAiTurn(int seat)
        {
            _aiTurn = _match.TryGetSeat(seat, out MatchSeat sitting) && sitting.Kind == SeatKind.Ai
                ? new AiTurn(seat, _aiDice.Next(Ai.ThinkingLineCount), MatchRules.AiThinkSeconds, MatchRules.AiDegreesPerSecond)
                : null;
        }

        // An AI seat's turn, a frame at a time: think, plan (and pick the weapon), turn the barrel, fire.
        private void PlayAi(float seconds)
        {
            if (_aiTurn == null || _match.Phase != MatchPhase.Aiming || _match.Current != _aiTurn.Seat)
            {
                return;
            }

            Player ai = PlayerAt(_aiTurn.Seat);
            if (ai == null || ai.ShotInFlight)
            {
                return;
            }

            switch (_aiTurn.Step(seconds, ai.Heading, _match.TimeLeft, out double next))
            {
                case AiStep.Plan:
                    AiPlan plan = Ai.Plan(ToCore(ai.Position), SeatTarget(_aiTurn.Seat), LevelFor(_session, _aiTurn.Seat), Targets(_session), _setup.AiSkill, _aiDice,
                        Rules, ArsenalRules, ai.Loadout);
                    _aiTurn.SetPlan(plan);
                    ai.ServerSelectWeapon(plan.Weapon);
                    break;
                case AiStep.Turn:
                    ai.ServerAim((float)next, (float)_aiTurn.Plan.Power);
                    break;
                case AiStep.Fire:
                    ai.ServerFire((float)_aiTurn.Plan.Heading, (float)_aiTurn.Plan.Power);
                    break;
            }
        }

        /// <summary>The player sitting in <paramref name="seat"/>, or null.</summary>
        public Player PlayerAt(int seat)
        {
            foreach (Player player in Player.In(_session))
            {
                if (player.Seat == seat)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>The vote this game offers: restart the round (in a match) or stand every dummy back up.</summary>
        public VoteKind CreateRestartVote() => new VoteKind(RestartVote, VoteMenu.RestartTitle)
        {
            Description = _ => VoteMenu.RestartDescription(_match != null ? _match.Phase : MatchPhase.Practice),
            Passed = vote => RouteVote(vote, _ =>
            {
                if (_match != null && _match.IsRunning)
                {
                    StartMatch(_setup);
                }
                else
                {
                    ResetDummies();
                    ResetHazards();
                }
            }),
        };

        /// <summary>
        /// The kick vote (UR-T14): the kit's own (<see cref="VoteBoard.CreateKickKind"/>: "Kick Bob?", voted on by everyone
        /// but Bob, never the host or the starter's self), with a line saying what passing does here. Passing disconnects
        /// them; the kit frees their seat at once (no reconnect grace), so in a match <see cref="SeatReleased"/> drops their
        /// seat out of the round and passes the turn if it was theirs.
        /// </summary>
        public VoteKind CreateKickVote()
        {
            VoteKind kick = VoteBoard.CreateKickKind(() => _session, VoteMenu.KickedLine);
            kick.Description = _ => VoteMenu.KickDescription(MatchRunning);
            Action<Vote> disconnect = kick.Passed;
            kick.Passed = vote => RouteVote(vote, disconnect);
            return kick;
        }

        /// <summary>
        /// The vote that starts a match; its number is how many AI opponents to add and its text the rest of the setup
        /// (<see cref="MatchSetup.Format"/>: the AI skill and the aim assists each).
        /// </summary>
        public VoteKind CreateMatchVote() => new VoteKind(MatchVote, r => "Start a match with " + r.Number + " AI opponent" + (r.Number == 1 ? "?" : "s?"))
        {
            Description = r => MatchSetup.Parse(r.Number, r.Text).DescribeGame() + ". " + MatchSetup.Parse(r.Number, r.Text).Describe() + ". "
                + MatchSetup.Parse(r.Number, r.Text).DescribeAnnouncer() + ". Everyone takes a seat and plays in turn; the last one standing wins the round.",
            Check = r => HumanCount() + Math.Max(0, r.Number) < 2 ? "A match needs two players: add an AI opponent." : null,
            Passed = vote => RouteVote(vote, v => StartMatch(MatchSetup.Parse(v.Number, v.Text))),
        };

        // The kit counts the ballots (its overlay and quorum are its own); what they decided goes through Core (UR-T65): the
        // vote opens on the authority, every ballot is a VoteCommand from the player who cast it, and the authority's
        // VotePassed is what carries the effect out, and only it: a vote Core does not pass is not carried out. A voter with
        // no Player on this peer cannot cast in Core, so they are left out of its electorate and the threshold falls by their yes; with no voter Core knows at all, the kit's count stands.
        private string _votingKind;
        private bool _votePassedInCore;

        private void RouteVote(Vote vote, Action<Vote> effect)
        {
            string kind = vote.Kind.Id;

            // Only voters with a player here can cast in Core: they are its electorate, and the kit's quorum less the yes
            // votes it cannot mirror is its threshold (the kit has already counted those).
            int electorate = 0;
            foreach (ulong voter in vote.Voters)
            {
                electorate += PlayerOf(voter) != null ? 1 : 0;
            }

            int unmirroredYes = 0;
            foreach (ulong voter in vote.Yes)
            {
                unmirroredYes += PlayerOf(voter) == null ? 1 : 0;
            }

            if (electorate == 0)
            {
                effect?.Invoke(vote); // nobody here is a player Core knows (a bare fixture): the kit's count is all there is
                return;
            }

            int needed = Math.Max(1, Math.Min(electorate, vote.Quorum - unmirroredYes));
            _authority.OpenVote(kind, electorate, (double)needed / electorate);
            _votingKind = kind;
            _votePassedInCore = false;
            CastBallots(vote.Yes, kind, true);
            CastBallots(vote.No, kind, false);
            _votingKind = null;
            _authority.CloseVote(kind);
            if (!_votePassedInCore)
            {
                Debug.LogWarning("[Ricochet] The " + kind + " vote passed on the kit's board but Core did not pass it; nothing carried out.");
                return;
            }

            effect?.Invoke(vote);
        }

        private Player PlayerOf(ulong clientId)
        {
            foreach (Player player in Player.In(_session))
            {
                if (!player.IsAi && player.OwnerClientId == clientId)
                {
                    return player;
                }
            }

            return null;
        }

        private void CastBallots(IReadOnlyList<ulong> voters, string kind, bool yes)
        {
            foreach (ulong voter in voters)
            {
                Player player = PlayerOf(voter);
                if (player != null)
                {
                    _authority.Apply(new Cmd.VoteCommand(player.PlayerId, kind, yes));
                }
            }
        }

        private int HumanCount()
        {
            int count = 0;
            foreach (Player player in Player.In(_session))
            {
                count += player.IsAi ? 0 : 1;
            }

            return count;
        }

        // Copies the authority's state out to every peer: each player's copy of what the authority holds (aim, tank,
        // loadout, shot) first, then the match's turn state and each seat's health.
        internal void Mirror()
        {
            foreach (Player each in Player.In(_session))
            {
                each.SyncFromAuthority();
            }

            if (_match == null)
            {
                return;
            }

            if (_turns != null && _turns.IsSpawned)
            {
                _turns.Mirror(_match);
                _turns.MirrorAi(_aiTurn != null && _aiTurn.Seat == _match.Current && _match.Phase == MatchPhase.Aiming ? _aiTurn.Message : -1);
                _turns.MirrorWait(_match.WaitingForCurrent ? (int)Math.Ceiling(_match.Seat(_match.Current).AwayLeft) : -1);
            }

            foreach (Player player in Player.In(_session))
            {
                if (player.Seat >= 0 && _match.TryGetSeat(player.Seat, out MatchSeat seat))
                {
                    player.Health = seat.Health;
                    player.InRound = seat.Alive;
                    player.Score = seat.Score;
                }
                else
                {
                    player.InRound = false;
                }
            }
        }

        // The announcer says a line (when it spoke at all): every peer's HUD shows it through Turns.
        private void Say(Announcement? announcement)
        {
            if (announcement != null && _turns != null && _turns.IsSpawned)
            {
                _turns.Announce(announcement.Value);
            }
        }

        private void RemoveAi()
        {
            for (int i = 1; i <= MatchRules.MaxSeats; i++)
            {
                _session.Objects.Despawn(AiKeyPrefix + i);
            }
        }

        private static GameObject OpponentPrefab() => LoadPrefab("Game/Opponent");

        private static GameObject TurnsPrefab() => LoadPrefab("Game/Turns");
    }
}
