using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Sim.Economy;

namespace HowToHunt.Economy
{
    /// <summary>Host → everyone: the world flags the party has unlocked (the east clearing...).</summary>
    public struct WorldFlagsBroadcast : IBroadcast
    {
        public string[] Flags;
    }

    /// <summary>Where a quest stands for a player.</summary>
    public enum QuestState : byte { Locked, Available, Active, Ready, Done }

    /// <summary>
    /// Quests (Phase 6). The host keeps each player's quest log in their economy profile (so it saves and syncs with
    /// coins), counts progress from what they do (catches, kills, sales, purchases), pays the reward when they turn it
    /// in, and sets world flags for the whole party (a quest that unlocks "east_clearing" opens it for everyone).
    /// </summary>
    public static partial class EconomyNet
    {
        private static readonly HashSet<string> Flags = new HashSet<string>();
        private static readonly List<QuestDefinition> QuestScratch = new List<QuestDefinition>();

        /// <summary>World flags this party has unlocked (every machine).</summary>
        public static bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && Flags.Contains(flag);

        /// <summary>A world flag appeared or went away (gates open, NPC lines change).</summary>
        public static event Action FlagsChanged;

        // --- client --------------------------------------------------------------------------------------------

        public static void RequestAcceptQuest(QuestDefinition q) => Request(new EconomyRequestBroadcast { Kind = (byte)EconomyRequestKind.AcceptQuest, Upgrade = q.Id.Value });

        public static void RequestTurnInQuest(QuestDefinition q) => Request(new EconomyRequestBroadcast { Kind = (byte)EconomyRequestKind.TurnInQuest, Upgrade = q.Id.Value });

        public static QuestState StateOf(EconomyProfile p, QuestDefinition q)
        {
            uint id = q.Id.Value;
            if (p.QuestsDone.Contains(id)) return QuestState.Done;
            if (p.Quests.TryGetValue(id, out int progress)) return progress >= q.Count ? QuestState.Ready : QuestState.Active;
            return q.Requires == null || p.QuestsDone.Contains(q.Requires.Id.Value) ? QuestState.Available : QuestState.Locked;
        }

        public static int ProgressOf(EconomyProfile p, QuestDefinition q) => p.Quests.TryGetValue(q.Id.Value, out int n) ? n : 0;

        /// <summary>What this character has for the player now: a quest to turn in, one in progress, or one to offer.</summary>
        public static QuestDefinition QuestFrom(NpcDefinition giver, EconomyProfile p, out QuestState state)
        {
            state = QuestState.Locked;
            QuestDefinition best = null;
            foreach (QuestDefinition q in AllQuests())
            {
                if (q.Giver != giver) continue;
                QuestState s = StateOf(p, q);
                if (s == QuestState.Done || s == QuestState.Locked) continue;
                if (best == null || Rank(s) > Rank(state)) { best = q; state = s; }
            }
            return best;
        }

        /// <summary>The player's quests in progress (for the HUD), in story order.</summary>
        public static List<QuestDefinition> ActiveQuests(EconomyProfile p)
        {
            QuestScratch.Clear();
            foreach (QuestDefinition q in AllQuests())
                if (p.Quests.ContainsKey(q.Id.Value)) QuestScratch.Add(q);
            return QuestScratch;
        }

        private static int Rank(QuestState s) => s == QuestState.Ready ? 3 : s == QuestState.Available ? 2 : s == QuestState.Active ? 1 : 0;

        private static List<QuestDefinition> _quests;

        private static List<QuestDefinition> AllQuests()
        {
            if (_quests != null) return _quests;
            _quests = new List<QuestDefinition>(ContentRegistry.All<QuestDefinition>());
            _quests.Sort((a, b) => a.Order.CompareTo(b.Order));
            return _quests;
        }

        private static void OnWorldFlags(WorldFlagsBroadcast msg, Channel channel)
        {
            Flags.Clear();
            if (msg.Flags != null) foreach (string f in msg.Flags) Flags.Add(f);
            FlagsChanged?.Invoke();
        }

