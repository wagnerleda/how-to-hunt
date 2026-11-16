using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Hunting;
using HowToHunt.Net;
using HowToHunt.Sim.Hunting;
using UnityEngine;

namespace HowToHunt.Economy
{
    /// <summary>
    /// Draws world items each frame (one loop for all): interpolated between ticks, tumbling while in the air.
    /// Carcasses lie on their side; live catches that fell out of a full inventory sit upright.
    /// </summary>
    public sealed class WorldItemRenderer : MonoBehaviour
    {
        public static void Create(WorldItem w)
        {
            PreyDefinition def = ContentRegistry.Get<PreyDefinition>(new ContentId(w.Item.Content));
            var root = new GameObject($"Item {w.Id}");
            if (def != null && def.Visual != null)
            {
                GameObject model = Instantiate(def.Visual, root.transform, false);
                model.transform.localScale = Vector3.one * def.VisualScale;
                Animator anim = model.GetComponentInChildren<Animator>();
                if (anim != null && !w.Item.Captured) anim.enabled = false; // carcasses don't flap
                // Pivot at the body's centre: models have their pivot at the feet.
                model.transform.localPosition = new Vector3(0f, -w.HalfExtents.Y, 0f);
            }
            w.Visual = root;
            Place(w, 0f);
        }

        private void LateUpdate()
        {
            if (NetSession.Manager == null) return;
            float t = (float)NetSession.Manager.TimeManager.GetTickPercentAsDouble();
            var all = WorldItems.All;
            for (int i = 0; i < all.Count; i++) Place(all[i], t);
        }

        private static void Place(WorldItem w, float t)
        {
            if (w.Visual == null) return;
            Vector3 p = Vector3.LerpUnclamped(w.PreviousPosition.ToUnity(), w.Body.Position.ToUnity(), t);
            float yaw = (w.Id * 137.5f) % 360f;
            Quaternion lie = w.Item.Captured ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f) * (w.Body.Resting ? lie : Quaternion.Euler(w.Body.Spin, 0f, w.Body.Spin * 0.4f) * lie);
            w.Visual.transform.SetPositionAndRotation(p, rot);
        }
    }
}
