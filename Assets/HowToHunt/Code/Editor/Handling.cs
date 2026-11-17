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

        /// <summary>The pistol, heavier: a bigger throw and slower springs.</summary>
        public static GunHandling HandCannon()
        {
            GunHandling h = Pistol();
            h.KickRot = new Vector2(20f, 25f);
            h.SpringPos = 1500f;
            h.SpringRot = 2500f;
            h.DamperRot = 60f;
            h.Push = 1.5f;
            return h;
        }

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
