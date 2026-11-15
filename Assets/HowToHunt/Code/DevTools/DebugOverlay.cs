using System;
using System.Text;
using FishNet.Managing;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Net;
using UnityEngine;

namespace HowToHunt.DevTools
{
    /// <summary>
    /// Debug overlay: fps, frame time, tick, ping, main-thread allocations per frame, speed.
    /// The text is rebuilt 4 times a second so the overlay itself stays cheap.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        public static readonly IntCvar ShowFps = new IntCvar("cl_showfps", 1, "0 off, 1 fps + net, 2 + allocations and frame time.", CvarFlags.Archive, 0, 2);

        /// <summary>Features can contribute a line (e.g. speed). Must not allocate.</summary>
        public static Func<StringBuilder, bool> ExtraLine;

        private readonly StringBuilder _sb = new StringBuilder(256);
        private string _text = string.Empty;
        private float _nextRefresh;
        private int _frames;
        private float _accumTime;
        private float _worstFrame;
        private long _lastAllocated;
        private long _allocAccum;
        private GUIStyle _style;
        private GUIStyle _shadow;

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
            Rebuild();
            _frames = 0;
            _accumTime = 0f;
            _worstFrame = 0f;
            _allocAccum = 0;
        }

        private void Rebuild()
        {
            if (ShowFps.Value == 0)
            {
                _text = string.Empty;
                return;
            }
            _sb.Clear();
            float avgMs = _frames > 0 ? _accumTime / _frames * 1000f : 0f;
            _sb.Append(Mathf.RoundToInt(_frames / Mathf.Max(0.0001f, _accumTime))).Append(" fps");
            if (ShowFps.Value >= 2)
            {
                _sb.Append("  ").Append(avgMs.ToString("0.0")).Append(" ms (worst ").Append((_worstFrame * 1000f).ToString("0.0")).Append(')');
                long perFrame = _frames > 0 ? _allocAccum / _frames : 0;
                _sb.Append("  alloc ").Append(perFrame).Append(" B/frame");
            }

            NetworkManager m = NetSession.Manager;
            if (m != null)
            {
                _sb.Append('\n').Append(NetSession.State).Append(' ').Append(NetSession.Transport);
                _sb.Append("  tick ").Append(m.TimeManager.Tick).Append(" @").Append(m.TimeManager.TickRate);
                if (!m.IsServerStarted) _sb.Append("  ping ").Append(m.TimeManager.RoundTripTime).Append(" ms");
                if (m.IsServerStarted) _sb.Append("  players ").Append(m.ServerManager.Clients.Count);
            }
            if (!string.IsNullOrEmpty(NetSession.RoomCode)) _sb.Append("\nparty code ").Append(NetSession.RoomCode);
            if (ExtraLine != null)
            {
                _sb.Append('\n');
                if (!ExtraLine(_sb)) _sb.Length--;
            }
            _text = _sb.ToString();
        }

        private void OnGUI()
        {
            if (_text.Length == 0) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _style.normal.textColor = new Color(0.95f, 0.97f, 1f);
                _shadow = new GUIStyle(_style);
                _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            }
            var r = new Rect(Screen.width - 330, 8, 320, 120);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), _text, _shadow);
            GUI.Label(r, _text, _style);
        }
    }
}
