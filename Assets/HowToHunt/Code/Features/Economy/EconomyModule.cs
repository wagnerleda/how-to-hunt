using FishNet.Managing;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Foundation;
using HowToHunt.Hunting;
using HowToHunt.Net;
using HowToHunt.Player;

[assembly: FeatureModule(typeof(EconomyModule))]

namespace HowToHunt.Economy
{
    /// <summary>
    /// Economy: the bag of unsold catch, coins and trophies, research (Bestiary mastery), camp stations (trader and
    /// outfitter) and upgrades. Host-authoritative ledger, persisted in each player's own profile.
    /// </summary>
    public sealed class EconomyModule : IFeatureModule
    {
        public string Id => "economy";
        public int Order => 20;

        public void Register(ModuleRegistrar r)
        {
            r.Cvar(EconomyNet.ThrowSpeed);
            r.Cvar(EconomyNet.ThrowSpeedMin);
            r.Cvar(EconomyNet.ThrowChargeTime);
            r.Cvar(EconomyNet.ThrowSlowTime);
            r.Cvar(EconomyNet.ThrowTapTime);
            r.Cvar(CashBurst.MinRatio);
            r.Cvar(CashBurst.BillsPerExpected);
            r.Cvar(CashBurst.MaxBills);
            r.Cvar(WorldItems.Lifetime);
            r.Cvar(WorldItems.PickupReach);
            r.Cvar(EconomyNet.StationReach);
            r.SaveSlice(new EconomySaveSlice(EconomyNet.Local, EconomyNet.OnLocalLoaded));
            r.Command("econ_give", "econ_give <coins>: give every hunter in the party coins (host, sv_cheats 1).", args =>
            {
                if (args.Count < 1 || !double.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double coins))
                {
                    r.Console.Warn("Usage: econ_give <coins>");
                    return;
                }
                if (NetSession.Manager == null || !NetSession.IsServer) { r.Console.Warn("Only the host can give coins."); return; }
                foreach (int id in NetSession.Manager.ServerManager.Clients.Keys) EconomyNet.HostGrant(id, coins);
            }, CommandFlags.Cheat);
            r.Command("quest_complete", "quest_complete: finish the objectives of every hunter's quests in progress; hand them in to their giver (host, sv_cheats 1).", _ =>
            {
                if (NetSession.Manager == null || !NetSession.IsServer) { r.Console.Warn("Only the host can complete quests."); return; }
                int total = 0;
                foreach (int id in NetSession.Manager.ServerManager.Clients.Keys) total += EconomyNet.HostCompleteQuests(id);
                r.Console.Print(total > 0 ? $"[quest] {total} quest(s) ready: hand them in to whoever gave them." : "[quest] No quest in progress. Accept one from a camp character first.");
            }, CommandFlags.Cheat);
            r.Command("econ_status", "Print your coins, bag, research and upgrades.", _ =>
            {
                EconomyProfile p = EconomyNet.Local;
                r.Console.Print($"[econ] {p.Coins.ToShortString()} coins, {p.Trophies:0} trophies, carrying {p.Inventory.PreyCount} prey in {p.Inventory.Capacity} slots, " +
                                $"{p.Captures} caught, {p.Kills} killed, {p.Upgrades.Count} upgrades, lifetime {p.LifetimeCoins.ToShortString()} coins");
                foreach (var kv in p.Research)
                {
                    PreyDefinition def = ContentRegistry.Get<PreyDefinition>(new ContentId(kv.Key));
                    r.Console.Print($"[econ]   research {def?.DisplayName ?? kv.Key.ToString()}: {kv.Value} (mastery {Sim.Hunting.EconomyRules.Default.MasteryLevel(kv.Value)})");
                }
            });
        }

        public void Initialize()
        {
            var root = Game.Root.gameObject;
            root.AddComponent<EconomyHud>();
            root.AddComponent<ShopPanel>();
            root.AddComponent<WorkshopPanel>();
            root.AddComponent<InventoryBar>();
            root.AddComponent<CashBurst>();
            root.AddComponent<WorldItemRenderer>();
            NetSession.ManagerCreated += OnManagerCreated;
            NetSession.SessionEnded += EconomyNet.Detach;
            PlayerMotor.LocalPlayerStarted += OnLocalPlayer;
            EventBus.Subscribe<PreyTakenDown>(EconomyNet.OnPreyTakenDown);
        }

        public void Shutdown()
        {
            NetSession.ManagerCreated -= OnManagerCreated;
            NetSession.SessionEnded -= EconomyNet.Detach;
            PlayerMotor.LocalPlayerStarted -= OnLocalPlayer;
            EventBus.Unsubscribe<PreyTakenDown>(EconomyNet.OnPreyTakenDown);
            EconomyNet.Detach();
        }

        private static void OnManagerCreated(NetworkManager m) => EconomyNet.Attach(m);

        private static void OnLocalPlayer(PlayerMotor motor) => EconomyNet.SendJoin();
    }
}
