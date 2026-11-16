using System;
using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Hunting;
using HowToHunt.Sim.Economy;
using HowToHunt.Sim.Hunting;
using UnityEngine;

namespace HowToHunt.Economy
{
    /// <summary>
    /// A player's economy: wallet, inventory (tools + prey stacks), research per species (Bestiary), upgrades bought,
    /// lifetime stats. Personal and persistent (the profile), but during a party the host owns the live copy (the
    /// ledger) and sends it back after every change.
    /// </summary>
    public sealed class EconomyProfile
    {
        public const int BaseSlots = 3; // Phase 3.5: three to start (net, knife, one catch); more slots come later in the game

        public BigDouble Coins = BigDouble.Zero;
        public double Trophies;
        public readonly Dictionary<uint, int> Research = new Dictionary<uint, int>();
        public readonly List<uint> Upgrades = new List<uint>();
        public readonly Inventory Inventory = new Inventory();
        public int Captures;
        public int Kills;
        public BigDouble LifetimeCoins = BigDouble.Zero;

        public int ResearchOf(uint prey) => Research.TryGetValue(prey, out int r) ? r : 0;
        public bool Owns(uint upgrade) => Upgrades.Contains(upgrade);

        /// <summary>Slots from the base plus every InventorySlots upgrade owned.</summary>
        public int SlotCapacity
        {
            get
            {
                int slots = BaseSlots;
                foreach (UpgradeDefinition u in Loadouts.AllUpgrades)
                    if (u.Effect == UpgradeEffect.InventorySlots && Owns(u.Id.Value)) slots += Mathf.RoundToInt(u.Value);
                return Mathf.Min(slots, Inventory.MaxSlots);
            }
        }

        /// <summary>Owned tools, in hotbar order.</summary>
        public List<uint> OwnedTools()
        {
            var loadout = new Loadout();
            loadout.SetUpgrades(Upgrades);
            var list = new List<uint>();
            foreach (ToolDefinition t in Loadouts.AllTools)
                if (loadout.Owns(t)) list.Add(t.Id.Value);
            return list;
        }

        /// <summary>Applies the slot count from upgrades; returns items that no longer fit.</summary>
        public List<ItemStack> ApplyCapacity() => Inventory.SetCapacity(SlotCapacity);

        /// <summary>Owned tools (start tools, unlocked ones) that aren't in the inventory: lost, dropped, never given.</summary>
        public List<uint> MissingTools()
        {
            var missing = new List<uint>();
            foreach (uint t in OwnedTools()) if (!Inventory.HasTool(t)) missing.Add(t);
            return missing;
        }

        /// <summary>Puts missing owned tools in the inventory; returns those that didn't fit.</summary>
        public List<ItemStack> GiveMissingTools()
        {
            var overflow = new List<ItemStack>();
            foreach (uint t in MissingTools())
            {
                ItemStack tool = ItemStack.Tool(t);
                if (Inventory.Add(tool, 1) > 0) overflow.Add(tool);
            }
            return overflow;
        }

        public void CopyFrom(EconomyProfile other)
        {
            Coins = other.Coins;
            Trophies = other.Trophies;
            Research.Clear();
            foreach (KeyValuePair<uint, int> kv in other.Research) Research[kv.Key] = kv.Value;
            Upgrades.Clear();
            Upgrades.AddRange(other.Upgrades);
            Inventory.CopyFrom(other.Inventory);
            Captures = other.Captures;
            Kills = other.Kills;
            LifetimeCoins = other.LifetimeCoins;
        }

        public void Clear()
        {
            Coins = BigDouble.Zero;
            Trophies = 0;
            Research.Clear();
            Upgrades.Clear();
            Inventory.Clear();
            Captures = 0;
            Kills = 0;
            LifetimeCoins = BigDouble.Zero;
        }

