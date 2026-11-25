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

        // The mask: a shell over the face (owner, 2026-11-25: the painting sits on the 3D shape), a little bigger than
        // the head, with a nose and a brow, held by a strap round the back of the head.
        public static readonly Vector3 MaskOffset = new Vector3(0f, 0.01f, 0.165f); // the mask's origin, just in front of the face (head radius 0.15)
        public const float MaskHeight = 0.35f;
        public const float MaskRadius = 0.162f;          // round the head, at the eyes
        public const float MaskVerticalRadius = 0.24f;   // how the shell curls back over the brow and under the chin
        public const float MaskSpan = 58f;               // degrees each side of the nose
        public const float NoseDepth = 0.022f, NoseWidth = 0.018f, NoseTop = 0.035f, NoseTip = -0.045f;
        public const float BrowHeight = 0.006f, BrowY = 0.065f;
        public const float StrapRadius = 0.154f, StrapWidth = 0.022f, StrapY = 0.025f, StrapFrom = 52f;
        /// <summary>The head's centre seen from the mask's origin.</summary>
        public static Vector3 HeadFromMask => -MaskOffset;
    }
}
