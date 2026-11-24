using HowToHunt.Core;
using HowToHunt.Hunting;
using HowToHunt.Player;
using HowToHunt.Sim.Economy;
using HowToHunt.Sim.Hunting;
using TMPro;
using UnityEngine;

namespace HowToHunt.Economy
{
    /// <summary>
    /// Wallet and bag (top right), the "[E] Sell 5 catches (~42 coins)" prompt near camp stations, the Interact key,
    /// and the feed lines for catches, sales and purchases. Text changes only when the numbers do.
    /// </summary>
    public sealed class EconomyHud : MonoBehaviour
    {
        private TextMeshProUGUI _wallet;
        private TextMeshProUGUI _prompt;
        private CampStation _promptStation;
        private int _promptBag = -1;
        private WorldItem _promptItem;
        private readonly ModifierStack _scratch = new ModifierStack();

        private void Awake()
        {
            var warm = new Color(1f, 0.93f, 0.7f);
            _wallet = Hud.Text("Wallet", new Vector2(1f, 1f), new Vector2(-30f, -84f), 30f, TextAlignmentOptions.TopRight, warm);
            _wallet.rectTransform.pivot = new Vector2(1f, 1f);
            _wallet.rectTransform.sizeDelta = new Vector2(600f, 40f);
            _wallet.fontStyle = FontStyles.Bold;
            _prompt = Hud.Text("Station Prompt", new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), 28f, TextAlignmentOptions.Center, Color.white);
            _prompt.rectTransform.sizeDelta = new Vector2(1000f, 44f);
            _prompt.gameObject.SetActive(false);
            EconomyNet.LocalChanged += OnChanged;
            Refresh();
        }

        private void OnDestroy() => EconomyNet.LocalChanged -= OnChanged;

        private void Refresh()
        {
            EconomyProfile p = EconomyNet.Local;
            _wallet.SetText(p.Trophies > 0 ? $"{p.Coins.ToShortString()} coins   {p.Trophies:0} trophies" : $"{p.Coins.ToShortString()} coins");
            _promptBag = -1; // force the prompt to recompute its estimate
        }

