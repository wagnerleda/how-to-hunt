using System;
using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Hunting;
using UnityEngine;

namespace HowToHunt.Hunting
{
    public enum PreyMovement
    {
        /// <summary>Deterministic flight from seed + tick (insects): no movement packets at all.</summary>
        Flyer,
        /// <summary>Host-simulated AI (idle, wander, flee), sent as compact snapshots.</summary>
        Ground,
    }

    /// <summary>The sound a prey makes when hit (synthesized, see PreySounds).</summary>
    public enum PreyVoice { Squeak, Flutter, Grunt, Roar }

    [Serializable]
    public struct HitboxShape
    {
        public Vector3 A;
        public Vector3 B;
        public float Radius;
        [Tooltip("Head or other weak point: headshot multiplier applies.")]
        public bool WeakPoint;
    }

}
