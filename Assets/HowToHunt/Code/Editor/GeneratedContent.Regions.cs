using HowToHunt.Core;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The world's regions (owner, 2026-11-25): each has a camp totem that takes you to any region you have been to.
    /// Keys are permanent (profiles save the ones a hunter discovered). Order follows the way the world unfolds.
    /// </summary>
    public static partial class GeneratedContent
    {
        public const string RegionLab = "region.lab", RegionPlains = "region.plains", RegionForest = "region.pine_forest", RegionDesert = "region.red_desert";

        private static void BuildRegions()
        {
            GeneratedAssets.EnsureFolder(ContentDir + "/Regions");
            Region("lab", "Movement Lab", "Courses, the full Armory and test pads.", 0, new Color(0.55f, 0.75f, 0.95f), knownFromStart: true);
            Region("plains", "The Plains", "The first camp: Tomas, Wren, Gus and the river rabbits.", 1, new Color(0.55f, 0.85f, 0.4f), knownFromStart: true);
            Region("pine_forest", "Pine Forest", "Ash's camp among the pines, deer, boars and the Old Bear.", 2, new Color(0.3f, 0.6f, 0.4f));
            Region("red_desert", "Red Desert", "Dry Creek: Rosa's trading post, snakes and scorpions.", 3, new Color(0.9f, 0.45f, 0.25f));
        }

        private static RegionDefinition Region(string id, string name, string tagline, int order, Color tint, bool knownFromStart = false) =>
            Asset<RegionDefinition>("Regions/Region_" + id, "region." + id, name, tagline, r =>
            {
                r.Order = order;
                r.Tagline = tagline;
                r.Tint = tint;
                r.KnownFromStart = knownFromStart;
            });
    }
}
