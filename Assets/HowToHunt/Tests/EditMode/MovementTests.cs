using System;
using System.Numerics;
using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Movement;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    public class MovementTests
    {
        private const float Dt = 1f / 64f;
        private MoveConfig _cfg;
        private BrushWorld _world;

        [SetUp]
        public void SetUp()
        {
            _cfg = new MoveConfig();
            _world = new BrushWorld();
            // 200 m floor, top surface at y = 0.
            _world.Add(Brush.Box(new Vector3(0f, -0.5f, 0f), new Vector3(100f, 0.5f, 100f)));
        }

        private MoveState Spawn(Vector3 feet) => new MoveState { Position = feet + new Vector3(0f, _cfg.HullHalfExtents.Y, 0f) };

        private void Run(ref MoveState s, MoveCmd cmd, int ticks)
        {
            for (int i = 0; i < ticks; i++) PlayerMove.Step(ref s, in cmd, _cfg, _world, Dt);
        }

        [Test]
        public void Trace_StopsAtFloor()
        {
            Vector3 ext = _cfg.HullHalfExtents;
            TraceResult tr = _world.TraceBox(new Vector3(0, 5, 0), new Vector3(0, -5, 0), ext);
            Assert.IsTrue(tr.Hit);
            Assert.AreEqual(ext.Y, tr.EndPosition.Y, 0.01f);
            Assert.AreEqual(1f, tr.Normal.Y, 1e-4f);
        }

        [Test]
        public void Falls_AndLandsOnFloor()
        {
            MoveState s = Spawn(new Vector3(0, 3, 0));
            Run(ref s, default, 128);
            Assert.IsTrue(s.OnGround);
            Assert.AreEqual(_cfg.HullHalfExtents.Y, s.Position.Y, 0.01f);
            Assert.AreEqual(0f, s.Velocity.Y, 1e-4f);
        }

        [Test]
        public void Walking_ReachesMaxSpeed_AndFrictionStops()
        {
            MoveState s = Spawn(Vector3.Zero);
            Run(ref s, new MoveCmd { Forward = 1f }, 128);
            Assert.AreEqual(_cfg.MaxSpeed, PlayerMove.HorizontalSpeed(s.Velocity), 0.05f);
            Assert.Greater(s.Position.Z, 5f, "moved forward along +Z at yaw 0");

            Run(ref s, default, 64);
            Assert.Less(PlayerMove.HorizontalSpeed(s.Velocity), 0.01f);
        }

        [Test]
        public void Wall_BlocksMovement()
        {
            _world.Add(Brush.Box(new Vector3(0, 2, 3), new Vector3(5, 2, 0.5f)));
            MoveState s = Spawn(Vector3.Zero);
            Run(ref s, new MoveCmd { Forward = 1f }, 256);
            Assert.Less(s.Position.Z, 2.5f - _cfg.HullHalfExtents.Z + 0.01f);
        }

        [Test]
        public void Steps_UpLowLedges_ButNotWalls()
        {
            _world.Add(Brush.Box(new Vector3(0, 0.15f, 6), new Vector3(5, 0.15f, 4)));
            MoveState s = Spawn(Vector3.Zero);
            Run(ref s, new MoveCmd { Forward = 1f }, 64);
            Assert.Greater(s.Position.Z, 3f, "walked onto the 30 cm step");
            Assert.AreEqual(0.3f + _cfg.HullHalfExtents.Y, s.Position.Y, 0.02f);
        }

        [Test]
        public void Jump_LeavesGround_AndReturns()
        {
            MoveState s = Spawn(Vector3.Zero);
            Run(ref s, default, 4);
            Run(ref s, new MoveCmd { Buttons = MoveButtons.Jump }, 1);
            Assert.IsFalse(s.OnGround);
            Assert.Greater(s.Velocity.Y, 0f);
            float peak = 0f;
            for (int i = 0; i < 128; i++)
            {
                Run(ref s, default, 1);
                peak = MathF.Max(peak, s.Position.Y - _cfg.HullHalfExtents.Y);
            }
            Assert.IsTrue(s.OnGround);
            // v^2 / 2g = 7.67^2 / 40.64 ≈ 1.45 m
            Assert.AreEqual(1.45f, peak, 0.08f);
        }

        [Test]
        public void HoldingJump_WithoutAutoHop_JumpsOnce()
        {
            MoveState s = Spawn(Vector3.Zero);
            int jumps = CountJumps(ref s, 256);
            Assert.AreEqual(1, jumps);

            _cfg.AutoHop = true;
            s = Spawn(Vector3.Zero);
            jumps = CountJumps(ref s, 256);
            Assert.Greater(jumps, 3);
        }

        private int CountJumps(ref MoveState s, int ticks)
        {
            int jumps = 0;
            var cmd = new MoveCmd { Buttons = MoveButtons.Jump };
            for (int i = 0; i < ticks; i++)
            {
                float vyBefore = s.Velocity.Y;
                PlayerMove.Step(ref s, in cmd, _cfg, _world, Dt);
                if (vyBefore <= 0f && s.Velocity.Y > 0f) jumps++;
            }
            return jumps;
        }

        [Test]
        public void AirStrafing_GainsSpeedBeyondMaxSpeed()
        {
            // Classic bhop: hold strafe and turn the view toward it while in the air.
            _cfg.AutoHop = true;
            MoveState s = Spawn(Vector3.Zero);
            Run(ref s, new MoveCmd { Forward = 1f }, 128);
            float yaw = 0f;
            for (int i = 0; i < 64 * 6; i++)
            {
                bool right = (i / 48) % 2 == 0;
                yaw += right ? 2.2f : -2.2f;
                var cmd = new MoveCmd { Side = right ? 1f : -1f, Yaw = yaw, Buttons = MoveButtons.Jump };
                PlayerMove.Step(ref s, in cmd, _cfg, _world, Dt);
            }
            Assert.Greater(PlayerMove.HorizontalSpeed(s.Velocity), _cfg.MaxSpeed * 1.3f);
        }

        [Test]
        public void SteepRamp_IsSurfed_NotStoodOn()
        {
            // 60° ramp rising toward +Z: too steep to stand on (normal.y = 0.5 < 0.7).
            var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -60f * MathF.PI / 180f);
            _world.Add(Brush.Oriented(new Vector3(0, 10, 0), new Vector3(4, 0.5f, 10), rot));
            MoveState s = Spawn(new Vector3(0, 10, -2));
            s.Velocity = new Vector3(8f, 0f, 0f);
            bool touchedRamp = false;
            for (int i = 0; i < 64; i++)
            {
                PlayerMove.Step(ref s, default, _cfg, _world, Dt);
                if (s.GroundNormal.Y > 0.3f && s.GroundNormal.Y < 0.7f)
                {
                    touchedRamp = true;
                    Assert.IsFalse(s.OnGround, "stood on a surf ramp");
                }
            }
            Assert.IsTrue(touchedRamp, "never reached the ramp");
            Assert.Greater(Math.Abs(s.Velocity.X), 7f, "lost sideways speed while surfing");
        }

        [Test]
        public void Duck_LowersHull_AndSlowsDown()
        {
            MoveState s = Spawn(Vector3.Zero);
            Run(ref s, new MoveCmd { Forward = 1f, Buttons = MoveButtons.Duck }, 128);
            Assert.IsTrue(s.Ducked);
            Assert.AreEqual(_cfg.DuckHalfHeight, s.Position.Y, 0.01f, "feet stay on the floor");
            Assert.AreEqual(_cfg.MaxSpeed * _cfg.DuckSpeedFactor, PlayerMove.HorizontalSpeed(s.Velocity), 0.05f);
        }

        [Test]
        public void Unduck_IsBlockedUnderLowCeiling()
        {
            // 1.5 m gap: fits the 1.37 m ducked hull, not the 1.83 m standing one.
            _world.Add(Brush.Box(new Vector3(0, 2.5f, 0), new Vector3(5, 1f, 5)));
            MoveState s = Spawn(Vector3.Zero);
            s.Ducked = true;
            s.Position.Y = _cfg.DuckHalfHeight;
            Run(ref s, default, 16);
            Assert.IsTrue(s.Ducked, "stood up into the ceiling");
        }

        [Test]
        public void CrouchJump_ReachesHigherLedge()
        {
            // Ledge top at 1.6 m: a standing jump (feet peak ≈ 1.45 m) can't clear it, a crouch-jump (≈ 1.9 m) can.
            _world.Add(Brush.Box(new Vector3(0, 0.8f, 4f), new Vector3(5, 0.8f, 2.5f)));
            Assert.IsFalse(TryLedge(crouch: false), "standing jump should not clear 1.6 m");
            Assert.IsTrue(TryLedge(crouch: true), "crouch-jump should clear 1.6 m");
        }

        private bool TryLedge(bool crouch)
        {
            // Run up, jump about 2.4 m before the ledge face (z = 1.5), keep running; duck once airborne if crouching.
            MoveState s = Spawn(new Vector3(0, 0, -5f));
            bool jumped = false;
            for (int i = 0; i < 120; i++)
            {
                var cmd = new MoveCmd { Forward = 1f };
                if (!jumped && s.Position.Z >= -1.3f) { cmd.Buttons |= MoveButtons.Jump; jumped = true; }
                else if (jumped && crouch && !s.OnGround) cmd.Buttons |= MoveButtons.Duck;
                PlayerMove.Step(ref s, in cmd, _cfg, _world, Dt);
                float feet = s.Position.Y - _cfg.Hull(s.Ducked).Y;
                if (s.OnGround && feet > 1.55f) return true; // standing on the ledge
            }
            return false;
        }

        [Test]
        public void Landing_ReportsEventAndImpactSpeed()
        {
            MoveState s = Spawn(new Vector3(0, 3, 0));
            bool landed = false;
            for (int i = 0; i < 128 && !landed; i++)
            {
                PlayerMove.Step(ref s, default, _cfg, _world, Dt);
                if ((s.Events & MoveEvents.Landed) != 0)
                {
                    landed = true;
                    // v = sqrt(2 g h) ≈ sqrt(2 * 20.32 * 3) ≈ 11 m/s
                    Assert.AreEqual(11f, s.LandSpeed, 0.6f);
                }
            }
            Assert.IsTrue(landed);
        }

        [Test]
        public void Noclip_FliesThroughWalls()
        {
            _world.Add(Brush.Box(new Vector3(0, 2, 3), new Vector3(5, 2, 0.5f)));
            MoveState s = Spawn(Vector3.Zero);
            s.Mode = MoveMode.Noclip;
            Run(ref s, new MoveCmd { Forward = 1f }, 64);
            Assert.Greater(s.Position.Z, 10f);
        }

        [Test]
        public void Step_IsDeterministic()
        {
            MoveState a = Spawn(Vector3.Zero), b = Spawn(Vector3.Zero);
            var rng = new Random(42);
            for (int i = 0; i < 2000; i++)
            {
                var cmd = new MoveCmd
                {
                    Forward = (float)rng.NextDouble() * 2 - 1,
                    Side = (float)rng.NextDouble() * 2 - 1,
                    Yaw = (float)rng.NextDouble() * 360,
                    Buttons = rng.Next(4) == 0 ? MoveButtons.Jump : MoveButtons.None,
                };
                PlayerMove.Step(ref a, in cmd, _cfg, _world, Dt);
                PlayerMove.Step(ref b, in cmd, _cfg, _world, Dt);
            }
            Assert.AreEqual(a.Position, b.Position);
            Assert.AreEqual(a.Velocity, b.Velocity);
        }

        [Test]
        public void Step_DoesNotAllocate()
        {
            MoveState s = Spawn(Vector3.Zero);
            var cmd = new MoveCmd { Forward = 1f, Side = 0.5f, Buttons = MoveButtons.Jump };
            PlayerMove.Step(ref s, in cmd, _cfg, _world, Dt);
            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 1000; i++) PlayerMove.Step(ref s, in cmd, _cfg, _world, Dt);
            });
        }
    }
}