        /// <summary>What all prey in the inventory sell for right now (mastery counts research at the moment of sale).</summary>
        public CatchReward InventoryValue(in EconomyRules rules, ModifierStack scratch)
        {
            var total = new CatchReward { Coins = BigDouble.Zero };
            for (int i = 0; i < Inventory.MaxSlots; i++)
            {
                ItemStack s = Inventory[i];
                if (s.Kind != ItemKind.Prey || s.Count == 0) continue;
                CatchReward r = ValueOf(s, rules, scratch);
                total.Coins += r.Coins * s.Count;
                total.Trophies += r.Trophies * s.Count;
            }
            return total;
        }

        /// <summary>The value of one item of this stack.</summary>
        public CatchReward ValueOf(in ItemStack item, in EconomyRules rules, ModifierStack scratch)
        {
            PreyDefinition def = ContentRegistry.Get<PreyDefinition>(new Foundation.ContentId(item.Content));
            if (def == null) return new CatchReward { Coins = BigDouble.Zero };
            return Rewards.Compute(def.Economy, rules, item.Captured, item.Quality, ResearchOf(item.Content), scratch);
        }

        public static int StackSize(uint prey)
        {
            PreyDefinition def = ContentRegistry.Get<PreyDefinition>(new Foundation.ContentId(prey));
            return def != null ? Mathf.Max(1, def.StackSize) : 1;
        }
    }

    /// <summary>
    /// The "economy" slice of the profile file. Content is saved by key (permanent) rather than by hash.
    /// v1 had an unordered "bag"; v2 stored prey stacks only; v3 stores every slot (tools are items you can drop).
    /// </summary>
    public sealed class EconomySaveSlice : ISaveSlice
    {
        [Serializable]
        private sealed class Data
        {
            public string coins = "0;0";
            public double trophies;
            public List<string> researchKeys = new List<string>();
            public List<int> researchCounts = new List<int>();
            public List<string> upgrades = new List<string>();
            // v3: every slot (kind 1 = tool, 2 = prey).
            public List<int> slotIndex = new List<int>();
            public List<byte> slotKind = new List<byte>();
            public List<string> slotKey = new List<string>();
            public List<byte> slotFlags = new List<byte>();
            public List<int> slotCounts = new List<int>();
            // v2 (read only): prey stacks.
            public List<string> stackPrey = new List<string>();
            public List<byte> stackFlags = new List<byte>();
            public List<int> stackCounts = new List<int>();
            // v1 (read only): the bag.
            public List<string> bagPrey = new List<string>();
            public List<byte> bagFlags = new List<byte>();
            public int captures;
            public int kills;
            public string lifetimeCoins = "0;0";
        }

        private readonly EconomyProfile _profile;
        private readonly Action _changed;

        public EconomySaveSlice(EconomyProfile profile, Action changed)
        {
            _profile = profile;
            _changed = changed;
        }

        public string Key => "economy";
        public int Version => 3;

        public string Save()
        {
            var d = new Data
            {
                coins = _profile.Coins.Serialize(),
                trophies = _profile.Trophies,
                captures = _profile.Captures,
                kills = _profile.Kills,
                lifetimeCoins = _profile.LifetimeCoins.Serialize(),
            };
            foreach (KeyValuePair<uint, int> kv in _profile.Research)
            {
                PreyDefinition p = ContentRegistry.Get<PreyDefinition>(new Foundation.ContentId(kv.Key));
                if (p == null) continue;
                d.researchKeys.Add(p.Key);
                d.researchCounts.Add(kv.Value);
            }
            foreach (uint u in _profile.Upgrades)
            {
                UpgradeDefinition def = ContentRegistry.Get<UpgradeDefinition>(new Foundation.ContentId(u));
                if (def != null) d.upgrades.Add(def.Key);
            }
            for (int i = 0; i < Inventory.MaxSlots; i++)
            {
                ItemStack s = _profile.Inventory[i];
                if (s.IsEmpty) continue;
                ContentDefinition def = s.Kind == ItemKind.Tool
                    ? ContentRegistry.Get<ToolDefinition>(new Foundation.ContentId(s.Content))
                    : ContentRegistry.Get<PreyDefinition>(new Foundation.ContentId(s.Content));
                if (def == null) continue;
                d.slotIndex.Add(i);
                d.slotKind.Add((byte)s.Kind);
                d.slotKey.Add(def.Key);
                d.slotFlags.Add(s.Flags);
                d.slotCounts.Add(s.Count);
            }
            return JsonUtility.ToJson(d);
        }

