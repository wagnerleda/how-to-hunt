#nullable enable
using System;
using System.Numerics;
using HowToHunt.Sim.Collision;

namespace HowToHunt.Sim.Hunting
{
    public enum PreyAiState : byte { Idle, Wander, Flee, Stunned, Captured, Dead }

    /// <summary>Behavior profile numbers (from content).</summary>
    public struct PreyBehavior
    {
        public float WalkSpeed;
        public float FleeSpeed;
        public float FleeRadius;
        public float CalmDownSeconds;
        public float HomeRadius;
        public float StunSeconds;
        public Vector3 HalfExtents;
        /// <summary>Seconds of full-speed flight before tiring (0 = never tires).</summary>
        public float StaminaSeconds;
        /// <summary>Flee speed once tired: below a running player, so a chase pays off.</summary>
        public float TiredSpeed;
        /// <summary>Flees from a player it can see this far away (clear line of sight). 0 = only the close FleeRadius.</summary>
        public float SightRange;
        /// <summary>Flees from gunshots heard within this distance (0 = unbothered: predators, deaf prey).</summary>
        public float HearingRange;
    }

    /// <summary>Everything the host simulates for one ground prey. Small, copyable, deterministic.</summary>
    public struct PreyState
    {
        public Vector3 Position;   // feet
        public float Yaw;
        public Vector3 Home;
        public PreyAiState Ai;
        public float Timer;
        public float Health;
        public uint Rng;
        public Vector3 MoveDir;
        public float LastThreatTime;
        public float Speed;        // current horizontal speed (animation)
        public float Stamina;      // seconds of sprint left
        public float JinkTimer;    // seconds until the next sharp turn while fleeing
        public Vector3 Knock;      // horizontal shove from a hit, m/s, fades quickly
    }

    /// <summary>
    /// Cheap bounded prey AI: idle, wander near home, flee from the nearest player, stunned when hit.
    /// Moves with box traces against the brush world and snaps to the ground. Pure and deterministic.
    /// </summary>
    public static class PreyBrain
    {
        private const float Gravity = 20f;
        private const float StepHeight = 0.45f;
        private const float KnockDrag = 7f;       // per second
        private const float KnockStopSpeed = 0.1f;

        public static void Step(ref PreyState s, in PreyBehavior b, Vector3 nearestThreat, bool hasThreat, ICollisionWorld world, float time, float dt)
            => Step(ref s, in b, nearestThreat, hasThreat, false, world, time, dt);

        /// <param name="seesThreat">The prey has spotted the threat (see <see cref="CanSee"/>): flee even beyond FleeRadius.</param>
        public static void Step(ref PreyState s, in PreyBehavior b, Vector3 nearestThreat, bool hasThreat, bool seesThreat, ICollisionWorld world, float time, float dt)
        {
            if (s.Ai == PreyAiState.Dead || s.Ai == PreyAiState.Captured) return;

            float threatDist = hasThreat ? Horizontal(nearestThreat - s.Position).Length() : float.MaxValue;
            if (s.Ai == PreyAiState.Stunned)
            {
                s.Timer -= dt;
                if (s.Timer <= 0f) Enter(ref s, PreyAiState.Flee, 2f);
            }
            else if (threatDist < b.FleeRadius || (hasThreat && seesThreat))
            {
                s.LastThreatTime = time;
                if (s.Ai != PreyAiState.Flee) Enter(ref s, PreyAiState.Flee, b.CalmDownSeconds);
                Vector3 away = Horizontal(s.Position - nearestThreat);
                s.JinkTimer -= dt;
                if (away.LengthSquared() > 1e-4f && s.JinkTimer <= 0f)
                {
                    // Jink: a sharp turn every second or so (left or right of straight away), so chases need aim and speed.
                    float side = Rand01(ref s.Rng) < 0.5f ? -1f : 1f;
                    s.MoveDir = Rotate(Vector3.Normalize(away), side * (0.35f + Rand01(ref s.Rng) * 0.45f));
                    s.JinkTimer = 0.7f + Rand01(ref s.Rng) * 0.6f;
                }
            }

            switch (s.Ai)
            {
                case PreyAiState.Idle:
                    s.Speed = 0f;
                    s.Timer -= dt;
                    if (s.Timer <= 0f)
                    {
                        Enter(ref s, PreyAiState.Wander, 1.5f + Rand01(ref s.Rng) * 2.5f);
                        float angle = Rand01(ref s.Rng) * MathF.PI * 2f;
                        s.MoveDir = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
                        Vector3 toHome = Horizontal(s.Home - s.Position);
                        if (toHome.Length() > b.HomeRadius) s.MoveDir = Vector3.Normalize(toHome);
                    }
                    break;
                case PreyAiState.Wander:
                    s.Speed = b.WalkSpeed;
                    s.Timer -= dt;
                    if (s.Timer <= 0f) Enter(ref s, PreyAiState.Idle, 1f + Rand01(ref s.Rng) * 2.5f);
                    break;
                case PreyAiState.Flee:
                    // Sprint faster than anyone can run, then tire: runners lose them, bunny hoppers catch them.
                    if (b.StaminaSeconds > 0f) s.Stamina -= dt;
                    s.Speed = b.StaminaSeconds <= 0f || s.Stamina > 0f ? b.FleeSpeed : b.TiredSpeed;
                    if (time - s.LastThreatTime > b.CalmDownSeconds) Enter(ref s, PreyAiState.Idle, 0.5f);
                    break;
                case PreyAiState.Stunned:
                    s.Speed = 0f;
                    break;
            }

            if (s.Ai != PreyAiState.Flee && b.StaminaSeconds > 0f) s.Stamina = MathF.Min(b.StaminaSeconds, s.Stamina + dt * 0.5f);

            Move(ref s, b, world, dt);
        }

