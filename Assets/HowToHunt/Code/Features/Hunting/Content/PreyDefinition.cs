using System;
using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Hunting;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>A huntable animal. Every prey can be caught or killed unless Allowed says otherwise.</summary>
    [CreateAssetMenu(menuName = "How to Hunt/Prey", fileName = "Prey_")]
    public sealed class PreyDefinition : ContentDefinition
    {
        public override string KeyPrefix => "prey.";

        [Header("Take-down")]
        public TakedownMethods Allowed = TakedownMethods.Either;
        public float MaxHealth = 10f;
        [Tooltip("Net capture strength needed to hold it.")]
        public float CaptureDifficulty = 1f;
        [Tooltip("Kill tools below this tier only scratch it.")]
        public int MinKillTier;
        [Tooltip("One hit dealing more than MaxHealth × this ruins the carcass.")]
        public float OverkillRatio = 2.5f;
        public List<HitboxShape> Hitboxes = new List<HitboxShape>();

        [Header("Economy")]
        public double BaseValue = 5;
        public double TrophiesOnKill;

        [Header("Behavior")]
        public PreyMovement Movement = PreyMovement.Ground;
        public float WalkSpeed = 1.2f;
        public float FleeSpeed = 6f;
        [Tooltip("Flees from any player this close, seen or not (hears footsteps).")]
        public float FleeRadius = 7f;
        [Tooltip("Flees from a player it can see this far away (line of sight; halved against crouching players).")]
        public float SightRange = 20f;
        [Tooltip("Flees from gunshots within this distance. 0 for prey that aren't scared of guns (predators).")]
        public float HearingRange = 45f;
        [Tooltip("Rare prey are warier: multiplies FleeRadius and SightRange.")]
        public float Wariness = 1f;
        public float CalmDownSeconds = 2.5f;
        [Tooltip("Wanders this far from its spawn point when calm (m). Bosses stay close to their den.")]
        public float WanderRadius = 18f;
        public float StunSeconds = 0.6f;
        [Tooltip("Seconds of full-speed flight before it tires (0 = never). Sprint above run speed, tire below it.")]
        public float StaminaSeconds;
        public float TiredSpeed = 4f;
        public Vector3 HalfExtents = new Vector3(0.2f, 0.18f, 0.25f);

        [Header("Aggression (boars, bosses)")]
        [Tooltip("None: only flees. WhenHurt: flees, but charges whoever hurts it. Always: charges hunters near its home.")]
        public PreyAggression Aggression = PreyAggression.None;
        [Tooltip("Always-aggressive: charges a hunter this close (m).")]
        public float AggroRange = 20f;
        [Tooltip("Gives up and walks home once the hunter is this far from its home (m).")]
        public float LeashRadius = 40f;
        [Tooltip("Its strike reaches hunters this close in front of it (m).")]
        public float AttackRange = 1.8f;
        [Tooltip("Visible wind-up before each strike (s): the time hunters get to dodge.")]
        public float AttackWindup = 0.6f;
        public float AttackCooldown = 1.5f;
        public float AttackDamage = 15f;
        [Tooltip("A strike throws the hunter back this fast (m/s).")]
        public float AttackPush = 7f;
        public float ChargeSpeed = 7f;
        [Tooltip("WhenHurt: how long it keeps charging after a hit (s).")]
        public float ProvokeSeconds = 8f;
        [Tooltip("Health regained per second once nobody has hurt it for a while (bosses reset when you run).")]
        public float RegenPerSecond;

        [Header("Boss")]
        [Tooltip("A boss: health bar for everyone nearby, announced when it wakes and falls, and every hunter who hurt it shares the reward.")]
        public bool IsBoss;
        [Tooltip("Shown on the boss bar: 'The Old Bear of the Pines'.")]
        public string BossTitle = "";
        [Tooltip("Coins for every hunter who hurt it, on top of the carcass.")]
        public double BossRewardCoins;
        public double BossRewardTrophies;

        [Header("Presentation")]
        public GameObject Visual;
        public float VisualScale = 1f;
        [Tooltip("Lower-case noun for feedback lines: 'a rabbit'.")]
        public string Noun = "it";
        public Color BloodColor = new Color(0.75f, 0.08f, 0.06f);
        [Tooltip("Colour of the chunks a kill leaves (fur, feathers, wings).")]
        public Color GibColor = new Color(0.62f, 0.5f, 0.38f);
        public int GibCount = 4;
        public PreyVoice Voice = PreyVoice.Squeak;

        [Header("Inventory")]
        [Tooltip("How many fit in one inventory slot (small prey stack, big prey take a slot each).")]
        public int StackSize = 1;

        public PreyStats Stats => new PreyStats
        {
            Allowed = Allowed,
            MaxHealth = MaxHealth,
            CaptureDifficulty = CaptureDifficulty,
            MinKillTier = MinKillTier,
            OverkillRatio = OverkillRatio,
        };

        public PreyEconomy Economy => new PreyEconomy { BaseValue = BaseValue, TrophiesOnKill = TrophiesOnKill };

        public PreyBehavior Behavior => new PreyBehavior
        {
            WalkSpeed = WalkSpeed,
            FleeSpeed = FleeSpeed,
            FleeRadius = FleeRadius * Wariness,
            SightRange = SightRange * Wariness,
            HearingRange = HearingRange,
            CalmDownSeconds = CalmDownSeconds,
            HomeRadius = WanderRadius,
            StunSeconds = StunSeconds,
            HalfExtents = HalfExtents.ToSim(),
            StaminaSeconds = StaminaSeconds,
            TiredSpeed = TiredSpeed,
            Aggression = Aggression,
            AggroRange = AggroRange,
            LeashRadius = LeashRadius,
            AttackRange = AttackRange,
            AttackWindup = AttackWindup,
            AttackCooldown = AttackCooldown,
            ChargeSpeed = ChargeSpeed,
            ProvokeSeconds = ProvokeSeconds,
        };

        private HitCapsule[] _capsules;

        public HitCapsule[] Capsules
        {
            get
            {
                if (_capsules == null || _capsules.Length != Hitboxes.Count)
                {
                    _capsules = new HitCapsule[Hitboxes.Count];
                    for (int i = 0; i < Hitboxes.Count; i++)
                        _capsules[i] = new HitCapsule(Hitboxes[i].A.ToSim(), Hitboxes[i].B.ToSim(), Hitboxes[i].Radius, Hitboxes[i].WeakPoint ? HitRegion.Head : HitRegion.Body);
                }
                return _capsules;
            }
        }

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (MaxHealth <= 0) errors.Add("MaxHealth must be > 0.");
            if (Hitboxes.Count == 0) errors.Add("Needs at least one hitbox.");
            if (BaseValue < 0) errors.Add("BaseValue must be >= 0.");
            if (Visual == null) errors.Add("Visual prefab missing.");
            if (Allowed == 0) errors.Add("Allowed must include Capture, Kill or both.");
            if (Aggression != PreyAggression.None)
            {
                if (Movement != PreyMovement.Ground) errors.Add("Only ground prey can be aggressive.");
                if (AttackRange <= 0f || AttackDamage <= 0f || ChargeSpeed <= 0f) errors.Add("An aggressive prey needs AttackRange, AttackDamage and ChargeSpeed > 0.");
                if (LeashRadius <= AttackRange) errors.Add("LeashRadius must be larger than AttackRange.");
                if (AttackWindup < 0.25f) errors.Add("AttackWindup under 0.25 s can't be dodged.");
            }
            if (IsBoss)
            {
                if (Aggression != PreyAggression.Always) errors.Add("A boss must be Always aggressive.");
                if (string.IsNullOrEmpty(BossTitle)) errors.Add("A boss needs a BossTitle.");
                if ((Allowed & TakedownMethods.Capture) != 0) errors.Add("A boss can't be caught in a net.");
            }
        }
    }

}
