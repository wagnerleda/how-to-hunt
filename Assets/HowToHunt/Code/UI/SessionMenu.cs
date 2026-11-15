using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HowToHunt.UI
{
    /// <summary>
    /// Minimal M0 menu: host, join by code, local session. Replaced by the real lobby UI in M2.
    /// Shown when offline; Esc opens it in game (leave / copy code).
    /// </summary>
    public sealed class SessionMenu : MonoBehaviour
    {
        private string _code = string.Empty;
        private bool _pauseOpen;
        private GUIStyle _title;
        private GUIStyle _big;
        private GUIStyle _label;
        private GUIStyle _codeStyle;
        private Texture2D _panel;

        private bool Visible => NetSession.State == SessionState.Offline || NetSession.State == SessionState.Starting ||
                                NetSession.State == SessionState.Connecting || _pauseOpen;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !DevConsoleView.IsOpen &&
                (NetSession.State == SessionState.Hosting || NetSession.State == SessionState.Connected))
                _pauseOpen = !_pauseOpen;

            if (Visible) Game.PushUiFocus(this);
            else Game.PopUiFocus(this);

            if (Visible && !DevConsoleView.IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void OnGUI()
        {
            if (!Visible) return;
            EnsureStyles();

            const float w = 440f;
            float h = _pauseOpen ? 250f : 380f;
            var area = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.DrawTexture(area, _panel);
            GUILayout.BeginArea(new Rect(area.x + 24, area.y + 18, w - 48, h - 36));

            GUILayout.Label("How to Hunt", _title);
            GUILayout.Label($"v{Game.Version}  ·  {(Game.Platform != null && Game.Platform.IsOnline ? $"Steam: {Game.Platform.LocalUserName}" : "Offline")}", _label);
            GUILayout.Space(10);

            if (_pauseOpen) DrawPause();
            else if (NetSession.State == SessionState.Offline) DrawMain();
            else GUILayout.Label(NetSession.LastMessage, _label);

            GUILayout.FlexibleSpace();
            if (NetSession.State == SessionState.Offline && !string.IsNullOrEmpty(NetSession.LastMessage))
                GUILayout.Label(NetSession.LastMessage, _label);
            GUILayout.EndArea();
        }

        private void DrawMain()
        {
            bool online = Game.Platform != null && Game.Platform.IsOnline;
            GUI.enabled = online;
            if (GUILayout.Button("Host a party", _big, GUILayout.Height(44))) NetSession.Host();
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("hth_code");
            _code = GUILayout.TextField(_code, 9, _codeStyle, GUILayout.Height(44), GUILayout.Width(200));
            bool valid = RoomCode.TryNormalize(_code, out _);
            GUI.enabled = online && valid;
            if (GUILayout.Button("Join", _big, GUILayout.Height(44))) NetSession.JoinByCode(_code);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (!online && Game.Platform != null) GUILayout.Label(Game.Platform.OfflineReason, _label);

            GUILayout.Space(10);
            if (GUILayout.Button("Play solo", GUILayout.Height(32))) NetSession.Host(forceLocal: true);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Local host (LAN/test)", GUILayout.Height(26))) NetSession.Host(forceLocal: true);
            if (GUILayout.Button("Connect to 127.0.0.1", GUILayout.Height(26))) NetSession.ConnectLocal("127.0.0.1");
            GUILayout.EndHorizontal();
            GUILayout.Label("Console: key left of 1 (or F1)", _label);
        }

        private void DrawPause()
        {
            if (!string.IsNullOrEmpty(NetSession.RoomCode))
            {
                GUILayout.Label("Party code", _label);
                GUILayout.BeginHorizontal();
                GUILayout.Label(NetSession.RoomCode, _codeStyle, GUILayout.Height(44));
                if (GUILayout.Button("Copy", GUILayout.Width(80), GUILayout.Height(44))) GUIUtility.systemCopyBuffer = NetSession.RoomCode;
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            if (GUILayout.Button("Resume", _big, GUILayout.Height(40))) _pauseOpen = false;
            if (GUILayout.Button(NetSession.IsServer ? "End party" : "Leave party", GUILayout.Height(32)))
            {
                _pauseOpen = false;
                NetSession.Disconnect("You left the session.");
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.82f, 0.35f);
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _label.normal.textColor = new Color(0.9f, 0.93f, 0.95f);
            _big = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            _codeStyle = new GUIStyle(GUI.skin.textField) { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _panel = new Texture2D(1, 1);
            _panel.SetPixel(0, 0, new Color(0.08f, 0.11f, 0.13f, 0.94f));
            _panel.Apply();
        }
    }
}
