using UnityEngine;

namespace HowToHunt.Player
{
    /// <summary>Where players appear. Place several; players are spread across them. The arrow shows facing.</summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9144f, new Vector3(0.81f, 1.83f, 0.81f));
            Gizmos.DrawRay(transform.position + Vector3.up * 1.6f, transform.forward * 0.8f);
        }
    }
}
