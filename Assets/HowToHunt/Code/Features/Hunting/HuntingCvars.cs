using HowToHunt.Foundation;

namespace HowToHunt.Hunting
{
    /// <summary>Hit registration tunables and test hooks. Tool numbers live in ToolDefinition content.</summary>
    public static class HuntingCvars
    {
        private const CvarFlags Sv = CvarFlags.Replicated | CvarFlags.Cheat;

        public static readonly BoolCvar AllTools = new BoolCvar("sv_alltools", false, "Everyone owns every tool (tests, sv_cheats 1).", Sv);
        public static readonly FloatCvar DamageScale = new FloatCvar("sv_damage_scale", 1f, "Multiplies all tool damage (tests, sv_cheats 1).", Sv, 0f, 100f);
        public static readonly FloatCvar CrouchSight = new FloatCvar("sv_crouch_sight", 0.5f, "Prey spot crouching players at this fraction of their sight range.", Sv, 0f, 1f);
        public static readonly FloatCvar RecoilScale = new FloatCvar("sv_recoil_scale", 1f, "Multiplies every firearm's aim kick (0 = none).", Sv, 0f, 5f);
        public static readonly FloatCvar AdsRecoil = new FloatCvar("sv_ads_recoil", 0.7f, "Recoil multiplier while aiming down sights.", Sv, 0f, 2f);
        public static readonly BoolCvar InfiniteMag = new BoolCvar("sv_infinite_mag", false, "Guns never need reloading (tests, sv_cheats 1).", Sv);
        public static readonly BoolCvar ForceAim = new BoolCvar("cl_force_aim", false, "Test: aim down sights as if Aim were held.", CvarFlags.None);
        public static readonly BoolCvar HitDebug = new BoolCvar("sv_hitdebug", false, "Host: log every tool use (dropped, missed, what it hit).", CvarFlags.None);
        public static readonly BoolCvar LagComp = new BoolCvar("sv_lagcomp", true, "Rewind targets to what the shooter saw (lag compensation).", Sv);
        public static readonly FloatCvar MaxUnlag = new FloatCvar("sv_maxunlag", 0.25f, "Max rewind for lag compensation, seconds.", Sv, 0f, 1f);
        public static readonly IntCvar InterpTicks = new IntCvar("cl_interp_ticks", 2, "How far behind the latest server state clients draw shared objects, in ticks.", CvarFlags.Archive, 0, 8);
        public static readonly BoolCvar ShowHits = new BoolCvar("cl_showhits", false, "Debug: show the target where you saw it (blue) and where the host checked it (green).", CvarFlags.Archive);

        public static void Register(ConsoleSystem c)
        {
            c.Register(AllTools);
            c.Register(DamageScale);
            c.Register(HitDebug);
            c.Register(LagComp);
            c.Register(MaxUnlag);
            c.Register(InterpTicks);
            c.Register(ShowHits);
            c.Register(PreyClock.InterpTicks);
            c.Register(CrouchSight);
            c.Register(RecoilScale);
            c.Register(AdsRecoil);
            c.Register(InfiniteMag);
            c.Register(ForceAim);
        }
    }
}
