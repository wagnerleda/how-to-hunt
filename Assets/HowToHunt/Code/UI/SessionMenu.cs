using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HowToHunt.UI
{
    /// <summary>
    /// M0 session menu (replaced by the real lobby UI in M2). Parties are invite-only through Steam:
    /// host, invite friends from the overlay, or join a friend's party from the list. The window sizes itself
    /// to its content.
    /// </summary>
    public sealed class SessionMenu : MonoBehaviour
    {
        private const int WindowId = 0x4854;
        private const float Width = 460f;
        private const float FriendRefreshSeconds = 5f;
        private const float ToastSeconds = 7f;

        private Rect _window = new Rect(0, 0, Width, 10);
        private bool _pauseOpen;
        private float _nextFriendRefresh;
        private string _inviteHint = string.Empty;
        private string _toast = string.Empty;
        private float _toastUntil;
        private GUIStyle _windowStyle;
        private GUIStyle _title;
        private GUIStyle _big;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _header;
        private GUIStyle _toastStyle;
        private Texture2D _panel;
        private Texture2D _toastBg;

        private static bool Online => Game.Platform != null && Game.Platform.IsOnline && Game.Platform.Lobbies != null;
        private static bool InSession => NetSession.State == SessionState.Hosting || NetSession.State == SessionState.Connected;

        private bool Visible => !InSession || _pauseOpen;

        private void OnEnable() => EventBus.Subscribe<SessionStateChanged>(OnSessionStateChanged);
        private void OnDisable() => EventBus.Unsubscribe<SessionStateChanged>(OnSessionStateChanged);

        private void OnSessionStateChanged(in SessionStateChanged e)
        {
            if (e.State == SessionState.Hosting || e.State == SessionState.Connected)
            {
                _pauseOpen = false;
                _toast = e.Message;
                _toastUntil = Time.unscaledTime + ToastSeconds;
            }
            _inviteHint = string.Empty;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !DevConsoleView.IsOpen && InSession)
                _pauseOpen = !_pauseOpen;

            if (Visible) Game.PushUiFocus(this);
            else Game.PopUiFocus(this);

            if (Visible && !DevConsoleView.IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (!InSession && Online && Time.unscaledTime >= _nextFriendRefresh)
            {
                _nextFriendRefresh = Time.unscaledTime + FriendRefreshSeconds;
                Game.Platform.Lobbies.RefreshFriendParties();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            // Scale the whole UI with screen height (designed for 720p) so it stays readable at 1080p and 4K.
            float scale = Mathf.Max(1f, Screen.height / 720f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenW = Screen.width / scale, screenH = Screen.height / scale;

            DrawToast(screenW);
            if (Visible)
            {
                _window.height = 10f; // shrink-to-fit every frame (GUILayout.Window only grows)
                _window = GUILayout.Window(WindowId, _window, DrawWindow, GUIContent.none, _windowStyle, GUILayout.Width(Width));
                // Keep centered; height follows the content.
                _window.x = Mathf.Round((screenW - _window.width) * 0.5f);
                _window.y = Mathf.Round(Mathf.Max(10f, (screenH - _window.height) * 0.5f));
            }
            GUI.matrix = previous;
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("How to Hunt", _title);
            GUILayout.Label($"v{Game.Version}  ·  {(Online ? $"Steam: {Game.Platform.LocalUserName}" : "Offline")}", _small);
            GUILayout.Space(12);

            if (_pauseOpen) DrawPause();
            else if (NetSession.State == SessionState.Offline) DrawMain();
            else DrawBusy();

            if (NetSession.State == SessionState.Offline && !string.IsNullOrEmpty(NetSession.LastMessage))
            {
                GUILayout.Space(10);
                GUILayout.Label(NetSession.LastMessage, _label);
            }
        }

        private void DrawMain()
        {
            GUI.enabled = Online;
            if (GUILayout.Button("Host a party", _big, GUILayout.Height(46))) NetSession.Host();
            GUI.enabled = true;
            GUILayout.Label(Online ? "Then invite friends through Steam (Esc > Invite friends)." : Game.Platform?.OfflineReason ?? string.Empty, _small);

            if (Online)
            {
                GUILayout.Space(12);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Friends' parties", _header);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                {
                    _nextFriendRefresh = Time.unscaledTime + FriendRefreshSeconds;
                    Game.Platform.Lobbies.RefreshFriendParties();
                }
                GUILayout.EndHorizontal();

                IReadOnlyList<FriendParty> parties = Game.Platform.Lobbies.FriendParties;
                if (parties.Count == 0)
                {
                    GUILayout.Label("None right now. When a friend invites you, accept it in Steam and you join automatically.", _small);
                }
                foreach (FriendParty p in parties)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{p.FriendName}  ({p.Members}/{p.MaxMembers})" + (p.SameVersion ? string.Empty : "  · different version"), _label);
                    GUILayout.FlexibleSpace();
                    GUI.enabled = p.SameVersion;
                    if (GUILayout.Button("Join", GUILayout.Width(80), GUILayout.Height(28))) NetSession.JoinLobby(p.LobbyId);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(14);
            if (GUILayout.Button("Play solo", GUILayout.Height(34))) NetSession.Host(forceLocal: true);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Local host (test)", GUILayout.Height(24))) NetSession.Host(forceLocal: true);
            if (GUILayout.Button("Connect 127.0.0.1 (test)", GUILayout.Height(24))) NetSession.ConnectLocal("127.0.0.1");
            GUILayout.EndHorizontal();
            GUILayout.Label("Console: key left of 1, or F1", _small);
        }

        private void DrawBusy()
        {
            GUILayout.Label(NetSession.LastMessage, _label);
            GUILayout.Space(8);
            if (GUILayout.Button("Cancel", GUILayout.Height(32))) NetSession.Disconnect("Cancelled.");
        }

        private void DrawPause()
        {
            bool onlineHost = NetSession.IsServer && NetSession.Transport == SessionTransport.Steam;
            if (onlineHost)
            {
                if (GUILayout.Button("Invite friends", _big, GUILayout.Height(44)))
                    _inviteHint = NetSession.OpenInviteDialog()
                        ? string.Empty
                        : "The Steam overlay didn't open. Enable it in Steam > Settings > In Game, or right-click a friend in your Steam friends list > Invite to game.";
                GUILayout.Label(string.IsNullOrEmpty(_inviteHint) ? "Or press Shift+Tab, right-click a friend > Invite to game." : _inviteHint, _small);
                GUILayout.Space(8);
            }
            else if (NetSession.State == SessionState.Connected && !string.IsNullOrEmpty(NetSession.HostName))
            {
                GUILayout.Label($"In {NetSession.HostName}'s party.", _label);
                GUILayout.Space(8);
            }

            if (NetSession.IsServer)
                GUILayout.Label($"Players: {NetSession.PlayerCount}/{GameIdentity.MaxPartySize}", _small);

            GUILayout.Space(6);
            if (GUILayout.Button("Resume", _big, GUILayout.Height(40))) _pauseOpen = false;
            GUILayout.Space(4);
            if (GUILayout.Button(NetSession.IsServer ? "End party" : "Leave party", GUILayout.Height(32)))
            {
                _pauseOpen = false;
                NetSession.Disconnect("You left the session.");
            }
            GUILayout.Space(4);
            if (GUILayout.Button("Quit game", GUILayout.Height(28))) Game.Console.Execute("quit");
        }

        private void DrawToast(float screenW)
        {
            if (Time.unscaledTime > _toastUntil || string.IsNullOrEmpty(_toast) || Visible) return;
            var content = new GUIContent(_toast);
            Vector2 size = _toastStyle.CalcSize(content);
            float w = Mathf.Min(size.x + 32f, screenW - 40f);
            var r = new Rect((screenW - w) * 0.5f, 24f, w, size.y + 16f);
            GUI.DrawTexture(r, _toastBg);
            GUI.Label(r, content, _toastStyle);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _panel = MakeTex(new Color(0.08f, 0.11f, 0.13f, 0.95f));
            _toastBg = MakeTex(new Color(0.08f, 0.11f, 0.13f, 0.8f));
            _windowStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(24, 24, 18, 20) };
            _windowStyle.normal.background = _panel;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.62f, 0.15f);
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _label.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
            _small = new GUIStyle(_label) { fontSize = 13 };
            _small.normal.textColor = new Color(0.7f, 0.76f, 0.8f);
            _header = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _big = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            _toastStyle = new GUIStyle(_label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
