using System.Collections.Generic;
using HowToHunt.Sim.Combat;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// A gun's numbers with its fitted attachments applied: what the shot, recoil, reload, zoom and noise really are.
    /// Computed from content (no state), identical on host and client; cached per loadout until attachments change.
    /// </summary>
    public sealed class GunSetup
    {
        public ToolDefinition Tool;
        public GunBallistics Ballistics;
        public int MagazineSize;
        public float ReloadSeconds;
        public float RecoilUp, RecoilSide, RecoilRecover;
        public float AdsZoom;
        public UnityEngine.Vector3 SightOffset;
        public bool ScopeOverlay;
        public float DamageMultiplier = 1f;
        public float NoiseMultiplier = 1f;
        public int Weight;

        public static GunSetup For(ToolDefinition tool, IReadOnlyList<AttachmentDefinition> fitted)
        {
            var s = new GunSetup
            {
                Tool = tool,
                Ballistics = tool.Ballistics,
                MagazineSize = tool.MagazineSize,
                ReloadSeconds = tool.ReloadSeconds,
                RecoilUp = tool.RecoilUp,
                RecoilSide = tool.RecoilSide,
                RecoilRecover = tool.RecoilRecover,
                AdsZoom = tool.AdsZoom,
                SightOffset = tool.SightOffset,
                ScopeOverlay = tool.ScopeOverlay,
                Weight = tool.Weight,
            };
            if (fitted == null) return s;
            for (int i = 0; i < fitted.Count; i++)
            {
                AttachmentDefinition a = fitted[i];
                if (a == null) continue;
                s.RecoilUp *= a.RecoilMultiplier;
                s.RecoilSide *= a.RecoilMultiplier;
                s.Ballistics.SpreadDegrees *= a.SpreadMultiplier;
                s.Ballistics.MaxSpreadDegrees *= a.SpreadMultiplier;
                s.Ballistics.MoveSpreadPerMps *= a.MoveSpreadMultiplier;
                s.Ballistics.AdsSpreadFactor *= a.AdsSpreadMultiplier;
                s.Ballistics.FalloffStart *= a.RangeMultiplier;
                s.Ballistics.FalloffEnd *= a.RangeMultiplier;
                s.Ballistics.BulletSpeed *= a.BulletSpeedMultiplier;
                s.DamageMultiplier *= a.DamageMultiplier;
                s.NoiseMultiplier *= a.NoiseMultiplier;
                s.ReloadSeconds *= a.ReloadMultiplier;
                s.MagazineSize += a.MagazineAdd;
                s.Weight += a.WeightAdd;
                if (a.Slot == AttachmentSlot.Optic)
                {
                    if (a.AdsZoom > 0f) s.AdsZoom = a.AdsZoom;
                    s.ScopeOverlay = a.ScopeOverlay;
                    if (tool.TryAnchor(AttachmentSlot.Optic, out AttachmentAnchor mount)) s.SightOffset = mount.Position + new Vector3(0f, a.SightLift, 0f);
                }
            }
            s.MagazineSize = Mathf.Max(1, s.MagazineSize);
            s.Weight = Mathf.Clamp(s.Weight, 0, 3);
            return s;
        }

        // --- the four bars of the workshop (0-100, higher is better) ------------------------------------------

        public float HipFire => Mathf.Clamp(100f - 9f * Ballistics.SpreadDegrees - 60f * Ballistics.MoveSpreadPerMps, 0f, 100f);
        public float Precision => Mathf.Clamp(30f + Ballistics.FalloffEnd / 4f - 40f * Ballistics.SpreadDegrees * Ballistics.AdsSpreadFactor, 0f, 100f);
        public float Control => Mathf.Clamp(100f - 6f * RecoilUp * (1f - RecoilRecover * 0.5f) - 4f * RecoilSide, 0f, 100f);
        public float Mobility => Mathf.Clamp(100f - 20f * Weight - 6f * ReloadSeconds, 0f, 100f);
        public float RoundsPerMinute => Tool.Cooldown > 0f ? 60f / Tool.Cooldown : 0f;
    }
}
