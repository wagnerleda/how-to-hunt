using HowToHunt.Foundation;

namespace HowToHunt.Hunting
{
    /// <summary>Weapon and hit-registration tunables (M2 dev pistol; real tools become content in M3).</summary>
    public static class HuntingCvars
    {
        private const CvarFlags Sv = CvarFlags.Replicated | CvarFlags.Cheat;

        public static readonly FloatCvar Damage = new FloatCvar("wpn_pistol_damage", 25f, "Dev pistol damage per hit (head x2).", Sv, 0f, 1000f);
        public static readonly FloatCvar FireRate = new FloatCvar("wpn_pistol_rate", 5f, "Dev pistol shots per second.", Sv, 0.5f, 30f);
        public static readonly FloatCvar Range = new FloatCvar("wpn_pistol_range", 150f, "Dev pistol range, m.", Sv, 1f, 1000f);
        public static readonly FloatCvar HeadMultiplier = new FloatCvar("wpn_headshot_mult", 2f, "Headshot damage multiplier.", Sv, 1f, 10f);
        public static readonly BoolCvar LagComp = new BoolCvar("sv_lagcomp", true, "Rewind targets to what the shooter saw (lag compensation).", Sv);
        public static readonly FloatCvar MaxUnlag = new FloatCvar("sv_maxunlag", 0.25f, "Max rewind for lag compensation, seconds.", Sv, 0f, 1f);
        public static readonly IntCvar InterpTicks = new IntCvar("cl_interp_ticks", 2, "How far behind the latest server state clients draw shared objects, in ticks.", CvarFlags.Archive, 0, 8);
        public static readonly BoolCvar ShowHits = new BoolCvar("cl_showhits", false, "Debug: show the target where you saw it (blue) and where the host checked it (green).", CvarFlags.Archive);

        public static void Register(ConsoleSystem c)
        {
            c.Register(Damage);
            c.Register(FireRate);
            c.Register(Range);
            c.Register(HeadMultiplier);
            c.Register(LagComp);
            c.Register(MaxUnlag);
            c.Register(InterpTicks);
            c.Register(ShowHits);
        }
    }
}
