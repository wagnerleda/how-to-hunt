using System;
using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Hunting;
using HowToHunt.Sim.Economy;
using HowToHunt.Sim.Hunting;
using UnityEngine;

namespace HowToHunt.Economy
{
    /// <summary>One unsold catch in the bag.</summary>
    public struct BagItem
    {
        public uint Prey;
        public bool Captured;
        public CarcassQuality Quality;
    }

    /// <summary>
    /// A player's economy: wallet, unsold catch, research per species (Bestiary), upgrades bought, lifetime stats.
    /// Personal and persistent (the profile), but during a party the host owns the live copy (the ledger) and sends
    /// it back after every change.
    /// </summary>
    public sealed class EconomyProfile
    {
        public BigDouble Coins = BigDouble.Zero;
        public double Trophies;
        public readonly Dictionary<uint, int> Research = new Dictionary<uint, int>();
        public readonly List<uint> Upgrades = new List<uint>();
        public readonly List<BagItem> Bag = new List<BagItem>();
        public int Captures;
        public int Kills;
        public BigDouble LifetimeCoins = BigDouble.Zero;

        public int ResearchOf(uint prey) => Research.TryGetValue(prey, out int r) ? r : 0;
        public bool Owns(uint upgrade) => Upgrades.Contains(upgrade);

        public void CopyFrom(EconomyProfile other)
        {
            Coins = other.Coins;
            Trophies = other.Trophies;
            Research.Clear();
            foreach (KeyValuePair<uint, int> kv in other.Research) Research[kv.Key] = kv.Value;
            Upgrades.Clear();
            Upgrades.AddRange(other.Upgrades);
            Bag.Clear();
            Bag.AddRange(other.Bag);
            Captures = other.Captures;
            Kills = other.Kills;
            LifetimeCoins = other.LifetimeCoins;
        }

        public void Clear() => CopyFrom(new EconomyProfile());

        /// <summary>What the whole bag sells for right now (mastery counts research at the moment of sale).</summary>
        public CatchReward BagValue(in EconomyRules rules, ModifierStack scratch)
        {
            var total = new CatchReward { Coins = BigDouble.Zero };
            for (int i = 0; i < Bag.Count; i++)
            {
                CatchReward r = ValueOf(Bag[i], rules, scratch);
                total.Coins += r.Coins;
                total.Trophies += r.Trophies;
            }
            return total;
        }

        public CatchReward ValueOf(in BagItem item, in EconomyRules rules, ModifierStack scratch)
        {
            PreyDefinition def = ContentRegistry.Get<PreyDefinition>(new Foundation.ContentId(item.Prey));
            if (def == null) return new CatchReward { Coins = BigDouble.Zero };
            return Rewards.Compute(def.Economy, rules, item.Captured, item.Quality, ResearchOf(item.Prey), scratch);
        }
    }

    /// <summary>
    /// The "economy" slice of the profile file. Content is saved by key (permanent) rather than by hash, so the file
    /// stays readable and survives renames of assets.
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
        public int Version => 1;

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
            foreach (BagItem b in _profile.Bag)
            {
                PreyDefinition p = ContentRegistry.Get<PreyDefinition>(new Foundation.ContentId(b.Prey));
                if (p == null) continue;
                d.bagPrey.Add(p.Key);
                d.bagFlags.Add(EconomyNet.PackFlags(b));
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
            for (int i = 0; i < d.bagPrey.Count && i < d.bagFlags.Count; i++)
            {
                PreyDefinition p = ContentRegistry.Get<PreyDefinition>(d.bagPrey[i]);
                if (p != null) _profile.Bag.Add(EconomyNet.UnpackFlags(p.Id.Value, d.bagFlags[i]));
            }
            _changed?.Invoke();
        }

        public void Reset()
        {
            _profile.Clear();
            _changed?.Invoke();
        }
    }
}
