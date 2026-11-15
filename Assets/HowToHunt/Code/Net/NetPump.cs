using UnityEngine;

namespace HowToHunt.Net
{
    /// <summary>Drives per-frame session housekeeping (timeouts).</summary>
    public sealed class NetPump : MonoBehaviour
    {
        private void Update() => NetSession.Update();
    }
}
