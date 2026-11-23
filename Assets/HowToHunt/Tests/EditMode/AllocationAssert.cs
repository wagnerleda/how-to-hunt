using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HowToHunt.Tests
{
    /// <summary>
    /// Zero-allocation checks for hot paths. Uses Unity's GC.Alloc profiler counter: GC.GetAllocatedBytesForCurrentThread
    /// always returns 0 on Unity's Mono, so tests built on it could never fail.
    /// </summary>
    public static class AllocationAssert
    {
        public static void None(TestDelegate body, string message = null) =>
            Assert.That(body, Is.Not.AllocatingGCMemory(), message);
    }
}
