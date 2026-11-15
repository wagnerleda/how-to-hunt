using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Core
{
    public enum ZoneKind : byte
    {
        /// <summary>Entering saves this spot as the player's respawn point on a course.</summary>
        Checkpoint = 0,
        /// <summary>Touching sends the player back to their last checkpoint (pits, water, lava).</summary>
        Hazard = 1,
        /// <summary>Touching moves the player to the target and makes it their checkpoint.</summary>
        Teleport = 2,
    }

    /// <summary>
    /// An axis-aligned trigger box that does one thing (Source-entity style). Level authors place it, size it with
    /// the transform scale, pick a kind, and for teleports drag a target. No code needed.
    /// Zones are evaluated inside the predicted movement tick, so client and host agree and teleports don't snap.
    /// </summary>
    public sealed class Zone : MonoBehaviour
    {
        [SerializeField] private ZoneKind _kind = ZoneKind.Checkpoint;
        [Tooltip("Teleport destination (position + facing). The target's position is where the feet land.")]
        [SerializeField] private Transform _target;

        private static readonly List<Zone> Active = new List<Zone>(32);

        public ZoneKind Kind { get => _kind; set => _kind = value; }
        public Transform Target { get => _target; set => _target = value; }

        /// <summary>World bounds, cached on enable (zones don't move).</summary>
        public Bounds Bounds { get; private set; }

        public static IReadOnlyList<Zone> All => Active;

        private void OnEnable()
        {
            Bounds = new Bounds(transform.position, Abs(transform.lossyScale));
            Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        /// <summary>Feet position and yaw where a player should appear for this zone.</summary>
        public void GetSpawn(out Vector3 feet, out float yaw)
        {
            Transform t = _kind == ZoneKind.Teleport && _target != null ? _target : transform;
            feet = _kind == ZoneKind.Checkpoint ? new Vector3(t.position.x, Bounds.min.y + 0.02f, t.position.z) : t.position;
            yaw = t.eulerAngles.y;
        }

        /// <summary>Does a hull (center + half extents) overlap this zone?</summary>
        public bool Overlaps(Vector3 center, Vector3 halfExtents)
        {
            Bounds b = Bounds;
            return center.x + halfExtents.x > b.min.x && center.x - halfExtents.x < b.max.x &&
                   center.y + halfExtents.y > b.min.y && center.y - halfExtents.y < b.max.y &&
                   center.z + halfExtents.z > b.min.z && center.z - halfExtents.z < b.max.z;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private void OnDrawGizmos()
        {
            Gizmos.color = _kind == ZoneKind.Hazard ? new Color(1f, 0.2f, 0.2f, 0.25f)
                : _kind == ZoneKind.Teleport ? new Color(0.4f, 0.4f, 1f, 0.3f) : new Color(0.2f, 1f, 0.4f, 0.25f);
            Gizmos.DrawCube(transform.position, Abs(transform.lossyScale));
            if (_kind == ZoneKind.Teleport && _target != null) Gizmos.DrawLine(transform.position, _target.position);
        }
    }
}
