using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Hunting;
using HowToHunt.Sim.Hunting;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// The Plains slice content (prey, tools, upgrades) as ScriptableObjects under Resources/Content, written from
    /// code so numbers are reviewable in diffs. Re-running updates values in place; keys and asset GUIDs stay.
    /// Tuning rationale: docs/DESIGN.md (Plains economy). Pacing is checked by EconomyPacingTests.
    /// </summary>
    public static class GeneratedContent
    {
        public const string ContentDir = GeneratedAssets.ResourcesDir + "/Content";
        private const string ModelsDir = GeneratedAssets.Root + "/Art/Models";

        [MenuItem("How to Hunt/Rebuild Content", priority = 2)]
        public static void Build()
        {
            GeneratedAssets.EnsureFolder(ContentDir + "/Prey");
            GeneratedAssets.EnsureFolder(ContentDir + "/Tools");
            GeneratedAssets.EnsureFolder(ContentDir + "/Upgrades");

            var butterflyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssets.ButterflyPrefabPath);
            var rabbitModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/Rabbit/Rabbit.fbx");

            Asset<PreyDefinition>("Prey/Prey_Butterfly", "prey.butterfly", "Meadow Butterfly",
                "Drifts between flowers. Easy to net; a blade does the job too, but a bullet leaves nothing worth selling.", p =>
            {
                p.Allowed = TakedownMethods.Either;
                p.MaxHealth = 4f;
                p.CaptureDifficulty = 1f;
                p.MinKillTier = 0;
                p.OverkillRatio = 2.5f;
                p.Hitboxes = new List<HitboxShape> { new HitboxShape { A = new Vector3(0f, -0.05f, 0f), B = new Vector3(0f, 0.05f, 0f), Radius = 0.22f } };
                p.BaseValue = 2; // Phase 3 rebalance: nets catch several per sweep, so each pays less
                p.TrophiesOnKill = 0;
                p.Movement = PreyMovement.Flyer;
                p.Visual = butterflyPrefab;
                p.VisualScale = 1.5f;
                p.Noun = "a butterfly";
                p.BloodColor = new Color(0.72f, 0.82f, 0.28f);
                p.GibColor = new Color(0.96f, 0.58f, 0.18f);
                p.GibCount = 3;
                p.StackSize = 10;
                p.Voice = PreyVoice.Flutter;
            });

            Asset<PreyDefinition>("Prey/Prey_Rabbit", "prey.rabbit", "Plains Rabbit",
                "Skittish: bolts when you get close. Too strong for a flimsy net; a knife needs two cuts, or one to the head.", p =>
            {
                p.Allowed = TakedownMethods.Either;
                p.MaxHealth = 10f;
                p.CaptureDifficulty = 2f;
                p.MinKillTier = 1;
                p.OverkillRatio = 3f;
                // Model faces +Z in Unity; pivot between the feet.
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 0.16f, -0.1f), B = new Vector3(0f, 0.18f, 0.08f), Radius = 0.15f },
                    new HitboxShape { A = new Vector3(0f, 0.28f, 0.17f), B = new Vector3(0f, 0.3f, 0.24f), Radius = 0.1f, WeakPoint = true },
                };
                p.BaseValue = 7;
                p.TrophiesOnKill = 1;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 1.1f;
                // Sprints at 8.5 m/s (faster than the 6.35 m/s run cap) for 3 s, then tires to 4.8 m/s:
                // runners lose it, bunny hoppers catch it (owner request, M3 feedback).
                p.FleeSpeed = 8.5f;
                p.StaminaSeconds = 3f;
                p.TiredSpeed = 4.8f;
                p.FleeRadius = 8f;
                // Bolts from players it can see within 22 m (11 m if they crouch), and from gunshots within 45 m.
                p.SightRange = 22f;
                p.HearingRange = 45f;
                p.Wariness = 1f;
                p.CalmDownSeconds = 3f;
                p.StunSeconds = 0.15f;
                p.HalfExtents = new Vector3(0.15f, 0.15f, 0.2f);
                p.Visual = rabbitModel;
                p.VisualScale = 1f;
                p.Noun = "a rabbit";
                p.BloodColor = new Color(0.72f, 0.07f, 0.06f);
                p.GibColor = new Color(0.6f, 0.45f, 0.31f);
                p.GibCount = 5;
                p.StackSize = 1;
                p.Voice = PreyVoice.Squeak;
            });

            ToolDefinition net = Asset<ToolDefinition>("Tools/Tool_Net", "tool.net", "Butterfly Net",
                "Catches prey alive: worth 1.5x and adds Bestiary research.", t =>
            {
                t.Archetype = ToolArchetype.Net;
                t.Mode = ToolMode.Capture;
                t.Tier = 1;
                t.Damage = 0f;
                t.CaptureStrength = 1f;
                t.Range = 2.4f;
                t.ArcDegrees = 70f;
                t.MaxTargets = 3; // one sweep scoops up to three (each checked against net strength)
                t.Cooldown = 0.55f;
                t.Slot = 1;
                t.StartOwned = true;
                t.Viewmodel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/Net/Net.fbx");
            });

            ToolDefinition knife = Asset<ToolDefinition>("Tools/Tool_Knife", "tool.knife", "Hunting Knife",
                "Clean kills keep the carcass intact. Aim for the head.", t =>
            {
                t.Archetype = ToolArchetype.Blade;
                t.Mode = ToolMode.Kill;
                t.Tier = 1;
                t.Damage = 6f;
                t.Knockback = 2.5f;
                t.HeadshotMultiplier = 2f;
                t.Range = 2.0f;
                t.ArcDegrees = 60f;
                t.Cooldown = 0.45f;
                t.Slot = 2;
                t.StartOwned = true;
                t.Viewmodel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/Knife/Knife.fbx");
            });

            ToolDefinition revolver = Asset<ToolDefinition>("Tools/Tool_Revolver", "tool.revolver", "Old Revolver",
                "Long reach. Too much gun for small prey: overkill ruins the carcass.", t =>
            {
                t.Archetype = ToolArchetype.Firearm;
                t.Mode = ToolMode.Kill;
                t.Tier = 2;
                t.Damage = 25f;
                t.Knockback = 6f;
                // Severe kick (owner, Phase 3): 7 degrees up per shot, about half settles back, the rest you pull down.
                t.RecoilUp = 7f;
                t.RecoilSide = 2.2f;
                t.RecoilRecover = 0.5f;
                t.HeadshotMultiplier = 2f;
                t.Range = 150f;
                t.ArcDegrees = 0f;
                t.Cooldown = 0.4f;
                t.Slot = 3;
                t.StartOwned = false;
                t.Viewmodel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/Revolver/Revolver.fbx");            });

            UpgradeDefinition sturdy = Asset<UpgradeDefinition>("Upgrades/Upgrade_SturdyNet", "upgrade.net_sturdy", "Sturdy Net",
                "Strong enough to hold a rabbit.", u =>
            {
                u.Tool = net;
                u.Effect = UpgradeEffect.CaptureStrength;
                u.Value = 1f;
                u.CostCoins = 140;
                u.SortOrder = 10;
                u.Requires = null;
            });

            Asset<UpgradeDefinition>("Upgrades/Upgrade_Whetstone", "upgrade.knife_whetstone", "Whetstone",
                "One cut fells a rabbit.", u =>
            {
                u.Tool = knife;
                u.Effect = UpgradeEffect.DamageMultiplier;
                u.Value = 1.6f;
                u.CostCoins = 110;
                u.SortOrder = 20;
                u.Requires = null;
            });

            Asset<UpgradeDefinition>("Upgrades/Upgrade_WideHoop", "upgrade.net_wide", "Wide Hoop",
                "Scoop from further away.", u =>
            {
                u.Tool = net;
                u.Effect = UpgradeEffect.Range;
                u.Value = 0.8f;
                u.CostCoins = 220;
                u.SortOrder = 30;
                u.Requires = sturdy;
            });

            Asset<UpgradeDefinition>("Upgrades/Upgrade_Revolver", "upgrade.revolver", "Old Revolver",
                "Hunt from afar. Bullets ruin small catches: keep the net for butterflies.", u =>
            {
                u.Tool = revolver;
                u.Effect = UpgradeEffect.UnlockTool;
                u.Value = 0f;
                u.CostCoins = 340;
                u.SortOrder = 40;
                u.Requires = null;
            });

            UpgradeDefinition pack = Asset<UpgradeDefinition>("Upgrades/Upgrade_PackSmall", "upgrade.pack_small", "Hunter's Satchel",
                "Two more inventory slots.", u =>
            {
                u.Tool = null;
                u.Effect = UpgradeEffect.InventorySlots;
                u.Value = 2f;
                u.CostCoins = 180;
                u.SortOrder = 50;
                u.Requires = null;
            });

            Asset<UpgradeDefinition>("Upgrades/Upgrade_PackLarge", "upgrade.pack_large", "Hunter's Backpack",
                "Two more inventory slots.", u =>
            {
                u.Tool = null;
                u.Effect = UpgradeEffect.InventorySlots;
                u.Value = 2f;
                u.CostCoins = 360;
                u.SortOrder = 60;
                u.Requires = pack;
            });

            AssetDatabase.SaveAssets();
            ContentRegistry.Reset();
            Debug.Log("[HowToHunt] Content rebuilt.");
        }

        private static T Asset<T>(string relativePath, string key, string displayName, string description, System.Action<T> fill) where T : ContentDefinition
        {
            string path = $"{ContentDir}/{relativePath}.asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.EditorSetKey(key);
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_description").stringValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
            fill(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