        private void OnChanged(EconomyStateBroadcast msg)
        {
            Refresh();
            switch ((EconomyEvent)msg.Event)
            {
                case EconomyEvent.Caught:
                case EconomyEvent.Killed:
                case EconomyEvent.PickedUp:
                case EconomyEvent.InventoryFull:
                {
                    PreyDefinition prey = ContentRegistry.Get<PreyDefinition>(new Foundation.ContentId(msg.EventContent));
                    var item = new ItemStack { Kind = ItemKind.Prey, Content = msg.EventContent, Flags = msg.EventFlags, Count = 1 };
                    string name = prey?.DisplayName ?? ContentRegistry.Get<ContentDefinition>(new Foundation.ContentId(msg.EventContent))?.DisplayName ?? "item";
                    string how = item.Captured ? "caught" : item.Quality == CarcassQuality.Ruined ? "ruined" : "killed";
                    var e = (EconomyEvent)msg.Event;
                    if (e == EconomyEvent.Caught)
                    {
                        HuntFeedback.Feed($"+ {name} · {BigDouble.Deserialize(msg.EventCoins).ToShortString()}c");
                        Sfx.Play2D("Kenney/handleSmallLeather", 0.35f, Random.Range(0.95f, 1.05f));
                    }
                    else if (e == EconomyEvent.Killed)
                        HuntFeedback.Feed($"{name} down · pick it up", item.Quality != CarcassQuality.Ruined);
                    else if (e == EconomyEvent.PickedUp)
                    {
                        HuntFeedback.Feed($"+ {name}");
                        Sfx.Play2D("Kenney/handleSmallLeather", 0.4f, Random.Range(0.9f, 1.05f));
                    }
                    else
                        HuntFeedback.Message("Inventory full", warning: true);
                    break;
                }
                case EconomyEvent.Sold:
                    HuntFeedback.Feed($"Sold {msg.EventCount} · {BigDouble.Deserialize(msg.EventCoins).ToShortString()}c");
                    HuntFeedback.Message($"+{BigDouble.Deserialize(msg.EventCoins).ToShortString()} coins");
                    Sfx.Play2D("Kenney/handleCoins", 0.6f);
                    if (Camera.main != null) CashBurst.Play(Camera.main.transform.position, BigDouble.Deserialize(msg.EventCoins).ToDouble());
                    break;
                case EconomyEvent.QuestAccepted:
                {
                    QuestDefinition q = ContentRegistry.Get<QuestDefinition>(new Foundation.ContentId(msg.EventContent));
                    if (q != null) HuntFeedback.Message($"New quest: {q.DisplayName}");
                    Sfx.Play2D("Kenney/confirmation_001", 0.5f);
                    break;
                }
                case EconomyEvent.QuestDone:
                {
                    QuestDefinition q = ContentRegistry.Get<QuestDefinition>(new Foundation.ContentId(msg.EventContent));
                    if (q != null) HuntFeedback.Message($"Quest done: {q.DisplayName}  +{BigDouble.Deserialize(msg.EventCoins).ToShortString()} coins");
                    Sfx.Play2D("Kenney/handleCoins", 0.6f);
                    if (Camera.main != null) CashBurst.Play(Camera.main.transform.position, BigDouble.Deserialize(msg.EventCoins).ToDouble());
                    break;
                }
                case EconomyEvent.BossReward:
                {
                    PreyDefinition boss = ContentRegistry.Get<PreyDefinition>(new Foundation.ContentId(msg.EventContent));
                    string title = boss != null && !string.IsNullOrEmpty(boss.BossTitle) ? boss.BossTitle : "The boss";
                    HuntFeedback.Message($"{title} is down! Your share: +{BigDouble.Deserialize(msg.EventCoins).ToShortString()} coins, +{msg.EventCount} trophies");
                    Sfx.Play2D("Kenney/handleCoins", 0.8f);
                    if (Camera.main != null) CashBurst.Play(Camera.main.transform.position, BigDouble.Deserialize(msg.EventCoins).ToDouble());
                    break;
                }
                case EconomyEvent.AttachmentBought:
                {
                    AttachmentDefinition a = ContentRegistry.Get<AttachmentDefinition>(new Foundation.ContentId(msg.EventContent));
                    if (a != null) HuntFeedback.Feed($"Bought {a.DisplayName}");
                    Sfx.Play2D("Kenney/confirmation_001", 0.5f);
                    Sfx.Play2D("Kenney/handleCoins", 0.4f, 0.9f);
                    break;
                }
                case EconomyEvent.AttachmentFitted:
                    Sfx.Play2D("Kenney/click_001", 0.5f, 0.8f);
                    break;
                case EconomyEvent.Bought:
                {
                    UpgradeDefinition u = ContentRegistry.Get<UpgradeDefinition>(new Foundation.ContentId(msg.EventContent));
                    if (u == null) break;
                    HuntFeedback.Feed($"Bought {u.DisplayName}");
                    Sfx.Play2D("Kenney/confirmation_001", 0.5f);
                    Sfx.Play2D("Kenney/handleCoins", 0.4f, 0.9f);
                    if (u.Effect == UpgradeEffect.UnlockTool && u.Tool != null)
                        HuntFeedback.Message($"{u.Tool.DisplayName} unlocked: it's in your inventory bar.");
                    if (u.Effect == UpgradeEffect.InventorySlots) HuntFeedback.Message($"+{u.Value:0} inventory slots.");
                    if (u.IsBait && u.Summons != null) HuntFeedback.Message($"You set out the {u.DisplayName}. {u.Summons.BossTitle} is coming out of its den!", warning: true);
                    break;
                }
                case EconomyEvent.ToolsReturned:
                    HuntFeedback.Message(msg.EventCount == 1 ? "The outfitter hands you a replacement tool." : $"The outfitter hands you {msg.EventCount} replacement tools.");
                    Sfx.Play2D("Kenney/handleSmallLeather", 0.4f);
                    break;
                case EconomyEvent.Refused:
                    HuntFeedback.Message(RefusalLine((EconomyRefusal)msg.Refusal), warning: true);
                    break;
            }
        }

