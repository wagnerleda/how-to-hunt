using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Hunting;
using HowToHunt.Player;
using HowToHunt.Sim.Hunting;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HowToHunt.Economy
{
    /// <summary>
    /// The Gunsmith's workshop (Phase 5), laid out like Battlefield 6's weapon customization in our low-poly style:
    /// the gun large in the middle (live 3D preview with its attachments), one box per attachment slot around it
    /// joined to its mount by a leader line (locked slots greyed), the slot's attachments in a list on the right,
    /// and the gun's numbers bottom right (damage, fire rate, magazine; hip fire, precision, control, mobility bars).
    /// Selecting an attachment previews it on the gun and in the bars before you buy or fit it.
    /// </summary>
    public sealed class WorkshopPanel : MonoBehaviour
    {
        private static readonly AttachmentSlot[] Slots =
            { AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Underbarrel, AttachmentSlot.Magazine, AttachmentSlot.Ammo, AttachmentSlot.Optic, AttachmentSlot.Top, AttachmentSlot.Stock };
        // Box centres around the preview (canvas units from the screen centre), muzzle on the left like the reference.
        private static readonly Vector2[] SlotBoxes =
        {
            new Vector2(-840f, 40f), new Vector2(-700f, -250f), new Vector2(-430f, -340f), new Vector2(-170f, -340f),
            new Vector2(90f, -340f), new Vector2(-300f, 330f), new Vector2(-580f, 330f), new Vector2(330f, 270f),
        };
        private static readonly Vector2 PreviewCenter = new Vector2(-230f, 20f);
        private static readonly Vector2 PreviewSize = new Vector2(1080f, 608f);
        private static readonly Vector2 SlotBoxSize = new Vector2(200f, 104f);
        private const float ListX = 660f, RowHeight = 92f, RowWidth = 560f, BarWidth = 340f;
        private static readonly Vector3 Stage = new Vector3(0f, -600f, 0f);

        private static readonly Color Background = new Color(0.07f, 0.08f, 0.09f, 1f);
        private static readonly Color Box = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color BoxSelected = new Color(1f, 0.82f, 0.45f, 0.28f);
        private static readonly Color BoxLocked = new Color(1f, 1f, 1f, 0.025f);
        private static readonly Color Accent = new Color(1f, 0.82f, 0.45f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color Better = new Color(0.45f, 0.85f, 0.4f);
        private static readonly Color Worse = new Color(0.95f, 0.4f, 0.3f);

        private static WorkshopPanel _instance;

        private RectTransform _root;
        private TextMeshProUGUI _gunName, _wallet, _listTitle, _actionLabel, _empty;
        private Image _action;
        private RawImage _preview;
        private readonly List<SlotBox> _boxes = new List<SlotBox>();
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Tab> _tabs = new List<Tab>();
        private TextMeshProUGUI _dmg, _rof, _mag;
        private readonly Bar[] _bars = new Bar[4];

        private Camera _camera;
        private RenderTexture _texture;
        private Light _light;
        private GameObject _model;
        private ToolDefinition _modelTool;
        private string _modelKey;

        private CampStation _station;
        private bool _open, _closeRequested;
        private int _openedFrame;
        private ToolDefinition _tool;
        private AttachmentSlot _slot = AttachmentSlot.Muzzle;
        private AttachmentDefinition _selected;
        private readonly List<AttachmentDefinition> _preview2 = new List<AttachmentDefinition>();

        private sealed class SlotBox { public AttachmentSlot Slot; public Image Image, Line; public TextMeshProUGUI Name, Value; public Button Button; }
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
            Image bg = _root.gameObject.AddComponent<Image>();
            bg.color = Background;
            bg.raycastTarget = true;

            Label("Title", new Vector2(0f, 1f), new Vector2(70f, -40f), 26f, Dim, TextAlignmentOptions.TopLeft).SetText("GUNSMITH  /  CUSTOMIZE");
            _gunName = Label("Gun", new Vector2(0f, 1f), new Vector2(70f, -78f), 68f, Color.white, TextAlignmentOptions.TopLeft);
            _gunName.fontStyle = FontStyles.Bold;
            _wallet = Label("Wallet", new Vector2(0f, 1f), new Vector2(70f, -168f), 30f, Accent, TextAlignmentOptions.TopLeft);
            Label("Hint", new Vector2(0f, 0f), new Vector2(70f, 30f), 20f, Dim, TextAlignmentOptions.BottomLeft)
                .SetText($"{KeyBindings.DisplayName(GameAction.Interact)} or Esc to close");
            _empty = Label("Empty", new Vector2(0.5f, 0.5f), Vector2.zero, 34f, Dim, TextAlignmentOptions.Center);
            _empty.SetText("Bring a gun: the Outfitter sells them.");

            _preview = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
            Place(_preview.rectTransform, new Vector2(0.5f, 0.5f), PreviewCenter, PreviewSize);
            _preview.raycastTarget = false;

            for (int i = 0; i < Slots.Length; i++) _boxes.Add(MakeBox(Slots[i], SlotBoxes[i]));

            _listTitle = Label("List Title", new Vector2(0.5f, 0.5f), new Vector2(ListX, 430f), 30f, Accent, TextAlignmentOptions.Center);
            _listTitle.fontStyle = FontStyles.Bold;
            _action = Panel("Action", new Vector2(0.5f, 0.5f), new Vector2(ListX, -60f), new Vector2(320f, 58f), Better);
            _action.raycastTarget = true;
            _action.gameObject.AddComponent<Button>().onClick.AddListener(OnAction);
            _actionLabel = Label("Action Label", new Vector2(0.5f, 0.5f), Vector2.zero, 24f, Color.white, TextAlignmentOptions.Center);
            _actionLabel.rectTransform.SetParent(_action.rectTransform, false);
            _actionLabel.rectTransform.anchoredPosition = Vector2.zero;
            _actionLabel.rectTransform.sizeDelta = new Vector2(320f, 58f);
            _actionLabel.fontStyle = FontStyles.Bold;

            _dmg = BigStat("DMG", new Vector2(ListX - 200f, -170f));
            _rof = BigStat("ROF", new Vector2(ListX - 20f, -170f));
            _mag = BigStat("MAG", new Vector2(ListX + 160f, -170f));
            string[] names = { "HIP FIRE", "PRECISION", "CONTROL", "MOBILITY" };
            for (int i = 0; i < 4; i++) _bars[i] = MakeBar(names[i], new Vector2(ListX, -280f - i * 46f));

            _root.gameObject.SetActive(false);
            EconomyNet.LocalChanged += OnChanged;
        }

        private void OnDestroy()
        {
            EconomyNet.LocalChanged -= OnChanged;
            if (_open) Game.PopUiFocus(this);
            DestroyStage();
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
            b.Line = Panel($"Line {slot}", new Vector2(0.5f, 0.5f), pos, new Vector2(10f, 2f), new Color(1f, 1f, 1f, 0.35f));
            b.Image = Panel($"Slot {slot}", new Vector2(0.5f, 0.5f), pos, SlotBoxSize, Box);
            b.Image.raycastTarget = true;
            b.Button = b.Image.gameObject.AddComponent<Button>();
            b.Button.onClick.AddListener(() => SelectSlot(slot));
            b.Name = Child(b.Image.rectTransform, "Name", new Vector2(0f, 28f), new Vector2(190f, 30f), 18f, Dim, TextAlignmentOptions.Center);
            b.Name.SetText(slot.ToString().ToUpperInvariant());
            b.Value = Child(b.Image.rectTransform, "Value", new Vector2(0f, -12f), new Vector2(190f, 54f), 21f, Color.white, TextAlignmentOptions.Center);
            b.Value.textWrappingMode = TextWrappingModes.Normal;
            return b;
        }

        private TextMeshProUGUI BigStat(string name, Vector2 pos)
        {
            Label($"{name} Label", new Vector2(0.5f, 0.5f), pos + new Vector2(0f, 46f), 20f, Dim, TextAlignmentOptions.Center).SetText(name);
            TextMeshProUGUI t = Label(name, new Vector2(0.5f, 0.5f), pos, 52f, Color.white, TextAlignmentOptions.Center);
            t.rectTransform.sizeDelta = new Vector2(180f, 70f);
            t.fontStyle = FontStyles.Bold;
            return t;
        }

        private Bar MakeBar(string name, Vector2 pos)
        {
            TextMeshProUGUI label = Label($"{name} Label", new Vector2(0.5f, 0.5f), pos + new Vector2(-BarWidth * 0.5f, 16f), 18f, Dim, TextAlignmentOptions.BottomLeft);
            label.rectTransform.pivot = new Vector2(0f, 0f);
            label.rectTransform.sizeDelta = new Vector2(240f, 24f);
            label.SetText(name);
            var bar = new Bar();
            bar.Value = Label($"{name} Value", new Vector2(0.5f, 0.5f), pos + new Vector2(BarWidth * 0.5f, 16f), 18f, Color.white, TextAlignmentOptions.BottomRight);
            bar.Value.rectTransform.pivot = new Vector2(1f, 0f);
            bar.Value.rectTransform.sizeDelta = new Vector2(120f, 24f);
            Panel($"{name} Back", new Vector2(0.5f, 0.5f), pos, new Vector2(BarWidth, 8f), new Color(1f, 1f, 1f, 0.12f));
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
            if (_instance == null || _instance._open) return;
            WorkshopPanel w = _instance;
            w._station = station;
            w._open = true;
            w._openedFrame = Time.frameCount;
            w._selected = null;
            w._tool = null;
            w.BuildTabs();
            if (w._tabs.Count > 0) w._tool = w._tabs[0].Tool;
            w._root.gameObject.SetActive(true);
            w.EnsureStage();
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
            if (_camera != null) _camera.enabled = false;
            if (_light != null) _light.enabled = false;
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
                if (tool.Archetype != ToolArchetype.Firearm || (!Loadouts.Local.Owns(tool) && GunUnlock(tool) == null)) continue;
                var tab = new Tab { Tool = tool };
                // Two rows of five to the right of the gun name.
                int col = _tabs.Count % 5, rowIndex = _tabs.Count / 5;
                tab.Image = Panel($"Tab {tool.Key}", new Vector2(0.5f, 1f), new Vector2(-150f + col * 232f, -70f - rowIndex * 56f), new Vector2(222f, 48f), Box);
                tab.Image.raycastTarget = true;
                tab.Image.gameObject.AddComponent<Button>().onClick.AddListener(() => { _tool = tool; _selected = null; Sfx.Play2D("Kenney/click_001", 0.4f); Refresh(); });
                tab.Label = Child(tab.Image.rectTransform, "Label", Vector2.zero, new Vector2(218f, 48f), 18f, Color.white, TextAlignmentOptions.Center);
                UpgradeDefinition unlock = Loadouts.Local.Owns(tool) ? null : GunUnlock(tool);
                tab.Label.SetText(unlock == null ? tool.DisplayName : $"{tool.DisplayName}  <color=#F2C66F>{unlock.CostCoins:0}c</color>");
                _tabs.Add(tab);
            }
        }

        /// <summary>The Gunsmith's upgrade that unlocks this gun (null if it isn't sold here).</summary>
        private static UpgradeDefinition GunUnlock(ToolDefinition tool)
        {
            foreach (UpgradeDefinition u in Loadouts.AllUpgrades)
                if (u.SoldByGunsmith && u.Effect == UpgradeEffect.UnlockTool && u.Tool == tool) return u;
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

        // --- the picture ------------------------------------------------------------------------------------------

        private void EnsureStage()
        {
            if (_camera == null)
            {
                _texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { name = "Workshop Preview", antiAliasing = 4 };
                var camGo = new GameObject("Workshop Camera");
                _camera = camGo.AddComponent<Camera>();
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _camera.fieldOfView = 18f;
                _camera.nearClipPlane = 0.05f;
                _camera.farClipPlane = 20f;
                _camera.targetTexture = _texture;
                camGo.transform.SetPositionAndRotation(Stage + new Vector3(0f, 0f, -3f), Quaternion.identity);
                var lightGo = new GameObject("Workshop Light");
                _light = lightGo.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.range = 6f;
                _light.intensity = 3f;
                lightGo.transform.position = Stage + new Vector3(-0.8f, 1.2f, -1.6f);
                _preview.texture = _texture;
            }
            _camera.enabled = true;
            _light.enabled = true;
        }

        private void DestroyStage()
        {
            if (_model != null) Destroy(_model);
            if (_camera != null) Destroy(_camera.gameObject);
            if (_light != null) Destroy(_light.gameObject);
            if (_texture != null) _texture.Release();
        }

        /// <summary>The gun on the stage with the fitted attachments plus the one being previewed.</summary>
        private void ShowModel(List<AttachmentDefinition> fitted)
        {
            string key = _tool != null ? _tool.Key : string.Empty;
            foreach (AttachmentDefinition a in fitted) key += "|" + a.Key;
            if (key == _modelKey) return;
            _modelKey = key;
            if (_model != null && _modelTool != _tool) { Destroy(_model); _model = null; }
            _modelTool = _tool;
            if (_tool == null || _tool.Viewmodel == null) return;
            if (_model == null)
            {
                _model = Instantiate(_tool.Viewmodel, Stage, Quaternion.Euler(0f, -90f, 0f)); // muzzle to the left
                IconRenderer.HideParts(_model, "Hand");
                foreach (Animator an in _model.GetComponentsInChildren<Animator>()) an.enabled = false;
                // Frame it: centre the bounds on the stage and back the camera off to fit.
                Bounds b = Bounds(_model);
                _model.transform.position += Stage - b.center;
                float fit = Mathf.Max(b.extents.x, b.extents.y * 1.78f) / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                _camera.transform.position = Stage + new Vector3(0f, 0f, -(fit * 0.62f + b.extents.z + 0.2f));
            }
            AttachmentVisuals.Apply(_model, _tool, fitted);
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
            _preview.gameObject.SetActive(hasGun);
            _action.gameObject.SetActive(hasGun);
            _listTitle.gameObject.SetActive(hasGun);
            foreach (Tab t in _tabs) t.Image.color = t.Tool == _tool ? BoxSelected : Box;
            foreach (SlotBox b in _boxes) { b.Image.gameObject.SetActive(hasGun); b.Line.gameObject.SetActive(hasGun); }
            foreach (Row r in _rows) Destroy(r.Image.gameObject);
            _rows.Clear();
            if (!hasGun)
            {
                _gunName.SetText(string.Empty);
                SetStats(null, null);
                return;
            }
            _gunName.SetText(_tool.DisplayName.ToUpperInvariant());
            if (!_tool.TryAnchor(_slot, out _)) _slot = FirstOpenSlot();

            // Fitted now, and with the selection swapped in (the preview).
            IReadOnlyList<AttachmentDefinition> fitted = Loadouts.Local.Fitted(_tool);
            _preview2.Clear();
            foreach (AttachmentDefinition a in fitted) if (_selected == null || a.Slot != _selected.Slot) _preview2.Add(a);
            if (_selected != null) _preview2.Add(_selected);
            ShowModel(_preview2);

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
                row.Image = Panel($"Row {a.Key}", new Vector2(0.5f, 0.5f), new Vector2(ListX, 360f - _rows.Count * (RowHeight + 8f)), new Vector2(RowWidth, RowHeight), a == _selected ? BoxSelected : Box);
                row.Image.raycastTarget = true;
                row.Image.gameObject.AddComponent<Button>().onClick.AddListener(() => { _selected = _selected == captured ? null : captured; Sfx.Play2D("Kenney/click_001", 0.35f, 1.2f); Refresh(); });
                row.Name = Child(row.Image.rectTransform, "Name", new Vector2(-70f, 20f), new Vector2(400f, 34f), 24f, Color.white, TextAlignmentOptions.Left);
                row.Name.SetText(a.DisplayName);
                row.Name.fontStyle = FontStyles.Bold;
                row.Detail = Child(row.Image.rectTransform, "Detail", new Vector2(-70f, -18f), new Vector2(400f, 40f), 16f, Dim, TextAlignmentOptions.Left);
                row.Detail.textWrappingMode = TextWrappingModes.Normal;
                row.Detail.SetText(a.Description);
                bool owned = p.Attachments.Contains(a.Id.Value), on = p.FittedIn(_tool.Id.Value, a.Slot) == a;
                row.Status = Child(row.Image.rectTransform, "Status", new Vector2(200f, 0f), new Vector2(140f, 40f), 20f, on ? Better : owned ? Color.white : Accent, TextAlignmentOptions.Right);
                row.Status.SetText(on ? "FITTED" : owned ? "OWNED" : $"{a.CostCoins:0}c");
                _rows.Add(row);
            }
            if (_rows.Count == 0)
            {
                var row = new Row();
                row.Image = Panel("Row none", new Vector2(0.5f, 0.5f), new Vector2(ListX, 360f), new Vector2(RowWidth, RowHeight), BoxLocked);
                row.Name = Child(row.Image.rectTransform, "Name", Vector2.zero, new Vector2(RowWidth - 40f, 40f), 20f, Dim, TextAlignmentOptions.Center);
                row.Name.SetText("Nothing for this slot yet.");
                _rows.Add(row);
            }

            // The action for the selection: buy it, fit it, or take it off.
            string action = null;
            Color color = Better;
            UpgradeDefinition buyGun = Loadouts.Local.Owns(_tool) ? null : GunUnlock(_tool);
            if (buyGun != null && _selected == null)
            {
                action = $"BUY GUN  {buyGun.CostCoins:0}c";
                if (EconomyNet.CanBuy(p, buyGun) != EconomyRefusal.None) color = new Color(0.4f, 0.38f, 0.35f);
            }
            else if (_selected != null)
            {
                bool owned = p.Attachments.Contains(_selected.Id.Value), on = p.FittedIn(_tool.Id.Value, _selected.Slot) == _selected;
                if (on) { action = "REMOVE"; color = new Color(0.45f, 0.45f, 0.5f); }
                else if (owned) action = "FIT";
                else
                {
                    action = $"BUY  {_selected.CostCoins:0}c";
                    if (EconomyNet.CanBuyAttachment(p, _selected) != EconomyRefusal.None) color = new Color(0.4f, 0.38f, 0.35f);
                }
            }
            _action.gameObject.SetActive(action != null);
            if (action != null) { _actionLabel.SetText(action); _action.color = color; }

            SetStats(Loadouts.Local.Setup(_tool), GunSetup.For(_tool, _preview2));
        }

        private AttachmentSlot FirstOpenSlot()
        {
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
                EconomyNet.RequestBuyAttachment(_selected.Id.Value);
            }
            Sfx.Play2D("Kenney/click_001", 0.45f);
        }

        // --- per frame ---------------------------------------------------------------------------------------------

        private void Update()
        {
            if (!_open || Time.frameCount == _openedFrame) return;
            Keyboard kb = Keyboard.current;
            bool close = KeyBindings.WasPressedThisFrame(GameAction.Interact) || (kb != null && kb.escapeKey.wasPressedThisFrame);
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
            if (!_open || _model == null || _tool == null) return;
            // A slow sway so the gun reads as 3D.
            _model.transform.rotation = Quaternion.Euler(4f * Mathf.Sin(Time.unscaledTime * 0.6f), -90f + 12f * Mathf.Sin(Time.unscaledTime * 0.4f), 0f);
            // Leader lines from each box to its mount on the gun.
            Rect r = _preview.rectTransform.rect;
            foreach (SlotBox b in _boxes)
            {
                if (!_tool.TryAnchor(b.Slot, out AttachmentAnchor anchor)) { b.Line.gameObject.SetActive(false); continue; }
                Vector3 vp = _camera.WorldToViewportPoint(_model.transform.TransformPoint(anchor.Position));
                Vector2 target = PreviewCenter + new Vector2((vp.x - 0.5f) * r.width, (vp.y - 0.5f) * r.height);
                Vector2 from = b.Image.rectTransform.anchoredPosition;
                Vector2 d = target - from;
                RectTransform line = b.Line.rectTransform;
                line.anchoredPosition = from + d * 0.5f;
                line.sizeDelta = new Vector2(d.magnitude, 2f);
                line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                b.Line.gameObject.SetActive(true);
            }
        }
    }
}
