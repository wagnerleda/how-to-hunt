#nullable enable
using System;
using System.Numerics;

namespace HowToHunt.Sim.Movement
{
    [Flags]
    public enum MoveButtons : byte
    {
        None = 0,
        Jump = 1 << 0,
        Duck = 1 << 1,
        /// <summary>Shift: a modest ground-speed boost for players who don't bunny hop (bit 2, once Walk).</summary>
        Sprint = 1 << 2,
        /// <summary>Per-player accessibility option: holding jump keeps hopping. Sent with every cmd so host and client agree.</summary>
        AutoHop = 1 << 3,
        /// <summary>Two bits: how heavy the held weapon is (0 light .. 3 very heavy). Heavy weapons slow you down.</summary>
        LoadLow = 1 << 4,
        LoadHigh = 1 << 5,
    }

    public enum MoveMode : byte
    {
        Walk = 0,
        /// <summary>Fly through everything (cheat, host-granted).</summary>
        Noclip = 1,
    }

    /// <summary>What happened during one step. Feedback (camera, sound, particles) reads these; they don't affect simulation.</summary>
    [Flags]
    public enum MoveEvents : byte
    {
        None = 0,
        Jumped = 1 << 0,
        Landed = 1 << 1,
        Ducked = 1 << 2,
        Unducked = 1 << 3,
    }

    /// <summary>One tick of player intent. This is what clients send to the host (a "usercmd").</summary>
    public struct MoveCmd
    {
        /// <summary>-1..1, forward positive.</summary>
        public float Forward;
        /// <summary>-1..1, right positive.</summary>
        public float Side;
        /// <summary>View yaw in degrees (0 = +Z, 90 = +X).</summary>
        public float Yaw;
        /// <summary>View pitch in degrees (positive looks down).</summary>
        public float Pitch;
        public MoveButtons Buttons;
        /// <summary>A gun's shove this tick, m/s backwards along the view (How to Fish knockback). 0 = none.</summary>
        public float Push;

        public bool Has(MoveButtons b) => (Buttons & b) != 0;

        /// <summary>Held weapon weight class 0..3 (MoveButtons.LoadLow/LoadHigh).</summary>
        public int Load => ((int)Buttons >> 4) & 3;

        public static MoveButtons LoadButtons(int level) => (MoveButtons)((Math.Clamp(level, 0, 3) & 3) << 4);
    }

    /// <summary>Complete movement state of one player. Everything prediction must restore on reconcile.</summary>
    public struct MoveState
    {
        /// <summary>Center of the collision hull (standing or ducked).</summary>
        public Vector3 Position;
        public Vector3 Velocity;
        public bool OnGround;
        /// <summary>Jump was held last tick (without auto-hop, the player must release and re-press).</summary>
        public bool JumpHeld;
        public bool Ducked;
        public MoveMode Mode;
        /// <summary>Seconds left slowed by a thrown item (set by the host, reaches the owner by reconciliation).</summary>
        public float SlowTime;
        /// <summary>Seconds of sprint used up (0 = fresh). Sprint stops at MoveConfig.SprintStamina; it recovers while not sprinting.</summary>
        public float Fatigue;
        /// <summary>Seconds on the ground since the last landing (landing grace: a late bunny hop keeps most of its speed).</summary>
        public float GroundTime;
        /// <summary>Seconds before another gun shove is accepted (so a command can't shove every tick).</summary>
        public float PushCooldown;
        /// <summary>Seconds left knocked back (a cactus prick): no air control, so the shove carries you off.</summary>
        public float KnockTime;

        // Derived/feedback fields (not needed to resume simulation).
        /// <summary>Normal of the surface under or against the player (for surf/feel feedback).</summary>
        public Vector3 GroundNormal;
        public MoveEvents Events;
        /// <summary>Downward speed at the moment of landing (m/s), valid when Events has Landed.</summary>
        public float LandSpeed;
        /// <summary>Feet in a water volume this tick (slower; splashes instead of footsteps).</summary>
        public bool InWater;

