// The feed presentation listens to (UR-T65). Core's GameAuthority answers every command with ordered events; on the server
// they arrive from it directly, and on every peer Player turns what the server sent (the volley it solved, the result it
// reached) back into the same events and raises them here, naming the player they are about. ShotView and GameAudio
// subscribe and play them: neither reads or changes Core's match, loadouts or shots, and a peer that is only a client
// sees exactly what the host does. MatchHud draws the synced Turns and Player copies, which is also no Core state.
using System;
using Ricochet.Core.Commands;

namespace Ricochet
{
    public static class Presentation
    {
        /// <summary>Raised for every event, with the player it is about (null for none).</summary>
        public static event Action<Player, GameEvent> Raised;

        public static void Raise(Player who, GameEvent e)
        {
            if (e != null)
            {
                Raised?.Invoke(who, e);
            }
        }
    }
}
