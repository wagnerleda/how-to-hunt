using System;
using System.Numerics;
using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Hunting;
using HowToHunt.Sim.Movement;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>Terrain heightfields as prism brushes: walkable hills, no snagging on seams, prey follow the ground.</summary>
    public sealed class TerrainTests
    {
        private const float Dt = 1f / 64f;

        /// <summary>Rolling hills: 5 m cells, up to ~20° slopes.</summary>
        private static Heightfield Hills(out BrushWorld world)
        {
            const int n = 41;
            var h = new float[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    h[z * n + x] = 2.5f * MathF.Sin(x * 0.35f) + 1.5f * MathF.Cos(z * 0.3f + x * 0.1f);
            var field = new Heightfield(-100f, -100f, 5f, n, n, h);
            world = new BrushWorld();
            field.AddBrushes(world);
            return field;
        }

        [Test]
        public void Sample_MatchesTraceDownOntoTheSurface()
        {
            Heightfield field = Hills(out BrushWorld world);
            var rng = new Random(3);
            for (int i = 0; i < 200; i++)
            {
                float x = (float)rng.NextDouble() * 180f - 90f, z = (float)rng.NextDouble() * 180f - 90f;
                TraceResult tr = world.TraceBox(new Vector3(x, 30f, z), new Vector3(x, -30f, z), Vector3.Zero);
                Assert.IsTrue(tr.Hit);
                Assert.AreEqual(field.Sample(x, z), tr.EndPosition.Y, 0.01f, $"at {x},{z}");
            }
        }

        [Test]
        public void Player_RunsAndBhopsAcrossHills_WithoutSnagging()
        {
            Hills(out BrushWorld world);
            var cfg = new MoveConfig();
            var s = new MoveState { Position = new Vector3(-80f, 10f, 0f) };
            float startX = s.Position.X;
            int stuckTicks = 0;
            for (int i = 0; i < 64 * 12; i++)
            {
                var cmd = new MoveCmd { Forward = 1f, Yaw = 90f, Buttons = i > 64 * 6 ? MoveButtons.Jump | MoveButtons.AutoHop : 0 };
                Vector3 before = s.Position;
                PlayerMove.Step(ref s, in cmd, cfg, world, Dt);
                if (s.OnGround && Vector3.Distance(before, s.Position) < 0.001f) stuckTicks++;
            }
            Assert.Greater(s.Position.X - startX, 60f, "travelled across the hills");
            Assert.Less(stuckTicks, 3, "never stopped dead on a seam");
            TraceResult inside = world.TraceBox(s.Position, s.Position, cfg.HullHalfExtents);
            Assert.IsFalse(inside.StartSolid, "never ended up inside the ground");
        }

        [Test]
        public void Rabbit_FleesUphill_AndStaysOnTheGround()
        {
            Heightfield field = Hills(out BrushWorld world);
            var b = new PreyBehavior { WalkSpeed = 1.2f, FleeSpeed = 8.5f, FleeRadius = 8f, CalmDownSeconds = 3f, HomeRadius = 30f, StunSeconds = 0.15f, HalfExtents = new Vector3(0.15f, 0.15f, 0.2f), StaminaSeconds = 3f, TiredSpeed = 4.8f };
            var s = new PreyState { Position = new Vector3(0f, field.Sample(0f, 0f), 0f), Home = Vector3.Zero, Rng = 77, Stamina = 3f };
            var threat = new Vector3(-3f, 0f, 0f);
            for (int i = 0; i < 64 * 4; i++) PreyBrain.Step(ref s, in b, threat, true, world, i * Dt, Dt);
            Assert.Greater(Vector3.Distance(new Vector3(s.Position.X, 0f, s.Position.Z), Vector3.Zero), 15f, "got away across the hills");
            Assert.AreEqual(field.Sample(s.Position.X, s.Position.Z), s.Position.Y, 0.08f, "feet on the ground");
        }

        private static float GroundSpeedAfter(MoveButtons buttons, bool water, int ticks = 64 * 3)
        {
            var cfg = new MoveConfig();
            var world = new BrushWorld();
            world.Add(Brush.Box(new Vector3(0, -0.5f, 0), new Vector3(200, 0.5f, 200)));
            if (water) world.Add(Brush.Box(new Vector3(0, 0.3f, 0), new Vector3(200, 0.5f, 200), SurfaceFlags.Water));
            var s = new MoveState { Position = new Vector3(0, cfg.HullHalfExtents.Y + 0.01f, 0) };
            var cmd = new MoveCmd { Forward = 1f, Yaw = 0f, Buttons = buttons };
            for (int i = 0; i < ticks; i++) PlayerMove.Step(ref s, in cmd, cfg, world, Dt);
            return PlayerMove.HorizontalSpeed(s.Velocity);
        }

        [Test]
        public void Sprint_IsFasterThanRunning_WaterIsSlower()
        {
            var cfg = new MoveConfig();
            float run = GroundSpeedAfter(MoveButtons.None, false);
            float sprint = GroundSpeedAfter(MoveButtons.Sprint, false);
            float wade = GroundSpeedAfter(MoveButtons.None, true);
            Assert.AreEqual(cfg.MaxSpeed, run, 0.1f);
            Assert.AreEqual(cfg.MaxSpeed * cfg.SprintFactor, sprint, 0.15f, "sprint is a modest boost");
            Assert.Less(sprint, 9f, "bunny hopping stays the way to go really fast");
            Assert.Less(wade, run * 0.7f, "wading slows you down");
        }

        [Test]
        public void Rabbit_SprintsFasterThanRunning_ThenTires()
        {
            var world = new BrushWorld();
            world.Add(Brush.Box(new Vector3(0f, -0.5f, 0f), new Vector3(200f, 0.5f, 200f)));
            var b = new PreyBehavior { WalkSpeed = 1.2f, FleeSpeed = 8.5f, FleeRadius = 8f, CalmDownSeconds = 3f, HomeRadius = 300f, StunSeconds = 0.15f, HalfExtents = new Vector3(0.15f, 0.15f, 0.2f), StaminaSeconds = 3f, TiredSpeed = 4.8f };
            var s = new PreyState { Position = Vector3.Zero, Rng = 5, Stamina = 3f };
            float fresh = 0f, tired = 0f;
            for (int i = 0; i < 64 * 5; i++)
            {
                // The threat keeps pace right behind the rabbit.
                var threat = s.Position - new Vector3(0f, 0f, 3f);
                PreyBrain.Step(ref s, in b, threat, true, world, i * Dt, Dt);
                if (i == 32) fresh = s.Speed;
                if (i == 64 * 4) tired = s.Speed;
            }
            Assert.Greater(fresh, new MoveConfig().MaxSpeed, "a running player can't keep up with the sprint");
            Assert.Less(tired, new MoveConfig().MaxSpeed, "once tired, a runner (and easily a bunny hopper) catches up");
        }
    }
}
