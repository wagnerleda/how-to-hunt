using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Hunting;
using HowToHunt.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HowToHunt.Economy
{
    /// <summary>
    /// The outfitter's upgrade list (uGUI). Each row says what the upgrade does, what it costs and whether you can buy
    /// it; Buy sends a request the host validates. Opens with Interact at a shop station; Interact or Escape closes it.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        private const float RowHeight = 78f;

        private static ShopPanel _instance;

        private RectTransform _panel;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _wallet;
        private Image _reclaim;
        private RectTransform _rows;
        private CampStation _station;
        private bool _open;
        private bool _closeRequested;
        private int _openedFrame;
        private readonly List<Row> _rowList = new List<Row>();
        private bool _rowsForBaits;

        private sealed class Row
        {
            public UpgradeDefinition Upgrade;
            public TextMeshProUGUI Name, Detail, Cost, ButtonLabel;
            public Button Button;
            public Image ButtonImage;
        }

        private static readonly Color PanelColor = new Color(0.12f, 0.1f, 0.08f, 0.92f);
        private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color BuyColor = new Color(0.45f, 0.72f, 0.35f);
        private static readonly Color DisabledColor = new Color(0.4f, 0.38f, 0.35f);
        private static readonly Color OwnedColor = new Color(0.3f, 0.5f, 0.65f);

        public static bool IsOpen => _instance != null && _instance._open;

        private void Awake()
        {
            _instance = this;
            EnsureEventSystem();
            if (!Hud.Root.TryGetComponent(out GraphicRaycaster _)) Hud.Root.gameObject.AddComponent<GraphicRaycaster>();
            _panel = Hud.Rect("Shop Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 620f), PanelColor).rectTransform;
            _panel.GetComponent<Image>().raycastTarget = true; // clicks on the panel don't fall through
            _title = Child(Hud.Text("Shop Title", new Vector2(0.5f, 1f), new Vector2(0f, -40f), 36f, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.65f)));
            _title.rectTransform.sizeDelta = new Vector2(860f, 50f);
            _title.fontStyle = FontStyles.Bold;
            _wallet = Child(Hud.Text("Shop Wallet", new Vector2(0.5f, 1f), new Vector2(0f, -82f), 24f, TextAlignmentOptions.Center, Color.white));
            _wallet.rectTransform.sizeDelta = new Vector2(860f, 34f);
            TextMeshProUGUI hint = Child(Hud.Text("Shop Hint", new Vector2(0.5f, 0f), new Vector2(0f, 26f), 20f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f)));
            hint.rectTransform.sizeDelta = new Vector2(860f, 30f);
            hint.SetText($"{KeyBindings.DisplayName(GameAction.Interact)} or Esc to close");
            // Lost a tool (dropped it, someone took it)? The outfitter replaces owned tools for free.
            _reclaim = Hud.Rect("Reclaim", new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(360f, 40f), new Color(0.35f, 0.55f, 0.7f));
            _reclaim.rectTransform.SetParent(_panel, false);
            _reclaim.raycastTarget = true;
            _reclaim.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.Play2D("Kenney/click_001", 0.4f); EconomyNet.RequestReclaimTools(); });
            TextMeshProUGUI reclaimLabel = Hud.Text("Label", new Vector2(0.5f, 0.5f), Vector2.zero, 20f, TextAlignmentOptions.Center, Color.white);
            reclaimLabel.rectTransform.SetParent(_reclaim.rectTransform, false);
            reclaimLabel.rectTransform.anchoredPosition = Vector2.zero;
            reclaimLabel.rectTransform.sizeDelta = new Vector2(360f, 40f);
            reclaimLabel.SetText("Recover lost tools (free)");
            _rows = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
            _rows.SetParent(_panel, false);
            _rows.anchorMin = _rows.anchorMax = new Vector2(0.5f, 1f);
            _rows.pivot = new Vector2(0.5f, 1f);
            _rows.anchoredPosition = new Vector2(0f, -115f);
            _rows.sizeDelta = new Vector2(860f, 440f);
            _panel.gameObject.SetActive(false);
            EconomyNet.LocalChanged += OnChanged;
        }

        private void OnDestroy()
        {
            EconomyNet.LocalChanged -= OnChanged;
            if (_open) Game.PopUiFocus(this);
            if (_instance == this) _instance = null;
        }

        private T Child<T>(T c) where T : Component
        {
            c.transform.SetParent(_panel, false);
            return c;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("[EventSystem]", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            DontDestroyOnLoad(go);
        }

        public static void Open(CampStation station)
        {
            if (_instance == null || _instance._open) return;
            ShopPanel s = _instance;
            s._station = station;
            s._open = true;
            s._openedFrame = Time.frameCount;
            s._title.SetText(station != null ? station.Label : "Shop");
            s.BuildRows(station != null && station.BaitStall);
            s.Refresh();
            s._panel.gameObject.SetActive(true);
            Game.PushUiFocus(s);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Sfx.Play2D("Kenney/open_001", 0.4f);
        }

        private void Close()
        {
            _open = false;
            _panel.gameObject.SetActive(false);
            Game.PopUiFocus(this);
            PlayerCamera.LockCursor();
            Sfx.Play2D("Kenney/close_001", 0.35f);
        }

        private void BuildRows(bool baits)
        {
            if (_rowList.Count > 0 && _rowsForBaits == baits) return;
            // An outfitter and a bait stall sell different things: rebuild when switching between them.
            for (int i = 0; i < _rowList.Count; i++) Destroy(_rowList[i].ButtonImage.transform.parent.gameObject);
            _rowList.Clear();
            _rowsForBaits = baits;
            IReadOnlyList<UpgradeDefinition> all = Loadouts.AllUpgrades;
            int shown = 0;
            for (int i = 0; i < all.Count; i++)
            {
                UpgradeDefinition u = all[i];
                if (u.Unlisted || u.SoldByGunsmith || u.IsBait != baits) continue; // not sold here (later areas, quests; guns at the Gunsmith; baits at bait stalls)
                float y = -(shown++) * (RowHeight + 8f);
                Image bg = Hud.Rect($"Row {u.Key}", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(860f, RowHeight), RowColor);
                RectTransform rt = bg.rectTransform;
                rt.SetParent(_rows, false);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, y);

                var row = new Row { Upgrade = u };
                row.Name = RowText(rt, "Name", new Vector2(20f, -12f), new Vector2(520f, 32f), 26f, FontStyles.Bold, new Color(1f, 0.95f, 0.85f));
                row.Detail = RowText(rt, "Detail", new Vector2(20f, -40f), new Vector2(560f, 36f), 16f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.7f));
                row.Cost = RowText(rt, "Cost", new Vector2(560f, -24f), new Vector2(140f, 32f), 22f, FontStyles.Bold, new Color(1f, 0.85f, 0.45f));
                row.Cost.alignment = TextAlignmentOptions.Right;

                row.ButtonImage = Hud.Rect("Buy", new Vector2(0f, 1f), Vector2.zero, new Vector2(130f, 50f), BuyColor);
                RectTransform brt = row.ButtonImage.rectTransform;
                brt.SetParent(rt, false);
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
                brt.pivot = new Vector2(0f, 1f);
                brt.anchoredPosition = new Vector2(715f, -14f);
                row.ButtonImage.raycastTarget = true; // HUD rects default to non-clickable
                row.Button = row.ButtonImage.gameObject.AddComponent<Button>();
                row.Button.targetGraphic = row.ButtonImage;
                UpgradeDefinition captured = u;
                row.Button.onClick.AddListener(() => OnBuy(captured));
                row.ButtonLabel = Hud.Text("Label", new Vector2(0.5f, 0.5f), Vector2.zero, 22f, TextAlignmentOptions.Center, Color.white);
                row.ButtonLabel.rectTransform.SetParent(brt, false);
                row.ButtonLabel.rectTransform.anchoredPosition = Vector2.zero;
                row.ButtonLabel.rectTransform.sizeDelta = new Vector2(130f, 50f);
                row.ButtonLabel.fontStyle = FontStyles.Bold;
                _rowList.Add(row);
            }
        }

        private static TextMeshProUGUI RowText(RectTransform parent, string name, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color)
        {
            TextMeshProUGUI t = Hud.Text(name, new Vector2(0f, 1f), Vector2.zero, fontSize, TextAlignmentOptions.TopLeft, color);
            RectTransform rt = t.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            t.fontStyle = style;
            return t;
        }

        private void OnBuy(UpgradeDefinition u)
        {
            EconomyRefusal why = EconomyNet.CanBuy(EconomyNet.Local, u);
            if (why != EconomyRefusal.None)
            {
                HuntFeedback.Message(EconomyHud.RefusalLine(why), warning: true);
                return;
            }
            Sfx.Play2D("Kenney/click_001", 0.4f);
            EconomyNet.RequestBuy(u);
        }

        private void OnChanged(EconomyStateBroadcast msg)
        {
            if (_open) Refresh();
        }

        private void Refresh()
        {
            EconomyProfile p = EconomyNet.Local;
            _wallet.SetText($"You have {p.Coins.ToShortString()} coins");
            _reclaim.gameObject.SetActive(!_rowsForBaits && p.MissingTools().Count > 0);
            foreach (Row row in _rowList)
            {
                UpgradeDefinition u = row.Upgrade;
                row.Name.SetText(u.DisplayName);
                row.Detail.SetText(Describe(u));
                row.Cost.SetText(u.CostCoins <= 0 ? "Free" : $"{((Sim.Economy.BigDouble)u.CostCoins).ToShortString()} c");
                EconomyRefusal why = EconomyNet.CanBuy(p, u);
                switch (why)
                {
                    case EconomyRefusal.None:
                        row.ButtonLabel.SetText("Buy");
                        row.ButtonImage.color = BuyColor;
                        break;
                    case EconomyRefusal.AlreadyOwned:
                        row.ButtonLabel.SetText("Owned");
                        row.ButtonImage.color = OwnedColor;
                        break;
                    case EconomyRefusal.Locked:
                        row.ButtonLabel.SetText("Locked");
                        row.ButtonImage.color = DisabledColor;
                        break;
                    case EconomyRefusal.BossAwake:
                        row.ButtonLabel.SetText("Awake");
                        row.ButtonImage.color = DisabledColor;
                        break;
                    default:
                        row.ButtonLabel.SetText("Buy");
                        row.ButtonImage.color = DisabledColor;
                        break;
                }
            }
        }

        /// <summary>What an upgrade does, in plain words.</summary>
        public static string Describe(UpgradeDefinition u)
        {
            string tool = u.Tool != null ? u.Tool.DisplayName : "tool";
            string effect;
            switch (u.Effect)
            {
                case UpgradeEffect.CaptureStrength: effect = $"{tool}: +{u.Value:0.#} strength (holds bigger prey)"; break;
                case UpgradeEffect.DamageMultiplier: effect = $"{tool}: ×{u.Value:0.##} damage"; break;
                case UpgradeEffect.Range: effect = $"{tool}: +{u.Value:0.#} m reach"; break;
                case UpgradeEffect.UnlockTool: effect = $"Unlocks the {tool}"; break;
                case UpgradeEffect.InventorySlots: effect = $"+{u.Value:0} inventory slots"; break;
                case UpgradeEffect.SummonBoss: effect = u.Summons != null ? $"Wakes {u.Summons.Noun} (used up)" : "A bait"; break;
                default: effect = string.Empty; break;
            }
            if (u.Requires != null) effect += $"   · needs {u.Requires.DisplayName}";
            return string.IsNullOrEmpty(u.Description) ? effect : effect + "   · " + u.Description;
        }

        private void Update()
        {
            if (!_open || Time.frameCount == _openedFrame) return;
            UiInput.KeepGamepadSelection(_panel); // gamepad: D-pad/stick and A drive the buttons
            bool close = KeyBindings.WasPressedThisFrame(GameAction.Interact) || UiInput.Back; // Esc or gamepad B
            if (PlayerMotor.Local == null || _station == null) close = true;
            else if (CampStation.NearestShop(PlayerMotor.Local.transform.position, _rowsForBaits, 1.5f) == null) close = true;
            // Close at the end of the frame so the pause menu doesn't also see this Escape press.
            if (close) _closeRequested = true;
        }

        private void LateUpdate()
        {
            if (!_closeRequested) return;
            _closeRequested = false;
            if (_open) Close();
        }
    }
}
