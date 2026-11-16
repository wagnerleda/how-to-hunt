using HowToHunt.Foundation;
using HowToHunt.Sim.Movement;

namespace HowToHunt.Player
{
    /// <summary>
    /// Every movement tunable as a console variable (meters and seconds; Source-equivalent defaults).
    /// sv_* cvars are host-owned (Replicated) and cheat-protected so a client can't change its own physics.
    /// </summary>
    public static class MovementCvars
    {
        private const CvarFlags Sv = CvarFlags.Replicated | CvarFlags.Cheat;

        public static readonly MoveConfig Config = new MoveConfig();

        // Defaults come from MoveConfig so the cvars, tests and demos can never disagree.
        private static readonly MoveConfig D = new MoveConfig();

        public static readonly FloatCvar Gravity = new FloatCvar("sv_gravity", D.Gravity, "Gravity, m/s² (Source 800 u/s²).", Sv, 0f, 200f);
        public static readonly FloatCvar Friction = new FloatCvar("sv_friction", D.Friction, "Ground friction.", Sv, 0f, 50f);
        public static readonly FloatCvar StopSpeed = new FloatCvar("sv_stopspeed", D.StopSpeed, "Below this speed friction acts as if at this speed, m/s.", Sv, 0f, 20f);
        public static readonly FloatCvar Accelerate = new FloatCvar("sv_accelerate", D.Accelerate, "Ground acceleration.", Sv, 0f, 100f);
        public static readonly FloatCvar AirAccelerate = new FloatCvar("sv_airaccelerate", D.AirAccelerate, "Air acceleration (strafe gain). 250 = forgiving; KZ 100, surf 150, CS:GO 12.", Sv, 0f, 1000f);
        public static readonly FloatCvar AirSpeedCap = new FloatCvar("sv_air_max_wishspeed", D.AirSpeedCap, "Air wish-speed cap, m/s (Source 30 u/s).", Sv, 0f, 20f);
        public static readonly FloatCvar MaxSpeed = new FloatCvar("sv_maxspeed", D.MaxSpeed, "Ground run speed, m/s (Source 250 u/s).", Sv, 0f, 50f);
        public static readonly FloatCvar SprintFactor = new FloatCvar("sv_sprint_factor", D.SprintFactor, "Ground speed while sprinting, x sv_maxspeed.", Sv, 1f, 3f);
        public static readonly FloatCvar WaterSpeed = new FloatCvar("sv_water_speed", D.WaterSpeedFactor, "Ground speed while wading, x sv_maxspeed.", Sv, 0.1f, 1f);
        public static readonly FloatCvar ThrowSlow = new FloatCvar("sv_throw_slow", D.HitSlowFactor, "Speed multiplier for a moment after being hit by a thrown item (never a stun).", Sv, 0.1f, 1f);
        public static readonly FloatCvar LoadSlow = new FloatCvar("sv_load_slow", D.LoadSlowPerLevel, "Ground speed lost per weapon weight class while holding a heavy weapon (fraction).", Sv, 0f, 0.3f);
        public static readonly FloatCvar RecoilSettleSpeed = new FloatCvar("sv_recoil_follow", 10f, "How fast the view follows a weapon kick (the gun kicks in the hand first), per second.", Sv, 0.5f, 60f);
        public static readonly FloatCvar JumpSpeed = new FloatCvar("sv_jump_impulse", D.JumpSpeed, "Jump vertical speed, m/s (Source 301.99 u/s).", Sv, 0f, 50f);
        public static readonly FloatCvar MaxVelocity = new FloatCvar("sv_maxvelocity", D.MaxVelocity, "Per-axis speed limit, m/s.", Sv, 1f, 500f);
        public static readonly FloatCvar Standable = new FloatCvar("sv_standable_normal", D.StandableNormalY, "Surfaces with normal.y below this are surfable, not walkable.", Sv, 0f, 1f);
        public static readonly FloatCvar StepHeight = new FloatCvar("sv_stepsize", D.StepHeight, "Max step-up height, m.", Sv, 0f, 2f);
        public static readonly BoolCvar ServerAutoHop = new BoolCvar("sv_autobunnyhopping", false, "Force auto-hop for everyone.", Sv);
        public static readonly IntCvar ReconcileInterval = new IntCvar("sv_reconcile_interval", 3, "Host sends movement corrections every N ticks (1 = every tick).", CvarFlags.Replicated, 1, 16);
        public static readonly BoolCvar ClientAutoHop = new BoolCvar("cl_autohop", false, "Accessibility: hold jump to keep hopping.", CvarFlags.Archive);

        // Scripted input for automated tests and bots. Moves like any player; the host validates it like any input.
        public static readonly FloatCvar BotForward = new FloatCvar("cl_bot_forward", 0f, "Test: constant forward input -1..1.", CvarFlags.None, -1f, 1f);
        public static readonly FloatCvar BotSide = new FloatCvar("cl_bot_side", 0f, "Test: constant strafe input -1..1.", CvarFlags.None, -1f, 1f);
        public static readonly FloatCvar BotYawSpeed = new FloatCvar("cl_bot_yawspeed", 0f, "Test: constant turn rate, degrees/s.", CvarFlags.None, -720f, 720f);
        public static readonly BoolCvar BotJump = new BoolCvar("cl_bot_jump", false, "Test: hold jump (auto-hop).");

        public static void Register(ConsoleSystem console)
        {
            foreach (FloatCvar c in new[] { Gravity, Friction, StopSpeed, Accelerate, AirAccelerate, AirSpeedCap, MaxSpeed, SprintFactor, WaterSpeed, ThrowSlow, LoadSlow, JumpSpeed, MaxVelocity, Standable, StepHeight })
            {
                console.Register(c);
                c.Changed += _ => Apply();
            }
            console.Register(ServerAutoHop).Changed += _ => Apply();
            console.Register(ClientAutoHop);
            console.Register(ReconcileInterval);
            console.Register(RecoilSettleSpeed);
            console.Register(BotForward);
            console.Register(BotSide);
            console.Register(BotYawSpeed);
            console.Register(BotJump);
            Apply();
        }

        private static void Apply()
        {
            MoveConfig c = Config;
            c.Gravity = Gravity;
            c.Friction = Friction;
            c.StopSpeed = StopSpeed;
            c.Accelerate = Accelerate;
            c.AirAccelerate = AirAccelerate;
            c.AirSpeedCap = AirSpeedCap;
            c.MaxSpeed = MaxSpeed;
            c.SprintFactor = SprintFactor;
            c.WaterSpeedFactor = WaterSpeed;
            c.HitSlowFactor = ThrowSlow;
            c.LoadSlowPerLevel = LoadSlow;
            c.JumpSpeed = JumpSpeed;
            c.MaxVelocity = MaxVelocity;
            c.StandableNormalY = Standable;
            c.StepHeight = StepHeight;
            c.AutoHop = ServerAutoHop;
        }
    }
}
