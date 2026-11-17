using System;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// How a gun handles, modelled on How to Fish's weapons: a kick that throws the gun model back and up and snaps
    /// back on springs, a free-aim box the gun drifts inside while firing from the hip, a shove backwards, the sprint
    /// pose and the reload timing. The view kick itself is ToolDefinition.RecoilUp / RecoilSide.
    /// </summary>
    [Serializable]
    public struct GunHandling
    {
        [Tooltip("Model kick per shot, metres: X random sideways, Y up, Z back.")]
        public Vector3 KickPos;
        [Tooltip("Model kick per shot, degrees: X random yaw and roll, Y muzzle up.")]
        public Vector2 KickRot;
        [Tooltip("Springs pulling the kicked gun back (position, rotation) and their dampers.")]
        public float SpringPos, DamperPos, SpringRot, DamperRot;
        [Tooltip("Aiming: the springs are this much stiffer (the gun settles faster).")]
        public float AdsSpring;
        [Tooltip("Aiming: model kick multipliers (position, rotation).")]
        public float AdsKickPos, AdsKickRot;
        [Tooltip("Free-aim box from the hip, degrees: pitch, yaw, roll. The gun drifts inside it when you turn and fire; bullets follow the barrel.")]
        public Vector3 FreeAim;
        [Tooltip("How much of your turning moves the gun inside the box.")]
        public float FreeAimSpeed;
        [Tooltip("Shove backwards along your view per shot, m/s.")]
        public float Push;
        [Tooltip("Sprinting pose: offset (m) and rotation (degrees), eased in over SprintSeconds.")]
        public Vector3 SprintPos, SprintRot;
        public float SprintSeconds;
        [Tooltip("Roll when strafing, degrees (negative leans into the strafe).")]
        public float StrafeTilt;
        [Tooltip("Reload progress (0..1) at which the new magazine counts as in.")]
        [Range(0f, 1f)] public float RefillAt;
        [Tooltip("Reload time multiplier with rounds still in the gun (no rack at the end).")]
        public float TacticalReload;

        public bool IsSet => SpringPos > 0f;
    }
}
