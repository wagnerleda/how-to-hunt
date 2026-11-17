using System.Collections.Generic;
using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Shows a gun's fitted attachments (first person, the workshop preview): attachments that are parts of the gun's own
    /// model (an Uzi's suppressor, a SCAR's stock) are shown only while fitted; the others are added as models at the
    /// gun's anchors.
    /// </summary>
    public static class AttachmentVisuals
    {
        private const string ContainerName = "Attachments";

        public static void Apply(GameObject gunModel, ToolDefinition tool, IReadOnlyList<AttachmentDefinition> fitted)
        {
            if (gunModel == null || tool == null) return;
            // Built-in parts: visible only when their attachment is fitted.
            foreach (AttachmentDefinition a in ContentRegistry.All<AttachmentDefinition>())
            {
                if (a.PartNames == null || a.PartNames.Length == 0 || !a.Fits(tool)) continue;
                bool on = Contains(fitted, a);
                foreach (string part in a.PartNames)
                {
                    Transform t = Find(gunModel.transform, part);
                    if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
                }
            }
            Transform old = gunModel.transform.Find(ContainerName);
            if (old != null) Object.Destroy(old.gameObject);
            if (fitted == null || fitted.Count == 0) return;
            var container = new GameObject(ContainerName).transform;
            container.SetParent(gunModel.transform, false);
            for (int i = 0; i < fitted.Count; i++)
            {
                AttachmentDefinition a = fitted[i];
                if (a == null || a.Visual == null || (a.PartNames != null && a.PartNames.Length > 0)) continue;
                if (!tool.TryAnchor(a.Slot, out AttachmentAnchor anchor)) continue;
                GameObject v = Object.Instantiate(a.Visual, container, false);
                v.transform.localPosition = anchor.Position;
                v.transform.localRotation = Quaternion.Euler(anchor.Euler);
                foreach (Renderer r in v.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Where a fitted laser sits (gun space), if the gun has one.</summary>
        public static bool TryLaser(ToolDefinition tool, IReadOnlyList<AttachmentDefinition> fitted, out Vector3 origin)
        {
            origin = Vector3.zero;
            if (fitted == null) return false;
            for (int i = 0; i < fitted.Count; i++)
            {
                if (fitted[i] == null || !fitted[i].Laser || !tool.TryAnchor(fitted[i].Slot, out AttachmentAnchor a)) continue;
                origin = a.Position + new Vector3(0f, -0.018f, 0.035f); // the lens at the front of the laser box
                return true;
            }
            return false;
        }

        private static bool Contains(IReadOnlyList<AttachmentDefinition> list, AttachmentDefinition a)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i] == a) return true;
            return false;
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform f = Find(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
