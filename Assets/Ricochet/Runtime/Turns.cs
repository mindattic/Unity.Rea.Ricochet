// The match's turn state on the wire: one keyed object ("turns") the server spawns when a match starts, mirroring the
// server's Core Match (whose turn, the phase, the turn clock, the round and its winner) for every peer's HUD and input.
// Only the server writes it (UR-LAW-1); its server-side Update is what runs the match clock. Each player's own seat
// and health live on their Player. On an AI seat's turn it also carries which of check_ai's "is thinking..." lines is up
// (UR-T7), for every peer's HUD. And the announcer (UR-T8): which personality is on the air, and the last line it said
// (its kind, which of the ten, the seats it names) with a count that goes up by one per line, so every peer's HUD slides
// the portrait and name box in with it. And the menu flow (UR-T9): the map the match is played on (every peer builds its
// level from the index), the game's goal, and once the goal is reached the champion, for every peer's endgame recap.
// And reconnects (UR-T13): while the turn waits for its dropped player to come back, the whole seconds they have left.
using Ricochet.Core.Ai;
using Ricochet.Core.Level;
using Ricochet.Core.Match;
using Unity.Multiplayer;      // NetEntity, NetSession
using Unity.Multiplayer.Core; // [Synced]
using UnityEngine;

namespace Ricochet
{
    public partial class Turns : NetEntity
    {
        public const string Key = "turns";

        // The turn clock goes out in tenths, not every frame; each peer counts down between updates.
        private const float ClockStep = 0.1f;

        [Synced] private int _phase;
        [Synced] private int _current = -1;
        [Synced] private float _timeLeft;
        [Synced] private int _round;
        [Synced] private int _winner = -1;
        [Synced] private int _aiLine = -1;

        // The current seat has driven this turn, so it cannot shoot (draw_movewarning.gml).
        [Synced] private bool _moved;

        // The announcer: the personality (an index into Announcer.Voices), the lines said so far, and the last one.
        [Synced] private int _voice;
        [Synced] private int _announced;
        [Synced] private int _lineKind;
        [Synced] private int _lineIndex;
        [Synced] private int _lineSubject = -1;
        [Synced] private int _lineOther = -1;

        // The game (UR-T9): the map (an index into Maps.All), the points that win, and the champion once it is over.
        [Synced] private int _map;
        [Synced] private int _goal;
        [Synced] private int _champion = -1;

        // The AI opponents' names (UR-T28): each AI's AiNames index plus one (0 for "AI n"), four to an int, AI 1 first.
        [Synced] private int _aiNames0;
        [Synced] private int _aiNames1;
        [Synced] private int _aiNames2;
        [Synced] private int _aiNames3;

        // The match rules the host set (UR-T76), packed by MatchSetup.PackRules: turn time, bounces, damage, arsenal, drops,
        // redirector pause. Every client applies them, so its bounce preview and rules agree with the host's.
        [Synced] private int _rules0;
        [Synced] private int _rules1;
        [Synced] private int _rules2;
        [Synced] private int _rules3;
        private int[] _appliedRules;

        // Reconnects (UR-T13): the seconds the current seat's dropped player has left to come back, or -1 (not waiting).
        [Synced] private int _waitSeconds = -1;

        private float _shownTime;
        private float _lastSyncedTime = -1f;

        /// <summary>The arena running the match (server only).</summary>
        internal Arena Arena { get; set; }

        public MatchPhase MatchPhase => (MatchPhase)Phase;

        /// <summary>The turn clock as this peer shows it: the synced tenths, counted down smoothly in between.</summary>
        public float ShownTimeLeft => _shownTime;

        /// <summary>The turn state <paramref name="session"/> has, or null before a match starts.</summary>
        public static Turns For(NetSession session)
        {
            Turns turns = null;
            return session != null && session.Objects != null && session.Objects.TryGet(Key, out turns) && turns != null && turns.IsSpawned ? turns : null;
        }

        /// <summary>
        /// Whether <paramref name="seat"/> may fire now, as <paramref name="session"/> sees it: always in practice (no
        /// match), otherwise only on its own turn while aiming. The server decides for real; this keeps a client from
        /// sending a shot it would refuse.
        /// </summary>
        public static bool MayFire(NetSession session, int seat)
        {
            Turns turns = For(session);
            return turns == null || turns.MatchPhase == MatchPhase.Practice || (turns.MatchPhase == MatchPhase.Aiming && turns.Current == seat && !turns.Waiting);
        }

        /// <summary>The current seat drove this turn and may not shoot until it ends.</summary>
        public bool HasMoved => Moved;

        /// <summary>The map this match is played on (an index into Maps.All).</summary>
        public int MapIndex => Map;

        /// <summary>The points that win the game (MatchSetup.OneRound for a single round).</summary>
        public int GoalPoints => Goal;

        /// <summary>The game's champion's seat once the goal is reached (MatchPhase.GameOver), else -1.</summary>
        public int ChampionSeat => Champion;

        /// <summary>The name shown for AI number <paramref name="number"/> (1 and up): the one the setup gave it, or "AI n".</summary>
        public string AiName(int number)
        {
            int slot = number - 1;
            int word = slot < 0 || slot >= MatchRules.MaxSeats - 1 ? 0 : (PackedAiNames(slot / 4) >> (8 * (slot % 4))) & 0xFF;
            return AiNames.NameFor(number, word - 1);
        }

        private int PackedAiNames(int word)
        {
            switch (word)
            {
                case 0: return AiNames0;
                case 1: return AiNames1;
                case 2: return AiNames2;
                default: return AiNames3;
            }
        }

