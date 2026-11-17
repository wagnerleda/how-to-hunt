using HowToHunt.Hunting;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Gun handling presets taken from How to Fish's weapons (its pistol, SMG, assault rifle, shotgun and sniper rifle
    /// prefabs: model recoil, recoil springs, free-aim box, knockback, sprint pose, reload refill point).
    /// </summary>
    public static class Handling
    {
        public static GunHandling Pistol() => new GunHandling
        {
            KickPos = new Vector3(0f, 0.05f, 0.5f), KickRot = new Vector2(15f, 15f),
            SpringPos = 2500f, DamperPos = 1f, SpringRot = 5000f, DamperRot = 100f,
            AdsSpring = 3f, AdsKickPos = 1f, AdsKickRot = 1f,
            FreeAim = new Vector3(2f, 8f, 5f), FreeAimSpeed = 0.25f,
            SprintPos = new Vector3(-0.1f, -0.1f, 0f), SprintRot = new Vector3(30f, -35f, 25f), SprintSeconds = 0.15f,
            StrafeTilt = -10f, RefillAt = 0.8f, TacticalReload = 0.85f,
        };

        /// <summary>
        /// The hand cannon: a heavy, slow shove back in the hands and a blast that throws you 14 m/s backwards along
        /// your view (fire it at the ground to fly: 20 m/s). Too heavy to sway much; one ball, so no tactical reload.
        /// </summary>
        public static GunHandling Cannon() => new GunHandling
        {
            KickPos = new Vector3(0.08f, 0.2f, 0.9f), KickRot = new Vector2(14f, 30f),
            SpringPos = 400f, DamperPos = 1f, SpringRot = 600f, DamperRot = 40f,
            AdsSpring = 1f, AdsKickPos = 1f, AdsKickRot = 1f,
            FreeAim = new Vector3(1f, 3f, 2f), FreeAimSpeed = 0.15f, Push = 20f,
            SprintPos = new Vector3(-0.1f, -0.15f, 0f), SprintRot = new Vector3(15f, -30f, 15f), SprintSeconds = 0.3f,
            StrafeTilt = -4f, RefillAt = 0.85f, TacticalReload = 1f,
            // Loaded from the muzzle: tipped back, muzzle up into view, while the hand rams the charge home.
            ReloadPos = new Vector3(-0.04f, 0.02f, -0.06f), ReloadRot = new Vector3(-38f, -12f, 6f),
        };

        public static GunHandling Smg() => new GunHandling
        {
            KickPos = new Vector3(0.1f, 0.1f, 0.5f), KickRot = new Vector2(6f, 6f),
            SpringPos = 20000f, DamperPos = 0f, SpringRot = 1000f, DamperRot = 0f,
            AdsSpring = 2f, AdsKickPos = 0.5f, AdsKickRot = 1f,
            FreeAim = new Vector3(2f, 8f, 5f), FreeAimSpeed = 0.25f,
            SprintPos = new Vector3(-0.2f, -0.1f, -0.1f), SprintRot = new Vector3(20f, -35f, 25f), SprintSeconds = 0.15f,
            StrafeTilt = -10f, RefillAt = 0.9f, TacticalReload = 0.85f,
        };

        public static GunHandling Rifle() => new GunHandling
        {
            KickPos = new Vector3(0.03f, 0.03f, 0.4f), KickRot = new Vector2(5f, 2f),
            SpringPos = 10000f, DamperPos = 0f, SpringRot = 1000f, DamperRot = 0f,
            AdsSpring = 2f, AdsKickPos = 0.75f, AdsKickRot = 1f,
            FreeAim = new Vector3(1f, 4f, 3f), FreeAimSpeed = 0.25f,
            SprintPos = new Vector3(-0.1f, -0.1f, 0f), SprintRot = new Vector3(20f, -45f, 25f), SprintSeconds = 0.2f,
            StrafeTilt = -5f, RefillAt = 0.8f, TacticalReload = 0.85f,
        };

        public static GunHandling Shotgun(float push = 5f) => new GunHandling
        {
            KickPos = new Vector3(0.1f, 0.1f, 0.3f), KickRot = new Vector2(25f, 30f),
            SpringPos = 500f, DamperPos = 1f, SpringRot = 500f, DamperRot = 25f,
            AdsSpring = 1f, AdsKickPos = 1f, AdsKickRot = 1f,
            FreeAim = new Vector3(2f, 8f, 5f), FreeAimSpeed = 0.25f, Push = push,
            SprintPos = new Vector3(-0.1f, -0.1f, 0f), SprintRot = new Vector3(20f, -45f, 25f), SprintSeconds = 0.15f,
            StrafeTilt = -10f, RefillAt = 0.75f, TacticalReload = 0.85f,
        };

        public static GunHandling Sniper(float push = 0f) => new GunHandling
        {
            KickPos = new Vector3(0.1f, 0.05f, 0.3f), KickRot = new Vector2(10f, 5f),
            SpringPos = 500f, DamperPos = 1f, SpringRot = 500f, DamperRot = 100f,
            AdsSpring = 1f, AdsKickPos = 1f, AdsKickRot = 1f,
            FreeAim = new Vector3(2f, 6f, 2f), FreeAimSpeed = 0.25f, Push = push,
            SprintPos = new Vector3(-0.15f, -0.2f, 0f), SprintRot = new Vector3(25f, -35f, 25f), SprintSeconds = 0.25f,
            StrafeTilt = -5f, RefillAt = 0.75f, TacticalReload = 0.85f,
        };
    }
}
