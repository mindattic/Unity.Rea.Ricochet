// The match's turn state on the wire: one keyed object ("turns") the server spawns when a match starts, mirroring the
// server's Core Match (whose turn, the phase, the turn clock, the round and its winner) for every peer's HUD and input.
// Only the server writes it (UR-LAW-1); its server-side Update is what runs the match clock. Each player's own seat
// and health live on their Player. On an AI seat's turn it also carries which of check_ai's "is thinking..." lines is up
// (UR-T7), for every peer's HUD. And the announcer (UR-T8): which personality is on the air, and the last line it said
// (its kind, which of the ten, the seats it names) with a count that goes up by one per line, so every peer's HUD slides
// the portrait and name box in with it. And the menu flow (UR-T9): the map the match is played on (every peer builds its
// level from the index), the game's goal, and once the goal is reached the champion, for every peer's endgame recap.
// And reconnects (UR-T13): while the turn waits for its dropped player to come back, the whole seconds they have left.
using Ricochet.Core;          // MatchPhase, MatchRules, Announcement
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
