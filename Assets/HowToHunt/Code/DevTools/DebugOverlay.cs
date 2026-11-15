using System;
using FishNet.Managing;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Net;
using TMPro;
using UnityEngine;

namespace HowToHunt.DevTools
{
    /// <summary>
    /// Debug overlay (top right): fps, frame time, main-thread allocations per frame, tick, ping, players.
    /// Refreshed 4 times a second with TMP's allocation-free SetText, so it never shows up in its own alloc counter.
    /// The allocation number is the budget check: steady-state gameplay must show 0 B/frame.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        public static readonly IntCvar ShowFps = new IntCvar("cl_showfps", 1, "0 off, 1 fps + net, 2 + frame time and allocations.", CvarFlags.Archive, 0, 2);

        private TextMeshProUGUI _perf;
        private TextMeshProUGUI _net;
        private TextMeshProUGUI _state;
        private float _nextRefresh;
        private int _frames;
        private float _accumTime;
        private float _worstFrame;
        private long _lastAllocated;
        private long _allocAccum;
        private SessionState _shownState = (SessionState)(-1);
        private SessionTransport _shownTransport = (SessionTransport)(-1);

        private static readonly string[] StateNames = Enum.GetNames(typeof(SessionState));
        private static readonly string[] TransportNames = Enum.GetNames(typeof(SessionTransport));

        private void Awake()
        {
            var color = new Color(0.95f, 0.97f, 1f, 0.9f);
            _perf = Hud.Text("Perf", new Vector2(1f, 1f), new Vector2(-16, -12), 18, TextAlignmentOptions.TopRight, color);
            _net = Hud.Text("Net", new Vector2(1f, 1f), new Vector2(-16, -36), 18, TextAlignmentOptions.TopRight, color);
            _state = Hud.Text("NetState", new Vector2(1f, 1f), new Vector2(-16, -60), 18, TextAlignmentOptions.TopRight, color);
        }

        private void Update()
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            if (_lastAllocated != 0) _allocAccum += allocated - _lastAllocated;
            _lastAllocated = allocated;

            float dt = Time.unscaledDeltaTime;
            _frames++;
            _accumTime += dt;
            if (dt > _worstFrame) _worstFrame = dt;

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
            _frames = 0;
            _accumTime = 0f;
            _worstFrame = 0f;
            _allocAccum = 0;
        }

        private void Refresh()
        {
            bool on = ShowFps.Value > 0;
            NetworkManager m = NetSession.Manager;
            SetActive(_perf.gameObject, on);
            SetActive(_net.gameObject, on && m != null);
            SetActive(_state.gameObject, on && m != null);
            if (!on) return;

            float fps = _frames / Mathf.Max(0.0001f, _accumTime);
            if (ShowFps.Value >= 2)
            {
                float avgMs = _frames > 0 ? _accumTime / _frames * 1000f : 0f;
                float allocPerFrame = _frames > 0 ? (float)_allocAccum / _frames : 0f;
                _perf.SetText("{0} fps  {1:1} ms (worst {2:1})  alloc {3} B/frame", Mathf.Round(fps), avgMs, _worstFrame * 1000f, Mathf.Round(allocPerFrame));
            }
            else
            {
                _perf.SetText("{0} fps", Mathf.Round(fps));
            }

            if (m == null) return;
            if (m.IsServerStarted)
                _net.SetText("tick {0}   players {2}  up {3:1} down {4:1} KB/s", m.TimeManager.Tick, m.TimeManager.TickRate, NetSession.PlayerCount, NetMeter.OutBytesPerSecond / 1024f, NetMeter.InBytesPerSecond / 1024f);
            else
                _net.SetText("tick {0}   ping {2} ms  up {3:1} down {4:1} KB/s", m.TimeManager.Tick, m.TimeManager.TickRate, m.TimeManager.RoundTripTime, NetMeter.OutBytesPerSecond / 1024f, NetMeter.InBytesPerSecond / 1024f);
            if (_shownState != NetSession.State || _shownTransport != NetSession.Transport)
            {
                _shownState = NetSession.State;
                _shownTransport = NetSession.Transport;
                _state.text = StateNames[(int)_shownState] + " · " + TransportNames[(int)_shownTransport];
            }
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }
    }
}
