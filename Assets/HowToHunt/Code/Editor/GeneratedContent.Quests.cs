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
        /// <summary>Set when the party brings down the Old Bear (the forest boss): the key to the next biome, later.</summary>
        public const string ForestBossFlag = "forest_boss";

        private static void BuildNpcsAndQuests()
        {
            GeneratedAssets.EnsureFolder(ContentDir + "/Npcs");
            GeneratedAssets.EnsureFolder(ContentDir + "/Quests");

            NpcDefinition tomas = Npc("trader", "Tomas", "Trader", "Buys whatever you bring back, alive or not.", CampStationKind.Sell,
                "Ha, a fresh face. Got something for me?",
                "Back again? Show me what you've caught.",
                "Rabbits keep the lights on, friend.");
            tomas.Idle = "Quiet day. The rabbits by the river logs have been fat this year.";
            Voice(tomas, 0.9f, "[shout] Fresh catches wanted! Best prices in the Plains!", "Selling? Bring it over here.", "[think] Rabbit stew again tonight...");
            NpcDefinition wren = Npc("outfitter", "Wren", "Outfitter", "Sells nets, knives and the gear to carry more.", CampStationKind.Shop,
                "You look under-equipped. Let's fix that.",
                "Need a sturdier net? A bigger bag?",
                "Everything on the rack is tested on actual rabbits.");
            wren.Idle = "If it tears, bring it back. It won't tear.";
            Voice(wren, 1.25f, "Nets! <wave>Sturdy nets!</wave>", "[whisper] Psst... the bigger bags are in the back.", "Mind the butterflies, they bruise.");
            NpcDefinition gus = Npc("gunsmith", "Gus", "Gunsmith", "Sells the Old Revolver and attachments, fits them while you wait.", CampStationKind.Workshop,
                "Mind the bench. What are we shooting today?",
                "A good gun's a quiet gun. Mostly.",
                "Bring it here, I'll make it kick less. Or more, if that's your thing.");
            gus.Idle = "Brakes, cans, stocks... everything's for sale.";
            Voice(gus, 0.75f, "[think] Where did I put that spring...", "Mind your aim out there.", "[shout] Who left a <shake>loaded</shake> gun on my bench?!");
            NpcDefinition ash = Npc("forest_trader", "Ash", "Forest Trader", "Runs the trading post in the pine forest. Pays well for deer.", CampStationKind.Sell,
                "You came down the slopes? Then you can hunt here.",
                "The pines are generous to the patient.",
                "Deer hear everything. Walk softly.");
            ash.Idle = "Forest catch sells for more here. Nobody wants to carry it back up.";
            Voice(ash, 1.05f, "The pines are watching.", "[whisper] Walk softly near the deer.", "Stay clear of the south-east glade... <shake>it</shake> sleeps there.");

            PreyDefinition deer = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_Deer.asset");

            QuestDefinition freshStock = Quest("fresh_stock", "Fresh Stock", 1, tomas, null, QuestObjective.Sell, null, 3, 60,
                "Sell 3 catches to Tomas",
                "New out here? Bring me three catches and I'll pay a bonus on top.",
                "Rabbits are by the logs.",
                "Here's your bonus. Go and see Wren at the outfitter.");
            QuestDefinition kittedOut = Quest("kitted_out", "Kitted Out", 2, wren, freshStock, QuestObjective.BuyUpgrade, null, 1, 80,
                "Buy an upgrade from Wren",
                "Tomas says you can hunt. Spend some of that coin on my rack.",
                "Something for every budget.",
                "Suits you. Gus at the gunsmith was asking about you.");
            QuestDefinition steadyHands = Quest("steady_hands", "Steady Hands", 3, gus, kittedOut, QuestObjective.Kill, null, 5, 120,
                "Take down 5 prey (killed, not caught)",
                "A hunter brings things down. Five of them, no catching alive.",
                "Five. Down.",
                "Not bad. Tomas wants a word about the trees to the east.");
            QuestDefinition beyond = Quest("beyond_the_trees", "Beyond the Trees", 4, tomas, steadyHands, QuestObjective.EarnCoins, null, 300, 150,
                "Earn 300 coins selling your catch",
                "Fallen trees block the way to the pine forest. Earn me 300 coins in trade and I'll get them cleared.",
                "Keep it coming.",
                "The way east is clear. Surf the slopes down to the pines; a pad at the forest camp brings you home.");
            beyond.Unlocks = EastClearingFlag;
            EditorUtility.SetDirty(beyond);
            QuestDefinition ghost = Quest("ghost_of_the_pines", "Ghost of the Pines", 5, ash, beyond, QuestObjective.TakeDown, deer, 1, 300,
                deer != null ? "Take down a deer" : "Take down anything in the forest",
                "Deer hear you long before you see them. Bring one down.",
                "Move slowly near the deer... or chase it.",
                "A real deer. You'll do well out here.");

            // Forest chain (2026-11-24): the new animals, then the forest boss. Squirrels need the Sturdy Net.
            PreyDefinition squirrel = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_Squirrel.asset");
            PreyDefinition fox = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_Fox.asset");
            PreyDefinition boar = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_Boar.asset");
            PreyDefinition bear = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_OldBear.asset");
            QuestDefinition paws = Quest("quick_paws", "Quick Paws", 6, ash, ghost, QuestObjective.Capture, squirrel, 3, 150,
                "Catch 3 red squirrels alive",
                "Three live squirrels, from the pine stands. A Sturdy Net holds them.",
                "They tire quickly: sprint after them.",
                "Three live ones, still cross about it.");
            QuestDefinition sly = Quest("the_sly_one", "The Sly One", 7, ash, paws, QuestObjective.TakeDown, fox, 2, 220,
                "Take down 2 red foxes",
                "Two foxes raid my stores. They're quick and they see you coming.",
                "Come at them through the trees, crouched.",
                "That'll teach them.");
            QuestDefinition tusks = Quest("tusks_and_temper", "Tusks and Temper", 8, ash, sly, QuestObjective.Kill, boar, 3, 300,
                "Kill 3 wild boars",
                "Hurt a boar and it charges. Three of them; jump aside when one rears up.",
                "Keep moving, jump aside.",
                "Three boars! You're ready for the worst.");
            QuestDefinition king = Quest("the_old_bear", "The Old Bear", 9, ash, tusks, QuestObjective.TakeDown, bear, 1, 800,
                "Bring down the Old Bear of the Pines",
                "The Old Bear sleeps in the south-east glade. A Honeycomb Bait from the stall behind me wakes it. Bring friends.",
                "Deep roar: jump. Low crouch: sidestep.",
                "The Old Bear... the whole forest will be talking about you.");
            king.Unlocks = ForestBossFlag;
            EditorUtility.SetDirty(king);
        }

        /// <summary>How a character sounds (blip pitch) and what they call out as you walk past (bubble markup works).</summary>
        private static void Voice(NpcDefinition npc, float pitch, params string[] barks)
        {
            npc.VoicePitch = pitch;
            npc.Barks = new List<string>(barks);
            EditorUtility.SetDirty(npc);
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