        /// <summary>Airborne while touching a surface too steep to stand on.</summary>
        public bool Surfing => !OnGround && GroundNormal.Y > 0.05f && GroundNormal.Y < 0.7f;
    }

    /// <summary>
    /// Movement tunables in meters and seconds. Defaults approximate CS:GO (1 Source unit ≈ 0.0254 m) with
    /// KZ/surf-style air acceleration. Every field is exposed as a console variable by the movement feature.
    /// </summary>
    public sealed class MoveConfig
    {
        public float Gravity = 20.32f;            // sv_gravity 800
        public float Friction = 5.2f;             // sv_friction
        /// <summary>Just after landing, friction is scaled by LandGraceFriction for LandGraceSeconds: missing a bunny hop by a
        /// few ticks costs a little speed, not most of it.</summary>
        public float LandGraceSeconds = 0.2f;
        public float LandGraceFriction = 0.15f;
        public float StopSpeed = 2.032f;          // sv_stopspeed 80
        public float Accelerate = 5.5f;           // sv_accelerate
        public float AirAccelerate = 250f;        // sv_airaccelerate: forgiving for new players (KZ 100, surf 150, CS:GO MM 12)
        public float AirSpeedCap = 0.762f;        // 30 u/s: the bhop/strafe "wishspeed" cap in the air
        public float MaxSpeed = 6.35f;            // 250 u/s (knife run speed)
        public float DuckSpeedFactor = 0.34f;
        /// <summary>Ground speed while sprinting (x MaxSpeed). Air speed is capped separately, so bhop stays faster.</summary>
        public float SprintFactor = 1.25f;
        /// <summary>Seconds of sprint from fresh, and seconds of fatigue recovered per second while not sprinting.</summary>
        public float SprintStamina = 3f;
        public float StaminaRegen = 0.5f;
        /// <summary>Ground speed while wading (x MaxSpeed), and horizontal drag per second in water.</summary>
        public float WaterSpeedFactor = 0.55f;
        public float WaterDrag = 3f;
        /// <summary>Speed multiplier while slowed by a thrown item (MoveState.SlowTime).</summary>
        public float HitSlowFactor = 0.9f;
        /// <summary>Ground speed lost per weapon weight class (MoveCmd.Load), as a fraction.</summary>
        public float LoadSlowPerLevel = 0.06f;
        public float JumpSpeed = 7.67f;           // 301.99 u/s
        public float MaxVelocity = 88.9f;         // sv_maxvelocity 3500
        /// <summary>Surfaces with normal.y below this are too steep to stand on: you slide (surf).</summary>
        public float StandableNormalY = 0.7f;
        public float StepHeight = 0.457f;         // 18 units
        public bool AutoHop;
        /// <summary>Half extents of the standing hull (32x72 units).</summary>
        public Vector3 HullHalfExtents = new Vector3(0.4064f, 0.9144f, 0.4064f);
        /// <summary>Half height of the ducked hull (54 units).</summary>
        public float DuckHalfHeight = 0.6858f;
        /// <summary>Eye height above the bottom of the hull (64 units standing, 46 ducked).</summary>
        public float EyeHeight = 1.6256f;
        public float DuckEyeHeight = 1.1684f;
        public float NoclipSpeed = 19f;
        /// <summary>Strongest gun shove accepted, m/s, and how often (seconds).</summary>
        public float MaxPush = 22f;
        public float PushInterval = 0.15f;

        public Vector3 Hull(bool ducked) => ducked ? new Vector3(HullHalfExtents.X, DuckHalfHeight, HullHalfExtents.Z) : HullHalfExtents;

        /// <summary>Eye position relative to the hull center.</summary>
        public float EyeOffset(bool ducked) => ducked ? DuckEyeHeight - DuckHalfHeight : EyeHeight - HullHalfExtents.Y;

        public MoveConfig Clone() => (MoveConfig)MemberwiseClone();
    }
}
