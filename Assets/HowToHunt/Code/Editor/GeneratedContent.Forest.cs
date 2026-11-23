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
                "Sly and wary: sees you from 40 m and outruns a sprint. Too strong for any net yet; a clean shot or a sneaky blade.", p =>
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
                p.AttackWindup = 0.45f;
                p.AttackCooldown = 1.6f;
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

            Asset<PreyDefinition>("Prey/Prey_OldBear", "prey.old_bear", "Old Bear of the Pines",
                "The forest's boss. Guards its den in the south-east glade and charges any hunter within 24 m: 35 damage a swipe, after a roar you can dodge. 1,500 health, regains it if everyone runs. Every hunter who hurts it shares the reward.", p =>
            {
                p.Allowed = TakedownMethods.Kill;
                p.MaxHealth = 1500f;
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
                p.AggroRange = 24f;
                p.LeashRadius = 42f;     // lead it further from its den and it walks home
                p.AttackRange = 2.6f;
                p.AttackWindup = 0.75f;  // the roar: time to jump aside or back off
                p.AttackCooldown = 1.3f;
                p.AttackDamage = 35f;    // three swipes put a hunter down
                p.AttackPush = 11f;
                p.ChargeSpeed = 7f;      // faster than a run, slower than a sprint or a bunny hop: kite it
                p.ProvokeSeconds = 10f;
                p.RegenPerSecond = 60f;
                p.WanderRadius = 5f;     // stays in its den until someone comes near
                p.IsBoss = true;
                p.BossTitle = "The Old Bear of the Pines";
                p.BossRewardCoins = 400;
                p.BossRewardTrophies = 25;
                p.Visual = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels.PrefabPath("OldBear"));
                p.VisualScale = 1f;
                p.Noun = "the Old Bear";
                p.BloodColor = new Color(0.6f, 0.04f, 0.04f);
                p.GibColor = new Color(0.28f, 0.19f, 0.12f);
                p.GibCount = 16;
                p.StackSize = 1;
                p.Voice = PreyVoice.Roar;
            });
        }
    }
}
