using System.Collections.Generic;
using HowToHunt.Economy;
using HowToHunt.Hunting;
using HowToHunt.Sim.Hunting;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The third biome, the Red Desert (started 2026-11-24): its animals (jackrabbit, desert lizard, scorpion,
    /// rattlesnake, coyote), its characters (Rosa at the trading post, Dusty the prospector by the fire) and their
    /// first quests. The map is GrayboxSceneBuilder.Desert.cs. Numbers follow the forest's: small things for the net,
    /// biters that strike when you come close, a wary runner worth a long chase.
    /// </summary>
    public static partial class GeneratedContent
    {
        private static void BuildDesertFauna()
        {
            Asset<PreyDefinition>("Prey/Prey_Jackrabbit", "prey.jackrabbit", "Jackrabbit",
                "Long ears, long legs: hears you from far off and zigzags away faster than a sprint. An Ironwood Net holds it.", p =>
            {
                p.Allowed = TakedownMethods.Either;
                p.MaxHealth = 12f;
                p.CaptureDifficulty = 3f;
                p.MinKillTier = 1;
                p.OverkillRatio = 3f;
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 0.2f, -0.12f), B = new Vector3(0f, 0.22f, 0.1f), Radius = 0.14f },
                    new HitboxShape { A = new Vector3(0f, 0.34f, 0.18f), B = new Vector3(0f, 0.36f, 0.26f), Radius = 0.09f, WeakPoint = true },
                };
                p.BaseValue = 16;
                p.TrophiesOnKill = 1;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 1.2f;
                p.FleeSpeed = 9.2f;      // faster than a sprint; tires after 4 s and a bunny hop catches it
                p.StaminaSeconds = 4f;
                p.TiredSpeed = 5.8f;
                p.FleeRadius = 10f;
                p.SightRange = 26f;
                p.HearingRange = 60f;
                p.Wariness = 1.2f;
                p.CalmDownSeconds = 3f;
                p.StunSeconds = 0.15f;
                p.HalfExtents = new Vector3(0.14f, 0.18f, 0.24f);
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("Jackrabbit"));
                p.VisualScale = 1f;
                p.Noun = "a jackrabbit";
                p.BloodColor = new Color(0.72f, 0.07f, 0.06f);
                p.GibColor = new Color(0.72f, 0.6f, 0.45f);
                p.GibCount = 5;
                p.StackSize = 1;
                p.Voice = PreyVoice.Squeak;
            });

            Asset<PreyDefinition>("Prey/Prey_Lizard", "prey.lizard", "Desert Lizard",
                "Basks on warm rocks and darts off in short bursts. Any net holds it; three fit in a slot.", p =>
            {
                p.Allowed = TakedownMethods.Either;
                p.MaxHealth = 5f;
                p.CaptureDifficulty = 1f;
                p.MinKillTier = 1;
                p.OverkillRatio = 3f;
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 0.05f, -0.12f), B = new Vector3(0f, 0.05f, 0.12f), Radius = 0.06f },
                    new HitboxShape { A = new Vector3(0f, 0.06f, 0.15f), B = new Vector3(0f, 0.06f, 0.2f), Radius = 0.05f, WeakPoint = true },
                };
                p.BaseValue = 8;
                p.TrophiesOnKill = 1;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 0.8f;
                p.FleeSpeed = 7.6f;      // a short dash: tires after 1.5 s
                p.StaminaSeconds = 1.5f;
                p.TiredSpeed = 3f;
                p.FleeRadius = 5f;
                p.SightRange = 12f;
                p.HearingRange = 30f;
                p.Wariness = 0.8f;
                p.CalmDownSeconds = 1.5f;
                p.StunSeconds = 0.2f;
                p.HalfExtents = new Vector3(0.07f, 0.05f, 0.2f);
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("Lizard"));
                p.VisualScale = 1.3f;
                p.Noun = "a lizard";
                p.BloodColor = new Color(0.6f, 0.1f, 0.06f);
                p.GibColor = new Color(0.55f, 0.6f, 0.35f);
                p.GibCount = 3;
                p.StackSize = 3;
                p.Voice = PreyVoice.Squeak;
            });

            Asset<PreyDefinition>("Prey/Prey_Scorpion", "prey.scorpion", "Red Scorpion",
                "Doesn't run: it comes at you and stings (10 damage, then poison: 3 a second for 5 s) when you step within 4 m. Slow; back off or net it (Ironwood Net). Sells alive to the curious.", p =>
            {
                p.Allowed = TakedownMethods.Either;
                p.MaxHealth = 14f;
                p.CaptureDifficulty = 3f;
                p.MinKillTier = 1;
                p.OverkillRatio = 3f;
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 0.07f, -0.12f), B = new Vector3(0f, 0.07f, 0.12f), Radius = 0.1f },
                    new HitboxShape { A = new Vector3(0f, 0.22f, -0.2f), B = new Vector3(0f, 0.27f, -0.08f), Radius = 0.05f, WeakPoint = true }, // the sting
                };
                p.BaseValue = 22;
                p.TrophiesOnKill = 2;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 0.7f;
                p.FleeSpeed = 3.2f;
                p.StaminaSeconds = 3f;
                p.TiredSpeed = 2.4f;
                p.FleeRadius = 3f;
                p.SightRange = 10f;
                p.HearingRange = 20f;
                p.Wariness = 0.6f;
                p.CalmDownSeconds = 2f;
                p.StunSeconds = 0.2f;
                p.HalfExtents = new Vector3(0.14f, 0.08f, 0.22f);
                p.Aggression = PreyAggression.Always;
                p.AggroRange = 4f;
                p.LeashRadius = 9f;
                p.AttackRange = 0.9f;
                p.AttackWindup = 0.55f;  // the tail rears: step back
                p.AttackCooldown = 1.2f;
                p.AttackDamage = 10f;
                p.AttackPush = 3f;
                p.PoisonDamagePerSecond = 3f; // owner, 2026-11-24: scorpions are venomous
                p.PoisonSeconds = 5f;
                p.ChargeSpeed = 3.4f;    // slower than a walk-back: easy to keep clear of
                p.ProvokeSeconds = 6f;
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("Scorpion"));
                p.VisualScale = 1.3f;
                p.Noun = "a scorpion";
                p.BloodColor = new Color(0.55f, 0.75f, 0.3f);
                p.GibColor = new Color(0.72f, 0.25f, 0.12f);
                p.GibCount = 5;
                p.StackSize = 2;
                p.Voice = PreyVoice.Squeak;
            });

            Asset<PreyDefinition>("Prey/Prey_Rattlesnake", "prey.rattlesnake", "Rattlesnake",
                "Lies still in the shade of rocks. Come within 3.5 m and it rattles, then strikes fast (14 damage, then venom: 4 a second for 6 s). Kill only.", p =>
            {
                p.Allowed = TakedownMethods.Kill;
                p.MaxHealth = 20f;
                p.CaptureDifficulty = 99f;
                p.MinKillTier = 1;
                p.OverkillRatio = 3.5f;
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 0.06f, -0.45f), B = new Vector3(0f, 0.06f, 0.3f), Radius = 0.07f },
                    new HitboxShape { A = new Vector3(0f, 0.16f, 0.35f), B = new Vector3(0f, 0.18f, 0.45f), Radius = 0.06f, WeakPoint = true },
                };
                p.BaseValue = 30;
                p.TrophiesOnKill = 3;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 0.6f;
                p.FleeSpeed = 2.6f;
                p.StaminaSeconds = 3f;
                p.TiredSpeed = 2f;
                p.FleeRadius = 2f;
                p.SightRange = 8f;
                p.HearingRange = 15f;
                p.Wariness = 0.5f;
                p.CalmDownSeconds = 2f;
                p.StunSeconds = 0.15f;
                p.HalfExtents = new Vector3(0.12f, 0.08f, 0.45f);
                p.Aggression = PreyAggression.Always;
                p.AggroRange = 3.5f;
                p.LeashRadius = 6f;
                p.AttackRange = 1.6f;    // a long strike
                p.AttackWindup = 0.5f;   // the rattle: the cue to jump back
                p.AttackCooldown = 1.4f;
                p.AttackDamage = 14f;   // less on the strike, the venom does the rest
                p.AttackPush = 4f;
                p.PoisonDamagePerSecond = 4f;
                p.PoisonSeconds = 6f;
                p.ChargeSpeed = 3f;
                p.ProvokeSeconds = 5f;
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("Rattlesnake"));
                p.VisualScale = 1f;
                p.Noun = "a rattlesnake";
                p.BloodColor = new Color(0.66f, 0.06f, 0.05f);
                p.GibColor = new Color(0.62f, 0.5f, 0.32f);
                p.GibCount = 5;
                p.StackSize = 1;
                p.Voice = PreyVoice.Squeak;
            });

            // The venomous one (owner, 2026-11-24: "add poisonous snakes"): small, bright, hidden in the dunes' scrub.
            Asset<PreyDefinition>("Prey/Prey_CoralSnake", "prey.coral_snake", "Coral Snake",
                "Small and bright: red, yellow and black bands. Its bite is light (6) but its venom is the worst in the desert: 6 a second for 8 s. Strikes within 2.5 m. Kill only.", p =>
            {
                p.Allowed = TakedownMethods.Kill;
                p.MaxHealth = 10f;
                p.CaptureDifficulty = 99f;
                p.MinKillTier = 1;
                p.OverkillRatio = 3f;
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 0.04f, -0.35f), B = new Vector3(0f, 0.04f, 0.25f), Radius = 0.05f },
                    new HitboxShape { A = new Vector3(0f, 0.1f, 0.28f), B = new Vector3(0f, 0.11f, 0.34f), Radius = 0.045f, WeakPoint = true },
                };
                p.BaseValue = 26;
                p.TrophiesOnKill = 3;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 0.7f;
                p.FleeSpeed = 3f;
                p.StaminaSeconds = 3f;
                p.TiredSpeed = 2.2f;
                p.FleeRadius = 2f;
                p.SightRange = 7f;
                p.HearingRange = 12f;
                p.Wariness = 0.5f;
                p.CalmDownSeconds = 2f;
                p.StunSeconds = 0.15f;
                p.HalfExtents = new Vector3(0.08f, 0.06f, 0.35f);
                p.Aggression = PreyAggression.Always;
                p.AggroRange = 2.5f;
                p.LeashRadius = 5f;
                p.AttackRange = 1.2f;
                p.AttackWindup = 0.4f;   // a quick strike: keep your distance
                p.AttackCooldown = 1.6f;
                p.AttackDamage = 6f;
                p.AttackPush = 2f;
                p.ChargeSpeed = 3.2f;
                p.ProvokeSeconds = 5f;
                p.PoisonDamagePerSecond = 6f;
                p.PoisonSeconds = 8f;
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("CoralSnake"));
                p.VisualScale = 1f;
                p.Noun = "a coral snake";
                p.BloodColor = new Color(0.66f, 0.06f, 0.05f);
                p.GibColor = new Color(0.85f, 0.2f, 0.12f);
                p.GibCount = 4;
                p.StackSize = 1;
                p.Voice = PreyVoice.Squeak;
            });

            Asset<PreyDefinition>("Prey/Prey_Coyote", "prey.coyote", "Coyote",
                "Runs in pairs and keeps its distance; hurt one and it snaps back at you for a while (12 damage). Worth the chase.", p =>
            {
                p.Allowed = TakedownMethods.Either;
                p.MaxHealth = 40f;
                p.CaptureDifficulty = 4f;
                p.MinKillTier = 1;
                p.OverkillRatio = 4f;
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 0.45f, -0.3f), B = new Vector3(0f, 0.47f, 0.3f), Radius = 0.16f },
                    new HitboxShape { A = new Vector3(0f, 0.17f, -0.22f), B = new Vector3(0f, 0.17f, 0.22f), Radius = 0.08f },
                    new HitboxShape { A = new Vector3(0f, 0.58f, 0.42f), B = new Vector3(0f, 0.55f, 0.6f), Radius = 0.11f, WeakPoint = true },
                };
                p.BaseValue = 48;
                p.TrophiesOnKill = 4;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 1.4f;
                p.FleeSpeed = 9f;
                p.StaminaSeconds = 6f;
                p.TiredSpeed = 6.4f;
                p.FleeRadius = 14f;
                p.SightRange = 34f;
                p.HearingRange = 60f;
                p.Wariness = 1.3f;
                p.CalmDownSeconds = 4f;
                p.StunSeconds = 0.25f;
                p.HalfExtents = new Vector3(0.17f, 0.3f, 0.42f);
                p.Aggression = PreyAggression.WhenHurt;
                p.AggroRange = 0f;
                p.ProvokeSeconds = 7f;
                p.LeashRadius = 40f;
                p.AttackRange = 1.4f;
                p.AttackWindup = 0.4f;
                p.AttackCooldown = 1.3f;
                p.AttackDamage = 12f;
                p.AttackPush = 6f;
                p.ChargeSpeed = 7.6f;
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("Coyote"));
                p.VisualScale = 1f;
                p.Noun = "a coyote";
                p.BloodColor = new Color(0.7f, 0.06f, 0.05f);
                p.GibColor = new Color(0.7f, 0.58f, 0.42f);
                p.GibCount = 7;
                p.StackSize = 1;
                p.Voice = PreyVoice.Grunt;
            });
        }

        private static void BuildDesertNpcsAndQuests()
        {
            NpcDefinition rosa = Npc("desert_trader", "Rosa", "Desert Trader", "Runs the trading post at Dry Creek. Desert catch sells for twice the Plains price.", CampStationKind.Sell,
                "Dust on your boots already? Welcome to Dry Creek.",
                "Water's free. Everything else costs.",
                "Snakes, scorpions, long-eared hares. Bring them here.");
            rosa.Idle = "Shake your boots out before you put them on. Trust me.";
            Voice(rosa, 1.1f, "[shout] Desert prices! Twice what the Plains pay!", "Mind the rocks, that's where they hide.", "[think] Too hot for this...");
            NpcDefinition dusty = NpcAbout("prospector", "Dusty", "Prospector", "Sits by the fire at Dry Creek, full of stories about the desert.", NpcActivity.Campfire,
                "Pull up a rock. Fire's warm, the night gets cold out here.",
                "Forty years I've panned these creeks. Found one nugget. Lost it.",
                "Coyotes sing at night. They're not singing to you.");
            dusty.Idle = "Rattlers lie in the shade. If you hear the rattle, jump back.";
            Voice(dusty, 0.7f, "[think] Gold in them hills... somewhere.", "Hot one today.", "[whisper] Heard a coyote out by the mesa.");

            PreyDefinition jack = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_Jackrabbit.asset");
            PreyDefinition scorpion = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_Scorpion.asset");
            PreyDefinition snake = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_Rattlesnake.asset");
            PreyDefinition coyote = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_Coyote.asset");
            QuestDefinition ears = Quest("long_ears", "Long Ears", 20, rosa, null, QuestObjective.TakeDown, jack, 4, 260,
                "Take down 4 jackrabbits",
                "Jackrabbits eat my garden. Four of them, any way you like.",
                "They tire after a few seconds. Bunny hop after them.",
                "Four! My lettuce thanks you.");
            QuestDefinition stings = Quest("sting_operation", "Sting Operation", 21, rosa, ears, QuestObjective.Capture, scorpion, 2, 340,
                "Catch 2 scorpions alive",
                "A collector wants live scorpions. Two, alive: an Ironwood Net holds them. Watch the tail.",
                "When the tail goes up, step back.",
                "Two angry scorpions. The collector will be thrilled.");
            Quest("rattled", "Rattled", 22, rosa, stings, QuestObjective.Kill, snake, 3, 420,
                "Kill 3 rattlesnakes",
                "Rattlers by the well again. Three of them, dead.",
                "Hear the rattle, jump back, then strike.",
                "The well's safe again. Thank you.");
            PreyDefinition coral = AssetDatabase.LoadAssetAtPath<PreyDefinition>(ContentDir + "/Prey/Prey_CoralSnake.asset");
            Quest("bright_bands", "Bright Bands", 24, dusty, ears, QuestObjective.Kill, coral, 2, 360,
                "Kill 2 coral snakes",
                "Red touches yellow, kills a fellow. Two coral snakes nest by my tent. Mind the venom.",
                "If you're bitten, back off and wait it out. It passes.",
                "Gone? Then I sleep tonight. Thank you, friend.");
            Quest("coyote_trouble", "Coyote Trouble", 23, dusty, ears, QuestObjective.TakeDown, coyote, 2, 380,
                "Take down 2 coyotes",
                "Two coyotes keep raiding my camp. They run, but hurt one and it bites back.",
                "Coyotes keep their distance. A long gun helps.",
                "Quiet nights again. Here, for your trouble.");
        }
    }
}
