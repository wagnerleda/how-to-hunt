using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HowToHunt.UI
{
    /// <summary>
    /// Menus (IMGUI for now; the art pass in M3 restyles them): main menu, in-game menu with the party and host
    /// controls, settings with key rebinding, and the hold-Tab scoreboard. Parties are invite-only through Steam.
    /// The window sizes itself to its content and scales with screen height. Menus may allocate; gameplay doesn't.
    /// </summary>
    public sealed class SessionMenu : MonoBehaviour
    {
        private enum Page { Home, Settings }

        private const int WindowId = 0x4854;
        private const float Width = 480f;
        private const float FriendRefreshSeconds = 5f;
        private const float ToastSeconds = 7f;

        private Rect _window = new Rect(0, 0, Width, 10);
        private bool _pauseOpen;
        private Page _page = Page.Home;
        private GameAction? _rebinding;
        private float _nextFriendRefresh;
        private string _inviteHint = string.Empty;
        private string _toast = string.Empty;
        private float _toastUntil;
        private Vector2 _settingsScroll;
        private GUIStyle _windowStyle, _title, _big, _label, _small, _header, _toastStyle, _boardStyle;
        private Texture2D _panel, _toastBg;
        private ImGuiHost _gui;

        private static bool Online => Game.Platform != null && Game.Platform.IsOnline && Game.Platform.Lobbies != null;
        private static bool InSession => NetSession.State == SessionState.Hosting || NetSession.State == SessionState.Connected;

        private bool Visible => !InSession || _pauseOpen;

        private void Awake() => _gui = ImGuiHost.Attach(gameObject, DrawGui);

        private void OnEnable() => EventBus.Subscribe<SessionStateChanged>(OnSessionStateChanged);
        private void OnDisable() => EventBus.Unsubscribe<SessionStateChanged>(OnSessionStateChanged);

        private void OnSessionStateChanged(in SessionStateChanged e)
        {
            if (e.State == SessionState.Hosting || e.State == SessionState.Connected)
            {
                _pauseOpen = false;
                _page = Page.Home;
                _toast = e.Message;
                _toastUntil = Time.unscaledTime + ToastSeconds;
            }
            _inviteHint = string.Empty;
        }

        private void Update()
        {
            if (_rebinding.HasValue)
            {
                UpdateRebind();
            }
            else
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame && !DevConsoleView.IsOpen && !(Game.UiHasFocus && !Visible))
                {
                    if (MenuPanels.Open != null) { } // the open panel handles Esc itself
                    else if (_page == Page.Settings) _page = Page.Home;
                    else if (InSession) _pauseOpen = !_pauseOpen;
                }
            }

            if (Visible) Game.PushUiFocus(this);
            else Game.PopUiFocus(this);

            if (Visible && !DevConsoleView.IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Draw only when something is on screen: the menu, a toast, or the scoreboard while its key is held.
            bool toast = !Visible && Time.unscaledTime <= _toastUntil && !string.IsNullOrEmpty(_toast);
            bool board = InSession && KeyBindings.IsPressed(GameAction.Scoreboard) && !DevConsoleView.IsOpen;
            _gui.Show(Visible || toast || board);

            if (!InSession && Online && Time.unscaledTime >= _nextFriendRefresh)
            {
                _nextFriendRefresh = Time.unscaledTime + FriendRefreshSeconds;
                Game.Platform.Lobbies.RefreshFriendParties();
            }
        }

        private void UpdateRebind()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                _rebinding = null;
                return;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && _rebinding == GameAction.Fire)
            {
                KeyBindings.BindMouse(GameAction.Fire);
                _rebinding = null;
                return;
            }
            Key key = KeyBindings.AnyKeyPressedThisFrame();
            if (key == Key.None) return;
            KeyBindings.Bind(_rebinding.Value, key);
            _rebinding = null;
        }

        private void DrawGui()
        {
            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.height / 720f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenW = Screen.width / scale, screenH = Screen.height / scale;

            DrawToast(screenW);
            if (Visible && MenuPanels.Open == null)
            {
                _window.height = 10f; // shrink-to-fit every frame (GUILayout.Window only grows)
                _window = GUILayout.Window(WindowId, _window, DrawWindow, GUIContent.none, _windowStyle, GUILayout.Width(Width));
                _window.x = Mathf.Round((screenW - _window.width) * 0.5f);
                _window.y = Mathf.Round(Mathf.Max(10f, (screenH - _window.height) * 0.5f));
            }
            else if (InSession && KeyBindings.IsPressed(GameAction.Scoreboard) && !DevConsoleView.IsOpen)
            {
                DrawScoreboard(screenW);
            }
            GUI.matrix = previous;
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("How to Hunt", _title);
            GUILayout.Label($"v{Game.Version}  ·  {(Online ? $"Steam: {Game.Platform.LocalUserName}" : "Offline")}", _small);
            GUILayout.Space(12);

            if (_page == Page.Settings) DrawSettings();
            else if (_pauseOpen) DrawPause();
            else if (NetSession.State == SessionState.Offline) DrawMain();
            else DrawBusy();

            if (_page == Page.Home && NetSession.State == SessionState.Offline && !string.IsNullOrEmpty(NetSession.LastMessage))
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
                    GUILayout.Label("None right now. When a friend invites you, accept it in Steam and you join automatically.", _small);
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
            foreach (MenuPanels.Entry e in MenuPanels.Buttons)
            {
                if (GUILayout.Button(e.Label, _big, GUILayout.Height(40))) e.Open?.Invoke();
                GUILayout.Space(4);
            }
            if (GUILayout.Button("Play solo", GUILayout.Height(34))) NetSession.Host(forceLocal: true);
            GUILayout.Space(4);
            if (GUILayout.Button("Settings", GUILayout.Height(30))) _page = Page.Settings;
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
                        : "The Steam overlay can't open in this build (dev app id). Friends can join from 'Friends' parties' in their main menu, or right-click them in your Steam friends list > Invite to game.";
                GUILayout.Label(string.IsNullOrEmpty(_inviteHint) ? "Or right-click a friend in Steam > Invite to game." : _inviteHint, _small);
                GUILayout.Space(8);
            }

            DrawParty();

            GUILayout.Space(8);
            if (GUILayout.Button("Resume", _big, GUILayout.Height(40))) _pauseOpen = false;
            if (NetSession.IsServer && ConsoleSystem.Global.TryGetCvar("sv_pvp", out Cvar pvp))
            {
                // The host decides whether hunters can hurt each other (friendly fire follows it).
                bool on = pvp.ValueString == "1" || pvp.ValueString == "true";
                GUILayout.Space(4);
                if (GUILayout.Button(on ? "PvP: ON (hunters can hurt each other)" : "PvP: OFF", GUILayout.Height(30))) Game.Console.Execute(on ? "sv_pvp 0" : "sv_pvp 1");
            }
            GUILayout.Space(4);
            if (GUILayout.Button("Settings", GUILayout.Height(30))) _page = Page.Settings;
            GUILayout.Space(4);
            if (GUILayout.Button(NetSession.IsServer ? "End party" : "Leave party", GUILayout.Height(30)))
            {
                _pauseOpen = false;
                NetSession.Disconnect("You left the session.");
            }
            GUILayout.Space(4);
            if (GUILayout.Button("Quit game", GUILayout.Height(26))) Game.Console.Execute("quit");
        }

        private void DrawParty()
        {
            IReadOnlyList<PartyMember> members = PartyRoster.Members;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Party  {members.Count}/{GameIdentity.MaxPartySize}", _header);
            GUILayout.FlexibleSpace();
            if (NetSession.IsServer)
            {
                bool locked = GUILayout.Toggle(PartyRoster.Locked, " Lock party");
                if (locked != PartyRoster.Locked) PartyRoster.SetLocked(locked);
            }
            else if (PartyRoster.Locked)
            {
                GUILayout.Label("locked", _small);
            }
            GUILayout.EndHorizontal();

            foreach (PartyMember m in members)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(m.IsHost ? $"{m.Name}  (host)" : m.Name, _label, GUILayout.Width(250));
                GUILayout.Label(m.IsHost ? "-" : $"{m.Ping} ms", _small, GUILayout.Width(70));
                GUILayout.FlexibleSpace();
                if (NetSession.IsServer && !m.IsHost && GUILayout.Button("Kick", GUILayout.Width(60))) PartyRoster.Kick(m.ClientId);
                GUILayout.EndHorizontal();
            }
            if (NetSession.IsServer)
                GUILayout.Label(PartyRoster.Locked ? "Locked: nobody new can join." : "Friends can join any time (join in progress).", _small);
        }

        private void DrawScoreboard(float screenW)
        {
            IReadOnlyList<PartyMember> members = PartyRoster.Members;
            float w = 420f, h = 54f + members.Count * 26f;
            var r = new Rect((screenW - w) * 0.5f, 80f, w, h);
            GUI.DrawTexture(r, _toastBg);
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 10, w - 32, h - 16));
            GUILayout.Label($"Party  {members.Count}/{GameIdentity.MaxPartySize}" + (PartyRoster.Locked ? "  · locked" : string.Empty), _header);
            foreach (PartyMember m in members)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(m.IsHost ? $"{m.Name}  (host)" : m.Name, _boardStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(m.IsHost ? "-" : $"{m.Ping} ms", _boardStyle);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void DrawSettings()
        {
            GUILayout.Label("Settings", _header);
            _settingsScroll = GUILayout.BeginScrollView(_settingsScroll, GUILayout.Height(Mathf.Min(520f, Screen.height / Mathf.Max(1f, Screen.height / 720f) - 220f)));

            Slider("Mouse sensitivity", CoreCvars.Sensitivity, 0.1f, 8f, "0.00");
            Slider("Field of view", CoreCvars.Fov, 70f, 120f, "0");
            Slider("Master volume", CoreCvars.Volume, 0f, 1f, "0%");
            SliderByName("Movement sounds", "snd_movement", 0f, 1f, "0%");
            Toggle("Invert mouse Y", CoreCvars.InvertY);
            ToggleByName("Auto-hop (hold jump to keep hopping)", "cl_autohop");
            ToggleByName("Crosshair", "cl_crosshair");
            ToggleByName("Speedometer", "cl_showspeed");
            FrameRateLimit();
            if (CoreCvars.FpsMax.Value <= 0) Toggle("Vertical sync", CoreCvars.VSync);
            else GUILayout.Label("Vertical sync: off while a frame rate limit is set", _small);

            GUILayout.Space(10);
            GUILayout.Label("Keys (click, then press a key; Esc cancels)", _header);
            for (int i = 0; i < KeyBindings.Count; i++)
            {
                var action = (GameAction)i;
                GUILayout.BeginHorizontal();
                GUILayout.Label(action.ToString(), _label, GUILayout.Width(170));
                string text = _rebinding == action ? "press a key..." : KeyBindings.DisplayName(action);
                if (GUILayout.Button(text, GUILayout.Width(180), GUILayout.Height(26))) _rebinding = action;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            if (GUILayout.Button("Back", _big, GUILayout.Height(38)))
            {
                _page = Page.Home;
                _rebinding = null;
            }
        }

        private void Slider(string label, FloatCvar cvar, float min, float max, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label, GUILayout.Width(170));
            float v = GUILayout.HorizontalSlider(cvar.Value, min, max, GUILayout.Width(200));
            GUILayout.Label(format == "0%" ? $"{v * 100f:0}%" : v.ToString(format), _small, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(v, cvar.Value)) cvar.Value = v;
        }

        private static readonly int[] FrameRateChoices = { 0, 30, 60, 120, 144, 165, 240 };

        /// <summary>fps_max as a row of buttons (0 = none, then vsync applies).</summary>
        private void FrameRateLimit()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Frame rate limit", _label, GUILayout.Width(170));
            for (int i = 0; i < FrameRateChoices.Length; i++)
            {
                int fps = FrameRateChoices[i];
                bool on = CoreCvars.FpsMax.Value == fps;
                if (GUILayout.Toggle(on, fps == 0 ? "None" : fps.ToString(), GUI.skin.button, GUILayout.Height(24)) && !on) CoreCvars.FpsMax.Value = fps;
            }
            GUILayout.EndHorizontal();
        }

        private void SliderByName(string label, string name, float min, float max, string format)
        {
            if (Game.Console.TryGetCvar(name, out Cvar c) && c is FloatCvar f) Slider(label, f, min, max, format);
        }

        private void Toggle(string label, BoolCvar cvar)
        {
            bool v = GUILayout.Toggle(cvar.Value, " " + label);
            if (v != cvar.Value) cvar.Value = v;
        }

        private void ToggleByName(string label, string name)
        {
            if (!Game.Console.TryGetCvar(name, out Cvar c)) return;
            bool on = c.ValueString != "0";
            bool v = GUILayout.Toggle(on, " " + label);
            if (v != on) Game.Console.TrySet(name, v ? "1" : "0");
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
            _boardStyle = new GUIStyle(_label) { fontSize = 16, wordWrap = false };
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
