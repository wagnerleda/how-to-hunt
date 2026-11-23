using System;
using Unity.Profiling;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Managed allocations per frame, for the debug overlay and perf_bench. <c>GC.GetAllocatedBytesForCurrentThread</c>
    /// always returns 0 on Unity's Mono, so it can't prove the zero-allocation budget. This reads Unity's own
    /// "GC Allocated In Frame" counter (editor and development builds) and falls back to managed heap growth in
    /// release builds (a lower bound: a collection during the frame hides what was allocated before it).
    /// Measured once per frame, whoever asks first; reading it allocates nothing.
    /// </summary>
    public static class AllocationMeter
    {
        private static ProfilerRecorder _recorder;
        private static bool _started;
        private static int _frame = -1;
        private static long _lastHeap, _bytes, _collections;
        private static int _lastGcCount;

        /// <summary>True when the exact counter is available (editor, development builds).</summary>
        public static bool Exact => Start() && _recorder.Valid;

        /// <summary>Managed bytes allocated during the last finished frame.</summary>
        public static long LastFrameBytes
        {
            get
            {
                Sample();
                return _bytes;
            }
        }

        /// <summary>Garbage collections since startup (any generation).</summary>
        public static long Collections
        {
            get
            {
                Sample();
                return _collections;
            }
        }

        private static bool Start()
        {
            if (_started) return true;
            _started = true;
            _recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            _lastHeap = GC.GetTotalMemory(false);
            _lastGcCount = GC.CollectionCount(0);
            return true;
        }

        private static void Sample()
        {
            Start();
            int frame = Time.frameCount;
            if (frame == _frame) return;
            _frame = frame;
            int gcs = GC.CollectionCount(0);
            _collections += gcs - _lastGcCount;
            long heap = GC.GetTotalMemory(false);
            if (_recorder.Valid) _bytes = _recorder.LastValue;
            else _bytes = gcs == _lastGcCount && heap > _lastHeap ? heap - _lastHeap : 0;
            _lastGcCount = gcs;
            _lastHeap = heap;
        }
    }
}