        public static void Stun(ref PreyState s, in PreyBehavior b) => Enter(ref s, PreyAiState.Stunned, b.StunSeconds);

        /// <summary>A hit shoves the prey along the ground (m/s, horizontal); it slides to a stop within half a second.</summary>
        public static void Knock(ref PreyState s, Vector3 velocity) => s.Knock += Horizontal(velocity);

        /// <summary>Eye height above the feet.</summary>
        public static float EyeHeight(in PreyBehavior b) => b.HalfExtents.Y * 2f + 0.05f;

        /// <summary>
        /// Can the prey see a player's eye? Within SightRange × rangeScale (crouching players are harder to spot)
        /// and with nothing solid in between (trees, rocks, hills).
        /// </summary>
        public static bool CanSee(in PreyState s, in PreyBehavior b, Vector3 threatEye, float rangeScale, ICollisionWorld world)
        {
            float range = b.SightRange * rangeScale;
            if (range <= 0f) return false;
            Vector3 eye = s.Position + new Vector3(0f, EyeHeight(b), 0f);
            if (Vector3.DistanceSquared(eye, threatEye) > range * range) return false;
            return !world.TraceBox(eye, threatEye, Vector3.Zero).Hit;
        }

        /// <summary>A loud noise (gunshot) at <paramref name="source"/>: bolt away from it if within hearing range.</summary>
        public static bool Startle(ref PreyState s, in PreyBehavior b, Vector3 source, float time)
        {
            if (s.Ai == PreyAiState.Dead || s.Ai == PreyAiState.Captured || s.Ai == PreyAiState.Stunned) return false;
            if (b.HearingRange <= 0f) return false;
            Vector3 away = Horizontal(s.Position - source);
            if (away.LengthSquared() > b.HearingRange * b.HearingRange) return false;
            s.LastThreatTime = time;
            if (s.Ai != PreyAiState.Flee) Enter(ref s, PreyAiState.Flee, b.CalmDownSeconds);
            if (away.LengthSquared() > 1e-4f) s.MoveDir = Vector3.Normalize(away);
            s.JinkTimer = 0.7f;
            return true;
        }

