using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Sim.Hunting;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// What each player owns (tools and upgrades) and the resulting tool numbers. The host keeps one per connection
    /// (fed by the economy module from the player's profile); the local player keeps its own for the hotbar.
    /// </summary>
    public sealed class Loadout
    {
        private readonly HashSet<uint> _upgrades = new HashSet<uint>();

        public IReadOnlyCollection<uint> Upgrades => _upgrades;

        public void SetUpgrades(IEnumerable<uint> upgradeIds)
        {
            _upgrades.Clear();
            foreach (uint id in upgradeIds) _upgrades.Add(id);
        }

        public void AddUpgrade(uint id) => _upgrades.Add(id);

        public bool HasUpgrade(UpgradeDefinition u) => u != null && _upgrades.Contains(u.Id.Value);

        public bool Owns(ToolDefinition tool)
        {
            if (tool == null) return false;
            if (tool.StartOwned) return true;
            foreach (UpgradeDefinition u in Loadouts.AllUpgrades)
                if (u.Tool == tool && u.Effect == UpgradeEffect.UnlockTool && HasUpgrade(u)) return true;
            return false;
        }

        /// <summary>Tool numbers after this player's upgrades.</summary>
        public ToolStats StatsFor(ToolDefinition tool, out float range)
        {
            var s = new ToolStats
            {
                Archetype = tool.Archetype,
                Mode = tool.Mode,
                Tier = tool.Tier,
                Damage = tool.Damage,
                CaptureStrength = tool.CaptureStrength,
                HeadshotMultiplier = tool.HeadshotMultiplier,
            };
            range = tool.Range;
            foreach (UpgradeDefinition u in Loadouts.AllUpgrades)
            {
                if (u.Tool != tool || !HasUpgrade(u)) continue;
                switch (u.Effect)
                {
                    case UpgradeEffect.CaptureStrength: s.CaptureStrength += u.Value; break;
                    case UpgradeEffect.DamageMultiplier: s.Damage *= u.Value; break;
                    case UpgradeEffect.Range: range += u.Value; break;
                }
            }
            return s;
        }
    }

    public static class Loadouts
    {
        private static readonly Dictionary<int, Loadout> ByClient = new Dictionary<int, Loadout>();
        private static List<UpgradeDefinition> _upgrades;
        private static List<ToolDefinition> _tools;

        /// <summary>The local player's loadout (from their profile).</summary>
        public static readonly Loadout Local = new Loadout();

        public static IReadOnlyList<UpgradeDefinition> AllUpgrades => _upgrades ??= Sorted(ContentRegistry.All<UpgradeDefinition>());

        public static IReadOnlyList<ToolDefinition> AllTools
        {
            get
            {
                if (_tools == null)
                {
                    _tools = ContentRegistry.All<ToolDefinition>();
                    _tools.Sort((a, b) => a.Slot.CompareTo(b.Slot));
                }
                return _tools;
            }
        }

        /// <summary>Host: a connection's loadout (created empty on first use).</summary>
        public static Loadout ForClient(int clientId)
        {
            if (!ByClient.TryGetValue(clientId, out Loadout l)) ByClient[clientId] = l = new Loadout();
            return l;
        }

        public static void Remove(int clientId) => ByClient.Remove(clientId);

        public static ToolDefinition ToolInSlot(int slot)
        {
            foreach (ToolDefinition t in AllTools) if (t.Slot == slot) return t;
            return null;
        }

        internal static void Reset()
        {
            ByClient.Clear();
            _upgrades = null;
            _tools = null;
            Local.SetUpgrades(System.Array.Empty<uint>());
        }

        private static List<UpgradeDefinition> Sorted(List<UpgradeDefinition> list)
        {
            list.Sort((a, b) => a.SortOrder != b.SortOrder ? a.SortOrder.CompareTo(b.SortOrder) : string.CompareOrdinal(a.Key, b.Key));
            return list;
        }
    }

    /// <summary>Published on the host when a player catches or kills prey (economy listens).</summary>
    public struct PreyTakenDown
    {
        public int ClientId;
        public ContentId Prey;
        public bool Captured;
        public CarcassQuality Quality;
    }
}
