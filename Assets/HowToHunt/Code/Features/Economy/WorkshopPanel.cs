using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Hunting;
using HowToHunt.Player;
using HowToHunt.Sim.Hunting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Economy
{
    /// <summary>
    /// The Gunsmith's counter (Phase 5, made 3D on 2026-11-24): the camera glides from your eyes down onto the counter,
    /// where the gun lies out on its felt mat in the real world (with its fitted attachments, turning slowly under a
    /// lamp), and the customization sits around it on glass panels: one box per attachment slot joined to its mount by
    /// a leader line (locked slots greyed), the guns along the top, the slot's attachments, the action and the gun's
    /// numbers in a column on the right (damage, fire rate, magazine; hip fire, precision, control, mobility bars).
    /// Selecting an attachment previews it on the gun and in the bars before you buy or fit it. A camp gunsmith sells
    /// only the camp guns (the Plains: the Old Revolver); the full armory (the movement lab) sells every gun.
    /// </summary>
    public sealed class WorkshopPanel : MonoBehaviour
    {
        private static readonly AttachmentSlot[] Slots =
            { AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Underbarrel, AttachmentSlot.Magazine, AttachmentSlot.Ammo, AttachmentSlot.Optic, AttachmentSlot.Top, AttachmentSlot.Stock };
        // Box centres around the gun (canvas units from the screen centre), muzzle on the left.
        private static readonly Vector2[] SlotBoxes =
        {
            new Vector2(-840f, 20f), new Vector2(-700f, -250f), new Vector2(-430f, -330f), new Vector2(-170f, -330f),
            new Vector2(90f, -330f), new Vector2(-300f, 290f), new Vector2(-580f, 290f), new Vector2(240f, 250f),
        };
        /// <summary>Where the gun sits on screen (canvas units from the centre): left of the right-hand column.</summary>
        private static readonly Vector2 GunOnScreen = new Vector2(-260f, -60f);
        private static readonly Vector2 SlotBoxSize = new Vector2(200f, 96f);
        private const float ListX = 660f, ColumnWidth = 600f, RowHeight = 76f, RowStep = 82f, RowWidth = 556f, BarWidth = 340f;
        private const float ListTop = 290f, ActionY = -45f, StatsY = -150f, BarsY = -255f;

        // The shot: the camera this far from the gun, looking down a little; the gun fills this share of the width.
        private const float ShotDistance = 1.9f, ShotPitch = 36f, ShotMinFov = 18f, ShotMaxFov = 45f, GunScreenShare = 0.32f, ShotMinDistance = 1.2f;
        private const float LieHeight = 0.025f, SwayYaw = 7f;

        private static readonly Color Glass = new Color(0.06f, 0.07f, 0.08f, 0.74f);
        private static readonly Color Box = new Color(0.06f, 0.07f, 0.08f, 0.66f);
        private static readonly Color BoxSelected = new Color(0.36f, 0.27f, 0.12f, 0.85f);
        private static readonly Color BoxLocked = new Color(0.06f, 0.07f, 0.08f, 0.3f);
        private static readonly Color Edge = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color Accent = new Color(1f, 0.82f, 0.45f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color Better = new Color(0.45f, 0.85f, 0.4f);
        private static readonly Color Worse = new Color(0.95f, 0.4f, 0.3f);
        private static readonly Color Muted = new Color(0.4f, 0.38f, 0.35f);

        private static WorkshopPanel _instance;

        private RectTransform _root;
        private CanvasGroup _group;
        private TextMeshProUGUI _title, _gunName, _wallet, _listTitle, _actionLabel, _empty;
        private Image _action;
        private readonly List<SlotBox> _boxes = new List<SlotBox>();
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Tab> _tabs = new List<Tab>();
        private TextMeshProUGUI _dmg, _rof, _mag;
        private readonly Bar[] _bars = new Bar[4];

        // The gun on the counter: a pivot (sways) holding the model, and a lamp on it.
        private Transform _pivot;
        private GameObject _model;
        private Light _lamp;
        private ToolDefinition _modelTool;
        private string _modelKey;
        private float _gunLength;

        private CampStation _station;
        private bool _open, _closeRequested;
        private int _openedFrame;
        private float _openedAt;
        private ToolDefinition _tool;
        private AttachmentSlot _slot = AttachmentSlot.Muzzle;
        private ToolDefinition _slotFor;
        private AttachmentDefinition _selected;
        private readonly List<AttachmentDefinition> _preview = new List<AttachmentDefinition>();

        private sealed class SlotBox { public AttachmentSlot Slot; public Image Image, Line, Dot; public TextMeshProUGUI Name, Value; public Button Button; }
        private sealed class Row { public AttachmentDefinition Attachment; public Image Image; public TextMeshProUGUI Name, Detail, Status; }
        private sealed class Tab { public ToolDefinition Tool; public Image Image; public TextMeshProUGUI Label; }
        private sealed class Bar { public Image Fill, Delta; public TextMeshProUGUI Value; }

        public static bool IsOpen => _instance != null && _instance._open;

        private void Awake()
        {
            _instance = this;
            _root = new GameObject("Workshop", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(Hud.Root, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            // Clicks anywhere stay in the panel (nothing behind it to press), but the picture is the real counter.
            Image catcher = _root.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            // Header: what you're looking at and your purse, on a glass plate top left.
            Glassy(Panel("Header", new Vector2(0f, 1f), new Vector2(380f, -112f), new Vector2(680f, 190f), Glass));
            _title = Label("Title", new Vector2(0f, 1f), new Vector2(66f, -38f), 24f, Dim, TextAlignmentOptions.TopLeft);
            _gunName = Label("Gun", new Vector2(0f, 1f), new Vector2(66f, -70f), 60f, Color.white, TextAlignmentOptions.TopLeft);
            _gunName.fontStyle = FontStyles.Bold;
            _wallet = Label("Wallet", new Vector2(0f, 1f), new Vector2(66f, -150f), 28f, Accent, TextAlignmentOptions.TopLeft);
            Label("Hint", new Vector2(0f, 1f), new Vector2(40f, -222f), 18f, Dim, TextAlignmentOptions.TopLeft)
                .SetText($"{KeyBindings.DisplayName(GameAction.Interact)}, Esc or gamepad B to step back from the counter");
            _empty = Label("Empty", new Vector2(0.5f, 0.5f), GunOnScreen, 34f, Color.white, TextAlignmentOptions.Center);
            _empty.SetText("No guns on this counter yet.");

            for (int i = 0; i < Slots.Length; i++) _boxes.Add(MakeBox(Slots[i], SlotBoxes[i]));

            // The right-hand column: the slot's attachments, the action, the numbers.
            Glassy(Panel("Column", new Vector2(0.5f, 0.5f), new Vector2(ListX, -30f), new Vector2(ColumnWidth, 820f), Glass));
            _listTitle = Label("List Title", new Vector2(0.5f, 0.5f), new Vector2(ListX, ListTop + 58f), 28f, Accent, TextAlignmentOptions.Center);
            _listTitle.fontStyle = FontStyles.Bold;
            _action = Panel("Action", new Vector2(0.5f, 0.5f), new Vector2(ListX, ActionY), new Vector2(340f, 58f), Better);
            Round(_action, 14);
            _action.raycastTarget = true;
            _action.gameObject.AddComponent<Button>().onClick.AddListener(OnAction);
            _actionLabel = Child(_action.rectTransform, "Action Label", Vector2.zero, new Vector2(340f, 58f), 24f, Color.white, TextAlignmentOptions.Center);
            _actionLabel.fontStyle = FontStyles.Bold;

            _dmg = BigStat("DMG", new Vector2(ListX - 180f, StatsY));
            _rof = BigStat("ROF", new Vector2(ListX, StatsY));
            _mag = BigStat("MAG", new Vector2(ListX + 180f, StatsY));
            string[] names = { "HIP FIRE", "PRECISION", "CONTROL", "MOBILITY" };
            for (int i = 0; i < 4; i++) _bars[i] = MakeBar(names[i], new Vector2(ListX, BarsY - i * 44f));

            _root.gameObject.SetActive(false);
            EconomyNet.LocalChanged += OnChanged;
        }

        private void OnDestroy()
        {
            EconomyNet.LocalChanged -= OnChanged;
            if (_open)
            {
                Game.PopUiFocus(this);
                PlayerCamera.EndShot();
            }
            if (_pivot != null) Destroy(_pivot.gameObject);
            if (_instance == this) _instance = null;
        }

        // --- building blocks ----------------------------------------------------------------------------------

        private void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private TextMeshProUGUI Label(string name, Vector2 anchor, Vector2 pos, float size, Color color, TextAlignmentOptions align)
        {
            TextMeshProUGUI t = Hud.Text(name, anchor, pos, size, align, color);
            t.rectTransform.SetParent(_root, false);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = anchor;
            t.rectTransform.pivot = anchor;
            t.rectTransform.anchoredPosition = pos;
            return t;
        }

        private Image Panel(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            Image i = Hud.Rect(name, anchor, pos, size, color);
            Place(i.rectTransform, anchor, pos, size);
            return i;
        }

        private static void Round(Image i, int radius)
        {
            i.sprite = UiSprites.Rounded(radius);
            i.type = Image.Type.Sliced;
        }

        /// <summary>A rounded glass plate with a faint rim.</summary>
        private static void Glassy(Image i, int radius = 18)
        {
            Round(i, radius);
            var rim = new GameObject("Rim", typeof(RectTransform)).AddComponent<Image>();
            rim.rectTransform.SetParent(i.rectTransform, false);
            rim.rectTransform.anchorMin = Vector2.zero;
            rim.rectTransform.anchorMax = Vector2.one;
            rim.rectTransform.offsetMin = rim.rectTransform.offsetMax = Vector2.zero;
            rim.sprite = UiSprites.Rounded(radius, 2);
            rim.type = Image.Type.Sliced;
            rim.color = Edge;
            rim.raycastTarget = false;
        }

        private TextMeshProUGUI Child(RectTransform parent, string name, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
        {
            TextMeshProUGUI t = Hud.Text(name, new Vector2(0.5f, 0.5f), Vector2.zero, fontSize, align, color);
            t.rectTransform.SetParent(parent, false);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.rectTransform.anchoredPosition = pos;
            t.rectTransform.sizeDelta = size;
            return t;
        }

        private SlotBox MakeBox(AttachmentSlot slot, Vector2 pos)
        {
            var b = new SlotBox { Slot = slot };
            b.Line = Panel($"Line {slot}", new Vector2(0.5f, 0.5f), pos, new Vector2(10f, 2f), new Color(1f, 0.9f, 0.7f, 0.5f));
            b.Dot = Panel($"Dot {slot}", new Vector2(0.5f, 0.5f), pos, new Vector2(12f, 12f), Accent);
            b.Dot.sprite = UiSprites.Circle();
            b.Image = Panel($"Slot {slot}", new Vector2(0.5f, 0.5f), pos, SlotBoxSize, Box);
            Glassy(b.Image, 14);
            b.Image.raycastTarget = true;
            b.Button = b.Image.gameObject.AddComponent<Button>();
            b.Button.onClick.AddListener(() => SelectSlot(slot));
            b.Name = Child(b.Image.rectTransform, "Name", new Vector2(0f, 26f), new Vector2(190f, 28f), 17f, Dim, TextAlignmentOptions.Center);
            b.Name.SetText(slot.ToString().ToUpperInvariant());
            b.Value = Child(b.Image.rectTransform, "Value", new Vector2(0f, -12f), new Vector2(186f, 52f), 20f, Color.white, TextAlignmentOptions.Center);
            b.Value.textWrappingMode = TextWrappingModes.Normal;
            return b;
        }

        private TextMeshProUGUI BigStat(string name, Vector2 pos)
        {
            Label($"{name} Label", new Vector2(0.5f, 0.5f), pos + new Vector2(0f, 44f), 18f, Dim, TextAlignmentOptions.Center).SetText(name);
            TextMeshProUGUI t = Label(name, new Vector2(0.5f, 0.5f), pos, 48f, Color.white, TextAlignmentOptions.Center);
            t.rectTransform.sizeDelta = new Vector2(170f, 64f);
            t.fontStyle = FontStyles.Bold;
            return t;
        }

        private Bar MakeBar(string name, Vector2 pos)
        {
            TextMeshProUGUI label = Label($"{name} Label", new Vector2(0.5f, 0.5f), pos + new Vector2(-BarWidth * 0.5f, 14f), 17f, Dim, TextAlignmentOptions.BottomLeft);
            label.rectTransform.pivot = new Vector2(0f, 0f);
            label.rectTransform.sizeDelta = new Vector2(240f, 24f);
            label.SetText(name);
            var bar = new Bar();
            bar.Value = Label($"{name} Value", new Vector2(0.5f, 0.5f), pos + new Vector2(BarWidth * 0.5f, 14f), 17f, Color.white, TextAlignmentOptions.BottomRight);
            bar.Value.rectTransform.pivot = new Vector2(1f, 0f);
            bar.Value.rectTransform.sizeDelta = new Vector2(120f, 24f);
            Round(Panel($"{name} Back", new Vector2(0.5f, 0.5f), pos, new Vector2(BarWidth, 8f), new Color(1f, 1f, 1f, 0.12f)), 4);
            bar.Delta = Panel($"{name} Delta", new Vector2(0.5f, 0.5f), pos, new Vector2(0f, 8f), Better);
            bar.Fill = Panel($"{name} Fill", new Vector2(0.5f, 0.5f), pos, new Vector2(0f, 8f), Color.white);
            foreach (Image i in new[] { bar.Delta, bar.Fill })
            {
                i.rectTransform.pivot = new Vector2(0f, 0.5f);
                i.rectTransform.anchoredPosition = pos - new Vector2(BarWidth * 0.5f, 0f);
            }
            return bar;
        }

        // --- open / close ---------------------------------------------------------------------------------------

        public static void Open(CampStation station)
        {
            if (_instance == null || _instance._open || station == null) return;
            WorkshopPanel w = _instance;
            w._station = station;
            w._open = true;
            w._openedFrame = Time.frameCount;
            w._openedAt = Time.unscaledTime;
            w._selected = null;
            w._tool = null;
            w._slotFor = null;
            if (w._model != null) Destroy(w._model); // laid out fresh on this counter
            w._model = null;
            w._modelKey = null;
            w._title.SetText($"{station.Label.ToUpperInvariant()}  /  CUSTOMIZE");
            w.BuildTabs();
            ToolDefinition held = PlayerTools.Local != null ? PlayerTools.Local.Equipped : null;
            foreach (Tab t in w._tabs) if (t.Tool == held) w._tool = held; // start on the gun in your hands
            if (w._tool == null && w._tabs.Count > 0) w._tool = w._tabs[0].Tool;
            w._root.gameObject.SetActive(true);
            w._group.alpha = 0f;
            w.Refresh();
            Game.PushUiFocus(w);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Sfx.Play2D("Kenney/open_001", 0.4f);
        }

        private void Close()
        {
            _open = false;
            _root.gameObject.SetActive(false);
            PlayerCamera.EndShot();
            if (_pivot != null) _pivot.gameObject.SetActive(false);
            Game.PopUiFocus(this);
            PlayerCamera.LockCursor();
            Sfx.Play2D("Kenney/close_001", 0.35f);
        }

        private void BuildTabs()
        {
            foreach (Tab t in _tabs) Destroy(t.Image.gameObject);
            _tabs.Clear();
            var tools = Loadouts.AllTools;
            for (int i = 0; i < tools.Count; i++)
            {
                ToolDefinition tool = tools[i];
                // Your own guns (fit them anywhere), and the guns this counter sells.
                if (tool.Archetype != ToolArchetype.Firearm || (!Loadouts.Local.Owns(tool) && GunUnlock(tool) == null)) continue;
                var tab = new Tab { Tool = tool };
                // Two rows of five along the top, right of the header.
                int col = _tabs.Count % 5, rowIndex = _tabs.Count / 5;
                tab.Image = Panel($"Tab {tool.Key}", new Vector2(0.5f, 1f), new Vector2(-110f + col * 214f, -62f - rowIndex * 54f), new Vector2(204f, 46f), Box);
                Glassy(tab.Image, 12);
                tab.Image.raycastTarget = true;
                tab.Image.gameObject.AddComponent<Button>().onClick.AddListener(() => { _tool = tool; _selected = null; Sfx.Play2D("Kenney/click_001", 0.4f); Refresh(); });
                tab.Label = Child(tab.Image.rectTransform, "Label", Vector2.zero, new Vector2(196f, 46f), 17f, Color.white, TextAlignmentOptions.Center);
                UpgradeDefinition unlock = Loadouts.Local.Owns(tool) ? null : GunUnlock(tool);
                tab.Label.SetText(unlock == null ? tool.DisplayName : $"{tool.DisplayName}  <color=#F2C66F>{unlock.CostCoins:0}c</color>");
                _tabs.Add(tab);
            }
        }

        /// <summary>This counter's upgrade that unlocks the gun (null if it isn't sold here).</summary>
        private UpgradeDefinition GunUnlock(ToolDefinition tool)
        {
            foreach (UpgradeDefinition u in Loadouts.AllUpgrades)
                if (u.Effect == UpgradeEffect.UnlockTool && u.Tool == tool && _station != null && _station.Sells(u)) return u;
            return null;
        }

        private void SelectSlot(AttachmentSlot slot)
        {
            if (_tool == null || !_tool.TryAnchor(slot, out _)) return;
            _slot = slot;
            _selected = null;
            Sfx.Play2D("Kenney/click_001", 0.35f, 1.1f);
            Refresh();
        }

        private void OnChanged(EconomyStateBroadcast msg)
        {
            if (!_open) return;
            BuildTabs(); // a gun bought here drops its price tag
            Refresh();
        }

        // --- the gun on the counter -----------------------------------------------------------------------------

        /// <summary>The gun on the counter with the fitted attachments plus the one being previewed.</summary>
        private void ShowModel(List<AttachmentDefinition> fitted)
        {
            string key = _tool != null ? _tool.Key : string.Empty;
            foreach (AttachmentDefinition a in fitted) key += "|" + a.Key;
            if (key == _modelKey && _model != null) return;
            _modelKey = key;
            if (_model != null && _modelTool != _tool) { Destroy(_model); _model = null; }
            _modelTool = _tool;
            EnsurePivot();
            if (_tool == null || _tool.Viewmodel == null)
            {
                _pivot.position = _station.CounterPoint + Vector3.up * 0.2f;
                FrameShot(0.5f);
                return;
            }
            if (_model == null)
            {
                _pivot.SetPositionAndRotation(_station.CounterPoint, Lying(0f));
                _model = Instantiate(_tool.Viewmodel, _pivot, false);
                IconRenderer.HideParts(_model, "Hand");
                foreach (Animator an in _model.GetComponentsInChildren<Animator>()) an.enabled = false;
                foreach (Renderer r in _model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                // Lay it on the felt: the model's middle on the pivot, its lowest side on the mat.
                Bounds b = Bounds(_model);
                _model.transform.position += _pivot.position - b.center;
                _pivot.position = _station.CounterPoint + Vector3.up * (b.extents.y + LieHeight);
                _gunLength = Mathf.Max(b.size.x, b.size.z); // lying flat: its length is across the counter
                FrameShot(_gunLength);
            }
            AttachmentVisuals.Apply(_model, _tool, fitted);
        }

        private void EnsurePivot()
        {
            if (_pivot == null)
            {
                _pivot = new GameObject("Workshop Gun").transform;
                var lampGo = new GameObject("Workshop Lamp", typeof(Light));
                lampGo.transform.SetParent(_pivot, false);
                _lamp = lampGo.GetComponent<Light>();
                _lamp.type = LightType.Spot;
                _lamp.spotAngle = 55f;
                _lamp.range = 4f;
                _lamp.intensity = 5f;
                _lamp.color = new Color(1f, 0.9f, 0.76f);
                _lamp.shadows = LightShadows.None;
            }
            _pivot.gameObject.SetActive(true);
        }

        /// <summary>
        /// Frame the counter: a camera on your side, a little above it, looking down at the gun so it sits left of the
        /// right-hand column and fills about 40 % of the width (a longer gun widens the lens rather than backing off).
        /// </summary>
        private void FrameShot(float length)
        {
            Camera cam = PlayerCamera.Camera;
            if (cam == null || _station == null) return;
            Vector3 right = _station.transform.right, fwd = _station.transform.forward;
            Quaternion look = Quaternion.LookRotation(Quaternion.AngleAxis(ShotPitch, right) * fwd, Vector3.up);
            Vector3 camFwd = look * Vector3.forward, camRight = look * Vector3.right, camUp = look * Vector3.up;
            float aspect = Mathf.Max(0.5f, cam.aspect);
            float tanH = Mathf.Max(0.05f, length) / (2f * GunScreenShare * ShotDistance);
            float vfov = Mathf.Clamp(2f * Mathf.Atan(tanH / aspect) * Mathf.Rad2Deg, ShotMinFov, ShotMaxFov);
            float tanV = Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad);
            tanH = tanV * aspect;
            float dist = Mathf.Max(ShotMinDistance, length / (2f * GunScreenShare * tanH));
            // Put the gun at GunOnScreen: aim the camera to the side of it by that much of the view.
            float canvasW = 1080f * aspect;
            float offX = GunOnScreen.x / canvasW, offY = GunOnScreen.y / 1080f;
            Vector3 gun = _pivot.position;
            Vector3 aim = gun - camRight * (offX * 2f * tanH * dist) - camUp * (offY * 2f * tanV * dist);
            PlayerCamera.FrameShot(aim - camFwd * dist, look, vfov);
            // The lamp: above and to the left of the camera, pointing at the gun.
            Vector3 lampAt = gun - camFwd * 1.1f + Vector3.up * 0.9f - camRight * 0.5f;
            _lamp.transform.SetPositionAndRotation(lampAt, Quaternion.LookRotation(gun - lampAt));
        }

        /// <summary>Lying on its side on the counter: muzzle to your left, grip toward you, turned by yaw degrees.</summary>
        private Quaternion Lying(float yaw)
        {
            Vector3 right = _station.transform.right, fwd = _station.transform.forward;
            return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.LookRotation(-right, fwd);
        }

        private static Bounds Bounds(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // --- refresh ----------------------------------------------------------------------------------------------

        private void Refresh()
        {
            EconomyProfile p = EconomyNet.Local;
            _wallet.SetText($"{p.Coins.ToShortString()} coins");
            bool hasGun = _tool != null;
            _empty.gameObject.SetActive(!hasGun);
            _action.gameObject.SetActive(hasGun);
            _listTitle.gameObject.SetActive(hasGun);
            foreach (Tab t in _tabs) t.Image.color = t.Tool == _tool ? BoxSelected : Box;
            foreach (SlotBox b in _boxes) { b.Image.gameObject.SetActive(hasGun); b.Line.gameObject.SetActive(hasGun); b.Dot.gameObject.SetActive(hasGun); }
            foreach (Row r in _rows) Destroy(r.Image.gameObject);
            _rows.Clear();
            if (!hasGun)
            {
                _gunName.SetText(string.Empty);
                SetStats(null, null);
                if (_model != null) { Destroy(_model); _model = null; }
                _modelKey = null;
                ShowModel(_preview);
                return;
            }
            _gunName.SetText(_tool.DisplayName.ToUpperInvariant());
            if (_slotFor != _tool || !_tool.TryAnchor(_slot, out _)) _slot = FirstOpenSlot(); // a new gun: its first slot with parts
            _slotFor = _tool;

            // Fitted now, and with the selection swapped in (the preview).
            IReadOnlyList<AttachmentDefinition> fitted = Loadouts.Local.Fitted(_tool);
            _preview.Clear();
            foreach (AttachmentDefinition a in fitted) if (_selected == null || a.Slot != _selected.Slot) _preview.Add(a);
            if (_selected != null) _preview.Add(_selected);
            ShowModel(_preview);

            foreach (SlotBox b in _boxes)
            {
                bool open = _tool.TryAnchor(b.Slot, out _);
                AttachmentDefinition on = p.FittedIn(_tool.Id.Value, b.Slot);
                b.Image.color = !open ? BoxLocked : b.Slot == _slot ? BoxSelected : Box;
                b.Value.color = open ? Color.white : Dim;
                b.Value.SetText(!open ? "LOCKED" : on != null ? on.DisplayName : "—");
                b.Button.interactable = open;
            }

            // The slot's attachments.
            _listTitle.SetText(_slot.ToString().ToUpperInvariant());
            var all = ContentRegistry.All<AttachmentDefinition>();
            all.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            foreach (AttachmentDefinition a in all)
            {
                if (a.Slot != _slot || !a.Fits(_tool)) continue;
                AttachmentDefinition captured = a;
                var row = new Row { Attachment = a };
                row.Image = Panel($"Row {a.Key}", new Vector2(0.5f, 0.5f), new Vector2(ListX, ListTop - _rows.Count * RowStep), new Vector2(RowWidth, RowHeight), a == _selected ? BoxSelected : Box);
                Round(row.Image, 12);
                row.Image.raycastTarget = true;
                row.Image.gameObject.AddComponent<Button>().onClick.AddListener(() => { _selected = _selected == captured ? null : captured; Sfx.Play2D("Kenney/click_001", 0.35f, 1.2f); Refresh(); });
                row.Name = Child(row.Image.rectTransform, "Name", new Vector2(-70f, 16f), new Vector2(390f, 30f), 22f, Color.white, TextAlignmentOptions.Left);
                row.Name.SetText(a.DisplayName);
                row.Name.fontStyle = FontStyles.Bold;
                row.Detail = Child(row.Image.rectTransform, "Detail", new Vector2(-70f, -16f), new Vector2(390f, 36f), 15f, Dim, TextAlignmentOptions.Left);
                row.Detail.textWrappingMode = TextWrappingModes.Normal;
                row.Detail.SetText(a.Description);
                bool owned = p.Attachments.Contains(a.Id.Value), on = p.FittedIn(_tool.Id.Value, a.Slot) == a;
                row.Status = Child(row.Image.rectTransform, "Status", new Vector2(196f, 0f), new Vector2(130f, 40f), 19f, on ? Better : owned ? Color.white : Accent, TextAlignmentOptions.Right);
                row.Status.SetText(on ? "FITTED" : owned ? "OWNED" : $"{a.CostCoins:0}c");
                _rows.Add(row);
            }
            if (_rows.Count == 0)
            {
                var row = new Row();
                row.Image = Panel("Row none", new Vector2(0.5f, 0.5f), new Vector2(ListX, ListTop), new Vector2(RowWidth, RowHeight), BoxLocked);
                Round(row.Image, 12);
                row.Name = Child(row.Image.rectTransform, "Name", Vector2.zero, new Vector2(RowWidth - 40f, 40f), 20f, Dim, TextAlignmentOptions.Center);
                row.Name.SetText("Nothing for this slot yet.");
                _rows.Add(row);
            }

            // The action for the selection: buy the gun, buy the attachment, fit it, or take it off.
            string action = null;
            Color color = Better;
            UpgradeDefinition buyGun = Loadouts.Local.Owns(_tool) ? null : GunUnlock(_tool);
            if (buyGun != null && _selected == null)
            {
                action = $"BUY GUN  {buyGun.CostCoins:0}c";
                if (EconomyNet.CanBuy(p, buyGun) != EconomyRefusal.None) color = Muted;
            }
            else if (_selected != null)
            {
                bool owned = p.Attachments.Contains(_selected.Id.Value), on = p.FittedIn(_tool.Id.Value, _selected.Slot) == _selected;
                if (on) { action = "REMOVE"; color = new Color(0.45f, 0.45f, 0.5f); }
                else if (owned) action = "FIT";
                else
                {
                    action = $"BUY  {_selected.CostCoins:0}c";
                    if (EconomyNet.CanBuyAttachment(p, _selected) != EconomyRefusal.None) color = Muted;
                }
            }
            _action.gameObject.SetActive(action != null);
            if (action != null) { _actionLabel.SetText(action); _action.color = color; }

            SetStats(Loadouts.Local.Setup(_tool), GunSetup.For(_tool, _preview));
        }

        /// <summary>The first slot with something to fit (else the first open one).</summary>
        private AttachmentSlot FirstOpenSlot()
        {
            foreach (AttachmentSlot s in Slots)
            {
                if (!_tool.TryAnchor(s, out _)) continue;
                foreach (AttachmentDefinition a in ContentRegistry.All<AttachmentDefinition>())
                    if (a.Slot == s && a.Fits(_tool)) return s;
            }
            foreach (AttachmentSlot s in Slots) if (_tool.TryAnchor(s, out _)) return s;
            return AttachmentSlot.Muzzle;
        }

        private void SetStats(GunSetup now, GunSetup preview)
        {
            if (now == null)
            {
                _dmg.SetText("-"); _rof.SetText("-"); _mag.SetText("-");
                foreach (Bar b in _bars) { b.Fill.rectTransform.sizeDelta = new Vector2(0f, 8f); b.Delta.rectTransform.sizeDelta = new Vector2(0f, 8f); b.Value.SetText(string.Empty); }
                return;
            }
            ToolStats stats = Loadouts.Local.StatsFor(_tool, out _);
            float baseDamage = stats.Damage / Mathf.Max(0.0001f, now.DamageMultiplier);
            _dmg.SetText("{0}", Mathf.Round(baseDamage * preview.DamageMultiplier));
            _rof.SetText("{0}", Mathf.Round(preview.RoundsPerMinute));
            _mag.SetText("{0}", preview.MagazineSize);
            Show(_bars[0], now.HipFire, preview.HipFire);
            Show(_bars[1], now.Precision, preview.Precision);
            Show(_bars[2], now.Control, preview.Control);
            Show(_bars[3], now.Mobility, preview.Mobility);
        }

        private static void Show(Bar bar, float now, float preview)
        {
            float lo = Mathf.Min(now, preview), hi = Mathf.Max(now, preview);
            bar.Fill.rectTransform.sizeDelta = new Vector2(BarWidth * lo / 100f, 8f);
            bar.Delta.rectTransform.sizeDelta = new Vector2(BarWidth * hi / 100f, 8f);
            bar.Delta.color = preview >= now ? Better : Worse;
            int diff = Mathf.RoundToInt(preview - now);
            bar.Value.SetText(diff == 0 ? $"{preview:0}" : $"{preview:0} <color=#{ColorUtility.ToHtmlStringRGB(diff > 0 ? Better : Worse)}>{(diff > 0 ? "+" : "")}{diff}</color>");
        }

        private void OnAction()
        {
            if (_tool == null) return;
            EconomyProfile p = EconomyNet.Local;
            UpgradeDefinition buyGun = Loadouts.Local.Owns(_tool) ? null : GunUnlock(_tool);
            if (buyGun != null && _selected == null)
            {
                EconomyRefusal gunWhy = EconomyNet.CanBuy(p, buyGun);
                if (gunWhy != EconomyRefusal.None) { HuntFeedback.Message(EconomyHud.RefusalLine(gunWhy), warning: true); return; }
                EconomyNet.RequestBuy(buyGun);
                Sfx.Play2D("Kenney/click_001", 0.45f);
                return;
            }
            if (_selected == null) return;
            bool owned = p.Attachments.Contains(_selected.Id.Value), on = p.FittedIn(_tool.Id.Value, _selected.Slot) == _selected;
            if (on) EconomyNet.RequestUnfitAttachment(_tool.Id.Value, _selected.Slot);
            else if (owned) EconomyNet.RequestFitAttachment(_tool.Id.Value, _selected.Id.Value);
            else
            {
                EconomyRefusal why = EconomyNet.CanBuyAttachment(p, _selected);
                if (why != EconomyRefusal.None) { HuntFeedback.Message(EconomyHud.RefusalLine(why), warning: true); return; }
                EconomyNet.RequestBuyAttachment(_selected.Id.Value, _tool.Id.Value);
            }
            Sfx.Play2D("Kenney/click_001", 0.45f);
        }

        // --- per frame ---------------------------------------------------------------------------------------------

        private void Update()
        {
            if (!_open || Time.frameCount == _openedFrame) return;
            UiInput.KeepGamepadSelection(_root); // gamepad: D-pad/stick and A drive the buttons
            bool close = KeyBindings.WasPressedThisFrame(GameAction.Interact) || UiInput.Back; // Esc or gamepad B
            if (PlayerMotor.Local == null || _station == null) close = true;
            else if (CampStation.Nearest(PlayerMotor.Local.transform.position, CampStationKind.Workshop, 1.5f) == null) close = true;
            if (close) _closeRequested = true;
        }

        private void LateUpdate()
        {
            if (_closeRequested)
            {
                _closeRequested = false;
                if (_open) Close();
                return;
            }
            if (!_open) return;
            // The panels fade in while the camera glides down onto the counter.
            float age = Time.unscaledTime - _openedAt;
            _group.alpha = UiFeel.Calm ? 1f : Mathf.Clamp01((age - 0.15f) / 0.3f);
            if (_model == null || _tool == null) return;
            // It turns a little on the felt so it reads as 3D (still with reduce motion).
            _pivot.rotation = Lying(UiFeel.Calm ? 0f : SwayYaw * Mathf.Sin(Time.unscaledTime * 0.4f));
            // Leader lines from each box to its mount on the gun.
            Camera cam = PlayerCamera.Camera;
            if (cam == null) return;
            Rect r = _root.rect;
            foreach (SlotBox b in _boxes)
            {
                if (!_tool.TryAnchor(b.Slot, out AttachmentAnchor anchor)) { b.Line.gameObject.SetActive(false); b.Dot.gameObject.SetActive(false); continue; }
                Vector3 vp = cam.WorldToViewportPoint(_model.transform.TransformPoint(anchor.Position));
                Vector2 target = new Vector2((vp.x - 0.5f) * r.width, (vp.y - 0.5f) * r.height);
                Vector2 from = b.Image.rectTransform.anchoredPosition;
                Vector2 d = target - from;
                RectTransform line = b.Line.rectTransform;
                line.anchoredPosition = from + d * 0.5f;
                line.sizeDelta = new Vector2(d.magnitude, 2f);
                line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                b.Dot.rectTransform.anchoredPosition = target;
                b.Line.gameObject.SetActive(true);
                b.Dot.gameObject.SetActive(true);
            }
        }
    }
}
