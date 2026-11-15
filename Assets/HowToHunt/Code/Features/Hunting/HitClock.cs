using FishNet.Managing;
using FishNet.Managing.Timing;
using HowToHunt.Net;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Which server tick this machine is looking at. The host sees the present; clients see the latest server state
    /// they received, minus a small interpolation buffer. Shots carry this "view tick" so the host can rewind targets
    /// to exactly what the shooter saw (lag compensation). Everything that is drawn from the server's timeline
    /// (dummies, later prey) is drawn at this tick, so what you see is what the host checks.
    /// </summary>
    public static class HitClock
    {
        public static double ViewTick()
        {
            NetworkManager m = NetSession.Manager;
            if (m == null) return 0;
            TimeManager tm = m.TimeManager;
            double percent = tm.GetTickPercentAsDouble();
            if (m.IsServerStarted) return tm.Tick + percent;
            uint last = tm.LastPacketTick.Value();
            if (last == TimeManager.UNSET_TICK) return 0;
            return last + percent - HuntingCvars.InterpTicks.Value;
        }

        public static uint ServerTick => NetSession.Manager != null ? NetSession.Manager.TimeManager.Tick : 0;
        public static int TickRate => NetSession.Manager != null ? NetSession.Manager.TimeManager.TickRate : Foundation.GameIdentity.TickRate;
    }
}
