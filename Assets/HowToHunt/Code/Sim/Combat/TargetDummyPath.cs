#nullable enable
using System;
using System.Numerics;

namespace HowToHunt.Sim.Combat
{
    /// <summary>
    /// A target dummy's motion as a pure function of the server tick (same idea as the ambient insects: seed + time,
    /// no movement packets). Host and clients compute the same pose for any tick; clients draw it at the tick they
    /// see, and the host rewinds to that tick when resolving a shot.
    /// </summary>
    public readonly struct TargetDummyPath
    {
        public readonly Vector3 Start;
        public readonly Vector3 End;
        public readonly float Speed;

        public TargetDummyPath(Vector3 start, Vector3 end, float speed)
        {
            Start = start;
            End = end;
            Speed = speed;
        }

        /// <summary>Feet position and facing at a (fractional) tick: back and forth with an ease at each end.</summary>
        public EntityPose PoseAt(double tick, int tickRate)
        {
            float length = Vector3.Distance(Start, End);
            if (length < 1e-4f || Speed <= 0f) return new EntityPose { Position = Start, Yaw = 0f };
            double period = 2.0 * length / Speed;
            double t = (tick / tickRate) % period;
            if (t < 0) t += period;
            bool outbound = t < period * 0.5;
            float u = (float)(outbound ? t / (period * 0.5) : 2.0 - t / (period * 0.5));
            float eased = u * u * (3f - 2f * u) * 0.25f + u * 0.75f; // mostly constant speed, soft turnarounds
            Vector3 dir = Vector3.Normalize(End - Start) * (outbound ? 1f : -1f);
            return new EntityPose
            {
                Position = Vector3.Lerp(Start, End, eased),
                Yaw = MathF.Atan2(dir.X, dir.Z) * 180f / MathF.PI,
            };
        }

        /// <summary>Standard humanoid-sized dummy hitboxes (feet at the origin): body capsule and head.</summary>
        public static readonly HitCapsule[] Hitboxes =
        {
            new HitCapsule(new Vector3(0f, 0.35f, 0f), new Vector3(0f, 1.25f, 0f), 0.32f, HitRegion.Body),
            new HitCapsule(new Vector3(0f, 1.55f, 0f), new Vector3(0f, 1.62f, 0f), 0.17f, HitRegion.Head),
        };
    }
}