        public static string RefusalLine(EconomyRefusal r)
        {
            switch (r)
            {
                case EconomyRefusal.NotAtStation: return "Get closer to the station.";
                case EconomyRefusal.NothingToSell: return "Nothing to sell. Go catch something!";
                case EconomyRefusal.TooFar: return "Too far away to pick that up.";
                case EconomyRefusal.InventoryFull: return "Your inventory is full. Sell at the trader, or drop something (Q).";
                case EconomyRefusal.TooPoor: return "Not enough coins.";
                case EconomyRefusal.AlreadyOwned: return "You already own that.";
                case EconomyRefusal.BossAwake: return "It's already awake. Go and fight it!";
                case EconomyRefusal.NoBossHere: return "Nothing would come for that bait here.";
                case EconomyRefusal.Locked: return "Buy the previous upgrade first.";
                default: return "That can't be done.";
            }
        }

        private void Update()
        {
            PlayerMotor me = PlayerMotor.Local;
            CampStation station = null;
            WorldItem item = null;
            Camera cam = Camera.main;
            if (me != null && !Game.UiHasFocus)
            {
                if (cam != null) item = WorldItems.Targeted(cam.transform.position, cam.transform.forward, WorldItems.PickupReach.Value);
                Vector3 pos = me.transform.position;
                station = CampStation.Nearest(pos, CampStationKind.Sell) ?? CampStation.Nearest(pos, CampStationKind.Shop) ?? CampStation.Nearest(pos, CampStationKind.Workshop);
            }

            int bag = EconomyNet.Local.Inventory.PreyCount;
            if (item != _promptItem || station != _promptStation || bag != _promptBag)
            {
                _promptItem = item;
                _promptStation = station;
                _promptBag = bag;
                string text = item != null ? PromptFor(item) : station != null ? PromptFor(station) : null;
                if (text != null) _prompt.SetText(text);
                _prompt.gameObject.SetActive(text != null);
            }

            if (!KeyBindings.WasPressedThisFrame(GameAction.Interact)) return;
            if (item == null && station != null && TalkAt != null && TalkAt(station)) return; // the stall's NPC takes it from here
            if (item != null) EconomyNet.RequestPickUp(item.Id);
            else if (station != null)
            {
                if (station.Kind == CampStationKind.Sell) EconomyNet.RequestSell();
                else if (station.Kind == CampStationKind.Workshop) WorkshopPanel.Open(station);
                else ShopPanel.Open(station);
            }
        }

        private static string PromptFor(WorldItem w)
        {
            string key = KeyBindings.DisplayName(GameAction.Interact);
            ContentDefinition def = ContentRegistry.Get<ContentDefinition>(new Foundation.ContentId(w.Item.Content));
            string name = def != null ? def.DisplayName : "item";
            if (w.Item.Kind == ItemKind.Tool) return $"[{key}] Pick up {name}";
            string how = w.Item.Captured ? "caught" : w.Item.Quality == CarcassQuality.Ruined ? "ruined" : "killed";
            string count = w.Item.Count > 1 ? $" x{w.Item.Count}" : string.Empty;
            return $"[{key}] Pick up {name}{count} ({how})";
        }

        /// <summary>Set by the NPC feature: talk to whoever runs this stall (true: handled, the stall's own action waits).</summary>
        public static System.Func<CampStation, bool> TalkAt;
        /// <summary>Set by the NPC feature: the prompt near a stall with someone at it ("[E] Talk to Tomas").</summary>
        public static System.Func<CampStation, string> TalkPrompt;

        private string PromptFor(CampStation station)
        {
            string talk = TalkPrompt?.Invoke(station);
            if (talk != null) return talk;
            string key = KeyBindings.DisplayName(GameAction.Interact);
            if (station.Kind == CampStationKind.Shop) return station.BaitStall ? $"[{key}] {station.Label}: baits that wake a boss" : $"[{key}] {station.Label}: browse upgrades";
            if (station.Kind == CampStationKind.Workshop) return station.FullArmory ? $"[{key}] {station.Label}: every gun and attachment" : $"[{key}] {station.Label}: guns and attachments";
            EconomyProfile p = EconomyNet.Local;
            int n = p.Inventory.PreyCount;
            if (n == 0) return $"{station.Label}: nothing to sell yet";
            CatchReward value = p.InventoryValue(EconomyRules.Default, _scratch);
            return $"[{key}] Sell {n} {(n == 1 ? "catch" : "catches")} to the {station.Label} (~{value.Coins.ToShortString()} coins)";
        }
    }
}
