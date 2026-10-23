using System.IO;
using NUnit.Framework;
using Ricochet.Tooling;

namespace Ricochet.ToolingTests
{
    // Ricochet.Core is the engine-free rules assembly: it must not reference UnityEngine or any assembly beyond the base
    // class library (it is also compiled as plain .NET for Tests/Core.Tests).
    public class CoreBoundaryTests
    {
        [Test]
        public void CoreReferencesNoUnityOrOtherAssembly()
        {
            Assert.That(CoreBoundaryValidator.Violations(), Is.Empty,
                "Ricochet.Core must stay engine-free: no UnityEngine and no other game assembly");
        }

        [Test]
        public void CoreAsmdefDeclaresNoReferencesAndNoEngine()
        {
            string asmdef = Path.Combine(UnityEngine.Application.dataPath, "Ricochet", "Core", "Ricochet.Core.asmdef");
            string text = File.ReadAllText(asmdef);
            Assert.That(text, Does.Contain("\"noEngineReferences\": true"));
            Assert.That(text, Does.Contain("\"references\": []"));
        }
    }
}
