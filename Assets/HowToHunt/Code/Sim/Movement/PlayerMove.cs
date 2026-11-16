#nullable enable
using System;
using System.Numerics;
using HowToHunt.Sim.Collision;

namespace HowToHunt.Sim.Movement
{
    /// <summary>
    /// Source-style kinematic player movement: ground friction and acceleration, air strafing,
    /// speed kept across jumps (bunny hop), and velocity clipped against steep planes (surf).
    /// Pure function of (state, cmd, config, world, dt): used identically by client prediction,
    /// the host simulation, bots and replay tests. Allocation-free.
    /// </summary>
    public static class PlayerMove
    {
        private const float Deg2Rad = MathF.PI / 180f;
        private const float GroundProbe = 0.06f;
        /// <summary>Moving up faster than this means we're airborne even if near the ground (140 u/s).</summary>
        private const float LeaveGroundUpSpeed = 3.556f;
        private const int MaxBumps = 4;
        private const int MaxClipPlanes = 5;
        private const float OverClip = 1.001f;
        /// <summary>1 Source unit/s, used to push off a plane we keep touching.</summary>
        private const float NudgeSpeed = 0.0254f;

        public static void Step(ref MoveState s, in MoveCmd cmd, MoveConfig cfg, ICollisionWorld world, float dt)
        {
            s.Events = MoveEvents.None;
            s.LandSpeed = 0f;

            if (s.Mode == MoveMode.Noclip)
            {
                NoclipMove(ref s, in cmd, cfg, dt);
                return;
            }

            CategorizePosition(ref s, cfg, world);
            bool wasOnGround = s.OnGround;

            UpdateDuck(ref s, in cmd, cfg, world);

            // Wading: feet a little above the bottom of the hull are under water.
            s.InWater = world.InWater(s.Position - new Vector3(0f, cfg.Hull(s.Ducked).Y - 0.3f, 0f));
            ComputeWish(in cmd, cfg, s.Ducked && s.OnGround, out Vector3 wishDir, out float wishSpeed);
            if (s.OnGround && !s.Ducked && cmd.Has(MoveButtons.Sprint) && cmd.Forward > 0.1f) wishSpeed *= cfg.SprintFactor;
            if (s.InWater) wishSpeed *= cfg.WaterSpeedFactor;

            bool jumpDown = cmd.Has(MoveButtons.Jump);
            bool autoHop = cfg.AutoHop || cmd.Has(MoveButtons.AutoHop);
            bool jumpPressed = jumpDown && (autoHop || !s.JumpHeld);
            s.JumpHeld = jumpDown;

            // Jump is checked before friction: landing and jumping on the same tick skips friction (bunny hop).
            if (s.OnGround && jumpPressed)
            {
                s.Velocity.Y = cfg.JumpSpeed;
                s.OnGround = false;
                s.Events |= MoveEvents.Jumped;
            }

            if (s.InWater)
            {
                float keep = MathF.Max(0f, 1f - cfg.WaterDrag * dt);
                s.Velocity.X *= keep;
                s.Velocity.Z *= keep;
            }

            Vector3 ext = cfg.Hull(s.Ducked);
            float fallSpeed = 0f;
            if (s.OnGround)
            {
                s.Velocity.Y = 0f;
                ApplyFriction(ref s.Velocity, cfg, dt);
                Accelerate(ref s.Velocity, wishDir, wishSpeed, cfg.Accelerate, dt);
                WalkMove(ref s, ext, cfg, world, dt);
            }
            else
            {
                AirAccelerate(ref s.Velocity, wishDir, wishSpeed, cfg, dt);
                s.Velocity.Y -= cfg.Gravity * dt * 0.5f;
                fallSpeed = -s.Velocity.Y; // before the floor clips it away
                FlyMove(ref s.Position, ref s.Velocity, ext, world, dt, airborne: true);
                s.Velocity.Y -= cfg.Gravity * dt * 0.5f;
            }

            CategorizePosition(ref s, cfg, world);
            if (!wasOnGround && s.OnGround && (s.Events & MoveEvents.Jumped) == 0)
            {
                s.Events |= MoveEvents.Landed;
                s.LandSpeed = MathF.Max(0f, fallSpeed);
            }
            if (s.OnGround && s.Velocity.Y < 0f) s.Velocity.Y = 0f;
            ClampVelocity(ref s.Velocity, cfg.MaxVelocity);
        }

