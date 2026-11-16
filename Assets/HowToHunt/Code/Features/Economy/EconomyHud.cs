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
        private TextMeshProUGUI _bag;
        private TextMeshProUGUI _prompt;
        private CampStation _promptStation;
        private int _promptBag = -1;
        private readonly ModifierStack _scratch = new ModifierStack();

        private void Awake()
        {
            var warm = new Color(1f, 0.93f, 0.7f);
            _wallet = Hud.Text("Wallet", new Vector2(1f, 1f), new Vector2(-30f, -84f), 30f, TextAlignmentOptions.TopRight, warm);
            _wallet.rectTransform.pivot = new Vector2(1f, 1f);
            _wallet.rectTransform.sizeDelta = new Vector2(600f, 40f);
            _wallet.fontStyle = FontStyles.Bold;
            _bag = Hud.Text("Bag", new Vector2(1f, 1f), new Vector2(-30f, -122f), 22f, TextAlignmentOptions.TopRight, new Color(1f, 1f, 1f, 0.85f));
            _bag.rectTransform.pivot = new Vector2(1f, 1f);
            _bag.rectTransform.sizeDelta = new Vector2(600f, 32f);
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
            _bag.SetText("Bag {0}/{1}", (float)p.Bag.Count, (float)EconomyNet.BagCapacity.Value);
            _promptBag = -1; // force the prompt to recompute its estimate
        }

        private void OnChanged(EconomyStateBroadcast msg)
        {
            Refresh();
            switch ((EconomyEvent)msg.Event)
            {
                case EconomyEvent.Caught:
                {
                    PreyDefinition prey = ContentRegistry.Get<PreyDefinition>(new Foundation.ContentId(msg.EventContent));
                    BagItem item = EconomyNet.UnpackFlags(msg.EventContent, msg.EventFlags);
                    string how = item.Captured ? "caught" : item.Quality == CarcassQuality.Ruined ? "ruined" : "killed";
                    HuntFeedback.Feed($"+ {prey?.DisplayName ?? "catch"} ({how})  ~{BigDouble.Deserialize(msg.EventCoins).ToShortString()} coins", item.Quality != CarcassQuality.Ruined);
                    Sfx.Play2D("Kenney/handleSmallLeather", 0.35f, Random.Range(0.95f, 1.05f));
                    break;
                }
                case EconomyEvent.BagFull:
                    HuntFeedback.Message($"Your bag is full ({EconomyNet.BagCapacity.Value}). Sell your catch at the trader in camp.", warning: true);
                    break;
                case EconomyEvent.Sold:
                    HuntFeedback.Feed($"Sold {msg.EventCount} {(msg.EventCount == 1 ? "catch" : "catches")} for {BigDouble.Deserialize(msg.EventCoins).ToShortString()} coins");
                    HuntFeedback.Message($"+{BigDouble.Deserialize(msg.EventCoins).ToShortString()} coins");
                    Sfx.Play2D("Kenney/handleCoins", 0.6f);
                    break;
                case EconomyEvent.Bought:
                {
                    UpgradeDefinition u = ContentRegistry.Get<UpgradeDefinition>(new Foundation.ContentId(msg.EventContent));
                    if (u == null) break;
                    HuntFeedback.Feed($"Bought {u.DisplayName}");
                    Sfx.Play2D("Kenney/confirmation_001", 0.5f);
                    Sfx.Play2D("Kenney/handleCoins", 0.4f, 0.9f);
                    if (u.Effect == UpgradeEffect.UnlockTool && u.Tool != null)
                        HuntFeedback.Message($"{u.Tool.DisplayName} unlocked. Press {u.Tool.Slot} to equip it.");
                    break;
                }
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
                case EconomyRefusal.EmptyBag: return "Nothing to sell. Go catch something!";
                case EconomyRefusal.TooPoor: return "Not enough coins.";
                case EconomyRefusal.AlreadyOwned: return "You already own that.";
                case EconomyRefusal.Locked: return "Buy the previous upgrade first.";
                default: return "That can't be done.";
            }
        }

        private void Update()
        {
            PlayerMotor me = PlayerMotor.Local;
            CampStation station = null;
            if (me != null && !Game.UiHasFocus)
            {
                Vector3 pos = me.transform.position;
                station = CampStation.Nearest(pos, CampStationKind.Sell) ?? CampStation.Nearest(pos, CampStationKind.Shop);
            }

            int bag = EconomyNet.Local.Bag.Count;
            if (station != _promptStation || (station != null && bag != _promptBag))
            {
                _promptStation = station;
                _promptBag = bag;
                if (station != null) _prompt.SetText(PromptFor(station));
                _prompt.gameObject.SetActive(station != null);
            }

            if (station != null && KeyBindings.WasPressedThisFrame(GameAction.Interact))
            {
                if (station.Kind == CampStationKind.Sell) EconomyNet.RequestSell();
                else ShopPanel.Open(station);
            }
        }

        private string PromptFor(CampStation station)
        {
            string key = KeyBindings.DisplayName(GameAction.Interact);
            if (station.Kind == CampStationKind.Shop) return $"[{key}] {station.Label}: browse upgrades";
            EconomyProfile p = EconomyNet.Local;
            if (p.Bag.Count == 0) return $"{station.Label}: nothing to sell yet";
            CatchReward value = p.BagValue(EconomyRules.Default, _scratch);
            return $"[{key}] Sell {p.Bag.Count} {(p.Bag.Count == 1 ? "catch" : "catches")} to the {station.Label} (~{value.Coins.ToShortString()} coins)";
        }
    }
}
