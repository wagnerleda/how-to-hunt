using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>Places every prey visual once per frame (one loop for all prey instead of one Update each).</summary>
    public sealed class PreyRenderDriver : MonoBehaviour
    {
        private void LateUpdate() => PreyVisual.RenderAll();
    }
}
