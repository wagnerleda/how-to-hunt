using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Hunting;
using HowToHunt.Player;
using HowToHunt.Sim.Economy;
using HowToHunt.Sim.Hunting;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HowToHunt.Economy
{
    /// <summary>
    /// The Outfitter's rack and the bait stall (uGUI, remade 2026-11-24 to be clearer and nicer): one column per thing
    /// you improve (the net, the knife, the bag; baits at a bait stall). Each column heads with the item (its picture,
    /// or your bag's slots drawn as boxes) and what it does now ("strength 2 · reach 2.4 m", "5 slots"), then its
    /// upgrades in order, joined by arrows: what each one adds in plain words, its price, and one clear state (owned,
    /// buy, too expensive, needs the one above). Buying sends a request the host validates. Interact or Escape
    /// closes it; it follows UiInput for the gamepad.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        private const float PanelW = 1240f, PanelH = 780f, ColumnGap = 22f, HeadH = 172f, CardH = 108f, CardGap = 24f, Top = 132f;

        private static readonly Color Glass = new Color(0.09f, 0.07f, 0.055f, 0.985f);
        private static readonly Color Column = new Color(1f, 0.93f, 0.8f, 0.06f);
        private static readonly Color Card = new Color(1f, 0.95f, 0.85f, 0.07f);
        private static readonly Color CardOwned = new Color(0.45f, 0.75f, 0.4f, 0.14f);
        private static readonly Color Edge = new Color(1f, 0.9f, 0.7f, 0.14f);
        private static readonly Color Cream = new Color(1f, 0.94f, 0.82f);
        private static readonly Color Dim = new Color(1f, 0.94f, 0.82f, 0.62f);
        private static readonly Color Gold = new Color(1f, 0.8f, 0.38f);
        private static readonly Color BuyColor = new Color(0.42f, 0.72f, 0.32f);
        private static readonly Color PoorColor = new Color(0.42f, 0.36f, 0.3f);
        private static readonly Color OwnedColor = new Color(0.45f, 0.75f, 0.4f);
        private static readonly Color LockedColor = new Color(0.32f, 0.3f, 0.28f);
        private static readonly Color Red = new Color(0.95f, 0.45f, 0.35f);

        private static ShopPanel _instance;

        private RectTransform _panel, _body;
        private TextMeshProUGUI _title, _subtitle, _wallet, _empty;
        private Image _reclaim;
        private CampStation _station;
        private bool _open, _closeRequested;
        private int _openedFrame;
        private bool _builtForBaits, _built;
        private readonly List<Group> _groups = new List<Group>();

        private sealed class Group
        {
            public string Name;
            public ToolDefinition Tool;   // null: the bag or baits
            public bool Bag;
            public readonly List<UpgradeDefinition> Upgrades = new List<UpgradeDefinition>();
            public TextMeshProUGUI Stat;
            public readonly List<Image> Slots = new List<Image>();
            public readonly List<CardView> Cards = new List<CardView>();
        }

        private sealed class CardView
        {
            public UpgradeDefinition Upgrade;
            public Image Back, ButtonImage;
            public TextMeshProUGUI Price, ButtonLabel, State;
            public Button Button;
        }

        public static bool IsOpen => _instance != null && _instance._open;

        private void Awake()
        {
            _instance = this;
            EnsureEventSystem();
            if (!Hud.Root.TryGetComponent(out GraphicRaycaster _)) Hud.Root.gameObject.AddComponent<GraphicRaycaster>();
            Image panel = Hud.Rect("Shop Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW, PanelH), Glass);
            Rounded(panel, 26, rim: true);
            panel.raycastTarget = true; // clicks on the panel don't fall through
            _panel = panel.rectTransform;

            _title = UiFonts.Use(Text("Title", _panel, new Vector2(0f, 1f), new Vector2(44f, -26f), new Vector2(700f, 70f), 58f, Cream, TextAlignmentOptions.TopLeft));
            _subtitle = Text("Subtitle", _panel, new Vector2(0f, 1f), new Vector2(46f, -92f), new Vector2(800f, 28f), 19f, Dim, TextAlignmentOptions.TopLeft);
            // The purse: a gold coin and the amount, top right.
            Image chip = Hud.Rect("Wallet", new Vector2(1f, 1f), Vector2.zero, new Vector2(250f, 64f), new Color(0f, 0f, 0f, 0.3f));
            Place(chip.rectTransform, _panel, new Vector2(1f, 1f), new Vector2(-40f, -32f), new Vector2(250f, 64f), new Vector2(1f, 1f));
            Rounded(chip, 32);
            Image coin = Hud.Rect("Coin", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(38f, 38f), Gold);
            Place(coin.rectTransform, chip.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(38f, 38f), new Vector2(0f, 0.5f));
            coin.sprite = UiSprites.Circle();
            _wallet = UiFonts.Use(Text("Amount", chip.rectTransform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(180f, 56f), 38f, Gold, TextAlignmentOptions.Right));
            _wallet.rectTransform.pivot = new Vector2(1f, 0.5f);

            _body = new GameObject("Columns", typeof(RectTransform)).GetComponent<RectTransform>();
            Place(_body, _panel, new Vector2(0.5f, 1f), new Vector2(0f, -Top), new Vector2(PanelW - 80f, PanelH - Top - 90f), new Vector2(0.5f, 1f));
            _empty = Text("Empty", _panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 60f), 26f, Dim, TextAlignmentOptions.Center);
            _empty.SetText("Nothing for sale here yet.");

            // Lost a tool (dropped it, someone took it)? The outfitter replaces owned tools for free.
            _reclaim = Hud.Rect("Reclaim", new Vector2(0f, 0f), Vector2.zero, new Vector2(330f, 46f), new Color(0.35f, 0.55f, 0.7f));
            Place(_reclaim.rectTransform, _panel, new Vector2(0f, 0f), new Vector2(44f, 28f), new Vector2(330f, 46f), Vector2.zero);
            Rounded(_reclaim, 14);
            _reclaim.raycastTarget = true;
            _reclaim.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.Play2D("Kenney/click_001", 0.4f); EconomyNet.RequestReclaimTools(); });
            UiFonts.Use(Text("Label", _reclaim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 46f), 22f, Color.white, TextAlignmentOptions.Center))
                .SetText("Recover lost tools (free)");
            Text("Hint", _panel, new Vector2(1f, 0f), new Vector2(-44f, 36f), new Vector2(500f, 28f), 18f, Dim, TextAlignmentOptions.BottomRight)
                .SetText($"{KeyBindings.DisplayName(GameAction.Interact)} or Esc to close");

            _panel.gameObject.SetActive(false);
            EconomyNet.LocalChanged += OnChanged;
        }

        private void OnDestroy()
        {
            EconomyNet.LocalChanged -= OnChanged;
            if (_open) Game.PopUiFocus(this);
            if (_instance == this) _instance = null;
        }

        // --- building blocks ----------------------------------------------------------------------------------

        private static void Place(RectTransform rt, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static TextMeshProUGUI Text(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
        {
            TextMeshProUGUI t = Hud.Text(name, anchor, pos, fontSize, align, color);
            Place(t.rectTransform, parent, anchor, pos, size, anchor);
            return t;
        }

        private static Image Box(string name, RectTransform parent, Vector2 pos, Vector2 size, Color color, int radius)
        {
            Image i = Hud.Rect(name, new Vector2(0f, 1f), Vector2.zero, size, color);
            Place(i.rectTransform, parent, new Vector2(0f, 1f), pos, size, new Vector2(0f, 1f));
            Rounded(i, radius);
            return i;
        }

        private static void Rounded(Image i, int radius, bool rim = false)
        {
            i.sprite = UiSprites.Rounded(radius);
            i.type = Image.Type.Sliced;
            if (!rim) return;
            var r = new GameObject("Rim", typeof(RectTransform)).AddComponent<Image>();
            r.rectTransform.SetParent(i.rectTransform, false);
            r.rectTransform.anchorMin = Vector2.zero;
            r.rectTransform.anchorMax = Vector2.one;
            r.rectTransform.offsetMin = r.rectTransform.offsetMax = Vector2.zero;
            r.sprite = UiSprites.Rounded(radius, 2);
            r.type = Image.Type.Sliced;
            r.color = Edge;
            r.raycastTarget = false;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("[EventSystem]", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            DontDestroyOnLoad(go);
        }

        // --- open / close ---------------------------------------------------------------------------------------

        public static void Open(CampStation station)
        {
            if (_instance == null || _instance._open) return;
            ShopPanel s = _instance;
            bool baits = station != null && station.BaitStall;
            s._station = station;
            s._open = true;
            s._openedFrame = Time.frameCount;
            s._title.SetText(station != null ? station.Label : "Shop");
            s._subtitle.SetText(baits ? "A bait wakes a boss in its den. It is used up when bought."
                : "Upgrades are yours for good. Each column gets better from top to bottom.");
            s.Build(baits);
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

        /// <summary>Sorts what this stall sells into columns and lays them out (once per kind of stall).</summary>
        private void Build(bool baits)
        {
            if (_built && _builtForBaits == baits) return;
            _built = true;
            _builtForBaits = baits;
            for (int i = _body.childCount - 1; i >= 0; i--) Destroy(_body.GetChild(i).gameObject);
            _groups.Clear();

            IReadOnlyList<UpgradeDefinition> all = Loadouts.AllUpgrades;
            for (int i = 0; i < all.Count; i++)
            {
                UpgradeDefinition u = all[i];
                if (u.Unlisted || u.SoldByGunsmith || u.IsBait != baits) continue; // guns at the Gunsmith, baits at bait stalls
                Group g = GroupFor(u);
                g.Upgrades.Add(u);
            }
            foreach (Group g in _groups) g.Upgrades.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            _empty.gameObject.SetActive(_groups.Count == 0);

            float width = _body.rect.width > 0f ? _body.rect.width : PanelW - 80f;
            int n = Mathf.Max(1, _groups.Count);
            float colW = Mathf.Min(380f, (width - ColumnGap * (n - 1)) / n);
            float x0 = (width - (colW * n + ColumnGap * (n - 1))) * 0.5f;
            for (int c = 0; c < _groups.Count; c++) BuildColumn(_groups[c], x0 + c * (colW + ColumnGap), colW);
        }

        private Group GroupFor(UpgradeDefinition u)
        {
            bool bag = u.Effect == UpgradeEffect.InventorySlots;
            ToolDefinition tool = bag || u.IsBait ? null : u.Tool;
            string name = u.IsBait ? "Baits" : bag ? "Your bag" : tool != null ? tool.DisplayName : "Gear";
            foreach (Group g in _groups) if (g.Name == name) return g;
            var made = new Group { Name = name, Tool = tool, Bag = bag };
            _groups.Add(made);
            return made;
        }

        private void BuildColumn(Group g, float x, float w)
        {
            float h = _body.rect.height > 0f ? _body.rect.height : PanelH - Top - 90f;
            Image col = Box($"Column {g.Name}", _body, new Vector2(x, 0f), new Vector2(w, h), Column, 20);
            RectTransform rt = col.rectTransform;

            // The head: the item (or your bag's slots), its name, what it does now.
            if (g.Tool != null && g.Tool.Viewmodel != null)
            {
                var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<RawImage>();
                Place(icon.rectTransform, rt, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(170f, 110f), new Vector2(0.5f, 1f));
                icon.texture = IconRenderer.Get(g.Tool.Viewmodel, 35f, 15f, "Hand");
                icon.raycastTarget = false;
            }
            else if (g.Bag)
            {
                // Up to 9 boxes in a row: your slots now (filled) and the ones the bag column can add (outlined).
                int max = EconomyProfile.BaseSlots;
                foreach (UpgradeDefinition u in g.Upgrades) max += Mathf.RoundToInt(u.Value);
                max = Mathf.Min(max, 9);
                float size = Mathf.Min(40f, (w - 40f) / max - 6f);
                float start = -(max * (size + 6f) - 6f) * 0.5f;
                for (int i = 0; i < max; i++)
                {
                    Image slot = Hud.Rect($"Slot {i}", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size, size), Cream);
                    Place(slot.rectTransform, rt, new Vector2(0.5f, 1f), new Vector2(start + i * (size + 6f) + size * 0.5f, -40f), new Vector2(size, size), new Vector2(0.5f, 1f));
                    Rounded(slot, 8);
                    g.Slots.Add(slot);
                }
            }
            UiFonts.Use(Text("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(w - 20f, 40f), 34f, Cream, TextAlignmentOptions.Top))
                .SetText(g.Name);
            g.Stat = Text("Now", rt, new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(w - 20f, 26f), 17f, Gold, TextAlignmentOptions.Top);

            // The upgrades, top to bottom, joined by arrows.
            for (int i = 0; i < g.Upgrades.Count; i++)
            {
                UpgradeDefinition u = g.Upgrades[i];
                float y = -HeadH - i * (CardH + CardGap);
                if (i > 0)
                {
                    Image arrow = Hud.Rect("Arrow", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(18f, 14f), Dim);
                    Place(arrow.rectTransform, rt, new Vector2(0.5f, 1f), new Vector2(0f, y + CardGap * 0.5f + 7f), new Vector2(18f, 14f), new Vector2(0.5f, 0.5f));
                    arrow.sprite = UiSprites.Tail();
                }
                var card = new CardView { Upgrade = u };
                card.Back = Box($"Card {u.Key}", rt, new Vector2(12f, y), new Vector2(w - 24f, CardH), Card, 14);
                RectTransform crt = card.Back.rectTransform;
                Text("Name", crt, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(w - 160f, 30f), 22f, Cream, TextAlignmentOptions.TopLeft).SetText(u.DisplayName);
                crt.GetChild(crt.childCount - 1).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
                TextMeshProUGUI what = Text("What", crt, new Vector2(0f, 1f), new Vector2(14f, -40f), new Vector2(w - 160f, 40f), 15f, Dim, TextAlignmentOptions.TopLeft);
                what.textWrappingMode = TextWrappingModes.Normal;
                what.overflowMode = TextOverflowModes.Ellipsis;
                what.SetText(Describe(u));
                card.Price = UiFonts.Use(Text("Price", crt, new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(130f, 36f), 30f, Gold, TextAlignmentOptions.TopRight));
                card.ButtonImage = Hud.Rect("Buy", new Vector2(1f, 0f), Vector2.zero, new Vector2(118f, 44f), BuyColor);
                Place(card.ButtonImage.rectTransform, crt, new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(118f, 44f), new Vector2(1f, 0f));
                Rounded(card.ButtonImage, 12);
                card.ButtonImage.raycastTarget = true;
                card.Button = card.ButtonImage.gameObject.AddComponent<Button>();
                card.Button.targetGraphic = card.ButtonImage;
                card.Button.onClick.AddListener(() => OnBuy(u));
                card.ButtonLabel = UiFonts.Use(Text("Label", card.ButtonImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 44f), 24f, Color.white, TextAlignmentOptions.Center));
                card.State = Text("State", crt, new Vector2(0f, 0f), new Vector2(14f, 8f), new Vector2(w - 170f, 22f), 15f, Gold, TextAlignmentOptions.BottomLeft);
                g.Cards.Add(card);
            }
        }

        // --- state --------------------------------------------------------------------------------------------

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
            _wallet.SetText(p.Coins.ToShortString());
            _reclaim.gameObject.SetActive(!_builtForBaits && p.MissingTools().Count > 0);
            foreach (Group g in _groups)
            {
                g.Stat.SetText(NowLine(g, p));
                for (int i = 0; i < g.Slots.Count; i++)
                    g.Slots[i].color = i < p.SlotCapacity ? Cream : new Color(Cream.r, Cream.g, Cream.b, 0.16f);
                foreach (CardView c in g.Cards) Show(c, p);
            }
        }

        private static void Show(CardView c, EconomyProfile p)
        {
            UpgradeDefinition u = c.Upgrade;
            c.Price.SetText(u.CostCoins <= 0 ? "Free" : $"{((BigDouble)u.CostCoins).ToShortString()}c");
            EconomyRefusal why = EconomyNet.CanBuy(p, u);
            bool owned = why == EconomyRefusal.AlreadyOwned;
            c.Back.color = owned ? CardOwned : Card;
            c.Price.gameObject.SetActive(!owned);
            c.Button.interactable = why == EconomyRefusal.None || why == EconomyRefusal.TooPoor;
            switch (why)
            {
                case EconomyRefusal.None:
                    c.ButtonLabel.SetText("Buy");
                    c.ButtonImage.color = BuyColor;
                    c.Price.color = Gold;
                    c.State.SetText(string.Empty);
                    break;
                case EconomyRefusal.AlreadyOwned:
                    c.ButtonLabel.SetText("Owned");
                    c.ButtonImage.color = OwnedColor;
                    c.State.SetText(string.Empty);
                    break;
                case EconomyRefusal.TooPoor:
                    c.ButtonLabel.SetText("Buy");
                    c.ButtonImage.color = PoorColor;
                    c.Price.color = Red;
                    c.State.SetText($"{((BigDouble)u.CostCoins - p.Coins).ToShortString()} more coins");
                    break;
                case EconomyRefusal.BossAwake:
                    c.ButtonLabel.SetText("Awake");
                    c.ButtonImage.color = LockedColor;
                    c.Price.color = Dim;
                    c.State.SetText("It's already out: go and fight it");
                    break;
                default:
                    c.ButtonLabel.SetText("Locked");
                    c.ButtonImage.color = LockedColor;
                    c.Price.color = Dim;
                    c.State.SetText(u.Requires != null ? $"Needs the {u.Requires.DisplayName}" : "Not yet");
                    break;
            }
        }

        /// <summary>What the column's item does right now, in a line.</summary>
        private static string NowLine(Group g, EconomyProfile p)
        {
            if (g.Bag) return $"{p.SlotCapacity} slots now";
            if (g.Tool == null) return string.Empty;
            ToolStats s = Loadouts.Local.StatsFor(g.Tool, out float range);
            if (s.Archetype == ToolArchetype.Net) return $"Strength {s.CaptureStrength:0.#}  ·  reach {range:0.#} m";
            if (s.Damage > 0f) return $"Damage {s.Damage:0.#}  ·  reach {range:0.#} m";
            return string.Empty;
        }

        /// <summary>What an upgrade does, in plain words.</summary>
        public static string Describe(UpgradeDefinition u)
        {
            string tool = u.Tool != null ? u.Tool.DisplayName : "tool";
            string effect;
            switch (u.Effect)
            {
                case UpgradeEffect.CaptureStrength: effect = $"+{u.Value:0.#} strength"; break;
                case UpgradeEffect.DamageMultiplier: effect = $"×{u.Value:0.##} damage"; break;
                case UpgradeEffect.Range: effect = $"+{u.Value:0.#} m reach"; break;
                case UpgradeEffect.UnlockTool: effect = $"Unlocks the {tool}"; break;
                case UpgradeEffect.InventorySlots: effect = $"+{u.Value:0} slots"; break;
                case UpgradeEffect.SummonBoss: effect = u.Summons != null ? $"Wakes {u.Summons.Noun} (used up)" : "A bait"; break;
                default: effect = string.Empty; break;
            }
            return string.IsNullOrEmpty(u.Description) ? effect : $"<b>{effect}</b>. {u.Description}";
        }

        // --- frame ----------------------------------------------------------------------------------------------

        private void Update()
        {
            if (!_open || Time.frameCount == _openedFrame) return;
            UiInput.KeepGamepadSelection(_panel); // gamepad: D-pad/stick and A drive the buttons
            bool close = KeyBindings.WasPressedThisFrame(GameAction.Interact) || UiInput.Back; // Esc or gamepad B
            if (PlayerMotor.Local == null || _station == null) close = true;
            else if (CampStation.NearestShop(PlayerMotor.Local.transform.position, _builtForBaits, 1.5f) == null) close = true;
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
