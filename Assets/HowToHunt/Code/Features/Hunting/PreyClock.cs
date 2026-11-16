using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// The server tick clients draw ground prey at. Prey snapshots come 16 times a second (every 4 ticks), unreliably,
    /// so prey are drawn cl_prey_interp_ticks behind the newest server state (two snapshot gaps: one late or lost
    /// packet never freezes a rabbit), on a clock that advances smoothly every frame and only drifts gently toward
    /// the packet clock (no jumps when packets bunch up). The host sees the present. Shots at prey from a client are
    /// rewound by the same extra delay on the host (see <see cref="ExtraLag"/>), so hits match what was drawn.
    /// </summary>
    public static class PreyClock
    {
        public static readonly IntCvar InterpTicks = new IntCvar("cl_prey_interp_ticks", 10, "How far behind the newest server state clients draw prey, in ticks (covers snapshot gaps).", CvarFlags.Archive, 4, 32);
        private const float Correction = 2f; // per second: how fast the smooth clock drifts toward the packet clock
        private const double SnapTicks = 12; // further off than this (stall, teleport): jump

        private static double _tick;
        private static int _frame = -1;
        private static bool _valid;

        /// <summary>Extra ticks prey are drawn behind the shared view tick (dummies, players).</summary>
        public static double ExtraLag => Mathf.Max(0, InterpTicks.Value - HuntingCvars.InterpTicks.Value);

        public static double RenderTick()
        {
            if (Time.frameCount == _frame) return _tick;
            _frame = Time.frameCount;
            double view = HitClock.ViewTick();
            if (Net.NetSession.Manager != null && Net.NetSession.Manager.IsServerStarted) { _valid = false; return _tick = view; }
            if (view <= 0) { _valid = false; return _tick = 0; }
            double target = view - ExtraLag;
            if (!_valid || System.Math.Abs(target - _tick) > SnapTicks)
            {
                _tick = target;
                _valid = true;
                return _tick;
            }
            _tick += Time.deltaTime * HitClock.TickRate;
            _tick += (target - _tick) * Mathf.Min(1f, Correction * Time.deltaTime);
            return _tick;
        }
    }
}
