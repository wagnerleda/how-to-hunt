#nullable enable
using System;
using System.Numerics;
using HowToHunt.Sim.Collision;

namespace HowToHunt.Sim.Combat
{
    public enum HitRegion : byte { Body = 0, Head = 1 }

    /// <summary>A capsule in an entity's local space (x right, y up, z forward).</summary>
    public readonly struct HitCapsule
    {
        public HitCapsule(Vector3 a, Vector3 b, float radius, HitRegion region)
        {
            A = a;
            B = b;
            Radius = radius;
            Region = region;
        }

        public readonly Vector3 A;
        public readonly Vector3 B;
        public readonly float Radius;
        public readonly HitRegion Region;
    }

    /// <summary>Where an entity was at one tick.</summary>
    public struct EntityPose
    {
        public Vector3 Position;
        public float Yaw;
    }

    /// <summary>
    /// Ring buffer of an entity's pose for the last second of ticks, so the host can rewind it to what a shooter saw
    /// (lag compensation). Fixed size, no allocations after construction.
    /// </summary>
    public sealed class PoseHistory
    {
        private readonly EntityPose[] _poses;
        private readonly uint[] _ticks;
        private uint _latest;
        private bool _any;

        public PoseHistory(int capacity = 128)
        {
            _poses = new EntityPose[capacity];
            _ticks = new uint[capacity];
        }

        public int Capacity => _poses.Length;

        public void Record(uint tick, in EntityPose pose)
        {
            int i = (int)(tick % (uint)_poses.Length);
            _poses[i] = pose;
            _ticks[i] = tick;
            if (!_any || tick > _latest) _latest = tick;
            _any = true;
        }

        /// <summary>Pose at a fractional tick, interpolated. False when the tick is outside the stored window.</summary>
        public bool TrySample(double tick, out EntityPose pose)
        {
            pose = default;
            if (!_any || tick < 0) return false;
            if (tick > _latest) tick = _latest;
            uint t0 = (uint)Math.Floor(tick);
            float f = (float)(tick - t0);
            if (!TryGet(t0, out EntityPose p0)) return false;
            if (f <= 0f || !TryGet(t0 + 1, out EntityPose p1))
            {
                pose = p0;
                return true;
            }
            pose.Position = Vector3.Lerp(p0.Position, p1.Position, f);
            pose.Yaw = LerpAngle(p0.Yaw, p1.Yaw, f);
            return true;
        }

        private bool TryGet(uint tick, out EntityPose pose)
        {
            int i = (int)(tick % (uint)_poses.Length);
            pose = _poses[i];
            return _any && _ticks[i] == tick;
        }

        private static float LerpAngle(float a, float b, float t)
        {
            float d = ((b - a) % 360f + 540f) % 360f - 180f;
            return a + d * t;
        }
    }

    public struct RayHit
    {
        public float Distance;
        public Vector3 Point;
        public int Target;      // index into the target list, -1 = world
        public HitRegion Region;
    }

    /// <summary>Hitscan resolution: world occlusion (brushes) plus capsule hitboxes at given poses.</summary>
    public static class HitScan
    {
        /// <summary>Closest hit along the ray, or false. Targets are capsule sets placed at their poses.</summary>
        public static bool Raycast(Vector3 origin, Vector3 dir, float range, ICollisionWorld world,
            ReadOnlySpan<EntityPose> poses, HitCapsule[] capsules, out RayHit hit)
        {
            hit = new RayHit { Distance = range, Target = -2 };
            dir = Vector3.Normalize(dir);

            TraceResult wall = world.TraceBox(origin, origin + dir * range, Vector3.Zero);
            if (wall.Hit && !wall.StartSolid)
            {
                hit.Distance = wall.Fraction * range;
                hit.Target = -1;
            }

            for (int t = 0; t < poses.Length; t++)
            {
                Quaternion rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, poses[t].Yaw * MathF.PI / 180f);
                for (int c = 0; c < capsules.Length; c++)
                {
                    HitCapsule cap = capsules[c];
                    Vector3 a = poses[t].Position + Vector3.Transform(cap.A, rot);
                    Vector3 b = poses[t].Position + Vector3.Transform(cap.B, rot);
                    if (RayCapsule(origin, dir, a, b, cap.Radius, out float d) && d < hit.Distance)
                    {
                        hit.Distance = d;
                        hit.Target = t;
                        hit.Region = cap.Region;
                    }
                }
            }

            if (hit.Target == -2) return false;
            hit.Point = origin + dir * hit.Distance;
            return true;
        }

        /// <summary>Ray (unit dir) vs capsule segment a-b. Returns the entry distance.</summary>
        public static bool RayCapsule(Vector3 ro, Vector3 rd, Vector3 a, Vector3 b, float r, out float distance)
        {
            distance = 0f;
            Vector3 ba = b - a;
            Vector3 oa = ro - a;
            float baba = Vector3.Dot(ba, ba);
            float bard = Vector3.Dot(ba, rd);
            float baoa = Vector3.Dot(ba, oa);
            float rdoa = Vector3.Dot(rd, oa);
            float oaoa = Vector3.Dot(oa, oa);
            float qa = baba - bard * bard;
            float qb = baba * rdoa - baoa * bard;
            float qc = baba * oaoa - baoa * baoa - r * r * baba;
            float h = qb * qb - qa * qc;
            float best = float.MaxValue;
            if (h >= 0f && qa > 1e-8f)
            {
                float t = (-qb - MathF.Sqrt(h)) / qa;
                float y = baoa + t * bard;
                if (t >= 0f && y > 0f && y < baba) best = t;
            }
            if (RaySphere(ro, rd, a, r, out float ta) && ta < best) best = ta;
            if (RaySphere(ro, rd, b, r, out float tb) && tb < best) best = tb;
            if (best == float.MaxValue) return false;
            distance = best;
            return true;
        }

        private static bool RaySphere(Vector3 ro, Vector3 rd, Vector3 center, float r, out float t)
        {
            Vector3 oc = ro - center;
            float b = Vector3.Dot(oc, rd);
            float c = Vector3.Dot(oc, oc) - r * r;
            float h = b * b - c;
            t = -b - MathF.Sqrt(MathF.Max(h, 0f));
            return h >= 0f && t >= 0f;
        }
    }
}
