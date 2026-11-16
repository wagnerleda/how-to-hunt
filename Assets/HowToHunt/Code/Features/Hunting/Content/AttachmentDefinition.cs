using System.Collections.Generic;
using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>Where an attachment goes on a gun (one per slot).</summary>
    public enum AttachmentSlot : byte { Muzzle, Barrel, Underbarrel, Magazine, Ammo, Optic, Top, Stock }

    /// <summary>
    /// A gun attachment sold at the Gunsmith (Phase 5). It multiplies or adds to the gun's numbers; several fitted
    /// attachments stack. Defaults change nothing, so a definition only states what it improves and what it costs.
    /// </summary>
    [CreateAssetMenu(menuName = "How to Hunt/Attachment", fileName = "Attachment_")]
    public sealed class AttachmentDefinition : ContentDefinition
    {
        public override string KeyPrefix => "attachment.";

        public AttachmentSlot Slot;
        [Tooltip("Guns it fits. Empty: every firearm.")]
        public List<ToolDefinition> Tools = new List<ToolDefinition>();
        public double CostCoins = 300;
        public int SortOrder;

        [Header("Effects (1 = unchanged)")]
        [Tooltip("Kick up and sideways.")]
        public float RecoilMultiplier = 1f;
        [Tooltip("Hip-fire cone at rest.")]
        public float SpreadMultiplier = 1f;
        [Tooltip("Extra cone from moving.")]
        public float MoveSpreadMultiplier = 1f;
        [Tooltip("Cone while aiming down sights.")]
        public float AdsSpreadMultiplier = 1f;
        [Tooltip("Falloff distances (longer = keeps damage further).")]
        public float RangeMultiplier = 1f;
        public float DamageMultiplier = 1f;
        public float BulletSpeedMultiplier = 1f;
        [Tooltip("How far prey hear the shot (a suppressor is well below 1).")]
        public float NoiseMultiplier = 1f;
        public float ReloadMultiplier = 1f;
        public int MagazineAdd;
        [Tooltip("Optic: zoom while aiming (0 = keep the gun's).")]
        public float AdsZoom;
        [Tooltip("Optic: look through a scope picture instead of iron sights.")]
        public bool ScopeOverlay;
        [Tooltip("Optic: the eye point sits this far above the optic's mount (aiming down sights looks through it).")]
        public float SightLift = 0.03f;
        [Tooltip("Weight classes added (heavy stocks, long barrels).")]
        public int WeightAdd;

        [Header("Look")]
        [Tooltip("Model shown on the gun (attached at the gun's anchor for this slot). Optional.")]
        public GameObject Visual;

        public bool Fits(ToolDefinition tool)
        {
            if (tool == null || tool.Archetype != Sim.Hunting.ToolArchetype.Firearm) return false;
            return (Tools.Count == 0 || Tools.Contains(tool)) && tool.TryAnchor(Slot, out _);
        }

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (CostCoins <= 0) errors.Add("Attachments need a price.");
            if (RecoilMultiplier <= 0 || SpreadMultiplier <= 0 || RangeMultiplier <= 0 || DamageMultiplier <= 0 || BulletSpeedMultiplier <= 0 || ReloadMultiplier <= 0)
                errors.Add("Multipliers must be > 0.");
        }
    }
}
