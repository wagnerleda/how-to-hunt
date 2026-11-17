using System.Collections.Generic;
using HowToHunt.Economy;
using HowToHunt.Hunting;
using UnityEditor;

namespace HowToHunt.Editor
{
    /// <summary>Phase 6: the camp characters and the first quest chain (it ends by opening the way east).</summary>
    public static partial class GeneratedContent
    {
        public const string EastClearingFlag = "east_clearing";

        private static void BuildNpcsAndQuests()
        {
            GeneratedAssets.EnsureFolder(ContentDir + "/Npcs");
            GeneratedAssets.EnsureFolder(ContentDir + "/Quests");

            NpcDefinition tomas = Npc("trader", "Tomas", "Trader", "Buys whatever you bring back, alive or not.", CampStationKind.Sell,
                "Ha, a fresh face. Got something for me?",
                "Back again? Show me what you've caught.",
                "Rabbits keep the lights on, friend.");
            tomas.Idle = "Quiet day. The rabbits by the river logs have been fat this year.";
            NpcDefinition wren = Npc("outfitter", "Wren", "Outfitter", "Sells nets, knives and the gear to carry more.", CampStationKind.Shop,
                "You look under-equipped. Let's fix that.",
                "Need a sturdier net? A bigger bag?",
                "Everything on the rack is tested on actual rabbits.");
            wren.Idle = "If it tears, bring it back. It won't tear.";
            NpcDefinition gus = Npc("gunsmith", "Gus", "Gunsmith", "Sells guns and attachments, fits them while you wait.", CampStationKind.Workshop,
                "Mind the bench. What are we shooting today?",
                "A good gun's a quiet gun. Mostly.",
                "Bring it here, I'll make it kick less. Or more, if that's your thing.");
            gus.Idle = "Brakes, cans, stocks... everything's for sale.";
            NpcDefinition ash = Npc("forest_trader", "Ash", "Forest Trader", "Runs the trading post in the pine forest. Pays well for deer.", CampStationKind.Sell,
                "You came down the slopes? Then you can hunt here.",
                "The pines are generous to the patient.",
                "Deer hear everything. Walk softly.");
            ash.Idle = "Forest catch sells for more here. Nobody wants to carry it back up.";

            PreyDefinition deer = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_Deer.asset");

            QuestDefinition freshStock = Quest("fresh_stock", "Fresh Stock", 1, tomas, null, QuestObjective.Sell, null, 3, 60,
                "Sell 3 catches to Tomas",
                "New out here? Then you'll want coin. Bring me three catches, caught or killed, and I'll pay a bonus on top.",
                "Still waiting on those catches. The rabbit warrens are by the logs.",
                "That's the spirit. Here's your bonus. Go see Wren at the outfitter, she'll want to see you kitted out.");
            QuestDefinition kittedOut = Quest("kitted_out", "Kitted Out", 2, wren, freshStock, QuestObjective.BuyUpgrade, null, 1, 80,
                "Buy an upgrade from Wren",
                "Tomas says you can hunt. Let's see you spend some of that coin: buy anything off my rack.",
                "Have a look at the rack. There's something for every budget.",
                "Suits you. Gus at the gunsmith was asking about you, too.");
            QuestDefinition steadyHands = Quest("steady_hands", "Steady Hands", 3, gus, kittedOut, QuestObjective.Kill, null, 5, 120,
                "Take down 5 prey (killed, not caught)",
                "A net's fine for rabbits. A hunter brings things down. Five of them, any way you like, but no catching alive.",
                "Five. Down. Off you go.",
                "Not bad at all. Tomas has been going on about the fallen trees to the east. Go and see him.");
            QuestDefinition beyond = Quest("beyond_the_trees", "Beyond the Trees", 4, tomas, steadyHands, QuestObjective.EarnCoins, null, 300, 150,
                "Earn 300 coins selling your catch",
                "Past the east clearing there's a pine forest, better hunting than here. Fallen trees block the way. Earn me 300 coins in trade and I'll get them cleared. Mind you, past the trees the land drops away in steep slopes: you'll have to surf down.",
                "Keep it coming. The forest won't wait forever.",
                "Done. The way east is clear. Ride the slopes down to the pines, and don't fall: it's a long climb back. There's a way home from the forest camp.");
            beyond.Unlocks = EastClearingFlag;
            EditorUtility.SetDirty(beyond);
            Quest("ghost_of_the_pines", "Ghost of the Pines", 5, ash, beyond, QuestObjective.TakeDown, deer, 1, 300,
                deer != null ? "Take down a deer" : "Take down anything in the forest",
                "Deer here are wary: they hear you long before you see them. Bring one down, caught or killed, and I'll know you're the real thing.",
                "No deer yet? Move slowly, stay downwind... or don't, and chase it.",
                "A real deer. You'll do well out here. That's the best I've paid all season.");
        }

        private static NpcDefinition Npc(string id, string name, string role, string description, CampStationKind station, params string[] greetings) =>
            Asset<NpcDefinition>("Npcs/Npc_" + id, "npc." + id, name, description, n =>
            {
                n.Role = role;
                n.Station = station;
                n.HasStation = true;
                n.Greetings = new List<string>(greetings);
            });

        private static QuestDefinition Quest(string id, string title, int order, NpcDefinition giver, QuestDefinition requires, QuestObjective objective,
            PreyDefinition prey, int count, double reward, string task, string offer, string waiting, string done) =>
            Asset<QuestDefinition>("Quests/Quest_" + id, "quest." + id, title, task, q =>
            {
                q.Order = order;
                q.Giver = giver;
                q.Requires = requires;
                q.Objective = objective;
                q.Prey = prey;
                q.Count = count;
                q.RewardCoins = reward;
                q.Task = task;
                q.Offer = offer;
                q.Waiting = waiting;
                q.Done = done;
                q.Unlocks = string.Empty;
            });
    }
}
