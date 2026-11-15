using UnityEngine;

namespace HowToHunt.Net
{
    /// <summary>
    /// Bytes in/out of this machine's game transport, as per-second rates (bandwidth budgets: client up 8 KB/s,
    /// client down 24 KB/s, host up 200 KB/s with 8 players). Fed by the metered transports.
    /// </summary>
    public static class NetMeter
    {
        private static long _in, _out;
        private static float _windowStart = -1f;

        public static float InBytesPerSecond { get; private set; }
        public static float OutBytesPerSecond { get; private set; }

        internal static void CountIn(int bytes) { _in += bytes; }
        internal static void CountOut(int bytes) { _out += bytes; }

        /// <summary>Called every frame; rolls the 1 s window.</summary>
        internal static void Update()
        {
            float now = Time.unscaledTime;
            if (_windowStart < 0f) _windowStart = now;
            float span = now - _windowStart;
            if (span < 1f) return;
            InBytesPerSecond = _in / span;
            OutBytesPerSecond = _out / span;
            _in = 0;
            _out = 0;
            _windowStart = now;
        }

        internal static void Reset()
        {
            _in = _out = 0;
            InBytesPerSecond = OutBytesPerSecond = 0f;
            _windowStart = -1f;
        }
    }
}
