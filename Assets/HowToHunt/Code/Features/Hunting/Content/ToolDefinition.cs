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
        [Tooltip("Prey one swing can reach (nets scoop several; knives and guns hit one).")]
        public int MaxTargets = 1;
        [Tooltip("Shove given to prey and items it hits, m/s.")]
        public float Knockback;
        [Tooltip("Firearms: degrees the view kicks up per shot. It stays up: you pull it back down (How to Fish).")]
        public float RecoilUp;
        [Tooltip("Firearms: largest random sideways view kick per shot, degrees.")]
        public float RecoilSide;
        [Tooltip("Firearms: fraction of the view kick that comes back down by itself (0: none, you pull it down).")]
        [Range(0f, 1f)] public float RecoilRecover;
        [Tooltip("Firearms: kick springs, free aim, shove, sprint pose and reload timing.")]
        public GunHandling Handling;
        [Header("Firearm")]
        [Tooltip("Rounds per magazine. Ammo is infinite; magazines are not (reload with R).")]
        public int MagazineSize = 6;
        public float ReloadSeconds = 2f;
        [Tooltip("Holds the trigger to keep firing (SMGs, rifles); otherwise one shot per click.")]
        public bool Automatic;
        [Tooltip("Muzzle speed, m/s. Bullets fly (tracers), they are not instant.")]
        public float BulletSpeed = 380f;
        [Tooltip("Bullet drop, m/s².")]
        public float BulletGravity = 4f;
        [Tooltip("Full damage up to this distance (m), falling to MinDamageFraction at FalloffEnd.")]
        public float FalloffStart = 25f;
        public float FalloffEnd = 70f;
        [Range(0f, 1f)] public float MinDamageFraction = 0.5f;
        [Tooltip("Hip-fire cone half-angle at rest, degrees.")]
        public float SpreadDegrees = 1f;
        [Tooltip("Extra cone per m/s of your speed. 0 for a scout rifle that stays accurate on the move.")]
        public float MoveSpreadPerMps = 0.35f;
        public float MaxSpreadDegrees = 6f;
        [Tooltip("Cone multiplier while aiming down sights.")]
        [Range(0f, 1f)] public float AdsSpreadFactor = 0.3f;
        [Tooltip("Optical zoom while aiming down sights (1 = iron sights, no zoom).")]
        public float AdsZoom = 1.2f;
        [Tooltip("Model space: the sight point that lines up with your eye when aiming down sights.")]
        public Vector3 SightOffset = new Vector3(0f, 0.09f, 0.05f);
        [Tooltip("Model space: where bullets leave (end of the barrel). They fly along the model's +Z.")]
        public Vector3 MuzzleOffset = new Vector3(0f, 0.07f, 0.26f);
        [Tooltip("Model space: the front sight top. Aiming tilts the gun so rear (SightOffset) and front line up. Zero: keep the gun level.")]
        public Vector3 SightFront;
        [Tooltip("Aiming down sights looks through a scope (black ring, reticle; the model hides) instead of iron sights.")]
        public bool ScopeOverlay;
        [Tooltip("Weight class 0-3: heavy weapons slow you while held (sv_load_slow per class). Light tools are 0.")]
        [Range(0, 3)] public int Weight;
        [Tooltip("Pellets per shot (shotguns). Each pellet deals Damage.")]
        public int Pellets = 1;
        [Tooltip("Cone the pellets spread over, degrees.")]
        public float PelletSpreadDegrees = 5f;
        [Tooltip("Explosive rounds (grenade launcher): blast radius, metres. 0 = a normal bullet. Damage falls off to the edge.")]
        public float ExplosionRadius;
        [Tooltip("Charge weapons (the Gunlance): hold Fire this long to charge, release to fire. 0 = fires on press.")]
        public float ChargeSeconds;
        [Tooltip("Explosive rounds that burst into this many smaller explosive orbs (cluster).")]
        public int ClusterCount;
        public float ClusterRadius = 2.3f;
        public float ClusterDamage = 40f;
        [Tooltip("The round is a glowing plasma orb (drawn as an orb, explodes in cyan) instead of a bullet.")]
        public bool PlasmaOrb;

        public GunBallistics Ballistics => new GunBallistics
        {
            BulletSpeed = BulletSpeed,
            BulletGravity = BulletGravity,
            FalloffStart = FalloffStart,
            FalloffEnd = FalloffEnd,
            MinDamageFraction = MinDamageFraction,
            SpreadDegrees = SpreadDegrees,
            MoveSpreadPerMps = MoveSpreadPerMps,
            MaxSpreadDegrees = MaxSpreadDegrees,
            AdsSpreadFactor = AdsSpreadFactor,
        };

        [Header("Attachments (Phase 5)")]
        [Tooltip("Where each attachment slot mounts on the model (model space, pointing +Z). Slots without one are locked on this gun.")]
        public List<AttachmentAnchor> AttachmentAnchors = new List<AttachmentAnchor>();

        public bool TryAnchor(AttachmentSlot slot, out AttachmentAnchor anchor)
        {
            for (int i = 0; i < AttachmentAnchors.Count; i++)
                if (AttachmentAnchors[i].Slot == slot) { anchor = AttachmentAnchors[i]; return true; }
            anchor = default;
            return false;
        }

        [Header("Sounds (linked by the content build from Art/Weapons/<name>/: fire*, reload*, dry*, equip*, inspect*)")]
        [Tooltip("One is picked at random per shot. Empty: the shared default shot.")]
        public AudioClip[] FireSounds = Array.Empty<AudioClip>();
        public AudioClip ReloadSound;
        public AudioClip DryFireSound;
        public AudioClip EquipSound;
        public AudioClip InspectSound;

        /// <summary>A fire sound for this shot (null: use the default).</summary>
        public AudioClip PickFireSound() => FireSounds != null && FireSounds.Length > 0 ? FireSounds[UnityEngine.Random.Range(0, FireSounds.Length)] : null;

        [Header("World item (computed by the content build)")]
        [Tooltip("How it lies when dropped: its natural resting face (lowest centre of mass), and its bounds in that pose.")]
        public Vector3 RestEuler;
        public Vector3 RestCenter;
        public Vector3 RestSize;

        [Header("Use")]
        [Tooltip("Seconds between uses.")]
        public float Cooldown = 0.5f;
        [Tooltip("Hotbar slot 1-9.")]
        public int Slot = 1;
        [Tooltip("Owned from the start (otherwise bought as an upgrade).")]
        public bool StartOwned = true;
        [Tooltip("First-person model: the tool and the hand holding it, grip at the origin, pointing +Z.")]
        public GameObject Viewmodel;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (Mode == ToolMode.Capture && CaptureStrength <= 0) errors.Add("Capture tools need CaptureStrength > 0.");
            if (Mode == ToolMode.Kill && Damage <= 0) errors.Add("Kill tools need Damage > 0.");
            if (Range <= 0 || Cooldown <= 0) errors.Add("Range and Cooldown must be > 0.");
            if (Slot < 1 || Slot > 9) errors.Add("Slot must be 1-9.");
            if (Archetype == ToolArchetype.Firearm && (MagazineSize <= 0 || ReloadSeconds <= 0 || BulletSpeed <= 0))
                errors.Add("Firearms need MagazineSize, ReloadSeconds and BulletSpeed > 0.");
            if (Archetype == ToolArchetype.Firearm && !Handling.IsSet) errors.Add("Firearms need Handling (kick springs and the rest).");
        }
    }

}

namespace HowToHunt.Hunting
{
    /// <summary>A mount point for one attachment slot on a gun model.</summary>
    [System.Serializable]
    public struct AttachmentAnchor
    {
        public AttachmentSlot Slot;
        public UnityEngine.Vector3 Position;
        public UnityEngine.Vector3 Euler;
    }
}