        // --- host ----------------------------------------------------------------------------------------------

        private static void HostAcceptQuest(NetworkConnection conn, EconomyProfile p, uint id)
        {
            QuestDefinition q = ContentRegistry.Get<QuestDefinition>(new ContentId(id));
            if (q == null || StateOf(p, q) != QuestState.Available) { Refuse(conn, p, EconomyRefusal.Locked, id); return; }
            p.Quests[id] = 0;
            CountOwnedGear(p);
            Send(conn, p, EconomyEvent.QuestAccepted, content: id);
        }

        /// <summary>
        /// Host: purchase quests ("buy an upgrade from Wren") count the gear the player already owns. Otherwise a hunter
        /// who bought everything before accepting could never finish them, and the chain behind (the way east) stayed
        /// shut. Runs on accept and when a player joins (for quests accepted in an older build). True if anything moved.
        /// </summary>
        private static bool CountOwnedGear(EconomyProfile p)
        {
            if (p.Quests.Count == 0) return false;
            bool changed = false;
            foreach (QuestDefinition q in AllQuests())
            {
                int owned = q.Objective == QuestObjective.BuyUpgrade ? p.Upgrades.Count
                    : q.Objective == QuestObjective.BuyAttachment ? p.Attachments.Count : 0;
                if (owned <= 0 || !p.Quests.TryGetValue(q.Id.Value, out int progress)) continue;
                int caught = Math.Min(q.Count, owned);
                if (caught <= progress) continue;
                p.Quests[q.Id.Value] = caught;
                changed = true;
            }
            return changed;
        }

        private static void HostTurnInQuest(NetworkConnection conn, EconomyProfile p, uint id)
        {
            QuestDefinition q = ContentRegistry.Get<QuestDefinition>(new ContentId(id));
            if (q == null || StateOf(p, q) != QuestState.Ready) { Refuse(conn, p, EconomyRefusal.Locked, id); return; }
            p.Quests.Remove(id);
            p.QuestsDone.Add(id);
            BigDouble reward = q.RewardCoins;
            p.Coins += reward;
            p.LifetimeCoins += reward;
            Send(conn, p, EconomyEvent.QuestDone, content: id, coins: reward);
            if (!string.IsNullOrEmpty(q.Unlocks)) HostPushFlags();
        }

        /// <summary>Host: a player did something a quest might count. <paramref name="amount"/>: how many (or coins).</summary>
        private static void CountQuest(int clientId, EconomyProfile p, QuestObjective what, uint prey, bool captured, int amount)
        {
            if (p.Quests.Count == 0 || amount <= 0) return;
            bool changed = false;
            foreach (QuestDefinition q in AllQuests())
            {
                if (!p.Quests.TryGetValue(q.Id.Value, out int progress) || progress >= q.Count) continue;
                bool counts = what == QuestObjective.TakeDown ? q.Counts(prey, captured) : q.Objective == what;
                if (!counts) continue;
                p.Quests[q.Id.Value] = Math.Min(q.Count, progress + amount);
                changed = true;
            }
            if (!changed) return;
            NetworkConnection conn = Connection(clientId);
            if (conn != null) Send(conn, p, EconomyEvent.QuestProgress);
        }

        /// <summary>Host: the flags every player in the party has unlocked, to everyone (also on each join).</summary>
        private static void HostPushFlags()
        {
            if (!IsHost) return;
            Flags.Clear();
            foreach (EconomyProfile p in Ledger.Values)
                foreach (uint done in p.QuestsDone)
                {
                    QuestDefinition q = ContentRegistry.Get<QuestDefinition>(new ContentId(done));
                    if (q != null && !string.IsNullOrEmpty(q.Unlocks)) Flags.Add(q.Unlocks);
                }
            var msg = new WorldFlagsBroadcast { Flags = new List<string>(Flags).ToArray() };
            _manager.ServerManager.Broadcast(msg);
        }
    }
}
