using System;
using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Hunting;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace HowToHunt.Tests
{
    /// <summary>Per-tick hunting code must not allocate (CLAUDE.md): prey AI, flight, take-down rules, pose history.</summary>
    public sealed class HuntingAllocationTests
    {
        [Test]
        public void PreyBrainStep_DoesNotAllocate()
        {
            var world = new BrushWorld();
            world.Add(Brush.Box(new Vector3(0, -0.5f, 0), new Vector3(100, 0.5f, 100)));
            var behavior = new PreyBehavior { WalkSpeed = 1.2f, FleeSpeed = 6f, FleeRadius = 6f, CalmDownSeconds = 2f, HomeRadius = 15f, StunSeconds = 1f, HalfExtents = new Vector3(0.2f, 0.2f, 0.2f) };
            var s = new PreyState { Position = Vector3.Zero, Home = Vector3.Zero, Rng = 99 };
            var threat = new Vector3(0, 0, -3);
            PreyBrain.Step(ref s, behavior, threat, true, world, 0f, 1f / 64f);
            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 1000; i++) PreyBrain.Step(ref s, behavior, threat, (i & 1) == 0, world, i / 64f, 1f / 64f);
            });
        }

        [Test]
        public void ButterflyFlightAndTakedown_DoNotAllocate()
        {
            var prey = new PreyStats { Allowed = TakedownMethods.Either, MaxHealth = 10f, CaptureDifficulty = 2f, MinKillTier = 1, OverkillRatio = 3f };
            var knife = new ToolStats { Archetype = ToolArchetype.Blade, Mode = ToolMode.Kill, Tier = 1, Damage = 6f, HeadshotMultiplier = 2f };
            var net = new ToolStats { Archetype = ToolArchetype.Net, Mode = ToolMode.Capture, Tier = 1, CaptureStrength = 1f };
            ButterflyFlight.PoseAt(7, Vector3.Zero, 6f, 0, 64);
            Takedown.Resolve(prey, 10f, knife, false);
            float sum = 0f;
            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    sum += ButterflyFlight.PoseAt(7, Vector3.Zero, 6f, i, 64).Position.Y;
                    sum += Takedown.Resolve(prey, 10f, (i & 1) == 0 ? knife : net, (i & 2) == 0).Damage;
                }
            });
            Assert.Greater(sum, 0f);
        }

        [Test]
        public void PoseHistory_RecordAndSample_DoNotAllocate()
        {
            var history = new PoseHistory();
            for (uint t = 0; t < 70; t++) history.Record(t, new EntityPose { Position = new Vector3(t, 0, 0) });
            history.TrySample(60.5, out _);
            AllocationAssert.None(() =>
            {
                for (uint t = 70; t < 1070; t++)
                {
                    history.Record(t, new EntityPose { Position = new Vector3(t, 0, 0) });
                    history.TrySample(t - 3.5, out _);
                }
            });
        }
    }
}
