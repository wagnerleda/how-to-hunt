using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Hunting;
using HowToHunt.Player;
using HowToHunt.Sim.Economy;
using HowToHunt.Sim.Hunting;
using UnityEngine;

namespace HowToHunt.Economy
{
    public enum EconomyEvent : byte
    {
        /// <summary>Client → host: here is my profile (on joining).</summary>
        Join,
        /// <summary>Host → client: your ledger, nothing special happened.</summary>
        Synced,
        Caught,
        Sold,
        Bought,
        Refused,
        BagFull,
    }

    public enum EconomyRefusal : byte { None, NotAtStation, EmptyBag, TooPoor, AlreadyOwned, Locked, Unknown }

    /// <summary>A whole economy profile on the wire, plus what just happened. Small (tens of bytes plus the bag).</summary>
    public struct EconomyStateBroadcast : IBroadcast
    {
        public byte Event;
        public byte Refusal;
        public uint EventContent;
        public byte EventFlags;
        public string EventCoins;
        public int EventCount;

        public string Coins;
        public double Trophies;
        public uint[] Upgrades;
        public uint[] ResearchPrey;
        public int[] ResearchCount;
        public uint[] BagPrey;
        public byte[] BagFlags;
        public int Captures;
        public int Kills;
        public string LifetimeCoins;
    }

    public enum EconomyRequestKind : byte { Sell, Buy }

    /// <summary>Client → host: sell the bag, or buy an upgrade.</summary>
    public struct EconomyRequestBroadcast : IBroadcast
    {
        public byte Kind;
        public uint Upgrade;
    }

    /// <summary>
    /// Host-authoritative economy. Each player's profile is theirs and persists on their machine, but during a party
    /// the host keeps the live copy (ledger): the client sends its profile once on joining, the host credits catches,
    /// sells and sells upgrades, and returns the full ledger after every change, which the client saves. Clients never
    /// change their own coins. (Trusting the joining profile is a v1 choice for co-op with friends: docs/adr/0011.)
    /// </summary>
    public static class EconomyNet
    {
        public static readonly IntCvar BagCapacity = new IntCvar("sv_bag_capacity", 12, "How many unsold catches a hunter can carry.", CvarFlags.Replicated | CvarFlags.Cheat, 1, 500);
        public static readonly FloatCvar StationReach = new FloatCvar("sv_station_reach", 3.5f, "Extra distance (m) beyond a camp station's radius at which the host still accepts its use.", CvarFlags.Replicated | CvarFlags.Cheat, 0f, 50f);

        /// <summary>The local player's profile (persistent, mirrored from the host during a party).</summary>
        public static readonly EconomyProfile Local = new EconomyProfile();

        /// <summary>Raised on this machine after the local profile changed (HUD, shop refresh).</summary>
        public static event Action<EconomyStateBroadcast> LocalChanged;

        private static readonly Dictionary<int, EconomyProfile> Ledger = new Dictionary<int, EconomyProfile>();
        private static readonly ModifierStack Scratch = new ModifierStack();
        private static NetworkManager _manager;
        private static bool _joined;

        private static bool IsHost => _manager != null && _manager.IsServerStarted;

        public static void Attach(NetworkManager m)
        {
            _manager = m;
            _joined = false;
            Ledger.Clear();
            m.ServerManager.RegisterBroadcast<EconomyStateBroadcast>(OnJoinFromClient);
            m.ServerManager.RegisterBroadcast<EconomyRequestBroadcast>(OnRequest);
            m.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            m.ClientManager.RegisterBroadcast<EconomyStateBroadcast>(OnStateFromHost);
        }

        public static void Detach()
        {
            _manager = null;
            _joined = false;
            Ledger.Clear();
        }

        // --- client -----------------------------------------------------------------------------------------

        /// <summary>Our player spawned: hand our profile to the host.</summary>
        public static void SendJoin()
        {
            if (_manager == null || !_manager.IsClientStarted || _joined) return;
            _joined = true;
            EconomyStateBroadcast msg = Pack(Local, EconomyEvent.Join);
            _manager.ClientManager.Broadcast(msg);
        }

        public static void RequestSell()
        {
            if (_manager != null && _manager.IsClientStarted)
                _manager.ClientManager.Broadcast(new EconomyRequestBroadcast { Kind = (byte)EconomyRequestKind.Sell });
        }

        public static void RequestBuy(UpgradeDefinition upgrade)
        {
            if (upgrade != null && _manager != null && _manager.IsClientStarted)
                _manager.ClientManager.Broadcast(new EconomyRequestBroadcast { Kind = (byte)EconomyRequestKind.Buy, Upgrade = upgrade.Id.Value });
        }

        private static void OnStateFromHost(EconomyStateBroadcast msg, Channel channel)
        {
            Unpack(msg, Local);
            Loadouts.Local.SetUpgrades(Local.Upgrades);
            ProfileStore.MarkDirty();
            LocalChanged?.Invoke(msg);
        }

