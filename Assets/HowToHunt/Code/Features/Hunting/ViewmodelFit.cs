using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>What a moving part of a gun does (see <see cref="GunRig"/>).</summary>
    public enum RigPartKind : byte
    {
        Magazine,       // drops out and goes back in during a reload
        Slide,          // snaps back on every shot (pistols)
        Bolt,           // bolt action: up, back, forward, down after each shot
        ChargingHandle, // pulled back at the end of a reload
        Cylinder,       // turns one chamber per shot, spins on reload (revolvers)
        Hinge,          // swings open for a reload (break-action, launcher head)
        LeafPort,       // opens to the left while charging (the Gunlance's cannon)
        LeafStarboard,  // opens to the right while charging
        BreakOpen,      // break action: the barrels swing down about their own origin (the hinge) for the reload
        Ejector,        // rides on the barrels and lifts the shells as they open
        Shell,          // a shell in a chamber: gone while the barrels are open and empty, back when reloaded
        Latch,          // the top lever that unlocks the barrels before they open
    }

    /// <summary>A named part of the viewmodel and the point it moves about (prefab space).</summary>
    [System.Serializable]
    public struct RigPart
    {
        public string Name;
        public RigPartKind Kind;
        public Vector3 Pivot;
    }

    /// <summary>
    /// Points measured on a generated gun viewmodel (grip at the origin, barrel along +Z): where bullets leave, the
    /// sight line (rear and front sight tops: aiming tilts the gun so they line up with the eye), where each
    /// attachment slot mounts, and the parts that animate. Written by the editor's weapon fitting, read by the
    /// content build (offsets) and by <see cref="GunRig"/> (animation).
    /// </summary>
    public sealed class ViewmodelFit : MonoBehaviour
    {
        public Vector3 Muzzle;
        public Vector3 Sight;
        public Vector3 SightFront;
        public List<AttachmentAnchor> Anchors = new List<AttachmentAnchor>();
        public List<RigPart> Parts = new List<RigPart>();
        /// <summary>Iron sight parts standing on the rail: folded away (hidden) while an optic is fitted.</summary>
        public List<string> IronSights = new List<string>();
    }
}
