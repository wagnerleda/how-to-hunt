using System;
using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Hunting;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>A hunting tool. Archetype decides how it reaches prey; Mode decides catch or kill.</summary>
    [CreateAssetMenu(menuName = "How to Hunt/Tool", fileName = "Tool_")]
    public sealed class ToolDefinition : ContentDefinition
    {
        public override string KeyPrefix => "tool.";

        public ToolArchetype Archetype;
        public ToolMode Mode;
        public int Tier = 1;
        public float Damage;
        public float CaptureStrength;
        public float HeadshotMultiplier = 2f;
        [Tooltip("Melee reach or firearm range, m.")]
        public float Range = 2.2f;
        [Tooltip("Melee arc, degrees (0 for hitscan).")]
        public float ArcDegrees = 70f;
        [Tooltip("Seconds between uses.")]
        public float Cooldown = 0.5f;
        [Tooltip("Hotbar slot 1-9.")]
        public int Slot = 1;
        [Tooltip("Owned from the start (otherwise bought as an upgrade).")]
        public bool StartOwned = true;
        public GameObject Viewmodel;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (Mode == ToolMode.Capture && CaptureStrength <= 0) errors.Add("Capture tools need CaptureStrength > 0.");
            if (Mode == ToolMode.Kill && Damage <= 0) errors.Add("Kill tools need Damage > 0.");
            if (Range <= 0 || Cooldown <= 0) errors.Add("Range and Cooldown must be > 0.");
            if (Slot < 1 || Slot > 9) errors.Add("Slot must be 1-9.");
        }
    }

}