        /// <summary>The profile was loaded from disk (or reset): refresh what depends on it.</summary>
        public static void OnLocalLoaded()
        {
            Loadouts.Local.SetUpgrades(Local.Upgrades);
            LocalChanged?.Invoke(new EconomyStateBroadcast { Event = (byte)EconomyEvent.Synced });
        }

        // --- host -------------------------------------------------------------------------------------------

        private static void OnJoinFromClient(NetworkConnection conn, EconomyStateBroadcast msg, Channel channel)
        {
            if (msg.Event != (byte)EconomyEvent.Join || Ledger.ContainsKey(conn.ClientId)) return;
            var p = new EconomyProfile();
            Unpack(msg, p);
            // Drop anything this build doesn't know, and keep the bag within capacity.
            p.Upgrades.RemoveAll(u => ContentRegistry.Get<UpgradeDefinition>(new ContentId(u)) == null);
            if (p.Bag.Count > BagCapacity.Value) p.Bag.RemoveRange(BagCapacity.Value, p.Bag.Count - BagCapacity.Value);
            Ledger[conn.ClientId] = p;
            Loadouts.ForClient(conn.ClientId).SetUpgrades(p.Upgrades);
            Send(conn, p, EconomyEvent.Synced);
        }

        private static void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;
            Ledger.Remove(conn.ClientId);
            Loadouts.Remove(conn.ClientId);
        }

        /// <summary>Host: a player caught or killed something (published by the hunting feature).</summary>
        public static void OnPreyTakenDown(in PreyTakenDown e)
        {
            if (!IsHost || !Ledger.TryGetValue(e.ClientId, out EconomyProfile p)) return;
            NetworkConnection conn = Connection(e.ClientId);
            if (conn == null) return;
            if (p.Bag.Count >= BagCapacity.Value)
            {
                Send(conn, p, EconomyEvent.BagFull, content: e.Prey.Value);
                return;
            }
            var item = new BagItem { Prey = e.Prey.Value, Captured = e.Captured, Quality = e.Quality };
            p.Bag.Add(item);
            if (e.Captured)
            {
                p.Captures++;
                p.Research[e.Prey.Value] = p.ResearchOf(e.Prey.Value) + 1;
            }
            else p.Kills++;
            CatchReward value = p.ValueOf(item, EconomyRules.Default, Scratch);
            Send(conn, p, EconomyEvent.Caught, content: e.Prey.Value, flags: PackFlags(item), coins: value.Coins);
        }

        private static void OnRequest(NetworkConnection conn, EconomyRequestBroadcast req, Channel channel)
        {
            if (!Ledger.TryGetValue(conn.ClientId, out EconomyProfile p)) return;
            if (req.Kind == (byte)EconomyRequestKind.Sell) Sell(conn, p);
            else if (req.Kind == (byte)EconomyRequestKind.Buy) Buy(conn, p, req.Upgrade);
        }

        private static void Sell(NetworkConnection conn, EconomyProfile p)
        {
            if (!NearStation(conn, CampStationKind.Sell)) { Refuse(conn, p, EconomyRefusal.NotAtStation); return; }
            if (p.Bag.Count == 0) { Refuse(conn, p, EconomyRefusal.EmptyBag); return; }
            CatchReward total = p.BagValue(EconomyRules.Default, Scratch);
            int count = p.Bag.Count;
            p.Bag.Clear();
            p.Coins += total.Coins;
            p.LifetimeCoins += total.Coins;
            p.Trophies += total.Trophies;
            Send(conn, p, EconomyEvent.Sold, coins: total.Coins, count: count);
        }

        private static void Buy(NetworkConnection conn, EconomyProfile p, uint upgradeId)
        {
            UpgradeDefinition u = ContentRegistry.Get<UpgradeDefinition>(new ContentId(upgradeId));
            EconomyRefusal why = CanBuy(p, u);
            if (why == EconomyRefusal.None && !NearStation(conn, CampStationKind.Shop)) why = EconomyRefusal.NotAtStation;
            if (why != EconomyRefusal.None) { Refuse(conn, p, why, upgradeId); return; }
            p.Coins -= u.CostCoins;
            p.Trophies -= u.CostTrophies;
            p.Upgrades.Add(upgradeId);
            Loadouts.ForClient(conn.ClientId).AddUpgrade(upgradeId);
            Send(conn, p, EconomyEvent.Bought, content: upgradeId, coins: u.CostCoins);
        }

        /// <summary>Why this profile can't buy that upgrade (None = it can). Used by the host and the shop UI.</summary>
        public static EconomyRefusal CanBuy(EconomyProfile p, UpgradeDefinition u)
        {
            if (u == null) return EconomyRefusal.Unknown;
            if (p.Owns(u.Id.Value)) return EconomyRefusal.AlreadyOwned;
            if (u.Requires != null && !p.Owns(u.Requires.Id.Value)) return EconomyRefusal.Locked;
            if (p.Coins < u.CostCoins || p.Trophies < u.CostTrophies) return EconomyRefusal.TooPoor;
            return EconomyRefusal.None;
        }

