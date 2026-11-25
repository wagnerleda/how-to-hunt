using System.Collections.Generic;
using HowToHunt.Hunting;
using HowToHunt.Sim.Hunting;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Pine Forest fauna (owner request, 2026-11-24): the red squirrel (small, comes in groups, for the Sturdy Net),
    /// the red fox (wary and fast), the wild boar (charges whoever hurts it) and the forest boss, the Old Bear of the
    /// Pines (guards its den, charges any hunter near it, shared reward). Numbers: docs/DESIGN.md (Pine Forest).
    /// Models: CreatureModels. Ash's quests for them: GeneratedContent.Quests.
    /// </summary>
    public static partial class GeneratedContent
    {
        private static void BuildForestFauna()
        {
            Asset<PreyDefinition>("Prey/Prey_Squirrel", "prey.squirrel", "Red Squirrel",
                "Darts between the pines in little groups. Quick but fragile: a Sturdy Net holds it, and four fit in one slot.", p =>
            {
                p.Allowed = TakedownMethods.Either;
                p.MaxHealth = 6f;
                p.CaptureDifficulty = 2f;
                p.MinKillTier = 1;
                p.OverkillRatio = 3f;
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 0.12f, -0.08f), B = new Vector3(0f, 0.13f, 0.08f), Radius = 0.08f },
                    new HitboxShape { A = new Vector3(0f, 0.2f, 0.11f), B = new Vector3(0f, 0.2f, 0.16f), Radius = 0.06f, WeakPoint = true },
                };
                p.BaseValue = 9;
                p.TrophiesOnKill = 1;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 1.4f;
                p.FleeSpeed = 8f;        // outruns a sprint for 3 s, then a runner catches it
                p.StaminaSeconds = 3f;
                p.TiredSpeed = 5.6f;
                p.FleeRadius = 7f;
                p.SightRange = 18f;
                p.HearingRange = 40f;
                p.Wariness = 1f;
                p.CalmDownSeconds = 2.5f;
                p.StunSeconds = 0.2f;
                p.HalfExtents = new Vector3(0.08f, 0.1f, 0.12f);
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("Squirrel"));
                p.VisualScale = 1.4f;    // a little bigger than life, so it reads at hunting distance
                p.Noun = "a squirrel";
                p.BloodColor = new Color(0.72f, 0.07f, 0.06f);
                p.GibColor = new Color(0.78f, 0.36f, 0.14f);
                p.GibCount = 4;
                p.StackSize = 4;
                p.Voice = PreyVoice.Squeak;
            });

            Asset<PreyDefinition>("Prey/Prey_Fox", "prey.fox", "Red Fox",
                "Sly and wary: sees you from 40 m and outruns a sprint. An Ironwood Net holds it; or a clean shot, or a sneaky blade.", p =>
            {
                p.Allowed = TakedownMethods.Either;
                p.MaxHealth = 25f;
                p.CaptureDifficulty = 3f;
                p.MinKillTier = 1;
                p.OverkillRatio = 3.5f;
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 0.38f, -0.25f), B = new Vector3(0f, 0.4f, 0.25f), Radius = 0.14f },
                    new HitboxShape { A = new Vector3(0f, 0.15f, -0.2f), B = new Vector3(0f, 0.15f, 0.2f), Radius = 0.07f }, // legs
                    new HitboxShape { A = new Vector3(0f, 0.51f, 0.36f), B = new Vector3(0f, 0.48f, 0.52f), Radius = 0.1f, WeakPoint = true },
                };
                p.BaseValue = 28;
                p.TrophiesOnKill = 2;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 1.3f;
                p.FleeSpeed = 9.5f;
                p.StaminaSeconds = 5f;
                p.TiredSpeed = 6.2f;
                p.FleeRadius = 11f;
                p.SightRange = 30f;
                p.HearingRange = 55f;
                p.Wariness = 1.3f;
                p.CalmDownSeconds = 4f;
                p.StunSeconds = 0.25f;
                p.HalfExtents = new Vector3(0.15f, 0.25f, 0.35f);
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("Fox"));
                p.VisualScale = 1f;
                p.Noun = "a fox";
                p.BloodColor = new Color(0.7f, 0.06f, 0.05f);
                p.GibColor = new Color(0.9f, 0.45f, 0.15f);
                p.GibCount = 6;
                p.StackSize = 1;
                p.Voice = PreyVoice.Squeak;
            });

            Asset<PreyDefinition>("Prey/Prey_Boar", "prey.boar", "Wild Boar",
                "Tough and bad-tempered: it runs from you, but hurt it and it charges (15 damage a hit, it throws you back). Jump aside when it rears up. Kill only.", p =>
            {
                p.Allowed = TakedownMethods.Kill;
                p.MaxHealth = 90f;
                p.CaptureDifficulty = 99f;
                p.MinKillTier = 2;
                p.OverkillRatio = 5f;
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 0.58f, -0.42f), B = new Vector3(0f, 0.6f, 0.38f), Radius = 0.32f },
                    new HitboxShape { A = new Vector3(0f, 0.18f, -0.3f), B = new Vector3(0f, 0.18f, 0.3f), Radius = 0.12f }, // legs
                    new HitboxShape { A = new Vector3(0f, 0.53f, 0.56f), B = new Vector3(0f, 0.48f, 0.86f), Radius = 0.18f, WeakPoint = true },
                };
                p.BaseValue = 60;
                p.TrophiesOnKill = 5;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 1.1f;
                p.FleeSpeed = 7f;
                p.StaminaSeconds = 4f;
                p.TiredSpeed = 5f;
                p.FleeRadius = 9f;
                p.SightRange = 24f;
                p.HearingRange = 50f;
                p.Wariness = 1f;
                p.CalmDownSeconds = 3f;
                p.StunSeconds = 0.2f;
                p.HalfExtents = new Vector3(0.28f, 0.42f, 0.55f);
                p.Aggression = PreyAggression.WhenHurt;
                p.ProvokeSeconds = 9f;
                p.LeashRadius = 45f;
                p.AttackRange = 1.6f;
                p.AttackWindup = 0.35f;  // owner, 2026-11-25: it took too long to load its attack (was 0.45)
                p.AttackCooldown = 0.8f; // and too long between strikes (was 1.6)
                p.AttackDamage = 15f;
                p.AttackPush = 8f;
                p.ChargeSpeed = 7.4f;   // a charging boar outpaces a run (6.35) but not a sprint (7.9) or a bunny hop
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("Boar"));
                p.VisualScale = 1f;
                p.Noun = "a boar";
                p.BloodColor = new Color(0.66f, 0.05f, 0.05f);
                p.GibColor = new Color(0.33f, 0.27f, 0.22f);
                p.GibCount = 9;
                p.StackSize = 1;
                p.Voice = PreyVoice.Grunt;
            });

            PreyDefinition bear = Asset<PreyDefinition>("Prey/Prey_OldBear", "prey.old_bear", "Old Bear of the Pines",
                "The forest's boss, woken by the Honeycomb Bait sold at Ash's camp. Charges any hunter within 30 m: 40-damage swipes after a roar, a ground slam every third strike (50 damage all around: jump or get away), a pounce at hunters who keep their distance. 3,000 health; enraged below 40 %. Regains health if everyone runs. Every hunter who hurts it shares the reward.", p =>
            {
                p.Allowed = TakedownMethods.Kill;
                p.MaxHealth = 3000f;
                p.CaptureDifficulty = 999f;
                p.MinKillTier = 2;
                p.OverkillRatio = 100f;  // nothing ruins it
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 1.05f, -0.75f), B = new Vector3(0f, 1.15f, 0.6f), Radius = 0.6f },
                    new HitboxShape { A = new Vector3(0f, 0.4f, -0.6f), B = new Vector3(0f, 0.4f, 0.6f), Radius = 0.3f }, // legs
                    new HitboxShape { A = new Vector3(0f, 1.17f, 1.05f), B = new Vector3(0f, 1.08f, 1.55f), Radius = 0.3f, WeakPoint = true },
                };
                p.BaseValue = 250;
                p.TrophiesOnKill = 10;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 1.2f;
                p.FleeSpeed = 6.8f;
                p.FleeRadius = 0f;
                p.SightRange = 0f;
                p.HearingRange = 0f;     // guns don't scare it
                p.CalmDownSeconds = 6f;
                p.StunSeconds = 0.08f;   // a flinch, never a stun-lock
                p.HalfExtents = new Vector3(0.55f, 0.8f, 0.95f);
                p.Aggression = PreyAggression.Always;
                p.AggroRange = 30f;
                p.LeashRadius = 55f;     // lead it further from its den and it walks home
                p.AttackRange = 2.8f;
                p.AttackWindup = 0.65f;  // the roar: time to jump aside or back off
                p.AttackCooldown = 1.05f;
                p.AttackDamage = 40f;    // three swipes put a hunter down
                p.AttackPush = 12f;
                p.ChargeSpeed = 7.2f;    // faster than a run, about a sprint: bunny hop to kite it
                p.ProvokeSeconds = 12f;
                p.RegenPerSecond = 80f;
                p.SlamEvery = 3;         // every third strike: rears up high and crashes down
                p.SlamRadius = 5.5f;
                p.SlamWindup = 1f;       // a deeper roar: get out of the circle or jump as it lands
                p.SlamDamage = 50f;
                p.SlamPush = 15f;
                p.SlamDodgeHeight = 0.5f;
                p.PounceRange = 13f;     // kiting at the edge of its reach no longer works
                p.PounceSpeed = 16f;
                p.PounceWindup = 0.55f;  // crouches low: sidestep
                p.PounceCooldown = 5f;
                p.EnrageBelow = 0.4f;
                p.EnrageTempo = 0.72f;
                p.EnrageSpeed = 1.1f;
                p.WanderRadius = 5f;     // stays in its den until someone comes near
                p.IsBoss = true;
                p.BossTitle = "The Old Bear of the Pines";
                p.BossRewardCoins = 650;
                p.BossRewardTrophies = 35;
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("OldBear"));
                p.VisualScale = 1f;
                p.Noun = "the Old Bear";
                p.BloodColor = new Color(0.6f, 0.04f, 0.04f);
                p.GibColor = new Color(0.28f, 0.19f, 0.12f);
                p.GibCount = 16;
                p.StackSize = 1;
                p.Voice = PreyVoice.Roar;
            });

            // The bear sleeps until a hunter buys its bait at the bait stall in Ash's camp (owner request, 2026-11-24).
            Asset<UpgradeDefinition>("Upgrades/Upgrade_BearBait", "upgrade.bait_old_bear", "Honeycomb Bait",
                "Its smell carries to the den. Come in numbers.", u =>
            {
                u.Tool = null;
                u.Effect = UpgradeEffect.SummonBoss;
                u.Summons = bear;
                u.Value = 0f;
                u.CostCoins = 150;
                u.SortOrder = 100;
                u.Requires = null;
                u.Unlisted = false;
                u.SoldByGunsmith = false;
            });
        }
    }
}
