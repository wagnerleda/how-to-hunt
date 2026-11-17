using UnityEngine;

namespace HowToHunt.Avatar
{
    /// <summary>The stick figure's proportions (figure space: hull centre, +Z forward), shared by the editor builder,
    /// the animation and the mask preview.</summary>
    public static class AvatarShape
    {
        public const float HeadSize = 0.3f;
        public static readonly Vector3 HeadCenter = new Vector3(0f, 0.7f, 0f);
        public static readonly Vector3 NeckCenter = new Vector3(0f, 0.53f, 0f);
        public static readonly Vector3 NeckScale = new Vector3(0.06f, 0.05f, 0.06f);
        public static readonly Vector3 TorsoCenter = new Vector3(0f, 0.2f, 0f);
        public static readonly Vector3 TorsoScale = new Vector3(0.4f, 0.3f, 0.24f);   // a capsule: 0.6 m tall
        public static readonly Vector3 LeafCenter = new Vector3(0f, -0.1f, 0.125f);
        public const float LeafLength = 0.24f, LeafWidth = 0.19f;
        public const float HandSize = 0.11f, FootSize = 0.12f;

        // The mask: a curved plate over the face, a little bigger than the head.
        public static readonly Vector3 MaskOffset = new Vector3(0f, 0.01f, 0.165f); // just in front of the face (head radius 0.15)
        public const float MaskWidth = 0.3f, MaskHeight = 0.35f, MaskCurve = 0.2f;
    }
}
