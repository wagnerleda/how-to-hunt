using HowToHunt.Core;
using HowToHunt.Net;
using UnityEngine;

namespace HowToHunt.Economy
{
    /// <summary>
    /// Draws world items each frame (one loop for all): interpolated between ticks, tumbling while in the air, lying
    /// in the same pose and at the same yaw their collision box was measured in (ItemShapes), so what you see is what
    /// collides and what you pick up. Tools on the ground show without the hand.
    /// </summary>
    public sealed class WorldItemRenderer : MonoBehaviour
    {
        public static void Create(WorldItem w)
        {
            ItemShapes.Shape shape = ItemShapes.Get(w.Item);
            var root = new GameObject($"Item {w.Id}");
            if (shape.Prefab != null)
            {
                GameObject model = Instantiate(shape.Prefab, root.transform, false);
                if (shape.HideHand) IconRenderer.HideParts(model, "Hand");
                foreach (Animator anim in model.GetComponentsInChildren<Animator>())
                    anim.enabled = w.Item.Captured; // a live catch keeps flapping; carcasses and tools don't
                model.transform.localScale = Vector3.one * shape.Scale;
                model.transform.localRotation = shape.Lie;
                // Put the model's centre on the body's centre.
                model.transform.localPosition = -(shape.Lie * (shape.LocalBounds.center * shape.Scale));
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
            Quaternion yaw = Quaternion.Euler(0f, ItemShapes.Yaw(w.Id), 0f);
            Quaternion rot = w.Body.Resting || w.Body.Spin == 0f ? yaw : yaw * Quaternion.Euler(w.Body.Spin, 0f, w.Body.Spin * 0.4f);
            w.Visual.transform.SetPositionAndRotation(p, rot);
        }
    }
}