        /// <summary>
        /// Ducking shrinks the hull. On the ground the head comes down; in the air the feet come up
        /// (Source-style crouch-jump to reach higher ledges). Standing up needs room.
        /// </summary>
        private static void UpdateDuck(ref MoveState s, in MoveCmd cmd, MoveConfig cfg, ICollisionWorld world)
        {
            bool wantDuck = cmd.Has(MoveButtons.Duck);
            if (wantDuck == s.Ducked) return;
            float delta = cfg.HullHalfExtents.Y - cfg.DuckHalfHeight;
            if (wantDuck)
            {
                s.Position.Y += s.OnGround ? -delta : delta;
                s.Ducked = true;
                s.Events |= MoveEvents.Ducked;
                return;
            }
            // Unduck: grow back the way we shrank, only if the standing hull fits.
            Vector3 standing = cfg.HullHalfExtents;
            Vector3 target = s.Position + new Vector3(0f, s.OnGround ? delta : -delta, 0f);
            TraceResult tr = world.TraceBox(target, target, standing);
            if (tr.StartSolid)
            {
                // In the air, try growing upward instead (e.g. near the ground).
                if (s.OnGround) return;
                target = s.Position + new Vector3(0f, delta, 0f);
                tr = world.TraceBox(target, target, standing);
                if (tr.StartSolid) return;
            }
            s.Position = target;
            s.Ducked = false;
            s.Events |= MoveEvents.Unducked;
        }

        private static void NoclipMove(ref MoveState s, in MoveCmd cmd, MoveConfig cfg, float dt)
        {
            float yaw = cmd.Yaw * Deg2Rad, pitch = cmd.Pitch * Deg2Rad;
            var look = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), -MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
            Vector3 wish = look * Math.Clamp(cmd.Forward, -1f, 1f) + Right(cmd.Yaw) * Math.Clamp(cmd.Side, -1f, 1f);
            if (cmd.Has(MoveButtons.Jump)) wish.Y += 1f;
            if (cmd.Has(MoveButtons.Duck)) wish.Y -= 1f;
            float speed = cfg.NoclipSpeed;
            s.Velocity = wish.LengthSquared() > 1e-6f ? Vector3.Normalize(wish) * speed : Vector3.Zero;
            s.Position += s.Velocity * dt;
            s.OnGround = false;
            s.GroundNormal = Vector3.Zero;
        }

        public static Vector3 Forward(float yawDegrees)
        {
            float y = yawDegrees * Deg2Rad;
            return new Vector3(MathF.Sin(y), 0f, MathF.Cos(y));
        }

        public static Vector3 Right(float yawDegrees)
        {
            float y = yawDegrees * Deg2Rad;
            return new Vector3(MathF.Cos(y), 0f, -MathF.Sin(y));
        }

