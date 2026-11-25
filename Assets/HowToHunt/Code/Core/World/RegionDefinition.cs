using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// A section of the world (key "region.plains"): a map with a camp and a sacred totem. Hunters discover it by going
    /// there; from then on any totem can take them to it (owner, 2026-11-25). Order sorts the totem's menu.
    /// </summary>
    [CreateAssetMenu(menuName = "How to Hunt/Region", fileName = "Region_New")]
    public sealed class RegionDefinition : ContentDefinition
    {
        public override string KeyPrefix => "region.";

        [Tooltip("Place in the totem's list (the way the world unfolds: lower first).")]
        public int Order;
        [Tooltip("Shown under the name in the totem's list.")]
        public string Tagline = "";
        [Tooltip("The colour of its row in the totem's list.")]
        public Color Tint = new Color(0.9f, 0.75f, 0.4f);
        [Tooltip("Known from the start (the starting camp and the lab), without having to go there.")]
        public bool KnownFromStart;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (string.IsNullOrWhiteSpace(Tagline)) errors.Add("A region needs a tagline for the totem's list.");
        }
    }
}
