using HowToHunt.Core;
using HowToHunt.Foundation;
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
    /// The inventory bar at the bottom of the screen: tools first, then prey stacks. Number keys select a slot; the
    /// selected tool is held (PlayerTools), or the selected prey is held in hand: Fire throws it, Q drops it.
    /// E picks up the item you look at (carcasses, drops). Text and icons change only when the inventory does.
    /// </summary>
    public sealed class InventoryBar : MonoBehaviour
    {
        private const float SlotSize = 74f, Gap = 8f;
        private const float ThrowCooldown = 0.35f;

        private readonly Image[] _frames = new Image[Inventory.MaxSlots];
        private readonly RawImage[] _icons = new RawImage[Inventory.MaxSlots];
        private readonly TextMeshProUGUI[] _counts = new TextMeshProUGUI[Inventory.MaxSlots];
        private readonly TextMeshProUGUI[] _keys = new TextMeshProUGUI[Inventory.MaxSlots];
        private TextMeshProUGUI _label;
        private float _labelLife;
        private int _selected;
        private int _shownCapacity = -1;
        private float _nextThrow;
        private ItemStack _heldItem;

        private static readonly Color Frame = new Color(0.1f, 0.09f, 0.08f, 0.55f);
        private static readonly Color FrameSelected = new Color(1f, 0.85f, 0.45f, 0.85f);

        public static int Selected { get; private set; }

        private void Awake()
        {
            for (int i = 0; i < Inventory.MaxSlots; i++)
            {
                _frames[i] = Hud.Rect($"Slot {i}", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(SlotSize, SlotSize), Frame);
                RectTransform frame = _frames[i].rectTransform;
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(frame, false);
                _icons[i] = iconGo.AddComponent<RawImage>();
                _icons[i].raycastTarget = false;
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(4f, 4f);
                irt.offsetMax = new Vector2(-4f, -4f);
                _counts[i] = Hud.Text("Count", new Vector2(1f, 0f), new Vector2(-6f, 4f), 20f, TextAlignmentOptions.BottomRight, Color.white);
                _counts[i].rectTransform.SetParent(frame, false);
                _counts[i].rectTransform.pivot = new Vector2(1f, 0f);
                _counts[i].rectTransform.sizeDelta = new Vector2(60f, 26f);
                _counts[i].fontStyle = FontStyles.Bold;
                _keys[i] = Hud.Text("Key", new Vector2(0f, 1f), new Vector2(5f, -3f), 15f, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.7f));
                _keys[i].rectTransform.SetParent(frame, false);
                _keys[i].rectTransform.pivot = new Vector2(0f, 1f);
                _keys[i].rectTransform.sizeDelta = new Vector2(30f, 20f);
                _keys[i].SetText(i < 9 ? (i + 1).ToString() : string.Empty);
            }
            _label = Hud.Text("Held Label", new Vector2(0.5f, 0f), new Vector2(0f, SlotSize + 34f), 22f, TextAlignmentOptions.Center, Color.white);
            _label.rectTransform.sizeDelta = new Vector2(800f, 30f);
            EconomyNet.LocalChanged += OnChanged;
            Refresh();
        }

        private void OnDestroy() => EconomyNet.LocalChanged -= OnChanged;

        private void OnChanged(EconomyStateBroadcast msg) => Refresh();

        private void Refresh()
        {
            Inventory inv = EconomyNet.Local.Inventory;
            int cap = inv.Capacity;
            if (cap != _shownCapacity)
            {
                _shownCapacity = cap;
                float width = cap * SlotSize + (cap - 1) * Gap;
                for (int i = 0; i < Inventory.MaxSlots; i++)
                {
                    bool on = i < cap;
                    _frames[i].gameObject.SetActive(on);
                    if (on) _frames[i].rectTransform.anchoredPosition = new Vector2(-width * 0.5f + SlotSize * 0.5f + i * (SlotSize + Gap), 22f + SlotSize * 0.5f);
                }
            }
            for (int i = 0; i < cap; i++)
            {
                ItemStack s = inv[i];
                Texture2D icon = IconFor(s);
                _icons[i].texture = icon;
                _icons[i].enabled = icon != null;
                _counts[i].SetText(s.Kind == ItemKind.Prey && s.Count > 1 ? s.Count.ToString() : string.Empty);
            }
            Select(Mathf.Clamp(_selected, 0, cap - 1), force: true);
        }

        private static Texture2D IconFor(in ItemStack s)
        {
            if (s.IsEmpty) return null;
            if (s.Kind == ItemKind.Tool)
            {
                ToolDefinition t = ContentRegistry.Get<ToolDefinition>(new ContentId(s.Content));
                return t != null ? IconRenderer.Get(t.Viewmodel, -70f, 20f) : null;
            }
            PreyDefinition p = ContentRegistry.Get<PreyDefinition>(new ContentId(s.Content));
            return p != null ? IconRenderer.Get(p.Visual, 140f, 18f) : null;
        }

        private void Select(int slot, bool force = false)
        {
            Inventory inv = EconomyNet.Local.Inventory;
            if (slot < 0 || slot >= inv.Capacity) return;
            ItemStack s = inv[slot];
            bool changed = slot != _selected || !s.Equals(_heldItem);
            if (!changed && !force) return;
            _frames[_selected].color = Frame;
            _selected = slot;
            Selected = slot;
            _frames[slot].color = FrameSelected;
            bool sameThing = s.Kind == _heldItem.Kind && s.Content == _heldItem.Content;
            _heldItem = s;
            if (force && sameThing && !changed) return;

            PlayerTools tools = PlayerTools.Local;
            if (tools == null) return;
            string label = string.Empty;
            if (s.Kind == ItemKind.Tool)
            {
                ToolDefinition t = ContentRegistry.Get<ToolDefinition>(new ContentId(s.Content));
                if (t != tools.Equipped || PlayerTools.HeldPrey != null) tools.SelectTool(t);
                label = t != null ? t.DisplayName : string.Empty;
            }
            else if (s.Kind == ItemKind.Prey)
            {
                PreyDefinition p = ContentRegistry.Get<PreyDefinition>(new ContentId(s.Content));
                if (PlayerTools.HeldPrey != p) tools.HoldPrey(p);
                string how = s.Captured ? "caught" : s.Quality == CarcassQuality.Ruined ? "ruined" : "killed";
                label = p != null ? $"{p.DisplayName} ({how})  ·  {KeyBindings.DisplayName(GameAction.Fire)} throw  ·  Q drop" : string.Empty;
            }
            else
            {
                if (tools.Equipped != null || PlayerTools.HeldPrey != null) tools.SelectTool(null);
            }
            if (changed && !force) ShowLabel(label);
        }

        private void ShowLabel(string text)
        {
            _label.SetText(text);
            _labelLife = string.IsNullOrEmpty(text) ? 0f : 2f;
            _label.gameObject.SetActive(_labelLife > 0f);
        }

        private void Update()
        {
            if (_labelLife > 0f)
            {
                _labelLife -= Time.deltaTime;
                Color c = _label.color;
                c.a = Mathf.Clamp01(_labelLife / 0.5f);
                _label.color = c;
                if (_labelLife <= 0f) _label.gameObject.SetActive(false);
            }

            if (PlayerTools.Local == null || Game.UiHasFocus || Cursor.lockState != CursorLockMode.Locked) return;
            Keyboard kb = Keyboard.current;
            Inventory inv = EconomyNet.Local.Inventory;
            if (kb != null)
            {
                for (int i = 0; i < 9 && i < inv.Capacity; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame) Select(i);
            }

            ItemStack held = inv[_selected];
            if (held.Kind != ItemKind.Prey) return;
            Camera cam = Camera.main;
            if (cam == null || Time.time < _nextThrow) return;
            bool drop = kb != null && kb.qKey.wasPressedThisFrame;
            bool throwIt = KeyBindings.WasPressedThisFrame(GameAction.Fire);
            if (!drop && !throwIt) return;
            _nextThrow = Time.time + ThrowCooldown;
            Transform ct = cam.transform;
            if (throwIt)
            {
                EconomyNet.RequestThrow(_selected, ct.position, ct.forward);
                PlayerTools.NotifyThrow();
                Sfx.Play2D("Kenney/cloth", 0.4f, Random.Range(1.1f, 1.25f));
            }
            else
            {
                EconomyNet.RequestDrop(_selected, ct.position, ct.forward);
                Sfx.Play2D("Kenney/cloth", 0.3f, 0.9f);
            }
        }
    }
}
