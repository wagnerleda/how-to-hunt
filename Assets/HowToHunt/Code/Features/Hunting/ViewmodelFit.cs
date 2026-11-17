using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Points measured on a generated gun viewmodel (grip at the origin, barrel along +Z): where bullets leave, where
    /// the eye goes when aiming, and where each attachment slot mounts. Written by the editor's weapon fitting, read by
    /// the content build to fill the tool's offsets; nothing at runtime depends on it.
    /// </summary>
    public sealed class ViewmodelFit : MonoBehaviour
    {
        public Vector3 Muzzle;
        public Vector3 Sight;
        public List<AttachmentAnchor> Anchors = new List<AttachmentAnchor>();
    }
}
