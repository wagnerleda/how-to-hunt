#nullable enable
using System;
using System.Numerics;
using HowToHunt.Sim.Collision;

namespace HowToHunt.Sim.Hunting
{
    /// <summary>A thrown, dropped or flung item (carcass, catch). Small, copyable, deterministic.</summary>
    public struct ItemBody
    {
        public Vector3 Position; // centre
        public Vector3 Velocity;
        public bool Resting;
        /// <summary>Visual spin while airborne, degrees (cosmetic, but deterministic too).</summary>
        public float Spin;
    }

    /// <summary>
    /// Item motion against the brush world: gravity, bounces that lose most of their energy, friction on the ground,
    /// and a rest state so settled items cost nothing. Same code and same world on every machine, so clients
    /// simulate thrown items themselves from the spawn message.
    /// </summary>
    public static class ItemPhysics
    {
        public const float Gravity = 20f;
        private const float Restitution = 0.3f;
        private const float GroundFriction = 6f;
        private const float RestSpeed = 0.35f;

        public static void Step(ref ItemBody b, ICollisionWorld world, Vector3 halfExtents, float dt)
        {
            if (b.Resting) return;
            b.Velocity.Y -= Gravity * dt;
            Vector3 remaining = b.Velocity * dt;
            bool grounded = false;
            for (int bump = 0; bump < 3 && remaining.LengthSquared() > 1e-8f; bump++)
            {
                TraceResult tr = world.TraceBox(b.Position, b.Position + remaining, halfExtents);
                if (tr.StartSolid)
                {
                    // Spawned overlapping something (thrown point-blank into a wall): pop up and stop.
                    b.Position.Y += 0.05f;
                    b.Velocity = Vector3.Zero;
                    return;
                }
                b.Position = tr.EndPosition;
                if (!tr.Hit) break;
                Vector3 n = tr.Normal;
                float into = Vector3.Dot(b.Velocity, n);
                if (into < 0f) b.Velocity -= (1f + Restitution) * into * n;
                if (n.Y > 0.7f) grounded = true;
                remaining = (remaining * (1f - tr.Fraction));
                remaining -= Vector3.Dot(remaining, n) * n;
            }
            if (grounded)
            {
                var flat = new Vector3(b.Velocity.X, 0f, b.Velocity.Z);
                float speed = flat.Length();
                float drop = GroundFriction * dt;
                flat = speed > drop ? flat * ((speed - drop) / speed) : Vector3.Zero;
                b.Velocity = new Vector3(flat.X, b.Velocity.Y, flat.Z);
                if (b.Velocity.Length() < RestSpeed)
                {
                    b.Velocity = Vector3.Zero;
                    b.Resting = true;
                }
            }
            if (!b.Resting) b.Spin += 540f * dt * MathF.Min(1f, b.Velocity.Length() / 6f);
        }
    }
}
