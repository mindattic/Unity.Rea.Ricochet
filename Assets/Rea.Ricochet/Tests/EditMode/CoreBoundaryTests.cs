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

        [System.Serializable]
        private class AsmdefData
        {
            public string[] references;
            public string[] precompiledReferences;
            public bool noEngineReferences;
            public bool overrideReferences;
        }

        [Test]
        public void CoreAsmdefDeclaresNoReferencesAndNoEngine()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "Ricochet", "Core", "Ricochet.Core.asmdef");
            var data = UnityEngine.JsonUtility.FromJson<AsmdefData>(File.ReadAllText(path));
            Assert.That(data.references, Is.Empty);
            Assert.That(data.precompiledReferences, Is.Empty);
            Assert.That(data.noEngineReferences, Is.True);
        }
    }
}
