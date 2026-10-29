// Editor-only validator for the Core boundary: Ricochet.Core is the engine-free rules assembly (it also builds as a plain
// .NET library for Tests/Core.Tests), so it may reference nothing but the BCL. Run from Ricochet > Validate Core Boundary,
// or let the EditMode test CoreBoundaryTests fail on the same list.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ricochet.Tooling
{
    public static class CoreBoundaryValidator
    {
        public const string CoreAssemblyName = "Ricochet.Core";

        // Assembly names the engine-free Core may reference: the .NET base class library only.
        private static bool IsBaseLibrary(string name) =>
            name == "mscorlib" || name == "netstandard" || name == "System" || name.StartsWith("System.", StringComparison.Ordinal);

        // Every assembly Core references that is not the base class library (empty when the boundary holds).
        public static List<string> Violations()
        {
            var core = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == CoreAssemblyName);
            if (core == null)
                return new List<string> { "assembly " + CoreAssemblyName + " is not loaded" };
            return core.GetReferencedAssemblies().Select(r => r.Name).Where(n => !IsBaseLibrary(n)).OrderBy(n => n).ToList();
        }

        [MenuItem("Ricochet/Validate Core Boundary")]
        private static void Validate()
        {
            var bad = Violations();
            if (bad.Count == 0) Debug.Log(CoreAssemblyName + " references only the base class library.");
            else Debug.LogError(CoreAssemblyName + " must not reference: " + string.Join(", ", bad));
        }
    }
}
