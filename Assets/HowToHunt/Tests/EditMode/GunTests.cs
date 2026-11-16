using System;
using System.Numerics;
using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Movement;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>Phase 4 guns: flying bullets, damage falloff, accuracy that drops with speed, heavy weapons slow you.</summary>
    public sealed class GunTests
    {
        private static GunBallistics Revolver => new GunBallistics
        {
            BulletSpeed = 360f, BulletGravity = 4f, FalloffStart = 25f, FalloffEnd = 70f, MinDamageFraction = 0.55f,
            SpreadDegrees = 1.2f, MoveSpreadPerMps = 0.4f, MaxSpreadDegrees = 7f, AdsSpreadFactor = 0.25f,
        };

        [Test]
        public void Falloff_FullUpClose_LinearDrop_FloorFarAway()
        {
            GunBallistics g = Revolver;
            Assert.AreEqual(1f, GunMath.Falloff(g, 10f));
            Assert.AreEqual(1f, GunMath.Falloff(g, 25f));
            Assert.AreEqual(1f - 0.45f * 0.5f, GunMath.Falloff(g, 47.5f), 1e-4f);
            Assert.AreEqual(0.55f, GunMath.Falloff(g, 200f), 1e-4f);
        }

        [Test]
        public void Spread_GrowsWithSpeed_ShrinksDownTheSights_ScoutIgnoresSpeed()
        {
            GunBallistics g = Revolver;
            float still = GunMath.Spread(g, 0f, false), running = GunMath.Spread(g, 6f, false), bhop = GunMath.Spread(g, 30f, false);
            Assert.AreEqual(1.2f, still, 1e-4f);
            Assert.Greater(running, still * 2f);
            Assert.AreEqual(7f, bhop, 1e-4f, "capped");
            Assert.Less(GunMath.Spread(g, 6f, true), running * 0.3f);
            var scout = new GunBallistics { SpreadDegrees = 0.15f, MoveSpreadPerMps = 0f, MaxSpreadDegrees = 0.15f, AdsSpreadFactor = 0.5f };
            Assert.AreEqual(GunMath.Spread(scout, 0f, false), GunMath.Spread(scout, 20f, false), "scout stays accurate on the move");
        }

        [Test]
        public void InCone_StaysInsideTheCone()
        {
            var rng = new Random(5);
            Vector3 f = Vector3.Normalize(new Vector3(0.3f, -0.1f, 1f));
            for (int i = 0; i < 500; i++)
            {
                Vector3 d = GunMath.InCone(f, 3f, (float)rng.NextDouble(), (float)rng.NextDouble());
                float angle = MathF.Acos(Math.Clamp(Vector3.Dot(d, f), -1f, 1f)) * 180f / MathF.PI;
                Assert.LessOrEqual(angle, 3.001f);
                Assert.AreEqual(1f, d.Length(), 1e-4f);
            }
        }

        [Test]
        public void Bullet_FliesItsRangeInSteps_WithALittleDrop()
        {
            GunBallistics g = Revolver;
            Bullet b = GunMath.Fire(Vector3.Zero, Vector3.UnitZ, g, 150f);
            int steps = 0;
            float total = 0f;
            while (!b.Done && steps < 1000)
            {
                GunMath.Step(ref b, g, 1f / 64f, out _, out _, out float length);
                total += length;
                steps++;
            }
            Assert.That(steps, Is.InRange(26, 28), "150 m at 360 m/s is about 0.42 s, not instant");
            Assert.AreEqual(150f, total, 0.01f);
            Assert.That(b.Position.Y, Is.InRange(-0.6f, -0.1f), "drops a little");
        }

        [Test]
        public void HeavyWeapons_SlowYouDown_LightOnesDont()
        {
            var cfg = new MoveConfig();
            float Speed(int load)
            {
                var world = new BrushWorld();
                world.Add(Brush.Box(new Vector3(0, -0.5f, 0), new Vector3(200, 0.5f, 200)));
                var s = new MoveState { Position = new Vector3(0, cfg.HullHalfExtents.Y + 0.01f, 0) };
                var cmd = new MoveCmd { Forward = 1f, Buttons = MoveCmd.LoadButtons(load) };
                for (int i = 0; i < 64 * 3; i++) PlayerMove.Step(ref s, in cmd, cfg, world, 1f / 64f);
                return PlayerMove.HorizontalSpeed(s.Velocity);
            }
            Assert.AreEqual(cfg.MaxSpeed, Speed(0), 0.1f);
            Assert.AreEqual(cfg.MaxSpeed * (1f - cfg.LoadSlowPerLevel), Speed(1), 0.1f);
            Assert.AreEqual(cfg.MaxSpeed * (1f - 3f * cfg.LoadSlowPerLevel), Speed(3), 0.1f);
            Assert.AreEqual(2, new MoveCmd { Buttons = MoveButtons.Sprint | MoveCmd.LoadButtons(2) }.Load, "load bits don't clash with the others");
        }
    }
}
