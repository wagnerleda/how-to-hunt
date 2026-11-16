using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>Puts the fitted attachments' models on a gun model (first person, the workshop preview), at the gun's anchors.</summary>
    public static class AttachmentVisuals
    {
        private const string ContainerName = "Attachments";

        public static void Apply(GameObject gunModel, ToolDefinition tool, IReadOnlyList<AttachmentDefinition> fitted)
        {
            if (gunModel == null || tool == null) return;
            Transform old = gunModel.transform.Find(ContainerName);
            if (old != null) Object.Destroy(old.gameObject);
            if (fitted == null || fitted.Count == 0) return;
            var container = new GameObject(ContainerName).transform;
            container.SetParent(gunModel.transform, false);
            for (int i = 0; i < fitted.Count; i++)
            {
                AttachmentDefinition a = fitted[i];
                if (a == null || a.Visual == null || !tool.TryAnchor(a.Slot, out AttachmentAnchor anchor)) continue;
                GameObject v = Object.Instantiate(a.Visual, container, false);
                v.transform.localPosition = anchor.Position;
                v.transform.localRotation = Quaternion.Euler(anchor.Euler);
                foreach (Renderer r in v.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