        public void Load(string data, int savedVersion)
        {
            Data d = JsonUtility.FromJson<Data>(data) ?? new Data();
            _profile.Clear();
            _profile.Coins = BigDouble.Deserialize(d.coins);
            _profile.Trophies = d.trophies;
            _profile.Captures = d.captures;
            _profile.Kills = d.kills;
            _profile.LifetimeCoins = BigDouble.Deserialize(d.lifetimeCoins);
            for (int i = 0; i < d.researchKeys.Count && i < d.researchCounts.Count; i++)
            {
                PreyDefinition p = ContentRegistry.Get<PreyDefinition>(d.researchKeys[i]);
                if (p != null) _profile.Research[p.Id.Value] = d.researchCounts[i];
            }
            foreach (string key in d.upgrades)
            {
                UpgradeDefinition u = ContentRegistry.Get<UpgradeDefinition>(key);
                if (u != null && !_profile.Upgrades.Contains(u.Id.Value)) _profile.Upgrades.Add(u.Id.Value);
            }
            _profile.ApplyCapacity();
            if (savedVersion >= 3)
            {
                int n = Math.Min(Math.Min(d.slotIndex.Count, d.slotKind.Count), Math.Min(d.slotKey.Count, Math.Min(d.slotFlags.Count, d.slotCounts.Count)));
                for (int i = 0; i < n; i++)
                {
                    var kind = (ItemKind)d.slotKind[i];
                    ContentDefinition def = kind == ItemKind.Tool
                        ? ContentRegistry.Get<ToolDefinition>(d.slotKey[i])
                        : ContentRegistry.Get<PreyDefinition>(d.slotKey[i]);
                    if (def == null || d.slotCounts[i] <= 0) continue;
                    var item = new ItemStack { Kind = kind, Content = def.Id.Value, Flags = d.slotFlags[i], Count = (ushort)d.slotCounts[i] };
                    int slot = d.slotIndex[i];
                    if (slot >= 0 && slot < _profile.Inventory.Capacity && _profile.Inventory[slot].IsEmpty) _profile.Inventory.SetSlot(slot, item);
                    else _profile.Inventory.Add(item, EconomyProfile.StackSize(item.Content));
                }
                _changed?.Invoke();
                return;
            }

            // Older saves had no tools in them: hand out the owned ones first.
            _profile.GiveMissingTools();
            if (savedVersion < 2)
            {
                // v1 bag -> stacks. Whatever doesn't fit in the starting slots is dropped from the save.
                for (int i = 0; i < d.bagPrey.Count && i < d.bagFlags.Count; i++)
                {
                    PreyDefinition p = ContentRegistry.Get<PreyDefinition>(d.bagPrey[i]);
                    if (p == null) continue;
                    var item = new ItemStack { Kind = ItemKind.Prey, Content = p.Id.Value, Flags = d.bagFlags[i], Count = 1 };
                    _profile.Inventory.Add(item, EconomyProfile.StackSize(item.Content));
                }
            }
            else
            {
                for (int i = 0; i < d.stackPrey.Count && i < d.stackFlags.Count && i < d.stackCounts.Count; i++)
                {
                    PreyDefinition p = ContentRegistry.Get<PreyDefinition>(d.stackPrey[i]);
                    if (p == null || d.stackCounts[i] <= 0) continue;
                    var item = new ItemStack { Kind = ItemKind.Prey, Content = p.Id.Value, Flags = d.stackFlags[i], Count = (ushort)d.stackCounts[i] };
                    _profile.Inventory.Add(item, EconomyProfile.StackSize(item.Content));
                }
            }
            _changed?.Invoke();
        }

        public void Reset()
        {
            _profile.Clear();
            _profile.ApplyCapacity();
            _profile.GiveMissingTools(); // a new hunter starts with the net and the knife
            _changed?.Invoke();
        }
    }
}
