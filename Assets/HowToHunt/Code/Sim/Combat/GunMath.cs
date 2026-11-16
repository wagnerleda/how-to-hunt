#nullable enable
using System;
using System.Numerics;

namespace HowToHunt.Sim.Combat
{
    /// <summary>Firearm numbers from content (per gun).</summary>
    public struct GunBallistics
    {
        /// <summary>Muzzle speed, m/s.</summary>
        public float BulletSpeed;
        /// <summary>Downward pull on the bullet, m/s² (a little: rounds stay fast and mostly straight).</summary>
        public float BulletGravity;
        /// <summary>Full damage up to this distance (m) ...</summary>
        public float FalloffStart;
        /// <summary>... dropping linearly to MinDamageFraction at this distance and beyond.</summary>
        public float FalloffEnd;
        public float MinDamageFraction;
        /// <summary>Hip-fire cone half-angle at rest, degrees.</summary>
        public float SpreadDegrees;
        /// <summary>Extra cone per m/s of the shooter's speed (0: a scout rifle stays accurate on the move).</summary>
        public float MoveSpreadPerMps;
        public float MaxSpreadDegrees;
        /// <summary>Cone multiplier while aiming down sights.</summary>
        public float AdsSpreadFactor;
    }

    /// <summary>A bullet in flight (host: hits; clients: tracer). Pure, deterministic stepping.</summary>
    public struct Bullet
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Travelled;
        public float Range;
        public bool Done;
    }

    public static class GunMath
    {
        /// <summary>Damage multiplier at this distance: 1 near, falling linearly to MinDamageFraction past FalloffEnd.</summary>
        public static float Falloff(in GunBallistics g, float distance)
        {
            if (distance <= g.FalloffStart) return 1f;
            if (g.FalloffEnd <= g.FalloffStart) return g.MinDamageFraction;
            float t = Math.Clamp((distance - g.FalloffStart) / (g.FalloffEnd - g.FalloffStart), 0f, 1f);
            return 1f + (g.MinDamageFraction - 1f) * t;
        }

        /// <summary>Cone half-angle (degrees) for a shot: grows with speed (unless the gun ignores it), shrinks when aiming.</summary>
        public static float Spread(in GunBallistics g, float speed, bool aiming)
        {
            float cone = g.SpreadDegrees + MathF.Max(0f, speed) * g.MoveSpreadPerMps;
            if (g.MaxSpreadDegrees > 0f) cone = MathF.Min(cone, g.MaxSpreadDegrees);
            if (aiming) cone *= g.AdsSpreadFactor;
            return cone;
        }

        /// <summary>
        /// Tilt <paramref name="dir"/> by a random angle inside a cone of <paramref name="halfAngleDeg"/>, uniform over the
        /// disc. <paramref name="u1"/> and <paramref name="u2"/> are 0..1 randoms.
        /// </summary>
        public static Vector3 InCone(Vector3 dir, float halfAngleDeg, float u1, float u2)
        {
            if (halfAngleDeg <= 0f) return Vector3.Normalize(dir);
            Vector3 f = Vector3.Normalize(dir);
            Vector3 helper = MathF.Abs(f.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 right = Vector3.Normalize(Vector3.Cross(helper, f));
            Vector3 up = Vector3.Cross(f, right);
            float r = MathF.Tan(halfAngleDeg * MathF.PI / 180f) * MathF.Sqrt(u1);
            float a = u2 * MathF.PI * 2f;
            return Vector3.Normalize(f + right * (r * MathF.Cos(a)) + up * (r * MathF.Sin(a)));
        }

        public static Bullet Fire(Vector3 origin, Vector3 dir, in GunBallistics g, float range) => new Bullet
        {
            Position = origin,
            Velocity = Vector3.Normalize(dir) * g.BulletSpeed,
            Range = range,
        };

        /// <summary>
        /// Advance one step. Returns the segment to test (from <paramref name="from"/>, along <paramref name="dir"/>,
        /// <paramref name="length"/> m); the bullet is Done once it has flown its range.
        /// </summary>
        public static void Step(ref Bullet b, in GunBallistics g, float dt, out Vector3 from, out Vector3 dir, out float length)
        {
            from = b.Position;
            b.Velocity.Y -= g.BulletGravity * dt;
            Vector3 delta = b.Velocity * dt;
            length = delta.Length();
            if (b.Travelled + length >= b.Range)
            {
                length = MathF.Max(0f, b.Range - b.Travelled);
                b.Done = true;
            }
            dir = length > 1e-6f ? Vector3.Normalize(delta) : Vector3.Normalize(b.Velocity);
            b.Position = from + dir * length;
            b.Travelled += length;
        }
    }
}
