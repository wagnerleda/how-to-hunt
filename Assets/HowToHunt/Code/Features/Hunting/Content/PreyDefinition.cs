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
        public float FleeRadius = 7f;
        public float CalmDownSeconds = 2.5f;
        public float StunSeconds = 0.6f;
        [Tooltip("Seconds of full-speed flight before it tires (0 = never). Sprint above run speed, tire below it.")]
        public float StaminaSeconds;
        public float TiredSpeed = 4f;
        public Vector3 HalfExtents = new Vector3(0.2f, 0.18f, 0.25f);

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
            FleeRadius = FleeRadius,
            CalmDownSeconds = CalmDownSeconds,
            HomeRadius = 18f,
            StunSeconds = StunSeconds,
            HalfExtents = HalfExtents.ToSim(),
            StaminaSeconds = StaminaSeconds,
            TiredSpeed = TiredSpeed,
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
        }
    }

}
