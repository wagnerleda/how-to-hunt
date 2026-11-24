using System;
using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Net;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HowToHunt.UI
{
    /// <summary>
    /// The menus (uGUI; ADR 0010 planned the move off IMGUI): main menu, in-game menu with the party and host
    /// controls, settings with key rebinding, the hold-Tab scoreboard and toasts. Pages come in as a cascade, the
    /// focused item glows, grows and breathes, presses squash; a living background on the main menu (MenuBackdrop);
    /// keyboard, mouse and gamepad all drive the same focus. Sounds and motion follow UiFeel (snd_ui,
    /// ui_reduce_motion); every look-and-feel number is in MenuTheme. Menus may allocate when a page changes;
    /// steady frames don't. Parties are invite-only through Steam.
    /// </summary>
    public sealed class SessionMenu : MonoBehaviour
    {
        private enum Page { Home, Settings }

        private const float PanelWidth = 560f, Inner = 496f, HeaderHeight = 118f, Pad = 32f, Gap = 8f, ScreenMargin = 40f;
        private const float FriendRefreshSeconds = 5f, ToastSeconds = 7f, LiveRefreshSeconds = 0.5f;

        private Page _page = Page.Home;
        private bool _pauseOpen;
        private GameAction? _rebinding;
        private float _nextFriendRefresh;
        private string _inviteHint = string.Empty;
        private string _toast = string.Empty;
        private float _toastUntil, _toastAge;

        // uGUI
        private RectTransform _canvasRt, _panel, _viewport, _content, _toastRt, _board;
        private CanvasGroup _panelGroup, _toastGroup;
        private Image _panelFill, _panelEdge, _shadow, _selector, _toastFill;
        private TextMeshProUGUI _title, _subtitle, _toastText, _boardText;
        private MenuBackdrop _backdrop;
        private readonly List<MenuItem> _items = new List<MenuItem>(48);
        private int _focus = -1;
        private float _scroll, _scrollTarget, _contentHeight, _viewportHeight;
        private int _signature = int.MinValue;
        private Page _builtPage;
        private bool _builtPause, _shown;
        private float _panelAge, _outT, _liveAt, _titleAge;
        private bool _switching;
        private int _dragging = -1;
        private string _boardShown;
        private float _boardAt;

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
                _page = Page.Home;
                _toast = e.Message;
                _toastUntil = Time.unscaledTime + ToastSeconds;
                _toastAge = 0f;
            }
            _inviteHint = string.Empty;
        }

        // --- build --------------------------------------------------------------------------------------------------

        private void Awake()
        {
            MenuTheme theme = MenuTheme.Current;
            var go = new GameObject("[Menu]", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // above the HUD and dialogue
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            _canvasRt = (RectTransform)go.transform;
            _backdrop = new MenuBackdrop(_canvasRt, theme);

            _panel = New("Panel", _canvasRt);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panelGroup = _panel.gameObject.AddComponent<CanvasGroup>();
            _shadow = Img("Shadow", _panel, UiSprites.Rounded(26), stretch: true);
            _shadow.color = new Color(0f, 0f, 0f, 0.35f);
            _shadow.rectTransform.offsetMin = new Vector2(10f, -14f);
            _shadow.rectTransform.offsetMax = new Vector2(10f, -14f);
            _panelFill = Img("Fill", _panel, UiSprites.Rounded(26), stretch: true);
            _panelEdge = Img("Edge", _panel, UiSprites.Rounded(26, 2), stretch: true);
            _title = Txt("Title", _panel, 54f, FontStyles.Bold, TextAlignmentOptions.Center);
            _title.rectTransform.anchorMin = _title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _title.rectTransform.sizeDelta = new Vector2(Inner, 64f);
            _title.SetText("How to Hunt");
            _title.outlineWidth = 0.18f;
            _title.outlineColor = new Color32(60, 26, 6, 255);
            _subtitle = Txt("Subtitle", _panel, 17f, FontStyles.Normal, TextAlignmentOptions.Center);
            _subtitle.rectTransform.anchorMin = _subtitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _subtitle.rectTransform.sizeDelta = new Vector2(Inner, 24f);

            _viewport = New("Viewport", _panel);
            _viewport.anchorMin = _viewport.anchorMax = new Vector2(0.5f, 1f);
            _viewport.pivot = new Vector2(0.5f, 1f);
            _viewport.gameObject.AddComponent<RectMask2D>().padding = new Vector4(-24f, 0f, -24f, 0f);
            _content = New("Content", _viewport);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _selector = Img("Selector", _content, UiSprites.Tail(), stretch: false);
            _selector.rectTransform.anchorMin = _selector.rectTransform.anchorMax = new Vector2(0.5f, 1f); // like the items
            _selector.rectTransform.sizeDelta = new Vector2(18f, 14f);
            _selector.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            _toastRt = New("Toast", _canvasRt);
            _toastRt.anchorMin = _toastRt.anchorMax = new Vector2(0.5f, 1f);
            _toastGroup = _toastRt.gameObject.AddComponent<CanvasGroup>();
            _toastFill = Img("Fill", _toastRt, UiSprites.Rounded(20), stretch: true);
            _toastText = Txt("Text", _toastRt, 20f, FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(_toastText.rectTransform);
            _toastRt.gameObject.SetActive(false);

            _board = New("Scoreboard", _canvasRt);
            _board.anchorMin = _board.anchorMax = new Vector2(0.5f, 1f);
            _board.pivot = new Vector2(0.5f, 1f);
            Image boardFill = Img("Fill", _board, UiSprites.Rounded(20), stretch: true);
            boardFill.color = new Color(0.07f, 0.1f, 0.12f, 0.85f);
            _boardText = Txt("Text", _board, 19f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Stretch(_boardText.rectTransform);
            _boardText.margin = new Vector4(22f, 16f, 22f, 12f);
            _board.gameObject.SetActive(false);

            _panel.gameObject.SetActive(false);
        }

        private static RectTransform New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Image Img(string name, Transform parent, Sprite sprite, bool stretch)
        {
            RectTransform rt = New(name, parent);
            if (stretch) Stretch(rt);
            Image i = rt.gameObject.AddComponent<Image>();
            i.sprite = sprite;
            i.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            i.raycastTarget = false;
            return i;
        }

        private static TextMeshProUGUI Txt(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
        {
            RectTransform rt = New(name, parent);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        // --- pages --------------------------------------------------------------------------------------------------

        /// <summary>Everything a page shows depends on these: when any changes, the page is rebuilt.</summary>
        private int Signature()
        {
            unchecked
            {
                int h = (int)_page * 31 + (_pauseOpen ? 7 : 0) + (int)NetSession.State * 131 + (Online ? 1 : 0) * 997;
                h = h * 17 + (NetSession.LastMessage?.GetHashCode() ?? 0);
                h = h * 17 + (_rebinding.HasValue ? (int)_rebinding.Value + 1 : 0);
                h = h * 17 + _inviteHint.Length;
                h = h * 17 + MenuPanels.Buttons.Count;
                h = h * 17 + (CoreCvars.FpsMax.Value > 0 ? 1 : 0);
                if (Online) h = h * 17 + Game.Platform.Lobbies.FriendParties.Count;
                IReadOnlyList<PartyMember> members = PartyRoster.Members;
                h = h * 17 + members.Count + (PartyRoster.Locked ? 1000 : 0);
                for (int i = 0; i < members.Count; i++) h = h * 17 + members[i].ClientId;
                if (NetSession.IsServer && ConsoleSystem.Global.TryGetCvar("sv_pvp", out Cvar pvp)) h = h * 17 + pvp.ValueString.GetHashCode();
                return h;
            }
        }

        private void Rebuild(bool animate)
        {
            string keep = _focus >= 0 && _focus < _items.Count ? _items[_focus].Key : null;
            foreach (MenuItem it in _items) Destroy(it.Root.gameObject);
            _items.Clear();
            _builtPage = _page;
            _builtPause = _pauseOpen;

            if (_page == Page.Settings) BuildSettings();
            else if (_pauseOpen) BuildPause();
            else if (NetSession.State == SessionState.Offline) BuildMain();
            else BuildBusy();

            Layout();
            _focus = -1;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Focusable && (keep == null || _items[i].Key == keep)) { _focus = i; break; }
            if (_focus < 0) _focus = NextFocusable(-1, 1);
            for (int i = 0; i < _items.Count; i++)
            {
                MenuItem it = _items[i];
                it.Delay = animate ? 0.06f + i * MenuTheme.Current.Stagger : 0f;
                it.Age = animate ? 0f : 10f;
                it.Highlight = i == _focus ? 1f : 0f;
            }
            if (animate) { _scroll = _scrollTarget = 0f; _titleAge = 0f; }
        }

        private void BuildMain()
        {
            Button("host", "Host a party", () => NetSession.Host(), big: true, enabled: Online);
            Small(Online ? "Then invite friends through Steam (Esc > Invite friends)." : Game.Platform?.OfflineReason ?? string.Empty);
            if (Online)
            {
                Header("Friends' parties");
                Button("refresh", "Refresh", () =>
                {
                    _nextFriendRefresh = Time.unscaledTime + FriendRefreshSeconds;
                    Game.Platform.Lobbies.RefreshFriendParties();
                }, small: true);
                IReadOnlyList<FriendParty> parties = Game.Platform.Lobbies.FriendParties;
                if (parties.Count == 0) Small("None right now. When a friend invites you, accept it in Steam and you join automatically.");
                foreach (FriendParty p in parties)
                {
                    ulong lobby = p.LobbyId;
                    Captioned($"join{lobby}", $"{p.FriendName}  ({p.Members}/{p.MaxMembers})" + (p.SameVersion ? string.Empty : "  · different version"), "Join", () => NetSession.JoinLobby(lobby), p.SameVersion);
                }
            }
            Spacer(6f);
            foreach (MenuPanels.Entry e in MenuPanels.Buttons)
            {
                MenuPanels.Entry entry = e;
                Button("panel" + e.Label, e.Label, () => entry.Open?.Invoke(), big: true);
            }
            Button("solo", "Play solo", () => NetSession.Host(forceLocal: true), big: true);
            Button("settings", "Settings", () => SwitchTo(Page.Settings));
            Button("localhost", "Local host (test)", () => NetSession.Host(forceLocal: true), small: true, half: true);
            Button("connect", "Connect 127.0.0.1 (test)", () => NetSession.ConnectLocal("127.0.0.1"), small: true, half: true);
            Small("Console: key left of 1, or F1");
            if (!string.IsNullOrEmpty(NetSession.LastMessage)) Text(NetSession.LastMessage);
        }

        private void BuildBusy()
        {
            Text(NetSession.LastMessage);
            Button("cancel", "Cancel", () => NetSession.Disconnect("Cancelled."));
        }

        private void BuildPause()
        {
            bool onlineHost = NetSession.IsServer && NetSession.Transport == SessionTransport.Steam;
            if (onlineHost)
            {
                Button("invite", "Invite friends", () =>
                {
                    _inviteHint = NetSession.OpenInviteDialog()
                        ? string.Empty
                        : "The Steam overlay can't open in this build (dev app id). Friends can join from 'Friends' parties' in their main menu, or right-click them in your Steam friends list > Invite to game.";
                }, big: true);
                Small(string.IsNullOrEmpty(_inviteHint) ? "Or right-click a friend in Steam > Invite to game." : _inviteHint);
            }

            IReadOnlyList<PartyMember> members = PartyRoster.Members;
            Header($"Party  {members.Count}/{GameIdentity.MaxPartySize}" + (!NetSession.IsServer && PartyRoster.Locked ? "  · locked" : string.Empty));
            if (NetSession.IsServer) Toggle("lock", "Lock party", () => PartyRoster.Locked, v => PartyRoster.SetLocked(v));
            foreach (PartyMember m in members)
            {
                PartyMember member = m;
                if (NetSession.IsServer && !m.IsHost)
                    Captioned($"kick{m.ClientId}", m.Name, "Kick", () => PartyRoster.Kick(member.ClientId), true, () => $"{member.Ping} ms");
                else
                    Small(m.IsHost ? $"{m.Name}  (host)" : $"{m.Name}   {m.Ping} ms");
            }
            if (NetSession.IsServer) Small(PartyRoster.Locked ? "Locked: nobody new can join." : "Friends can join any time (join in progress).");

            Spacer(6f);
            Button("resume", "Resume", () => _pauseOpen = false, big: true);
            if (NetSession.IsServer && ConsoleSystem.Global.TryGetCvar("sv_pvp", out Cvar pvp))
            {
                // The host decides whether hunters can hurt each other (friendly fire follows it).
                bool on = pvp.ValueString == "1" || pvp.ValueString == "true";
                Button("pvp", on ? "PvP: ON (hunters can hurt each other)" : "PvP: OFF", () => Game.Console.Execute(on ? "sv_pvp 0" : "sv_pvp 1"));
            }
            Button("settings", "Settings", () => SwitchTo(Page.Settings));
            Button("leave", NetSession.IsServer ? "End party" : "Leave party", () =>
            {
                _pauseOpen = false;
                NetSession.Disconnect("You left the session.");
            });
            Button("quit", "Quit game", () => Game.Console.Execute("quit"), small: true);
        }

        private void BuildSettings()
        {
            Header("Settings");
            Slider("sens", "Mouse sensitivity", CoreCvars.Sensitivity, 0.1f, 8f, "0.00");
            Slider("fov", "Field of view", CoreCvars.Fov, 70f, 120f, "0");
            Slider("volume", "Master volume", CoreCvars.Volume, 0f, 1f, "%");
            if (Game.Console.TryGetCvar("snd_movement", out Cvar move) && move is FloatCvar moveF) Slider("movevol", "Movement sounds", moveF, 0f, 1f, "%");
            Slider("uivol", "Menu and dialogue sounds", UiFeel.Volume, 0f, 1f, "%");
            ToggleCvar("invert", "Invert mouse Y", CoreCvars.InvertY);
            ToggleByName("autohop", "Auto-hop (hold jump to keep hopping)", "cl_autohop");
            ToggleByName("crosshair", "Crosshair", "cl_crosshair");
            ToggleByName("speed", "Speedometer", "cl_showspeed");
            ToggleCvar("calm", "Reduce animations (no shake, flashes, particles)", UiFeel.ReduceMotion);
            Choice("fps", "Frame rate limit", new[] { 0, 30, 60, 120, 144, 165, 240 }, new[] { "None", "30", "60", "120", "144", "165", "240" },
                () => CoreCvars.FpsMax.Value, v => CoreCvars.FpsMax.Value = v);
            if (CoreCvars.FpsMax.Value <= 0) ToggleCvar("vsync", "Vertical sync", CoreCvars.VSync);
            else Small("Vertical sync: off while a frame rate limit is set");

            Header("Gamepad");
            if (Game.Console.TryGetCvar("joy_sensitivity", out Cvar joySens) && joySens is FloatCvar joySensF) Slider("joysens", "Look speed", joySensF, 30f, 720f, "0");
            if (Game.Console.TryGetCvar("joy_deadzone", out Cvar joyDz) && joyDz is FloatCvar joyDzF) Slider("joydz", "Stick dead zone", joyDzF, 0f, 0.6f, "0.00");
            ToggleByName("joyinvert", "Invert gamepad look", "joy_invert_y");
            Small("Left stick move, right stick look. A jump, B crouch, L-stick click sprint, RT fire, LT aim, X interact, Y reload, RB throw (hold), R-stick click inspect, D-pad left/right hotbar, View scoreboard, Start menu.");

            Header("Keys (choose one, then press a key; Esc cancels)");
            for (int i = 0; i < KeyBindings.Count; i++)
            {
                var action = (GameAction)i;
                Captioned("key" + i, action.ToString(), _rebinding == action ? "press a key..." : KeyBindings.DisplayName(action), () => _rebinding = action, true);
            }
            Spacer(6f);
            Button("back", "Back", () => SwitchTo(Page.Home), big: true);
        }

        private void SwitchTo(Page page)
        {
            _page = page;
            if (page == Page.Home) _rebinding = null;
            BeginSwitch();
        }

        /// <summary>Page change: the current items slide out quickly, then the new page cascades in.</summary>
        private void BeginSwitch()
        {
            _switching = true;
            _outT = 0f;
        }

        // --- item factories ------------------------------------------------------------------------------------------

        private MenuItem Add(MenuItemKind kind, string key, float height)
        {
            var it = new MenuItem { Kind = kind, Key = key, Height = height };
            it.Root = New(key ?? kind.ToString(), _content);
            it.Root.anchorMin = it.Root.anchorMax = new Vector2(0.5f, 1f);
            it.Group = it.Root.gameObject.AddComponent<CanvasGroup>();
            it.Body = New("Body", it.Root);
            Stretch(it.Body);
            _items.Add(it);
            return it;
        }

        private void Text(string text) => TextItem(text, 18f, MenuTheme.Current.Text, FontStyles.Normal, MenuItemKind.Text);
        private void Small(string text) => TextItem(text, 15f, MenuTheme.Current.Dim, FontStyles.Normal, MenuItemKind.Small);
        private void Header(string text) => TextItem(text, 20f, MenuTheme.Current.Title, FontStyles.Bold, MenuItemKind.Header);

        private void TextItem(string text, float size, Color color, FontStyles style, MenuItemKind kind)
        {
            if (string.IsNullOrEmpty(text)) return;
            MenuItem it = Add(kind, null, 0f);
            it.Label = Txt("Label", it.Body, size, style, kind == MenuItemKind.Header ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.TopLeft);
            Stretch(it.Label.rectTransform);
            it.Label.color = color;
            it.Label.SetText(text);
            it.Height = Mathf.Ceil(it.Label.GetPreferredValues(text, Inner, 0f).y) + (kind == MenuItemKind.Header ? 10f : 2f);
        }

        private void Spacer(float h) => Add(MenuItemKind.Spacer, null, h);

        private MenuItem Button(string key, string label, Action onClick, bool big = false, bool small = false, bool half = false, bool enabled = true)
        {
            MenuItem it = Add(MenuItemKind.Button, key, big ? 48f : small ? 32f : 40f);
            it.Half = half;
            it.Enabled = enabled;
            it.OnActivate = onClick;
            ButtonLook(it, it.Body, label, big ? 21f : small ? 16f : 18f);
            return it;
        }

        /// <summary>Text on the left, a compact button on the right (friend parties, kicks, key bindings).</summary>
        private void Captioned(string key, string caption, string button, Action onClick, bool enabled, Func<string> live = null)
        {
            MenuItem it = Add(MenuItemKind.Button, key, 36f);
            it.Enabled = enabled;
            it.OnActivate = onClick;
            it.Caption = caption;
            it.ButtonWidth = 190f;
            it.CaptionText = Txt("Caption", it.Body, 17f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Stretch(it.CaptionText.rectTransform);
            it.CaptionText.rectTransform.offsetMax = new Vector2(-it.ButtonWidth - 70f, 0f);
            it.CaptionText.color = MenuTheme.Current.Text;
            it.CaptionText.textWrappingMode = TextWrappingModes.NoWrap;
            it.CaptionText.overflowMode = TextOverflowModes.Ellipsis;
            it.CaptionText.SetText(caption);
            if (live != null)
            {
                it.Live = live;
                it.Value = Txt("Live", it.Body, 15f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
                Stretch(it.Value.rectTransform);
                it.Value.rectTransform.offsetMax = new Vector2(-it.ButtonWidth - 12f, 0f);
                it.Value.color = MenuTheme.Current.Dim;
            }
            RectTransform holder = New("Button", it.Body);
            holder.anchorMin = new Vector2(1f, 0f);
            holder.anchorMax = new Vector2(1f, 1f);
            holder.pivot = new Vector2(1f, 0.5f);
            holder.sizeDelta = new Vector2(it.ButtonWidth, 0f);
            ButtonLook(it, holder, button, 16f);
        }

        private static void ButtonLook(MenuItem it, RectTransform parent, string label, float size)
        {
            it.Glow = Img("Glow", parent, UiSprites.Glow(), stretch: true);
            it.Glow.rectTransform.offsetMin = new Vector2(-26f, -20f);
            it.Glow.rectTransform.offsetMax = new Vector2(26f, 20f);
            it.Fill = Img("Fill", parent, UiSprites.Rounded(14), stretch: true);
            it.Outline = Img("Outline", parent, UiSprites.Rounded(14, 2), stretch: true);
            it.Label = Txt("Label", parent, size, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(it.Label.rectTransform);
            it.Label.textWrappingMode = TextWrappingModes.NoWrap;
            it.Label.SetText(label);
        }

        private void Toggle(string key, string label, Func<bool> get, Action<bool> set)
        {
            MenuItem it = Add(MenuItemKind.Toggle, key, 34f);
            it.GetToggle = get;
            it.SetToggle = set;
            it.OnActivate = () => { set(!get()); UiFeel.Play(UiFeel.ToggleSound, 0.6f); };
            it.Box = Img("Box", it.Body, UiSprites.Rounded(7, 2), stretch: false);
            Place(it.Box.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(26f, 26f));
            it.Check = Img("Check", it.Box.rectTransform, UiSprites.Rounded(5), stretch: true);
            it.Check.rectTransform.offsetMin = new Vector2(5f, 5f);
            it.Check.rectTransform.offsetMax = new Vector2(-5f, -5f);
            it.Label = Txt("Label", it.Body, 18f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Stretch(it.Label.rectTransform);
            it.Label.rectTransform.offsetMin = new Vector2(40f, 0f);
            it.Label.SetText(label);
        }

        private void ToggleCvar(string key, string label, BoolCvar cvar) => Toggle(key, label, () => cvar.Value, v => cvar.Value = v);

        private void ToggleByName(string key, string label, string name)
        {
            if (!Game.Console.TryGetCvar(name, out Cvar c)) return;
            Toggle(key, label, () => c.ValueString != "0" && c.ValueString != "false", v => Game.Console.TrySet(name, v ? "1" : "0"));
        }

        private void Slider(string key, string label, FloatCvar cvar, float min, float max, string format)
        {
            MenuItem it = Add(MenuItemKind.Slider, key, 40f);
            it.Min = min;
            it.Max = max;
            it.Format = format;
            it.GetValue = () => cvar.Value;
            it.SetValue = v => cvar.Value = v;
            it.Label = Txt("Label", it.Body, 18f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Stretch(it.Label.rectTransform);
            it.Label.rectTransform.offsetMax = new Vector2(-260f, 0f);
            it.Label.SetText(label);
            it.Track = Img("Track", it.Body, UiSprites.Rounded(4), stretch: false);
            Place(it.Track.rectTransform, new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(180f, 8f), new Vector2(1f, 0.5f));
            it.Track.color = new Color(1f, 1f, 1f, 0.16f);
            it.TrackFill = Img("Fill", it.Track.rectTransform, UiSprites.Rounded(4), stretch: false);
            Place(it.TrackFill.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 8f), new Vector2(0f, 0.5f));
            it.Knob = Img("Knob", it.Track.rectTransform, UiSprites.Circle(), stretch: false);
            Place(it.Knob.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            it.Value = Txt("Value", it.Body, 16f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            Stretch(it.Value.rectTransform);
            it.Value.color = MenuTheme.Current.Dim;
        }

        private void Choice(string key, string label, int[] values, string[] labels, Func<int> get, Action<int> set)
        {
            MenuItem it = Add(MenuItemKind.Choice, key, 62f);
            it.ChoiceValues = values;
            it.ChoiceLabels = labels;
            it.GetChoice = get;
            it.SetChoice = set;
            it.Label = Txt("Label", it.Body, 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Stretch(it.Label.rectTransform);
            it.Label.SetText(label);
            it.OptionBacks = new Image[values.Length];
            it.Options = new TextMeshProUGUI[values.Length];
            float w = (Inner - (values.Length - 1) * 6f) / values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                Image back = Img("Option", it.Body, UiSprites.Rounded(10), stretch: false);
                Place(back.rectTransform, new Vector2(0f, 0f), new Vector2(i * (w + 6f) + w * 0.5f, 15f), new Vector2(w, 28f));
                TextMeshProUGUI t = Txt("Text", back.rectTransform, 15f, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(t.rectTransform);
                t.SetText(labels[i]);
                it.OptionBacks[i] = back;
                it.Options[i] = t;
            }
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>Stacks the items top-down (half-width buttons two to a row) and sizes the panel to fit the screen.</summary>
        private void Layout()
        {
            float y = 0f;
            for (int i = 0; i < _items.Count; i++)
            {
                MenuItem it = _items[i];
                bool pair = it.Half && i + 1 < _items.Count && _items[i + 1].Half;
                float w = it.Half ? Inner * 0.5f - 4f : Inner;
                it.Root.sizeDelta = new Vector2(w, it.Height);
                it.Home = new Vector2(it.Half ? -Inner * 0.25f + 2f : 0f, -y - it.Height * 0.5f);
                if (pair)
                {
                    MenuItem next = _items[i + 1];
                    next.Root.sizeDelta = new Vector2(w, next.Height);
                    next.Home = new Vector2(Inner * 0.25f - 2f, it.Home.y);
                    i++;
                }
                it.Root.anchoredPosition = it.Home;
                y += it.Height + Gap;
            }
            _contentHeight = Mathf.Max(0f, y - Gap);
            float canvasH = _canvasRt.rect.height;
            _viewportHeight = Mathf.Min(_contentHeight, canvasH - ScreenMargin * 2f - HeaderHeight - Pad * 2f);
            _content.sizeDelta = new Vector2(Inner, _contentHeight);
            _viewport.sizeDelta = new Vector2(Inner + 48f, _viewportHeight);
            _viewport.anchoredPosition = new Vector2(0f, -HeaderHeight - Pad * 0.5f);
            _panel.sizeDelta = new Vector2(PanelWidth, HeaderHeight + _viewportHeight + Pad * 1.5f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, -48f);
            _subtitle.rectTransform.anchoredPosition = new Vector2(0f, -94f);
        }

        // --- frame ----------------------------------------------------------------------------------------------------

        private void Update()
        {
            if (_rebinding.HasValue)
            {
                UpdateRebind();
            }
            else
            {
                Keyboard kb = Keyboard.current;
                Gamepad pad = Gamepad.current;
                bool escape = kb != null && kb.escapeKey.wasPressedThisFrame;
                bool start = pad != null && pad.startButton.wasPressedThisFrame;
                bool consoleOrPanel = DevConsoleView.IsOpen || MenuPanels.Open != null;
                if (!consoleOrPanel && !(Game.UiHasFocus && !Visible))
                {
                    if (Visible && (UiInput.Back || start))
                    {
                        if (_page == Page.Settings) { UiFeel.Back(); SwitchTo(Page.Home); }
                        else if (InSession) { UiFeel.Back(); _pauseOpen = false; }
                    }
                    else if (!Visible && InSession && (escape || start))
                    {
                        _pauseOpen = true;
                        UiFeel.Open();
                    }
                }
            }

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

            Animate();
        }

        private void UpdateRebind()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                _rebinding = null;
                UiFeel.Back();
                return;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && _rebinding == GameAction.Fire)
            {
                KeyBindings.BindMouse(GameAction.Fire);
                _rebinding = null;
                UiFeel.Confirm();
                return;
            }
            Key key = KeyBindings.AnyKeyPressedThisFrame();
            if (key == Key.None) return;
            KeyBindings.Bind(_rebinding.Value, key);
            _rebinding = null;
            UiFeel.Confirm();
        }

        private void Animate()
        {
            MenuTheme theme = MenuTheme.Current;
            float dt = Time.unscaledDeltaTime;
            bool calm = UiFeel.Calm;
            bool show = Visible && MenuPanels.Open == null;
            Vector2 canvasSize = _canvasRt.rect.size;

            _backdrop.Update(!show ? MenuBackdrop.Mode.Off : InSession ? MenuBackdrop.Mode.Pause : MenuBackdrop.Mode.Main, theme, dt, canvasSize);
            UpdateToast(theme, dt, calm);
            UpdateScoreboard();

            if (show != _shown)
            {
                _shown = show;
                _panelAge = 0f;
                if (show)
                {
                    _panel.gameObject.SetActive(true); // TextMeshPro sets its font up in Awake: measure only once active
                    Rebuild(true);
                    _signature = Signature();
                }
            }
            if (!show)
            {
                _panelGroup.alpha = Mathf.MoveTowards(_panelGroup.alpha, 0f, dt * 8f);
                if (_panelGroup.alpha <= 0f && _panel.gameObject.activeSelf) _panel.gameObject.SetActive(false);
                return;
            }
            if (!_panel.gameObject.activeSelf) _panel.gameObject.SetActive(true);

            // Page switches: slide out, rebuild, cascade in. Content changes on the same page: rebuild quietly.
            if (_switching)
            {
                _outT = Mathf.Min(1f, _outT + dt / Mathf.Max(0.01f, theme.PageOutSeconds));
                if (_outT >= 1f) { _switching = false; _outT = 0f; Rebuild(true); _signature = Signature(); }
            }
            else
            {
                int sig = Signature();
                if (sig != _signature)
                {
                    bool pageChanged = _builtPage != _page || _builtPause != _pauseOpen;
                    _signature = sig;
                    if (pageChanged && !calm) BeginSwitch();
                    else Rebuild(pageChanged);
                }
            }

            // Panel: pops in, the title's letters drop in one by one, then sway gently.
            _panelAge += dt;
            float p = Mathf.Clamp01(_panelAge / Mathf.Max(0.01f, theme.PanelInSeconds));
            _panelGroup.alpha = Mathf.Clamp01(p * 2f);
            float ps = calm ? 1f : Mathf.LerpUnclamped(0.92f, 1f, Ease.OutBack(p));
            _panel.localScale = new Vector3(ps, ps, 1f);
            _panelFill.color = theme.Panel;
            _panelEdge.color = theme.PanelEdge;
            _title.color = theme.Title;
            _subtitle.color = theme.Dim;
            _subtitle.SetText(Online ? $"v{Game.Version}  ·  Steam: {Game.Platform.LocalUserName}" : $"v{Game.Version}  ·  Offline");
            _titleAge += dt;
            AnimateTitle(calm);

            HandleInput(theme, dt);

            bool live = Time.unscaledTime >= _liveAt;
            if (live) _liveAt = Time.unscaledTime + LiveRefreshSeconds;
            for (int i = 0; i < _items.Count; i++)
            {
                MenuItem it = _items[i];
                it.Animate(theme, dt, i == _focus, calm, _switching ? Ease.OutCubic(_outT) : 0f);
                if (live && it.Live != null && it.Value != null) it.Value.SetText(it.Live());
            }

            // Scroll so the focused item stays in view; the selector arrow points at it.
            _scroll = Ease.Damp(_scroll, _scrollTarget, 14f, dt);
            _content.anchoredPosition = new Vector2(0f, _scroll);
            bool hasFocus = _focus >= 0 && _focus < _items.Count;
            _selector.enabled = hasFocus;
            if (hasFocus)
            {
                MenuItem f = _items[_focus];
                float bob = calm ? 0f : Mathf.Sin(Time.unscaledTime * 6f) * 4f;
                float left = f.Home.x - f.Root.sizeDelta.x * 0.5f;
                _selector.rectTransform.anchoredPosition = new Vector2(left - 16f + bob, f.Home.y);
                _selector.color = new Color(theme.Focus.r, theme.Focus.g, theme.Focus.b, f.Group.alpha);
            }
        }

        private void HandleInput(MenuTheme theme, float dt)
        {
            if (_switching || _rebinding.HasValue || DevConsoleView.IsOpen) return;
            int before = _focus;
            Vector2Int move = UiInput.Move;
            MenuItem focused = _focus >= 0 && _focus < _items.Count ? _items[_focus] : null;
            if (move.y != 0) _focus = NextFocusable(_focus, -move.y);
            if (move.x != 0 && focused != null)
            {
                if (focused.Kind == MenuItemKind.Slider) { focused.StepSlider(move.x); UiFeel.Hover(); }
                else if (focused.Kind == MenuItemKind.Choice) { focused.StepChoice(move.x); UiFeel.Hover(); }
                else if (focused.Half) _focus = NextFocusable(_focus, move.x);
            }

            Mouse mouse = Mouse.current;
            int hovered = -1;
            if (mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                bool insideView = RectTransformUtility.RectangleContainsScreenPoint(_viewport, mp, null);
                for (int i = 0; i < _items.Count && insideView; i++)
                    if (_items[i].Focusable && RectTransformUtility.RectangleContainsScreenPoint(_items[i].Root, mp, null)) hovered = i;
                if (hovered >= 0 && mouse.delta.ReadValue().sqrMagnitude > 0.01f) _focus = hovered;
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && insideView) _scrollTarget = Mathf.Clamp(_scrollTarget - Mathf.Sign(wheel) * 60f, 0f, Mathf.Max(0f, _contentHeight - _viewportHeight));

                // Sliders follow a drag; choice rows take a click on an option.
                if (mouse.leftButton.wasPressedThisFrame && hovered >= 0 && _items[hovered].Kind == MenuItemKind.Slider) _dragging = hovered;
                if (!mouse.leftButton.isPressed) _dragging = -1;
                if (_dragging >= 0)
                {
                    MenuItem s = _items[_dragging];
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(s.Track.rectTransform, mp, null, out Vector2 local);
                    float k = Mathf.Clamp01((local.x - s.Track.rectTransform.rect.xMin) / Mathf.Max(1f, s.Track.rectTransform.rect.width));
                    s.SetValue(Mathf.Lerp(s.Min, s.Max, k));
                }
                if (mouse.leftButton.wasPressedThisFrame && hovered >= 0 && _items[hovered].Kind == MenuItemKind.Choice)
                {
                    int option = _items[hovered].OptionAt(mp);
                    if (option >= 0) { _items[hovered].SetChoice(_items[hovered].ChoiceValues[option]); UiFeel.Confirm(); }
                }
            }
            if (_focus != before && _focus >= 0)
            {
                UiFeel.Hover();
                KeepInView(_items[_focus]);
            }

            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame && hovered >= 0 && hovered == _focus;
            bool confirm = UiInput.Confirm;
            if ((confirm || click) && _focus >= 0 && _focus < _items.Count)
            {
                MenuItem it = _items[_focus];
                if (it.Kind == MenuItemKind.Slider || it.Kind == MenuItemKind.Choice) return;
                if (!it.Enabled) { UiFeel.Error(); return; }
                it.Press = 1f;
                if (it.Kind != MenuItemKind.Toggle) UiFeel.Confirm();
                it.OnActivate?.Invoke();
            }
        }

        private int NextFocusable(int from, int step)
        {
            int n = _items.Count;
            for (int k = 1; k <= n; k++)
            {
                int i = from + k * step;
                if (i < 0 || i >= n) break;
                if (_items[i].Focusable) return i;
            }
            return from >= 0 && from < n && _items[from].Focusable ? from : FirstFocusable();
        }

        private int FirstFocusable()
        {
            for (int i = 0; i < _items.Count; i++) if (_items[i].Focusable) return i;
            return -1;
        }

        private void KeepInView(MenuItem it)
        {
            float top = -it.Home.y - it.Height * 0.5f, bottom = top + it.Height;
            if (top < _scrollTarget) _scrollTarget = Mathf.Max(0f, top - 12f);
            else if (bottom > _scrollTarget + _viewportHeight) _scrollTarget = Mathf.Min(_contentHeight - _viewportHeight, bottom - _viewportHeight + 12f);
        }

        private void AnimateTitle(bool calm)
        {
            _title.ForceMeshUpdate();
            TMP_TextInfo info = _title.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible) continue;
                float t = Mathf.Clamp01((_titleAge - i * 0.045f) / 0.4f);
                float drop = calm ? 0f : (1f - Ease.OutBack(t)) * 36f;
                float sway = calm ? 0f : Mathf.Sin(Time.unscaledTime * 2.2f + i * 0.5f) * 2.2f;
                var offset = new Vector3(0f, drop + sway, 0f);
                Vector3[] v = info.meshInfo[c.materialReferenceIndex].vertices;
                Color32[] col = info.meshInfo[c.materialReferenceIndex].colors32;
                byte a = (byte)(255 * Mathf.Clamp01(t * 2f));
                for (int k = 0; k < 4; k++)
                {
                    v[c.vertexIndex + k] += offset;
                    col[c.vertexIndex + k].a = a;
                }
            }
            _title.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        private void UpdateToast(MenuTheme theme, float dt, bool calm)
        {
            bool on = !Visible && Time.unscaledTime <= _toastUntil && !string.IsNullOrEmpty(_toast);
            if (_toastRt.gameObject.activeSelf != on)
            {
                _toastRt.gameObject.SetActive(on);
                if (on)
                {
                    _toastText.SetText(_toast);
                    Vector2 size = _toastText.GetPreferredValues(_toast, 900f, 0f);
                    _toastRt.sizeDelta = new Vector2(Mathf.Min(980f, size.x + 48f), size.y + 22f);
                    _toastAge = 0f;
                }
            }
            if (!on) return;
            _toastAge += dt;
            float t = Mathf.Clamp01(_toastAge / 0.4f);
            float left = _toastUntil - Time.unscaledTime;
            _toastGroup.alpha = Mathf.Clamp01(t * 2f) * Mathf.Clamp01(left / 0.4f);
            _toastRt.anchoredPosition = new Vector2(0f, -40f + (calm ? 0f : (1f - Ease.OutBack(t)) * 50f));
            _toastFill.color = new Color(theme.Panel.r, theme.Panel.g, theme.Panel.b, 0.85f);
            _toastText.color = theme.Text;
        }

        private void UpdateScoreboard()
        {
            bool on = InSession && !Visible && KeyBindings.IsPressed(GameAction.Scoreboard) && !DevConsoleView.IsOpen;
            if (_board.gameObject.activeSelf != on) _board.gameObject.SetActive(on);
            if (!on || Time.unscaledTime < _boardAt) return;
            _boardAt = Time.unscaledTime + LiveRefreshSeconds; // pings change slowly: no garbage every frame while Tab is held
            IReadOnlyList<PartyMember> members = PartyRoster.Members;
            var sb = new System.Text.StringBuilder(128);
            sb.Append("<b><color=#FF9E26>Party  ").Append(members.Count).Append('/').Append(GameIdentity.MaxPartySize).Append("</color></b>");
            if (PartyRoster.Locked) sb.Append("  · locked");
            foreach (PartyMember m in members)
                sb.Append('\n').Append(m.Name).Append(m.IsHost ? "  (host)" : string.Empty).Append("<pos=78%>").Append(m.IsHost ? "-" : m.Ping + " ms");
            string text = sb.ToString();
            if (text == _boardShown) return;
            _boardShown = text;
            _boardText.SetText(text);
            _board.sizeDelta = new Vector2(440f, 56f + members.Count * 26f);
            _board.anchoredPosition = new Vector2(0f, -80f);
        }
    }
}
