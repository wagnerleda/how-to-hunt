using HowToHunt.Foundation;

namespace HowToHunt.Hunting
{
    /// <summary>Hit registration tunables and test hooks. Tool numbers live in ToolDefinition content.</summary>
    public static class HuntingCvars
    {
        private const CvarFlags Sv = CvarFlags.Replicated | CvarFlags.Cheat;

        public static readonly BoolCvar AllTools = new BoolCvar("sv_alltools", false, "Everyone owns every tool (tests, sv_cheats 1).", Sv);
        public static readonly FloatCvar DamageScale = new FloatCvar("sv_damage_scale", 1f, "Multiplies all tool damage (tests, sv_cheats 1).", Sv, 0f, 100f);
        public static readonly BoolCvar LagComp = new BoolCvar("sv_lagcomp", true, "Rewind targets to what the shooter saw (lag compensation).", Sv);
        public static readonly FloatCvar MaxUnlag = new FloatCvar("sv_maxunlag", 0.25f, "Max rewind for lag compensation, seconds.", Sv, 0f, 1f);
        public static readonly IntCvar InterpTicks = new IntCvar("cl_interp_ticks", 2, "How far behind the latest server state clients draw shared objects, in ticks.", CvarFlags.Archive, 0, 8);
        public static readonly BoolCvar ShowHits = new BoolCvar("cl_showhits", false, "Debug: show the target where you saw it (blue) and where the host checked it (green).", CvarFlags.Archive);

        public static void Register(ConsoleSystem c)
        {
            c.Register(AllTools);
            c.Register(DamageScale);
            c.Register(LagComp);
            c.Register(MaxUnlag);
            c.Register(InterpTicks);
            c.Register(ShowHits);
        }
    }
}
