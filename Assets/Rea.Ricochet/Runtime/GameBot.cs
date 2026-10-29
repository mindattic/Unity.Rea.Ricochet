// The game's bot: the only bot code a game writes. Launched with -bot A (mp-host game smoke, NetBotTest), the library
// joins the game by code with no menus and hands the connected session to RunAsync. This one picks its practice dummy
// (A takes dummy:0, B dummy:1, any other role dummy:2), finds an aim whose bounce path ends on it with the same solver
// the preview uses (BounceSolver.FindHeading, the original's find_target_angle), fires, and passes once the server has
// knocked the dummy down. Keep the shape: wait for what you need, play like a player would (the same [ToServer]
// calls), and Pass or Fail.
using System.Threading.Tasks;
using Ricochet.Core.Aim;
using Ricochet.Core.Level;
using Unity.Multiplayer; // IBotDriver, NetBot, [NetBotDriver]
using UnityEngine.Scripting;

namespace Ricochet
{
    [NetBotDriver, Preserve]
    public sealed class GameBot : IBotDriver
    {
        public static string DummyFor(string role)
        {
            return Arena.DummyKey(role == "A" ? 0 : role == "B" ? 1 : 2);
        }

        public async Task RunAsync(NetBot bot)
        {
            Player me = await bot.LocalPlayer<Player>();
            string key = DummyFor(bot.Role);
            Dummy dummy = null;
            await bot.Until(() => bot.Session.Objects.TryGet(key, out dummy) && dummy.Position != UnityEngine.Vector2.zero,
                "the dummy " + key + " to arrive");
            await bot.Until(() => me.Position != UnityEngine.Vector2.zero, "my seat's position");

            double? heading = BounceSolver.FindHeading(Arena.ToCore(me.Position), key, Arena.Level,
                Arena.Targets(bot.Session), Arena.Rules);
            if (heading == null)
            {
                bot.Fail("no aim from " + me.Position + " banks onto " + key);
                return;
            }

            me.Fire((float)heading.Value);
            await bot.Until(() => dummy != null && dummy.Down, "the server to knock down " + key, 20f);
            bot.Pass("knocked down " + key + " firing at " + heading.Value.ToString("0.0") + " degrees from " + me.Position
                + " as " + me.DisplayName);
        }
    }
}