        /// <summary>Server: the AI opponents' names for this match.</summary>
        internal void MirrorAiNames(MatchSetup setup)
        {
            var words = new int[4];
            for (int i = 0; i < MatchRules.MaxSeats - 1; i++)
            {
                words[i / 4] |= (setup.NameIndexOf(i) + 1 & 0xFF) << (8 * (i % 4));
            }

            if (AiNames0 != words[0]) AiNames0 = words[0];
            if (AiNames1 != words[1]) AiNames1 = words[1];
            if (AiNames2 != words[2]) AiNames2 = words[2];
            if (AiNames3 != words[3]) AiNames3 = words[3];
        }

        /// <summary>Fog of war (UR-T26) as the host set it: each player sees only around their own tank. Read from the synced rules on every peer.</summary>
        public bool FogOfWar => MatchSetup.UnpackRules(new[] { Rules0, Rules1, Rules2, Rules3 }).FogOfWar;

        /// <summary>Server: the match rules for this match, for every client to take on.</summary>
        internal void MirrorRules(MatchSetup setup)
        {
            int[] packed = setup.PackRules();
            if (Rules0 != packed[0]) Rules0 = packed[0];
            if (Rules1 != packed[1]) Rules1 = packed[1];
            if (Rules2 != packed[2]) Rules2 = packed[2];
            if (Rules3 != packed[3]) Rules3 = packed[3];
        }

        // A client takes the host's rules once they arrive and again whenever they change (the server already holds them).
        private void AdoptRules()
        {
            int[] packed = { Rules0, Rules1, Rules2, Rules3 };
            if (!MatchSetup.IsPackedRules(packed) || (_appliedRules != null && System.Linq.Enumerable.SequenceEqual(_appliedRules, packed)))
            {
                return;
            }

            _appliedRules = packed;
            MatchSetup rules = MatchSetup.UnpackRules(packed);
            rules.ApplyTo(Arena.ArsenalRules);
            rules.ApplyTo(Arena.MatchRules, Arena.Rules, Arena.DropRules, Arena.HazardRules);
        }

        /// <summary>Server: the map the match is played on.</summary>
        internal void MirrorMap(int map)
        {
            if (Map != map)
            {
                Map = map;
            }
        }

        /// <summary>
        /// While the turn waits for its player to reconnect (UR-T13), about how many seconds they have left to come back;
        /// -1 when the turn is not waiting. The clock stands still meanwhile.
        /// </summary>
        public int WaitingSeconds => WaitSeconds;

        /// <summary>True while the turn waits for its dropped player to reconnect.</summary>
        public bool Waiting => WaitSeconds >= 0 && MatchPhase == MatchPhase.Aiming;

        /// <summary>Server: the seconds the current seat's dropped player has left, or -1.</summary>
        internal void MirrorWait(int seconds)
        {
            if (WaitSeconds != seconds)
            {
                WaitSeconds = seconds;
            }
        }

        /// <summary>The "is thinking..." line (Ai.ThinkingLine) the AI whose turn it is shows, or -1 on a human's turn.</summary>
        public int ThinkingLine => AiLine;

        /// <summary>Server: the AI's "is thinking..." line for this turn, or -1.</summary>
        internal void MirrorAi(int line)
        {
            if (AiLine != line)
            {
                AiLine = line;
            }
        }

        /// <summary>The last line the announcer said (meaningful once <see cref="Announced"/> is above 0).</summary>
        public Announcement LastAnnouncement => new Announcement((AnnouncementKind)LineKind, LineIndex, LineSubject, LineOther);

        /// <summary>Server: the personality on the air for this match.</summary>
        internal void MirrorVoice(int voice)
        {
            if (Voice != voice)
            {
                Voice = voice;
            }
        }

        /// <summary>Server: the announcer says <paramref name="announcement"/> (every peer's HUD shows it).</summary>
        internal void Announce(Announcement announcement)
        {
            LineKind = (int)announcement.Kind;
            LineIndex = announcement.Line;
            LineSubject = announcement.Subject;
            LineOther = announcement.Other;
            Announced = Announced + 1;
        }

        /// <summary>Server: copies the match's state out (the clock only when it moves by a tenth, or jumps back up).</summary>
        internal void Mirror(Match match)
        {
            Phase = (int)match.Phase;
            Current = match.Current;
            Round = match.Round;
            Winner = match.Winner;
            Moved = match.HasMoved;
            Goal = match.Goal;
            Champion = match.Champion;
            float time = (float)match.TimeLeft;
            if (Mathf.Abs(time - TimeLeft) >= ClockStep || time > TimeLeft || time == 0f)
            {
                TimeLeft = time;
            }
        }

        protected override void OnSpawned()
        {
            _shownTime = TimeLeft;
            _lastSyncedTime = TimeLeft;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer && Arena != null)
            {
                Arena.Tick(Time.deltaTime);
            }

            // A client in a process of its own plays on the level the server chose: the map and seed ride on every Player
            // (UR-T23), which adopts them, so a random round's level is not reset to the map's here.

            if (!IsServer)
            {
                AdoptRules();
            }

            // Every peer: follow the synced clock, ticking down locally between its tenths.
            if (TimeLeft != _lastSyncedTime)
            {
                _lastSyncedTime = TimeLeft;
                _shownTime = TimeLeft;
            }
            else if (MatchPhase == MatchPhase.Aiming && !Waiting)
            {
                _shownTime = Mathf.Max(Mathf.Max(0f, TimeLeft - ClockStep), _shownTime - Time.deltaTime);
            }
        }
    }
}