        public static float HorizontalSpeed(in Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

        private static void ComputeWish(in MoveCmd cmd, MoveConfig cfg, bool duckedOnGround, out Vector3 wishDir, out float wishSpeed)
        {
            float f = Math.Clamp(cmd.Forward, -1f, 1f);
            float sd = Math.Clamp(cmd.Side, -1f, 1f);
            Vector3 wish = Forward(cmd.Yaw) * f + Right(cmd.Yaw) * sd;
            float len = wish.Length();
            if (len < 1e-5f)
            {
                wishDir = Vector3.Zero;
                wishSpeed = 0f;
                return;
            }
            wishDir = wish / len;
            wishSpeed = MathF.Min(len, 1f) * cfg.MaxSpeed;
            if (duckedOnGround) wishSpeed *= cfg.DuckSpeedFactor;
        }

        private static void ApplyFriction(ref Vector3 vel, MoveConfig cfg, float dt)
        {
            float speed = vel.Length();
            if (speed < 0.0025f)
            {
                vel = Vector3.Zero;
                return;
            }
            float control = speed < cfg.StopSpeed ? cfg.StopSpeed : speed;
            float drop = control * cfg.Friction * dt;
            float newSpeed = MathF.Max(speed - drop, 0f);
            vel *= newSpeed / speed;
        }

        private static void Accelerate(ref Vector3 vel, Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            float current = Vector3.Dot(vel, wishDir);
            float add = wishSpeed - current;
            if (add <= 0f) return;
            float accelSpeed = MathF.Min(accel * dt * wishSpeed, add);
            vel += wishDir * accelSpeed;
        }

        private static void AirAccelerate(ref Vector3 vel, Vector3 wishDir, float wishSpeed, MoveConfig cfg, float dt)
        {
            // The cap applies to the projected speed only, not to the acceleration amount:
            // this is what makes air strafing gain speed.
            float capped = MathF.Min(wishSpeed, cfg.AirSpeedCap);
            float current = Vector3.Dot(vel, wishDir);
            float add = capped - current;
            if (add <= 0f) return;
            float accelSpeed = MathF.Min(cfg.AirAccelerate * wishSpeed * dt, add);
            vel += wishDir * accelSpeed;
        }

        private static void CategorizePosition(ref MoveState s, MoveConfig cfg, ICollisionWorld world)
        {
            if (s.Velocity.Y > LeaveGroundUpSpeed)
            {
                s.OnGround = false;
                return;
            }
            Vector3 down = s.Position - new Vector3(0f, GroundProbe, 0f);
            TraceResult tr = world.TraceBox(s.Position, down, cfg.Hull(s.Ducked));
            s.GroundNormal = tr.Hit ? tr.Normal : Vector3.Zero;
            if (tr.Hit && !tr.AllSolid && tr.Normal.Y >= cfg.StandableNormalY)
            {
                s.OnGround = true;
                if (!tr.StartSolid) s.Position = tr.EndPosition;
            }
            else
            {
                s.OnGround = false;
            }
        }

        private static void WalkMove(ref MoveState s, Vector3 ext, MoveConfig cfg, ICollisionWorld world, float dt)
        {
            Vector3 dest = s.Position + s.Velocity * dt;
            TraceResult direct = world.TraceBox(s.Position, dest, ext);
            if (!direct.Hit && !direct.StartSolid)
            {
                s.Position = direct.EndPosition;
                StayOnGround(ref s, ext, cfg, world);
                return;
            }

            // Blocked: compare sliding along the obstacle with stepping up over it, keep whichever goes further.
            Vector3 origPos = s.Position;
            Vector3 origVel = s.Velocity;

            Vector3 downPos = origPos;
            Vector3 downVel = origVel;
            FlyMove(ref downPos, ref downVel, ext, world, dt);

            Vector3 upPos = origPos;
            Vector3 upVel = origVel;
            TraceResult up = world.TraceBox(upPos, upPos + new Vector3(0f, cfg.StepHeight, 0f), ext);
            if (!up.AllSolid) upPos = up.EndPosition;
            FlyMove(ref upPos, ref upVel, ext, world, dt);
            TraceResult stepDown = world.TraceBox(upPos, upPos - new Vector3(0f, cfg.StepHeight, 0f), ext);

            bool useDown = stepDown.AllSolid || !stepDown.Hit || stepDown.Normal.Y < cfg.StandableNormalY;
            if (!useDown)
            {
                upPos = stepDown.EndPosition;
                float downDist = Sq(downPos.X - origPos.X) + Sq(downPos.Z - origPos.Z);
                float upDist = Sq(upPos.X - origPos.X) + Sq(upPos.Z - origPos.Z);
                useDown = downDist > upDist;
            }

            if (useDown)
            {
                s.Position = downPos;
                s.Velocity = downVel;
            }
            else
            {
                s.Position = upPos;
                s.Velocity = new Vector3(upVel.X, downVel.Y, upVel.Z);
            }
            StayOnGround(ref s, ext, cfg, world);
        }

        /// <summary>Keeps the player glued to the ground when walking down slopes and stairs.</summary>
        private static void StayOnGround(ref MoveState s, Vector3 ext, MoveConfig cfg, ICollisionWorld world)
        {
            Vector3 start = s.Position + new Vector3(0f, 0.02f, 0f);
            TraceResult lift = world.TraceBox(s.Position, start, ext);
            start = lift.EndPosition;
            TraceResult tr = world.TraceBox(start, s.Position - new Vector3(0f, cfg.StepHeight, 0f), ext);
            if (tr.Fraction > 0f && tr.Fraction < 1f && !tr.StartSolid && tr.Normal.Y >= cfg.StandableNormalY)
                s.Position = tr.EndPosition;
        }

        /// <summary>Quake/Source "slide move": move, clip velocity against every plane hit, repeat.</summary>
        public static void FlyMove(ref Vector3 pos, ref Vector3 vel, Vector3 ext, ICollisionWorld world, float dt, bool airborne = false)
        {
            Span<Vector3> planes = stackalloc Vector3[MaxClipPlanes];
            int numPlanes = 0;
            Vector3 primal = vel;
            Vector3 original = vel;
            float timeLeft = dt;

            for (int bump = 0; bump < MaxBumps; bump++)
            {
                if (vel.LengthSquared() < 1e-10f) break;

                Vector3 end = pos + vel * timeLeft;
                TraceResult tr = world.TraceBox(pos, end, ext);
                if (tr.AllSolid)
                {
                    vel = Vector3.Zero;
                    return;
                }
                if (tr.Fraction > 0f)
                {
                    pos = tr.EndPosition;
                    original = vel;
                    numPlanes = 0;
                }
                if (tr.Fraction >= 1f) break;

                timeLeft -= timeLeft * tr.Fraction;

                if (numPlanes >= MaxClipPlanes)
                {
                    vel = Vector3.Zero;
                    break;
                }
                // Hitting the same plane again (we were already touching it): nudge away from it instead of
                // treating it as a second plane, which would stop us dead (Quake III does the same).
                bool duplicate = false;
                for (int k = 0; k < numPlanes; k++)
                {
                    if (Vector3.Dot(tr.Normal, planes[k]) > 0.99f)
                    {
                        vel += tr.Normal * NudgeSpeed;
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;
                planes[numPlanes++] = tr.Normal;

                // Source: airborne against a single plane, just clip (no "stop dead" check). This is what keeps
                // momentum while surfing; the multi-plane path below handles corners. The tiny over-clip keeps
                // float rounding from leaving us moving into the plane.
                if (airborne && numPlanes == 1)
                {
                    vel = ClipVelocity(original, planes[0], OverClip);
                    original = vel;
                    continue;
                }

                int i;
                Vector3 clipped = vel;
                for (i = 0; i < numPlanes; i++)
                {
                    clipped = ClipVelocity(original, planes[i], 1f);
                    int j;
                    for (j = 0; j < numPlanes; j++)
                        if (j != i && Vector3.Dot(clipped, planes[j]) < 0f) break;
                    if (j == numPlanes) break;
                }

                if (i != numPlanes)
                {
                    vel = clipped;
                }
                else
                {
                    if (numPlanes != 2)
                    {
                        vel = Vector3.Zero;
                        break;
                    }
                    // Slide along the crease between two planes.
                    Vector3 dir = Vector3.Cross(planes[0], planes[1]);
                    float lenSq = dir.LengthSquared();
                    if (lenSq < 1e-10f)
                    {
                        vel = Vector3.Zero;
                        break;
                    }
                    dir /= MathF.Sqrt(lenSq);
                    vel = dir * Vector3.Dot(dir, vel);
                }

                // Moving against the original direction: stop, to avoid jittering in corners.
                if (Vector3.Dot(vel, primal) <= 0f)
                {
                    vel = Vector3.Zero;
                    break;
                }
            }
        }

        /// <summary>Removes the component of velocity going into the plane. This is what makes surfing work.</summary>
        public static Vector3 ClipVelocity(Vector3 v, Vector3 normal, float overbounce)
        {
            float backoff = Vector3.Dot(v, normal) * overbounce;
            Vector3 result = v - normal * backoff;
            float adjust = Vector3.Dot(result, normal);
            if (adjust < 0f) result -= normal * adjust;
            return result;
        }

        private static void ClampVelocity(ref Vector3 v, float max)
        {
            v.X = Math.Clamp(v.X, -max, max);
            v.Y = Math.Clamp(v.Y, -max, max);
            v.Z = Math.Clamp(v.Z, -max, max);
        }

        private static float Sq(float x) => x * x;
    }
}
