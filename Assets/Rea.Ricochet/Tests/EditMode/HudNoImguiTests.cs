// UR-T38: the game draws no IMGUI. Every OnGUI was rebuilt as uGUI / TextMeshPro (MatchHud, PlayerLabels, Crate), so no
// type in the runtime assembly may define one again: IMGUI cannot scale, theme, localise or sit under the kit's menus.
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Ricochet.EditorTests
{
    public class HudNoImguiTests
    {
        [Test]
        public void NoRuntimeTypeDefinesOnGUI()
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            string[] offenders = typeof(Player).Assembly.GetTypes()
                .Where(t => t.GetMethod("OnGUI", all) != null)
                .Select(t => t.FullName)
                .ToArray();
            Assert.That(offenders, Is.Empty, "still IMGUI: " + string.Join(", ", offenders));
        }
    }
}
