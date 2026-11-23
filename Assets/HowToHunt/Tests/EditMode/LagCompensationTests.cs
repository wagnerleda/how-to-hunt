using System;
using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Combat;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace HowToHunt.Tests
{
    public class LagCompensationTests
    {
        private const int Rate = 64;

        [Test]
        public void RayCapsule_HitsBodyAndCaps_MissesBeside()
        {
            var a = new Vector3(0, 0, 5);
            var b = new Vector3(0, 2, 5);
            Assert.IsTrue(HitScan.RayCapsule(new Vector3(0, 1, 0), Vector3.UnitZ, a, b, 0.5f, out float d));
            Assert.AreEqual(4.5f, d, 1e-3f);
            Assert.IsTrue(HitScan.RayCapsule(new Vector3(0, 2.3f, 0), Vector3.UnitZ, a, b, 0.5f, out d), "top cap");
            Assert.IsFalse(HitScan.RayCapsule(new Vector3(0.6f, 1, 0), Vector3.UnitZ, a, b, 0.5f, out _), "beside");
            Assert.IsFalse(HitScan.RayCapsule(new Vector3(0, 1, 10), Vector3.UnitZ, a, b, 0.5f, out _), "behind the ray");
        }

        [Test]
        public void PoseHistory_InterpolatesBetweenTicks_AndForgetsOldOnes()
        {
            var h = new PoseHistory(16);
            for (uint t = 100; t < 120; t++) h.Record(t, new EntityPose { Position = new Vector3(t, 0, 0) });
            Assert.IsTrue(h.TrySample(110.5, out EntityPose p));
            Assert.AreEqual(110.5f, p.Position.X, 1e-4f);
            Assert.IsFalse(h.TrySample(101, out _), "older than the buffer");
        }

        [Test]
        public void Walls_BlockShots()
        {
            var world = new BrushWorld();
            world.Add(Brush.Box(new Vector3(0, 1, 3), new Vector3(2, 2, 0.2f)));
            var poses = new[] { new EntityPose { Position = new Vector3(0, 0, 6) } };
            Assert.IsTrue(HitScan.Raycast(new Vector3(0, 1, 0), Vector3.UnitZ, 50f, world, poses, TargetDummyPath.Hitboxes, out RayHit hit));
            Assert.AreEqual(-1, hit.Target, "hit the wall, not the dummy");
        }

        [Test]
        public void Headshots_AreDetected()
        {
            var poses = new[] { new EntityPose { Position = new Vector3(0, 0, 8) } };
            Assert.IsTrue(HitScan.Raycast(new Vector3(0, 1.58f, 0), Vector3.UnitZ, 50f, new BrushWorld(), poses, TargetDummyPath.Hitboxes, out RayHit hit));
            Assert.AreEqual(HitRegion.Head, hit.Region);
        }

        /// <summary>
        /// The point of lag compensation: a client ~150 ms behind aims exactly at the dummy it sees. Rewinding to the
        /// client's view tick hits; resolving against the dummy's current position misses.
        /// </summary>
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(14)]
        public void Rewind_MakesShotsLandWhereThePlayerAimed(int ticksBehind)
        {
            var path = new TargetDummyPath(new Vector3(-6, 0, 12), new Vector3(6, 0, 12), 5f);
            var history = new PoseHistory();
            const uint serverTick = 1000;
            for (uint t = serverTick - 100; t <= serverTick; t++) history.Record(t, path.PoseAt(t, Rate));

            double viewTick = serverTick - ticksBehind + 0.4;
            EntityPose seen = path.PoseAt(viewTick, Rate);
            var eye = new Vector3(0, 1.6f, 0);
            Vector3 aim = Vector3.Normalize(seen.Position + new Vector3(0, 0.8f, 0) - eye);
            var world = new BrushWorld();

            Assert.IsTrue(history.TrySample(viewTick, out EntityPose rewound));
            Assert.IsTrue(HitScan.Raycast(eye, aim, 100f, world, new[] { rewound }, TargetDummyPath.Hitboxes, out RayHit withLagComp),
                "rewound shot should hit");
            Assert.AreEqual(0, withLagComp.Target);

            EntityPose now = path.PoseAt(serverTick, Rate);
            bool hitWithoutLagComp = HitScan.Raycast(eye, aim, 100f, world, new[] { now }, TargetDummyPath.Hitboxes, out RayHit noComp) && noComp.Target == 0;
            Assert.IsFalse(hitWithoutLagComp, "without rewinding, the dummy has moved on and the shot misses");
        }

        [Test]
        public void HitScan_DoesNotAllocate()
        {
            var poses = new[] { new EntityPose { Position = new Vector3(0, 0, 8) } };
            var world = new BrushWorld();
            HitScan.Raycast(new Vector3(0, 1, 0), Vector3.UnitZ, 50f, world, poses, TargetDummyPath.Hitboxes, out _);
            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 1000; i++) HitScan.Raycast(new Vector3(0, 1, 0), Vector3.UnitZ, 50f, world, poses, TargetDummyPath.Hitboxes, out _);
            });
        }
    }
}
