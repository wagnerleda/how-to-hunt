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
        /// <summary>Caught alive: straight into the inventory.</summary>
        Caught,
        /// <summary>Killed: a carcass dropped where it died (go and pick it up).</summary>
        Killed,
        PickedUp,
        Sold,
        Bought,
        Refused,
        /// <summary>No room: the catch fell at the player's feet.</summary>
        InventoryFull,
        /// <summary>The outfitter handed back lost tools.</summary>
        ToolsReturned,
    }

    public enum EconomyRefusal : byte { None, NotAtStation, NothingToSell, TooPoor, AlreadyOwned, Locked, Unknown, TooFar, InventoryFull }

    /// <summary>A whole economy profile on the wire, plus what just happened. Small (tens of bytes plus the slots).</summary>
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
        /// <summary>Every slot in order (exact layout, so slot numbers match on both sides).</summary>
        public byte[] SlotKind;
        public uint[] SlotContent;
        public byte[] SlotFlags;
        public ushort[] SlotCount;
        public int Captures;
        public int Kills;
        public string LifetimeCoins;
    }

    public enum EconomyRequestKind : byte { Sell, Buy, PickUp, Throw, Drop, ReclaimTools }

    /// <summary>Client → host: sell, buy, pick an item up, throw or drop the held prey.</summary>
    public struct EconomyRequestBroadcast : IBroadcast
    {
        public byte Kind;
        public uint Upgrade;
        public ushort Item;
        public byte Slot;
        public Vector3 Origin;
        public Vector3 Direction;
    }

    /// <summary>
    /// Host-authoritative economy and inventory. Each player's profile is theirs and persists on their machine, but
    /// during a party the host keeps the live copy (ledger): the client sends its profile once on joining; the host
    /// puts catches in the inventory, drops carcasses for kills, handles pick-ups, throws, drops, sales and purchases,
    /// and returns the whole ledger after every change, which the client saves. Clients never change their own coins
    /// or items. (Trusting the joining profile is a v1 choice for co-op with friends: docs/adr/0011.)
    /// </summary>
    public static class EconomyNet
    {
        public static readonly FloatCvar StationReach = new FloatCvar("sv_station_reach", 3.5f, "Extra distance (m) beyond a camp station's radius at which the host still accepts its use.", CvarFlags.Replicated | CvarFlags.Cheat, 0f, 50f);
        public static readonly FloatCvar ThrowSpeed = new FloatCvar("sv_throw_speed", 13f, "How fast a thrown prey leaves your hand (m/s).", CvarFlags.Replicated | CvarFlags.Cheat, 1f, 60f);

        /// <summary>The local player's profile (persistent, mirrored from the host during a party).</summary>
        public static readonly EconomyProfile Local = new EconomyProfile();

        /// <summary>Raised on this machine after the local profile changed (HUD, shop, inventory bar).</summary>
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
            WorldItems.Attach(m);
            // Using a tool requires carrying it (tools can be dropped, lost or picked up by someone else).
            PlayerTools.HostHasTool = (clientId, tool) =>
                HuntingCvars.AllTools.Value || (Ledger.TryGetValue(clientId, out EconomyProfile l) && l.Inventory.HasTool(tool));
        }

        public static void Detach()
        {
            _manager = null;
            _joined = false;
            Ledger.Clear();
            PlayerTools.HostHasTool = null;
            WorldItems.Detach();
        }

        // --- client -----------------------------------------------------------------------------------------

        /// <summary>Our player spawned: hand our profile to the host.</summary>
        public static void SendJoin()
        {
            if (_manager == null || !_manager.IsClientStarted || _joined) return;
            _joined = true;
            _manager.ClientManager.Broadcast(Pack(Local, EconomyEvent.Join));
        }

        private static void Request(EconomyRequestBroadcast req)
        {
            if (_manager != null && _manager.IsClientStarted) _manager.ClientManager.Broadcast(req);
        }

        public static void RequestSell() => Request(new EconomyRequestBroadcast { Kind = (byte)EconomyRequestKind.Sell });

        public static void RequestBuy(UpgradeDefinition upgrade)
        {
            if (upgrade != null) Request(new EconomyRequestBroadcast { Kind = (byte)EconomyRequestKind.Buy, Upgrade = upgrade.Id.Value });
        }

        public static void RequestReclaimTools() => Request(new EconomyRequestBroadcast { Kind = (byte)EconomyRequestKind.ReclaimTools });

        public static void RequestPickUp(ushort item) => Request(new EconomyRequestBroadcast { Kind = (byte)EconomyRequestKind.PickUp, Item = item });

        public static void RequestThrow(int slot, Vector3 origin, Vector3 direction)
            => Request(new EconomyRequestBroadcast { Kind = (byte)EconomyRequestKind.Throw, Slot = (byte)slot, Origin = origin, Direction = direction });

        public static void RequestDrop(int slot, Vector3 origin, Vector3 direction)
            => Request(new EconomyRequestBroadcast { Kind = (byte)EconomyRequestKind.Drop, Slot = (byte)slot, Origin = origin, Direction = direction });

        private static void OnStateFromHost(EconomyStateBroadcast msg, Channel channel)
        {
            Unpack(msg, Local, exact: true);
            Loadouts.Local.SetUpgrades(Local.Upgrades);
            ProfileStore.MarkDirty();
            LocalChanged?.Invoke(msg);
        }

        /// <summary>The profile was loaded from disk (or reset): refresh what depends on it.</summary>
        public static void OnLocalLoaded()
        {
            Loadouts.Local.SetUpgrades(Local.Upgrades);
            Local.ApplyCapacity();
            LocalChanged?.Invoke(new EconomyStateBroadcast { Event = (byte)EconomyEvent.Synced });
        }

        // --- host -------------------------------------------------------------------------------------------

        private static void OnJoinFromClient(NetworkConnection conn, EconomyStateBroadcast msg, Channel channel)
        {
            if (msg.Event != (byte)EconomyEvent.Join || Ledger.ContainsKey(conn.ClientId)) return;
            var p = new EconomyProfile();
            Unpack(msg, p, exact: false); // re-lays the slots with this build's tools and capacity
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

        /// <summary>
        /// Host: a player caught or killed something (published by the hunting feature). Catches go straight into the
        /// inventory; kills leave a carcass where the prey died, flung by the hit.
        /// </summary>
        public static void OnPreyTakenDown(in PreyTakenDown e)
        {
            if (!IsHost || !Ledger.TryGetValue(e.ClientId, out EconomyProfile p)) return;
            NetworkConnection conn = Connection(e.ClientId);
            if (conn == null) return;
            var item = ItemStack.Prey(e.Prey.Value, e.Captured, e.Quality);
            CatchReward value = p.ValueOf(item, EconomyRules.Default, Scratch);
            if (!e.Captured)
            {
                p.Kills++;
                WorldItems.HostSpawn(item, e.Position, e.Fling);
                Send(conn, p, EconomyEvent.Killed, content: e.Prey.Value, flags: item.Flags, coins: value.Coins);
                return;
            }
            p.Captures++;
            p.Research[e.Prey.Value] = p.ResearchOf(e.Prey.Value) + 1;
            if (p.Inventory.Add(item, EconomyProfile.StackSize(item.Content)) > 0)
            {
                DropAtFeet(e.ClientId, item);
                Send(conn, p, EconomyEvent.InventoryFull, content: e.Prey.Value, flags: item.Flags);
                return;
            }
            Send(conn, p, EconomyEvent.Caught, content: e.Prey.Value, flags: item.Flags, coins: value.Coins);
        }

        private static void OnRequest(NetworkConnection conn, EconomyRequestBroadcast req, Channel channel)
        {
            if (!Ledger.TryGetValue(conn.ClientId, out EconomyProfile p)) return;
            switch ((EconomyRequestKind)req.Kind)
            {
                case EconomyRequestKind.Sell: Sell(conn, p); break;
                case EconomyRequestKind.Buy: Buy(conn, p, req.Upgrade); break;
                case EconomyRequestKind.PickUp: PickUp(conn, p, req.Item); break;
                case EconomyRequestKind.Throw: Release(conn, p, req, thrown: true); break;
                case EconomyRequestKind.Drop: Release(conn, p, req, thrown: false); break;
                case EconomyRequestKind.ReclaimTools: Reclaim(conn, p); break;
            }
        }

        private static void Sell(NetworkConnection conn, EconomyProfile p)
        {
            if (!NearStation(conn, CampStationKind.Sell)) { Refuse(conn, p, EconomyRefusal.NotAtStation); return; }
            CatchReward total = p.InventoryValue(EconomyRules.Default, Scratch);
            int count = p.Inventory.PreyCount;
            if (count == 0) { Refuse(conn, p, EconomyRefusal.NothingToSell); return; }
            p.Inventory.TakeAllPrey();
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
            foreach (ItemStack overflow in p.ApplyCapacity()) DropAtFeet(conn.ClientId, overflow);
            if (u.Effect == UpgradeEffect.UnlockTool && u.Tool != null && p.Inventory.Add(ItemStack.Tool(u.Tool.Id.Value), 1) > 0)
                DropAtFeet(conn.ClientId, ItemStack.Tool(u.Tool.Id.Value));
            Send(conn, p, EconomyEvent.Bought, content: upgradeId, coins: u.CostCoins);
        }

        /// <summary>Host: the outfitter replaces owned tools the player no longer carries (lost, dropped, taken).</summary>
        private static void Reclaim(NetworkConnection conn, EconomyProfile p)
        {
            if (!NearStation(conn, CampStationKind.Shop)) { Refuse(conn, p, EconomyRefusal.NotAtStation); return; }
            int missing = p.MissingTools().Count;
            if (missing == 0) { Refuse(conn, p, EconomyRefusal.AlreadyOwned); return; }
            foreach (ItemStack overflow in p.GiveMissingTools()) DropAtFeet(conn.ClientId, overflow);
            Send(conn, p, EconomyEvent.ToolsReturned, count: missing);
        }

        private static void PickUp(NetworkConnection conn, EconomyProfile p, ushort itemId)
        {
            WorldItem w = WorldItems.Get(itemId);
            PlayerMotor motor = MotorOf(conn.ClientId);
            if (w == null || motor == null) return; // someone else was faster: nothing to say
            Vector3 eye = Eye(motor);
            if ((w.Body.Position.ToUnity() - eye).magnitude > WorldItems.PickupReach.Value + 0.8f) { Refuse(conn, p, EconomyRefusal.TooFar); return; }
            int stack = EconomyProfile.StackSize(w.Item.Content);
            if (!p.Inventory.CanAdd(w.Item, stack)) { Refuse(conn, p, EconomyRefusal.InventoryFull, w.Item.Content); return; }
            ItemStack item = w.Item;
            int left = p.Inventory.Add(item, stack);
            Vector3 at = w.Body.Position.ToUnity();
            WorldItems.HostRemove(w);
            if (left > 0)
            {
                item.Count = (ushort)left;
                WorldItems.HostSpawn(item, at, Vector3.zero);
            }
            Send(conn, p, EconomyEvent.PickedUp, content: item.Content, flags: item.Flags);
        }

        private static void Release(NetworkConnection conn, EconomyProfile p, EconomyRequestBroadcast req, bool thrown)
        {
            PlayerMotor motor = MotorOf(conn.ClientId);
            if (motor == null || !p.Inventory.TakeOne(req.Slot, out ItemStack item)) return;
            Vector3 eye = Eye(motor);
            Vector3 dir = req.Direction.sqrMagnitude > 0.5f && !float.IsNaN(req.Direction.x) ? req.Direction.normalized : motor.transform.forward;
            Vector3 origin = (req.Origin - eye).sqrMagnitude <= 2.5f * 2.5f ? req.Origin : eye;
            Vector3 playerVelocity = motor.State.Velocity.ToUnity();
            Vector3 velocity = thrown
                ? dir * ThrowSpeed.Value + Vector3.up * 1.5f + playerVelocity * 0.6f
                : dir * 1.5f + playerVelocity * 0.5f;
            Vector3 start = origin + dir * 0.45f + (thrown ? Vector3.zero : Vector3.down * 0.5f);
            // Don't start inside a wall: pull the start back toward the eye if the path is blocked.
            var tr = SimWorld.Current.TraceBox(eye.ToSim(), start.ToSim(), new System.Numerics.Vector3(0.1f, 0.1f, 0.1f));
            if (tr.Hit) start = tr.EndPosition.ToUnity();
            WorldItems.HostSpawn(item, start, velocity);
            Send(conn, p, EconomyEvent.Synced);
        }

        private static void DropAtFeet(int clientId, in ItemStack item)
        {
            PlayerMotor motor = MotorOf(clientId);
            if (motor != null) WorldItems.HostSpawn(item, motor.transform.position + motor.transform.forward * 0.6f, Vector3.up * 1.5f);
        }

        private static Vector3 Eye(PlayerMotor motor)
        {
            var s = motor.State;
            return s.Position.ToUnity() + new Vector3(0f, MovementCvars.Config.EyeOffset(s.Ducked), 0f);
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
            return CampStation.Nearest(motor.transform.position, kind, StationReach.Value) != null;
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

        public static EconomyStateBroadcast Pack(EconomyProfile p, EconomyEvent evt)
        {
            int slots = p.Inventory.Capacity;
            var msg = new EconomyStateBroadcast
            {
                Event = (byte)evt,
                Coins = p.Coins.Serialize(),
                Trophies = p.Trophies,
                Upgrades = p.Upgrades.ToArray(),
                ResearchPrey = new uint[p.Research.Count],
                ResearchCount = new int[p.Research.Count],
                SlotKind = new byte[slots],
                SlotContent = new uint[slots],
                SlotFlags = new byte[slots],
                SlotCount = new ushort[slots],
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
            for (int s = 0; s < slots; s++)
            {
                ItemStack item = p.Inventory[s];
                msg.SlotKind[s] = (byte)item.Kind;
                msg.SlotContent[s] = item.Content;
                msg.SlotFlags[s] = item.Flags;
                msg.SlotCount[s] = item.Count;
            }
            return msg;
        }

        /// <summary>
        /// exact: copy the slot layout as sent (host → client, so slot numbers match). Otherwise re-lay tools for this
        /// build and add the prey stacks in order (client → host on join).
        /// </summary>
        public static void Unpack(in EconomyStateBroadcast msg, EconomyProfile p, bool exact)
        {
            p.Clear();
            p.Coins = BigDouble.Deserialize(msg.Coins);
            p.Trophies = msg.Trophies;
            if (msg.Upgrades != null)
                foreach (uint u in msg.Upgrades)
                    if (exact || ContentRegistry.Get<UpgradeDefinition>(new ContentId(u)) != null) p.Upgrades.Add(u);
            if (msg.ResearchPrey != null && msg.ResearchCount != null)
                for (int i = 0; i < msg.ResearchPrey.Length && i < msg.ResearchCount.Length; i++) p.Research[msg.ResearchPrey[i]] = msg.ResearchCount[i];
            p.Captures = msg.Captures;
            p.Kills = msg.Kills;
            p.LifetimeCoins = BigDouble.Deserialize(msg.LifetimeCoins);

            int n = msg.SlotKind != null ? msg.SlotKind.Length : 0;
            if (msg.SlotContent == null || msg.SlotFlags == null || msg.SlotCount == null) n = 0;
            if (exact) p.Inventory.SetCapacity(Math.Max(1, n));
            else p.ApplyCapacity();
            for (int s = 0; s < n; s++)
            {
                var kind = (ItemKind)msg.SlotKind[s];
                if (kind == ItemKind.Empty || msg.SlotCount[s] == 0) continue;
                var item = new ItemStack { Kind = kind, Content = msg.SlotContent[s], Flags = msg.SlotFlags[s], Count = msg.SlotCount[s] };
                if (exact) { p.Inventory.SetSlot(s, item); continue; }
                bool known = kind == ItemKind.Tool
                    ? ContentRegistry.Get<ToolDefinition>(new ContentId(item.Content)) != null
                    : ContentRegistry.Get<PreyDefinition>(new ContentId(item.Content)) != null;
                if (known) p.Inventory.Add(item, EconomyProfile.StackSize(item.Content));
            }
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
