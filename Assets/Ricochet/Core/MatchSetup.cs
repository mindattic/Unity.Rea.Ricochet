// What players choose before a match (UR-T7): how many AI opponents fill the empty seats, how well they aim, and how
// many aim assists everyone starts with. The 2013 original set these on its setup screens (design reference only,
// UR-LAW-2): obj_btn_ai_less/more stepped aiCount, the skill arrows each AI player's pSkill (0 to 10), and the
// aim-assist checkbox and arrows hasAimAssist and count_AimAssist (0 to 10, 10 meaning unlimited; 3 by default).
// Here one skill covers every AI seat, and an aim-assist level of 0 takes the item out of play (the unchecked box).
// The announcer (UR-T8) rides along: the personality on the air (obj_btn_announcer_last/next) and how often it speaks
// (the "announcements" setting, obj_btn_announcer_less/more: 0 off to 4 rarely). The AI count travels as a vote's number
// and the rest as its text ("skill=5;assist=3;voice=0;announce=1").
using System;
using System.Globalization;

namespace Ricochet.Core
{
    public sealed class MatchSetup
    {
        /// <summary>Aim assists each player starts with unless set (count_AimAssist = 3).</summary>
        public const int DefaultAimAssists = 3;

        private int _aiCount = 1;
        private int _aiSkill = Ai.DefaultSkill;
        private int _aimAssists = DefaultAimAssists;
        private int _voice = Announcer.DefaultVoice;
        private int _announcements = Announcer.DefaultFrequency;

        /// <summary>AI opponents to add (0 to 15; the server seats fewer when the table is full).</summary>
        public int AiCount
        {
            get { return _aiCount; }
            set { _aiCount = Math.Max(0, Math.Min(MatchRules.MaxSeats - 1, value)); }
        }

        /// <summary>Every AI seat's skill, 0 (aim nudged up to 10 degrees) to 10 (exact).</summary>
        public int AiSkill
        {
            get { return _aiSkill; }
            set { _aiSkill = Ai.ClampSkill(value); }
        }

        /// <summary>Aim assists each player starts a round with: 0 (none, and none drop), 1 to 9, or 10 (unlimited).</summary>
        public int AimAssists
        {
            get { return _aimAssists; }
            set { _aimAssists = ArsenalRules.ClampCount(value); }
        }

        /// <summary>The announcer on the air (an index into Announcer.Voices).</summary>
        public int AnnouncerVoice
        {
            get { return _voice; }
            set { _voice = Announcer.ClampVoice(value); }
        }

        /// <summary>How often the announcer speaks: 0 never, 1 every time, 2 to 4 rarer and rarer.</summary>
        public int Announcements
        {
            get { return _announcements; }
            set { _announcements = Announcer.ClampFrequency(value); }
        }

        public MatchSetup Clone() => (MatchSetup)MemberwiseClone();

        /// <summary>Gives <paramref name="rules"/> this setup's aim assists: the starting count, and in play only when above 0.</summary>
        public void ApplyTo(ArsenalRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            rules.SetStartCount(Item.AimAssist, AimAssists);
            rules.SetInPlay(Item.AimAssist, AimAssists > 0);
        }

        /// <summary>The vote's text payload: everything but the AI count.</summary>
        public string Format()
        {
            return "skill=" + AiSkill.ToString(CultureInfo.InvariantCulture) + ";assist=" + AimAssists.ToString(CultureInfo.InvariantCulture)
                + ";voice=" + AnnouncerVoice.ToString(CultureInfo.InvariantCulture) + ";announce=" + Announcements.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A setup read back from a vote's number (<paramref name="aiCount"/>) and text (<see cref="Format"/>); anything
        /// missing or unreadable keeps its default.
        /// </summary>
        public static MatchSetup Parse(long aiCount, string text)
        {
            var setup = new MatchSetup { AiCount = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, aiCount)) };
            if (string.IsNullOrEmpty(text))
            {
                return setup;
            }

            foreach (string part in text.Split(';'))
            {
                int equals = part.IndexOf('=');
                if (equals <= 0 || !int.TryParse(part.Substring(equals + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    continue;
                }

                switch (part.Substring(0, equals).Trim())
                {
                    case "skill":
                        setup.AiSkill = value;
                        break;
                    case "assist":
                        setup.AimAssists = value;
                        break;
                    case "voice":
                        setup.AnnouncerVoice = value;
                        break;
                    case "announce":
                        setup.Announcements = value;
                        break;
                }
            }

            return setup;
        }

        /// <summary>"Kurt Sly on the air, every time", or "No announcer".</summary>
        public string DescribeAnnouncer()
        {
            return Announcements == 0 ? "No announcer"
                : Announcer.VoiceAt(AnnouncerVoice).Name + " on the air, " + Announcer.FrequencyName(Announcements).ToLowerInvariant();
        }

        /// <summary>"AI skill 5 of 10, 3 aim assists each" (the AI part only when there are AI opponents).</summary>
        public string Describe()
        {
            string assists = AimAssists == 0 ? "no aim assist"
                : AimAssists >= ArsenalRules.Unlimited ? "unlimited aim assist"
                : AimAssists + " aim assist" + (AimAssists == 1 ? string.Empty : "s") + " each";
            return AiCount > 0 ? "AI skill " + AiSkill + " of " + Ai.MaxSkill + ", " + assists : char.ToUpperInvariant(assists[0]) + assists.Substring(1);
        }
    }
}
