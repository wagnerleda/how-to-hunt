using UnityEngine;

namespace HowToHunt.Travel
{
    /// <summary>Runs the host's region discovery a few times a second (TravelNet.HostTick).</summary>
    public sealed class TravelTicker : MonoBehaviour
    {
        private void Update() => TravelNet.HostTick();
    }
}