        private static void Move(ref PreyState s, in PreyBehavior b, ICollisionWorld world, float dt)
        {
            Vector3 ext = b.HalfExtents;
            Vector3 center = s.Position + new Vector3(0f, ext.Y + 0.02f, 0f);
            bool walking = s.Speed > 0f && s.MoveDir.LengthSquared() > 1e-4f;
            bool knocked = s.Knock.LengthSquared() > KnockStopSpeed * KnockStopSpeed;
            if (walking || knocked)
            {
                // Step up first (hills, small ledges), move, then drop back onto the ground below.
                TraceResult up = world.TraceBox(center, center + new Vector3(0f, StepHeight, 0f), ext);
                center = up.EndPosition;
                Vector3 delta = (walking ? s.MoveDir * s.Speed : Vector3.Zero) * dt + (knocked ? s.Knock * dt : Vector3.Zero);
                TraceResult tr = world.TraceBox(center, center + delta, ext);
                if (tr.Hit)
                {
                    // Blocked: turn away from the wall for next time; a knock stops dead.
                    if (walking) s.MoveDir = Vector3.Normalize(Horizontal(Vector3.Reflect(s.MoveDir, tr.Normal)) + new Vector3(0.001f, 0f, 0f));
                    s.Knock = Vector3.Zero;
                }
                center = tr.EndPosition;
                if (walking)
                {
                    float targetYaw = MathF.Atan2(s.MoveDir.X, s.MoveDir.Z) * 180f / MathF.PI;
                    s.Yaw = LerpAngle(s.Yaw, targetYaw, Math.Min(1f, dt * 10f));
                }
            }
            if (knocked) s.Knock *= MathF.Max(0f, 1f - KnockDrag * dt);
            else s.Knock = Vector3.Zero;
            // Ground snap / fall.
            TraceResult down = world.TraceBox(center, center - new Vector3(0f, 2f + StepHeight + Gravity * dt, 0f), ext);
            if (down.Hit && !down.StartSolid) center = down.EndPosition;
            s.Position = center - new Vector3(0f, ext.Y + 0.02f, 0f);
        }

        private static void Enter(ref PreyState s, PreyAiState state, float timer)
        {
            s.Ai = state;
            s.Timer = timer;
        }

        private static Vector3 Horizontal(Vector3 v) => new Vector3(v.X, 0f, v.Z);

        private static Vector3 Rotate(Vector3 v, float radians)
        {
            float c = MathF.Cos(radians), sn = MathF.Sin(radians);
            return new Vector3(v.X * c - v.Z * sn, 0f, v.X * sn + v.Z * c);
        }

        private static float LerpAngle(float a, float b, float t)
        {
            float d = ((b - a) % 360f + 540f) % 360f - 180f;
            return a + d * t;
        }

        /// <summary>xorshift32: deterministic per prey.</summary>
        public static float Rand01(ref uint state)
        {
            if (state == 0) state = 2463534242u;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / 16777216f;
        }
    }

    /// <summary>
    /// Butterflies: deterministic flight from (seed, tick) like the target dummies. Hundreds cost no bandwidth:
    /// only catch/kill events are sent; the host checks a shot or swing against the pose at the player's view tick.
    /// </summary>
    public static class ButterflyFlight
    {
        public static Combat.EntityPose PoseAt(uint seed, Vector3 home, float radius, double tick, int tickRate)
        {
            uint rng = seed * 747796405u + 2891336453u;
            float p1 = PreyBrain.Rand01(ref rng) * 6.283f, p2 = PreyBrain.Rand01(ref rng) * 6.283f, p3 = PreyBrain.Rand01(ref rng) * 6.283f;
            float f1 = 0.13f + PreyBrain.Rand01(ref rng) * 0.1f, f2 = 0.11f + PreyBrain.Rand01(ref rng) * 0.1f;
            double t = tick / tickRate;
            Vector3 pos = Sample(home, radius, t, p1, p2, p3, f1, f2);
            Vector3 ahead = Sample(home, radius, t + 0.05, p1, p2, p3, f1, f2);
            Vector3 d = ahead - pos;
            return new Combat.EntityPose { Position = pos, Yaw = MathF.Atan2(d.X, d.Z) * 180f / MathF.PI };
        }

        private static Vector3 Sample(Vector3 home, float radius, double t, float p1, float p2, float p3, float f1, float f2)
        {
            double a = t * f1 * 2 * Math.PI + p1;
            double b = t * f2 * 2 * Math.PI + p2;
            float x = (float)(Math.Sin(a) + 0.35 * Math.Sin(a * 3.1 + p3)) * radius * 0.75f;
            float z = (float)(Math.Cos(b) + 0.35 * Math.Cos(b * 2.7 + p1)) * radius * 0.75f;
            float y = 1.1f + 0.45f * (float)Math.Sin(t * 1.9 + p3) + 0.12f * (float)Math.Sin(t * 11.0 + p2); // flutter
            return home + new Vector3(x, y, z);
        }
    }
}