        private static void Refuse(NetworkConnection conn, EconomyProfile p, EconomyRefusal why, uint content = 0)
            => Send(conn, p, EconomyEvent.Refused, content: content, refusal: why);

        private static bool NearStation(NetworkConnection conn, CampStationKind kind)
        {
            PlayerMotor motor = MotorOf(conn.ClientId);
            if (motor == null) return false;
            CampStation s = CampStation.Nearest(motor.transform.position, kind, StationReach.Value);
            return s != null;
        }

        private static PlayerMotor MotorOf(int clientId)
        {
            var all = PlayerMotor.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Owner != null && all[i].Owner.ClientId == clientId) return all[i];
            return null;
        }

        private static NetworkConnection Connection(int clientId)
            => _manager != null && _manager.ServerManager.Clients.TryGetValue(clientId, out NetworkConnection c) ? c : null;

        private static void Send(NetworkConnection conn, EconomyProfile p, EconomyEvent evt, uint content = 0, byte flags = 0,
            BigDouble coins = default, int count = 0, EconomyRefusal refusal = EconomyRefusal.None)
        {
            EconomyStateBroadcast msg = Pack(p, evt);
            msg.EventContent = content;
            msg.EventFlags = flags;
            msg.EventCoins = coins.Serialize();
            msg.EventCount = count;
            msg.Refusal = (byte)refusal;
            _manager.ServerManager.Broadcast(conn, msg);
        }

        // --- packing ----------------------------------------------------------------------------------------

        public static byte PackFlags(in BagItem b) => (byte)((b.Captured ? 1 : 0) | ((byte)b.Quality << 1));

        public static BagItem UnpackFlags(uint prey, byte flags)
            => new BagItem { Prey = prey, Captured = (flags & 1) != 0, Quality = (CarcassQuality)((flags >> 1) & 3) };

        public static EconomyStateBroadcast Pack(EconomyProfile p, EconomyEvent evt)
        {
            var msg = new EconomyStateBroadcast
            {
                Event = (byte)evt,
                Coins = p.Coins.Serialize(),
                Trophies = p.Trophies,
                Upgrades = p.Upgrades.ToArray(),
                ResearchPrey = new uint[p.Research.Count],
                ResearchCount = new int[p.Research.Count],
                BagPrey = new uint[p.Bag.Count],
                BagFlags = new byte[p.Bag.Count],
                Captures = p.Captures,
                Kills = p.Kills,
                LifetimeCoins = p.LifetimeCoins.Serialize(),
                EventCoins = string.Empty,
            };
            int i = 0;
            foreach (KeyValuePair<uint, int> kv in p.Research)
            {
                msg.ResearchPrey[i] = kv.Key;
                msg.ResearchCount[i] = kv.Value;
                i++;
            }
            for (int b = 0; b < p.Bag.Count; b++)
            {
                msg.BagPrey[b] = p.Bag[b].Prey;
                msg.BagFlags[b] = PackFlags(p.Bag[b]);
            }
            return msg;
        }

        public static void Unpack(in EconomyStateBroadcast msg, EconomyProfile p)
        {
            p.Clear();
            p.Coins = BigDouble.Deserialize(msg.Coins);
            p.Trophies = msg.Trophies;
            if (msg.Upgrades != null) p.Upgrades.AddRange(msg.Upgrades);
            if (msg.ResearchPrey != null && msg.ResearchCount != null)
                for (int i = 0; i < msg.ResearchPrey.Length && i < msg.ResearchCount.Length; i++) p.Research[msg.ResearchPrey[i]] = msg.ResearchCount[i];
            if (msg.BagPrey != null && msg.BagFlags != null)
                for (int i = 0; i < msg.BagPrey.Length && i < msg.BagFlags.Length; i++) p.Bag.Add(UnpackFlags(msg.BagPrey[i], msg.BagFlags[i]));
            p.Captures = msg.Captures;
            p.Kills = msg.Kills;
            p.LifetimeCoins = BigDouble.Deserialize(msg.LifetimeCoins);
        }

        /// <summary>Console/dev: the host's ledger for a client (null when not hosting).</summary>
        public static EconomyProfile LedgerOf(int clientId) => Ledger.TryGetValue(clientId, out EconomyProfile p) ? p : null;

        /// <summary>Host cheat: give coins to a client and send them the ledger.</summary>
        public static void HostGrant(int clientId, double coins)
        {
            if (!IsHost || !Ledger.TryGetValue(clientId, out EconomyProfile p)) return;
            NetworkConnection conn = Connection(clientId);
            if (conn == null) return;
            p.Coins += coins;
            Send(conn, p, EconomyEvent.Synced);
        }
    }
}
