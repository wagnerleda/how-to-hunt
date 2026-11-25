using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HowToHunt.Travel
{
    /// <summary>
    /// Consulting a totem: every part of the world in the order it unfolds. The ones you have been to can be chosen;
    /// the rest stay "Unknown land" (where they are is a secret until you find the way); the one you stand in says
    /// "You are here". Up/down (keyboard, D-pad, stick) or the mouse select, confirm or a click travels, back or
    /// Interact closes. uGUI, built once; rows are reused.
    /// </summary>
    public sealed class TotemMenu : MonoBehaviour
    {
        private const float PanelW = 760f, RowH = 92f, RowGap = 12f, Top = 150f, Bottom = 70f, MaxRows = 8;
        private static readonly Color Glass = new Color(0.1f, 0.08f, 0.07f, 0.94f), Edge = new Color(0.95f, 0.75f, 0.4f, 0.55f);
        private static readonly Color Cream = new Color(1f, 0.95f, 0.85f), Dim = new Color(0.78f, 0.72f, 0.62f), Gold = new Color(1f, 0.8f, 0.38f);
        private static readonly Color RowIdle = new Color(1f, 1f, 1f, 0.05f), RowLocked = new Color(0f, 0f, 0f, 0.25f);

        private sealed class Row
        {
            public RegionDefinition Region;
            public Image Back, Swatch;
            public TextMeshProUGUI Name, Line, State;
            public bool Selectable;
        }

        private static TotemMenu _instance;
        private RectTransform _panel;
        private TextMeshProUGUI _title, _subtitle;
        private readonly List<Row> _rows = new List<Row>();
        private Totem _totem;
        private bool _open;
        private int _selected = -1;
        private int _openedFrame;
        private float _openedAt;

        public static bool IsOpen => _instance != null && _instance._open;

        private void Awake()
        {
            _instance = this;
            EnsureEventSystem();
            if (!Hud.Root.TryGetComponent(out GraphicRaycaster _)) Hud.Root.gameObject.AddComponent<GraphicRaycaster>();
            Image panel = Hud.Rect("Totem Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW, 400f), Glass);
            panel.sprite = UiSprites.Rounded(26);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            _panel = panel.rectTransform;
            Image rim = Child<Image>("Rim", _panel);
            Stretch(rim.rectTransform);
            rim.sprite = UiSprites.Rounded(26, 2);
            rim.type = Image.Type.Sliced;
            rim.color = Edge;
            rim.raycastTarget = false;
            _title = UiFonts.Use(Text("Title", _panel, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(PanelW - 80f, 70f), 56f, Cream, TextAlignmentOptions.Top));
            _title.SetText("The Totem");
            _subtitle = Text("Subtitle", _panel, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(PanelW - 80f, 28f), 19f, Dim, TextAlignmentOptions.Top);
            _subtitle.SetText("Its eyes glow. Where do you want to go?");
            Text("Hint", _panel, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(PanelW - 80f, 26f), 17f, Dim, TextAlignmentOptions.Bottom)
                .SetText($"Choose a land, {KeyBindings.DisplayName(GameAction.Interact)} or Esc to close");
            _panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_open) Game.PopUiFocus(this);
            if (_instance == this) _instance = null;
        }

        public static bool Open(Totem totem)
        {
            if (_instance == null || _instance._open || TravelTransition.Busy || totem == null) return false;
            TotemMenu s = _instance;
            s._totem = totem;
            s._open = true;
            s._openedFrame = Time.frameCount;
            s._openedAt = Time.unscaledTime;
            s.Build();
            s._panel.gameObject.SetActive(true);
            Game.PushUiFocus(s);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Sfx.Play2D("open_001", 0.4f, 0.8f);
            return true;
        }

        private void Close(bool sound = true)
        {
            _open = false;
            _panel.gameObject.SetActive(false);
            Game.PopUiFocus(this);
            HowToHunt.Player.PlayerCamera.LockCursor();
            if (sound) Sfx.Play2D("close_001", 0.35f);
        }

        private void Build()
        {
            List<RegionDefinition> regions = ContentRegistry.All<RegionDefinition>();
            regions.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Key, b.Key));
            EconomyProfile me = EconomyNet.Local;
            int n = Mathf.Min(regions.Count, (int)MaxRows);
            while (_rows.Count < n) _rows.Add(MakeRow(_rows.Count));
            float height = Top + n * (RowH + RowGap) - RowGap + Bottom;
            _panel.sizeDelta = new Vector2(PanelW, height);
            _selected = -1;
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                bool shown = i < n;
                row.Back.gameObject.SetActive(shown);
                if (!shown) continue;
                RegionDefinition r = regions[i];
                row.Region = r;
                bool known = TravelNet.Known(me, r);
                bool here = _totem.Region == r;
                bool hasTotem = Totem.Of(r) != null;
                row.Selectable = known && !here && hasTotem;
                row.Back.rectTransform.anchoredPosition = new Vector2(0f, -Top - i * (RowH + RowGap));
                row.Swatch.color = known ? r.Tint : new Color(0.3f, 0.28f, 0.26f);
                row.Name.SetText(known ? r.DisplayName : "Unknown land");
                row.Line.SetText(known ? r.Tagline : "Find the way there first.");
                row.State.SetText(here ? "You are here" : !known ? "Undiscovered" : hasTotem ? "Travel" : "No totem");
                row.State.color = here ? Gold : row.Selectable ? Cream : Dim;
                row.Name.color = known ? Cream : Dim;
                if (row.Selectable && _selected < 0) _selected = i;
            }
            Highlight();
        }

        private Row MakeRow(int index)
        {
            var row = new Row();
            row.Back = Child<Image>("Region " + index, _panel);
            Place(row.Back.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(PanelW - 80f, RowH), new Vector2(0.5f, 1f));
            row.Back.sprite = UiSprites.Rounded(18);
            row.Back.type = Image.Type.Sliced;
            row.Back.raycastTarget = true;
            row.Swatch = Child<Image>("Swatch", row.Back.rectTransform);
            Place(row.Swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(54f, 54f), new Vector2(0f, 0.5f));
            row.Swatch.sprite = UiSprites.Circle();
            row.Swatch.raycastTarget = false;
            row.Name = UiFonts.Use(Text("Name", row.Back.rectTransform, new Vector2(0f, 1f), new Vector2(96f, -12f), new Vector2(420f, 44f), 34f, Cream, TextAlignmentOptions.TopLeft));
            row.Line = Text("Line", row.Back.rectTransform, new Vector2(0f, 0f), new Vector2(96f, 12f), new Vector2(440f, 28f), 17f, Dim, TextAlignmentOptions.BottomLeft);
            row.State = UiFonts.Use(Text("State", row.Back.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(170f, 40f), 26f, Cream, TextAlignmentOptions.Right));
            int captured = index;
            Button b = row.Back.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Pick(captured));
            var hover = row.Back.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (_rows[captured].Selectable && _selected != captured) { _selected = captured; Highlight(); UiFeel.Hover(); } });
            hover.triggers.Add(enter);
            return row;
        }

        private void Update()
        {
            if (!_open) return;
            if (Time.frameCount == _openedFrame) return; // the press that opened it
            if (UiInput.Back || (KeyBindings.WasPressedThisFrame(GameAction.Interact) && Time.unscaledTime - _openedAt > 0.1f)) { Close(); return; }
            int move = -UiInput.Move.y;
            if (move != 0) Step(move);
            if (UiInput.Confirm && _selected >= 0) Pick(_selected);
            // Walked off (pushed, a teammate's blast): the totem no longer hears you.
            var me = HowToHunt.Player.PlayerMotor.Local;
            if (me == null || !_totem.InReach(me.transform.position, TravelNet.Reach.Value)) Close();
        }

        private void Step(int dir)
        {
            int n = 0;
            for (int i = 0; i < _rows.Count; i++) if (_rows[i].Back.gameObject.activeSelf) n++;
            if (n == 0) return;
            int at = _selected < 0 ? (dir > 0 ? -1 : n) : _selected;
            for (int k = 0; k < n; k++)
            {
                at = (at + dir + n) % n;
                if (!_rows[at].Selectable) continue;
                if (at != _selected) { _selected = at; Highlight(); UiFeel.Hover(); }
                return;
            }
        }

        private void Pick(int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            Row row = _rows[index];
            if (!row.Selectable) { UiFeel.Error(); return; }
            Close(sound: false);
            UiFeel.Confirm();
            TravelTransition.Begin(row.Region);
        }

        private void Highlight()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                bool on = i == _selected;
                row.Back.color = on ? new Color(row.Region != null ? row.Region.Tint.r : 1f, row.Region != null ? row.Region.Tint.g : 1f, row.Region != null ? row.Region.Tint.b : 1f, 0.28f)
                    : row.Selectable ? RowIdle : RowLocked;
                row.Back.rectTransform.localScale = on ? new Vector3(1.02f, 1.02f, 1f) : Vector3.one;
            }
        }

        // --- building blocks ----------------------------------------------------------------------------------

        private static T Child<T>(string name, RectTransform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI Text(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
        {
            TextMeshProUGUI t = Hud.Text(name, anchor, pos, fontSize, align, color);
            t.rectTransform.SetParent(parent, false);
            Place(t.rectTransform, anchor, pos, size, anchor);
            return t;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("[EventSystem]", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            DontDestroyOnLoad(go);
        }
    }
}
