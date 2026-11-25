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
    public static partial class GeneratedContent
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
                // Sprints at 8.5 m/s (faster than the 6.35 m/s run cap and the 7.9 m/s player sprint) for 5 s, then tires to 5.4 m/s:
                // runners lose it, bunny hoppers catch it (owner request, M3 feedback).
                p.FleeSpeed = 8.5f;
                p.StaminaSeconds = 5f; // Phase 3.5: tired too soon once players could sprint
                p.TiredSpeed = 5.4f;
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

            // Phase 6: the pine forest's own prey. Big, wary and fast: it hears you from far off and outruns a sprint
            // (bunny hoppers can still catch it). Worth far more than a rabbit; a gun or a strong net to take it.
            var deerModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/Deer/Deer.fbx");
            Asset<PreyDefinition>("Prey/Prey_Deer", "prey.deer", "Pine Deer",
                "Wary: hears you from 70 m and sees you from 35. Outruns a sprint for six seconds. Needs a gun or a strong net; a head shot drops it fast.", p =>
            {
                p.Allowed = TakedownMethods.Either;
                p.MaxHealth = 60f;
                p.CaptureDifficulty = 3f;
                p.MinKillTier = 2;
                p.OverkillRatio = 4f;
                // Model faces +Z in Unity; pivot between the feet. Body along Z, head up front.
                p.Hitboxes = new List<HitboxShape>
                {
                    new HitboxShape { A = new Vector3(0f, 1.0f, -0.45f), B = new Vector3(0f, 1.0f, 0.5f), Radius = 0.28f },
                    new HitboxShape { A = new Vector3(0f, 0.5f, -0.4f), B = new Vector3(0f, 0.5f, 0.45f), Radius = 0.12f }, // legs
                    new HitboxShape { A = new Vector3(0f, 1.55f, 0.82f), B = new Vector3(0f, 1.6f, 0.98f), Radius = 0.13f, WeakPoint = true },
                };
                p.BaseValue = 45;
                p.TrophiesOnKill = 4;
                p.Movement = PreyMovement.Ground;
                p.WalkSpeed = 1.3f;
                p.FleeSpeed = 10.5f;
                p.StaminaSeconds = 6f;
                p.TiredSpeed = 6.8f;
                p.FleeRadius = 14f;
                p.SightRange = 35f;
                p.HearingRange = 70f;
                p.Wariness = 1.6f;
                p.CalmDownSeconds = 5f;
                p.StunSeconds = 0.3f;
                p.HalfExtents = new Vector3(0.3f, 0.55f, 0.6f);
                p.Visual = deerModel;
                p.VisualScale = 1f;
                p.Noun = "a deer";
                p.BloodColor = new Color(0.7f, 0.06f, 0.05f);
                p.GibColor = new Color(0.5f, 0.32f, 0.18f);
                p.GibCount = 8;
                p.StackSize = 1;
                p.Voice = PreyVoice.Grunt;
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
                "Your first gun: an old six-shooter. Light damage, a gentle kick; only a laser or a speed loader fits it.", t =>
            {
                t.Archetype = ToolArchetype.Firearm;
                t.Mode = ToolMode.Kill;
                t.Tier = 2;
                t.Damage = 14f;
                t.Knockback = 6f;
                // How to Fish handling: the view kicks and stays (you pull it down); the gun snaps back on springs.
                t.RecoilUp = 2.5f;
                t.RecoilSide = 1.5f;
                t.RecoilRecover = 0f;
                t.Handling = Handling.Pistol();
                t.Handling.KickRot = new Vector2(8f, 10f); // a gentle gun: thrown less than the pistols
                // Phase 4: a real six-shooter. Bullets fly at 360 m/s with a little drop, lose damage past 25 m, and
                // spray when you shoot on the run; aim down the sights to tighten it.
                t.MagazineSize = 6;
                t.ReloadSeconds = 2.2f;
                t.Automatic = false;
                t.BulletSpeed = 360f;
                t.BulletGravity = 4f;
                t.FalloffStart = 25f;
                t.FalloffEnd = 70f;
                t.MinDamageFraction = 0.55f;
                t.SpreadDegrees = 1.2f;
                t.MoveSpreadPerMps = 0.4f;
                t.MaxSpreadDegrees = 7f;
                t.AdsSpreadFactor = 0.25f;
                t.AdsZoom = 1.15f;
                t.SightOffset = new Vector3(0f, 0.075f, 0f);
                t.MuzzleOffset = new Vector3(0f, 0.04f, 0.29f);
                // Phase 5 mounts (model space): muzzle, top rail for a red dot, under the barrel, and internal slots.
                t.AttachmentAnchors = new List<AttachmentAnchor>
                {
                    new AttachmentAnchor { Slot = AttachmentSlot.Muzzle, Position = new Vector3(0f, 0.04f, 0.29f) },
                    new AttachmentAnchor { Slot = AttachmentSlot.Optic, Position = new Vector3(0f, 0.066f, 0.1f) },
                    new AttachmentAnchor { Slot = AttachmentSlot.Underbarrel, Position = new Vector3(0f, 0.02f, 0.2f) },
                    new AttachmentAnchor { Slot = AttachmentSlot.Magazine, Position = new Vector3(0f, 0.02f, 0.05f) }, // the cylinder
                    new AttachmentAnchor { Slot = AttachmentSlot.Ammo, Position = new Vector3(0f, 0.02f, 0.05f) },
                };
                t.Weight = 0;
                t.HeadshotMultiplier = 2f;
                t.Range = 150f;
                t.ArcDegrees = 0f;
                t.Cooldown = 0.4f;
                t.Slot = 3;
                t.StartOwned = false;
                t.Viewmodel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/Revolver/Revolver.fbx");
            });

            ToolDefinition scout = Asset<ToolDefinition>("Tools/Tool_Scout", "tool.scout", "Scout Rifle",
                "Bolt-action with a scope. Stays accurate on the move; far too much gun for small prey.", t =>
            {
                t.Archetype = ToolArchetype.Firearm;
                t.Mode = ToolMode.Kill;
                t.Tier = 3;
                t.Damage = 55f;
                t.HeadshotMultiplier = 2f;
                t.Range = 300f;
                t.ArcDegrees = 0f;
                t.Cooldown = 1.1f;
                t.Slot = 4;
                t.StartOwned = false; // no shop yet: sv_alltools 1 (cheats) until the workshop and later areas (Phase 5)
                t.Knockback = 9f;
                t.RecoilUp = 5f;
                t.RecoilSide = 5f;
                t.RecoilRecover = 0f;
                t.Handling = Handling.Sniper();
                t.MagazineSize = 5;
                t.ReloadSeconds = 2.8f;
                t.BulletSpeed = 820f;
                t.BulletGravity = 6f;
                t.FalloffStart = 120f;
                t.FalloffEnd = 300f;
                t.MinDamageFraction = 0.75f;
                t.SpreadDegrees = 0.15f;
                t.MoveSpreadPerMps = 0f; // the scout exception: moving doesn't spoil the shot
                t.MaxSpreadDegrees = 0.15f;
                t.AdsSpreadFactor = 0.5f;
                t.AdsZoom = 3.5f;
                t.ScopeOverlay = true;
                t.SightOffset = new Vector3(0f, 0.09f, -0.06f);
                t.MuzzleOffset = new Vector3(0f, 0.03f, 0.78f);
                // The scope is part of the rifle (no optic slot); muzzle, foregrip, magazine, ammo and butt pad.
                t.AttachmentAnchors = new List<AttachmentAnchor>
                {
                    new AttachmentAnchor { Slot = AttachmentSlot.Muzzle, Position = new Vector3(0f, 0.03f, 0.78f) },
                    new AttachmentAnchor { Slot = AttachmentSlot.Underbarrel, Position = new Vector3(0f, -0.025f, 0.36f) },
                    new AttachmentAnchor { Slot = AttachmentSlot.Magazine, Position = new Vector3(0f, -0.01f, 0.1f) },
                    new AttachmentAnchor { Slot = AttachmentSlot.Ammo, Position = new Vector3(0f, 0.02f, 0.05f) },
                    new AttachmentAnchor { Slot = AttachmentSlot.Stock, Position = new Vector3(0f, -0.035f, -0.39f) },
                };
                t.Weight = 1; // a little heavy: slows you slightly while held
                t.Viewmodel = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedAssets.ScoutPrefabPath);
            });

            // --- the owner's guns (drop-ins fitted by DropInWeapons; sold at the Gunsmith) -------------------
            void Fit(ToolDefinition t, string prefab)
            {
                var vm = AssetDatabase.LoadAssetAtPath<GameObject>(DropInWeapons.PrefabPath(prefab));
                if (vm == null) return;
                t.Viewmodel = vm;
                var fit = vm.GetComponent<ViewmodelFit>();
                if (fit == null) return;
                t.MuzzleOffset = fit.Muzzle;
                t.SightOffset = fit.Sight;
                t.SightFront = fit.SightFront;
                t.AttachmentAnchors = new List<AttachmentAnchor>(fit.Anchors);
            }
            ToolDefinition Gun(string id, string name, string description, int slot, string prefab, System.Action<ToolDefinition> stats) =>
                Asset<ToolDefinition>("Tools/Tool_" + id, "tool." + id, name, description, t =>
                {
                    t.Archetype = ToolArchetype.Firearm;
                    t.Mode = ToolMode.Kill;
                    t.ArcDegrees = 0f;
                    t.Slot = slot;
                    t.StartOwned = false;
                    t.HeadshotMultiplier = 2f;
                    t.Automatic = false;
                    t.Pellets = 1;
                    t.ExplosionRadius = 0f;
                    t.ChargeSeconds = 0f;
                    t.PlasmaOrb = false;
                    t.ClusterCount = 0;
                    t.ScopeOverlay = false;
                    t.AdsSpreadFactor = 0.3f;
                    t.BulletGravity = 4f;
                    stats(t);
                    Fit(t, prefab);
                });
            void GunsmithSells(ToolDefinition t, double cost, int order) =>
                Asset<UpgradeDefinition>("Upgrades/Upgrade_Gun_" + t.Key.Substring(5), "upgrade.gun_" + t.Key.Substring(5), t.DisplayName,
                    "Sold at the armory in the movement lab.", u =>
                {
                    u.Tool = t;
                    u.Effect = UpgradeEffect.UnlockTool;
                    u.Value = 0f;
                    u.CostCoins = cost;
                    u.SortOrder = order;
                    u.Requires = null;
                    u.Unlisted = false;
                    u.SoldByGunsmith = true;
                    u.CampGunsmith = false; // the full armory only (the movement lab); the camp gunsmith has the revolver
                });

            // The scout keeps its key and numbers; the owner's model replaces the stand-in.
            Fit(scout, "ScoutSniper");
            scout.SightOffset = new Vector3(0f, scout.SightOffset.y, -0.05f);
            scout.AttachmentAnchors.RemoveAll(a => a.Slot == AttachmentSlot.Optic); // the scope is built in
            EditorUtility.SetDirty(scout);
            GunsmithSells(scout, 1100, 140);

            ToolDefinition glock = Gun("glock", "Glock 19", "Light, quick and accurate. Fifteen rounds.", 4, "Glock", t =>
            {
                t.Tier = 2; t.Damage = 16f; t.Range = 120f; t.Cooldown = 0.15f; t.Knockback = 3f;
                t.RecoilUp = 4f; t.RecoilSide = 2f; t.RecoilRecover = 0f; t.Handling = Handling.Pistol();
                t.MagazineSize = 15; t.ReloadSeconds = 1.5f; t.BulletSpeed = 375f;
                t.FalloffStart = 20f; t.FalloffEnd = 55f; t.MinDamageFraction = 0.5f;
                t.SpreadDegrees = 1f; t.MoveSpreadPerMps = 0.3f; t.MaxSpreadDegrees = 6f; t.AdsZoom = 1.15f; t.Weight = 0;
            });
            GunsmithSells(glock, 300, 100);
            ToolDefinition uzi = Gun("uzi", "Uzi", "Hold the trigger: 800 rounds a minute, and it climbs.", 5, "Uzi", t =>
            {
                t.Tier = 2; t.Damage = 8f; t.Range = 100f; t.Cooldown = 0.075f; t.Automatic = true; t.Knockback = 1.5f;
                t.RecoilUp = 3f; t.RecoilSide = 3.5f; t.RecoilRecover = 0f; t.Handling = Handling.Smg();
                t.MagazineSize = 25; t.ReloadSeconds = 1.9f; t.BulletSpeed = 330f;
                t.FalloffStart = 12f; t.FalloffEnd = 40f; t.MinDamageFraction = 0.4f;
                t.SpreadDegrees = 2.2f; t.MoveSpreadPerMps = 0.5f; t.MaxSpreadDegrees = 9f; t.AdsZoom = 1.15f; t.Weight = 0;
            });
            GunsmithSells(uzi, 700, 110);
            ToolDefinition sawedOff = Gun("sawedoff", "Sawed-Off", "Eight pellets per barrel, two barrels. Devastating up close, useless far away.", 6, "SawedOff", t =>
            {
                t.Tier = 2; t.Damage = 6f; t.Range = 60f; t.Cooldown = 0.35f; t.Pellets = 8; t.PelletSpreadDegrees = 6f; t.Knockback = 2f;
                t.RecoilUp = 6f; t.RecoilSide = 6f; t.RecoilRecover = 0f; t.Handling = Handling.Shotgun();
                t.MagazineSize = 2; t.ReloadSeconds = 2.4f; t.BulletSpeed = 300f;
                t.FalloffStart = 6f; t.FalloffEnd = 22f; t.MinDamageFraction = 0.2f;
                t.SpreadDegrees = 0.5f; t.MoveSpreadPerMps = 0.2f; t.MaxSpreadDegrees = 3f; t.AdsZoom = 1.1f; t.Weight = 1;
            });
            GunsmithSells(sawedOff, 600, 120);
            ToolDefinition handCannon = Gun("handcannon", "Hand Cannon", "A real cannon, held by its wheels. One ball per load and a slow reload, but the blast throws you back: fire it at the ground to fly.", 7, "HandCannon", t =>
            {
                t.Tier = 3; t.Damage = 90f; t.Range = 120f; t.Cooldown = 0.8f; t.Knockback = 15f;
                t.RecoilUp = 14f; t.RecoilSide = 4f; t.RecoilRecover = 0f; t.Handling = Handling.Cannon();
                t.MagazineSize = 1; t.ReloadSeconds = 3.5f; t.BulletSpeed = 45f; t.BulletGravity = 9.8f; // an iron ball, as slow as a grenade
                t.IronBall = 0.14f;
                t.FalloffStart = 60f; t.FalloffEnd = 120f; t.MinDamageFraction = 0.7f;
                t.SpreadDegrees = 0.8f; t.MoveSpreadPerMps = 0.2f; t.MaxSpreadDegrees = 4f; t.AdsZoom = 1.1f; t.Weight = 2;
                t.ViewOffset = new Vector3(0.05f, -0.12f, 0.06f); // held low by the wheels, the barrel below your eye
            });
            GunsmithSells(handCannon, 900, 130);

            // The old revolver: the owner's 1851 navy revolver (it used to be the Hand Cannon). The first gun you get:
            // light damage, a gentle kick, and only a laser or a speed loader to fit.
            Fit(revolver, "OldRevolver");
            EditorUtility.SetDirty(revolver);
            ToolDefinition heavyRifle = Gun("heavyrifle", "Heavy Assault Rifle", "Full-auto battle rifle: hits hard and far, slows you down.", 8, "HeavyRifle", t =>
            {
                t.Tier = 3; t.Damage = 16f; t.Range = 250f; t.Cooldown = 0.1f; t.Automatic = true; t.Knockback = 4f;
                t.RecoilUp = 3f; t.RecoilSide = 1.5f; t.RecoilRecover = 0f; t.Handling = Handling.Rifle();
                t.MagazineSize = 20; t.ReloadSeconds = 2.5f; t.BulletSpeed = 700f;
                t.FalloffStart = 50f; t.FalloffEnd = 150f; t.MinDamageFraction = 0.6f;
                t.SpreadDegrees = 1.6f; t.MoveSpreadPerMps = 0.45f; t.MaxSpreadDegrees = 8f; t.AdsZoom = 1.4f; t.Weight = 2;
            });
            GunsmithSells(heavyRifle, 1400, 150);
            ToolDefinition intervention = Gun("intervention", "Intervention", "A .408 anti-materiel rifle. One shot, very heavy, deadly from far away.", 9, "Intervention", t =>
            {
                t.Tier = 4; t.Damage = 120f; t.Range = 500f; t.Cooldown = 1.4f; t.Knockback = 12f;
                t.RecoilUp = 6f; t.RecoilSide = 5f; t.RecoilRecover = 0f; t.Handling = Handling.Sniper(push: 1.5f);
                t.MagazineSize = 5; t.ReloadSeconds = 3.4f; t.BulletSpeed = 900f; t.BulletGravity = 6f;
                t.FalloffStart = 200f; t.FalloffEnd = 500f; t.MinDamageFraction = 0.8f;
                t.SpreadDegrees = 2f; t.MoveSpreadPerMps = 0.6f; t.MaxSpreadDegrees = 12f; t.AdsSpreadFactor = 0.02f;
                t.AdsZoom = 6f; t.ScopeOverlay = true; t.Weight = 3;
            });
            intervention.AttachmentAnchors.RemoveAll(a => a.Slot == AttachmentSlot.Optic);
            GunsmithSells(intervention, 2000, 160);
            ToolDefinition grenadeLauncher = Gun("grenadelauncher", "GL-37", "Lobs a grenade that explodes on impact. Ruins whatever it does not scare away.", 9, "GrenadeLauncher", t =>
            {
                t.Tier = 4; t.Damage = 90f; t.Range = 200f; t.Cooldown = 0.9f; t.ExplosionRadius = 4.5f; t.Knockback = 10f;
                t.RecoilUp = 5f; t.RecoilSide = 4f; t.RecoilRecover = 0f; t.Handling = Handling.Shotgun(push: 2f);
                t.MagazineSize = 1; t.ReloadSeconds = 2.2f; t.BulletSpeed = 45f; t.BulletGravity = 9.8f;
                t.FalloffStart = 200f; t.FalloffEnd = 400f; t.MinDamageFraction = 1f;
                t.SpreadDegrees = 0.6f; t.MoveSpreadPerMps = 0.2f; t.MaxSpreadDegrees = 3f; t.AdsZoom = 1.2f; t.Weight = 2;
            });
            GunsmithSells(grenadeLauncher, 2500, 170);
            ToolDefinition gunlance = Gun("gunlance", "Gunlance", "Endgame. Hold Fire to charge a plasma orb, release to throw it: it bursts into seven smaller explosive orbs. One orb per charge.", 9, "Gunlance", t =>
            {
                t.Tier = 5; t.Damage = 120f; t.Range = 150f; t.Cooldown = 0.6f; t.Knockback = 12f;
                t.ChargeSeconds = 1.1f; t.PlasmaOrb = true; t.ExplosionRadius = 4f;
                t.ClusterCount = 7; t.ClusterRadius = 2.3f; t.ClusterDamage = 45f;
                t.RecoilUp = 8f; t.RecoilSide = 5f; t.RecoilRecover = 0f; t.Handling = Handling.Shotgun(push: 6f);
                t.MagazineSize = 1; t.ReloadSeconds = 3f; t.BulletSpeed = 32f; t.BulletGravity = 5f;
                t.FalloffStart = 150f; t.FalloffEnd = 300f; t.MinDamageFraction = 1f;
                t.SpreadDegrees = 0.4f; t.MoveSpreadPerMps = 0.2f; t.MaxSpreadDegrees = 3f; t.AdsZoom = 1.3f; t.Weight = 2;
            });
            GunsmithSells(gunlance, 3000, 180);

            // The owner's knife model replaces the Blender one.
            var knifeV2 = AssetDatabase.LoadAssetAtPath<GameObject>(DropInWeapons.PrefabPath("KnifeV2"));
            if (knifeV2 != null) { knife.Viewmodel = knifeV2; EditorUtility.SetDirty(knife); }

            UpgradeDefinition sturdy = Asset<UpgradeDefinition>("Upgrades/Upgrade_SturdyNet", "upgrade.net_sturdy", "Sturdy Net",
                "Strong enough to hold a rabbit.", u =>
            {
                u.Tool = net;
                u.Effect = UpgradeEffect.CaptureStrength;
                u.Value = 1f;
                u.CostCoins = 130;
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

            UpgradeDefinition wide = Asset<UpgradeDefinition>("Upgrades/Upgrade_WideHoop", "upgrade.net_wide", "Wide Hoop",
                "Scoop from further away.", u =>
            {
                u.Tool = net;
                u.Effect = UpgradeEffect.Range;
                u.Value = 0.8f;
                u.CostCoins = 150;
                u.SortOrder = 30;
                u.Requires = sturdy;
            });

            // The net's third step (2026-11-24): strong enough for foxes, jackrabbits and scorpions.
            Asset<UpgradeDefinition>("Upgrades/Upgrade_IronwoodNet", "upgrade.net_ironwood", "Ironwood Net",
                "Holds a fox, a jackrabbit or a scorpion.", u =>
            {
                u.Tool = net;
                u.Effect = UpgradeEffect.CaptureStrength;
                u.Value = 1f;
                u.CostCoins = 380;
                u.SortOrder = 35;
                u.Requires = wide;
            });

            // The Plains gunsmith's only gun (owner, 2026-11-24); the armory in the movement lab has every gun.
            Asset<UpgradeDefinition>("Upgrades/Upgrade_Revolver", "upgrade.revolver", "Old Revolver",
                "Hunt from afar. Bullets ruin small catches: keep the net for butterflies.", u =>
            {
                u.Tool = revolver;
                u.Effect = UpgradeEffect.UnlockTool;
                u.Value = 0f;
                u.CostCoins = 200;
                u.SortOrder = 40;
                u.Requires = null;
                u.SoldByGunsmith = true;
                u.CampGunsmith = true;
            });

            UpgradeDefinition pack = Asset<UpgradeDefinition>("Upgrades/Upgrade_PackSmall", "upgrade.pack_small", "Hunter's Satchel",
                "Carry two more catches before the walk to the trader.", u =>
            {
                u.Tool = null;
                u.Effect = UpgradeEffect.InventorySlots;
                u.Unlisted = false; // owner, 2026-11-24: slots are bought at the Outfitter (the bag column)
                u.Value = 2f;
                u.CostCoins = 250;
                u.SortOrder = 50;
                u.Requires = null;
            });

            Asset<UpgradeDefinition>("Upgrades/Upgrade_PackLarge", "upgrade.pack_large", "Hunter's Backpack",
                "Room for a real day's hunt.", u =>
            {
                u.Tool = null;
                u.Effect = UpgradeEffect.InventorySlots;
                u.Unlisted = false;
                u.Value = 2f;
                u.CostCoins = 650;
                u.SortOrder = 60;
                u.Requires = pack;
            });

            // --- Phase 5: the Gunsmith's attachments (expensive: a gun becomes a killing machine of its own) -----
            GeneratedAssets.EnsureFolder(ContentDir + "/Attachments");
            GameObject AttachmentModel(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{GeneratedAssets.AttachmentsDir}/{name}.prefab");
            Attach("muzzle_brake", "Muzzle Brake", "View kick -20 %. A little louder.", AttachmentSlot.Muzzle, 450, 10, a =>
            {
                a.Tools = new List<ToolDefinition> { glock, sawedOff, intervention };
                a.RecoilMultiplier = 0.8f;
                a.NoiseMultiplier = 1.15f;
                a.Visual = AttachmentModel("MuzzleBrake");
            });
            Attach("suppressor", "Suppressor", "Prey hear the shot from a third as far. Slightly shorter reach.", AttachmentSlot.Muzzle, 700, 20, a =>
            {
                a.Tools = new List<ToolDefinition> { glock, heavyRifle, intervention }; // the SCAR's on its own flash hider
                a.NoiseMultiplier = 0.35f;
                a.RangeMultiplier = 0.9f;
                a.RecoilMultiplier = 0.9f;
                a.DamageMultiplier = 0.95f;
                a.Visual = AttachmentModel("Suppressor");
            });
            Attach("laser", "Laser Sight", "Steadier from the hip and on the move.", AttachmentSlot.Underbarrel, 500, 30, a =>
            {
                a.Laser = true;
                a.FreeAimMultiplier = 0.7f; // you see where the gun points, so it wanders less
                a.Tools = new List<ToolDefinition> { revolver, glock, uzi };
                a.SpreadMultiplier = 0.6f;
                a.MoveSpreadMultiplier = 0.75f;
                a.Visual = AttachmentModel("Laser");
            });
            Attach("foregrip", "Foregrip", "The gun kicks 20 % less in the hand and wanders less from the hip; tighter down the sights.", AttachmentSlot.Underbarrel, 450, 31, a =>
            {
                a.Tools = new List<ToolDefinition> { heavyRifle, grenadeLauncher };
                a.KickMultiplier = 0.8f;
                a.FreeAimMultiplier = 0.6f;
                a.AdsSpreadMultiplier = 0.8f;
                a.Visual = AttachmentModel("Foregrip");
            });
            Attach("speed_loader", "Speed Loader", "Reloads almost twice as fast.", AttachmentSlot.Magazine, 400, 40, a =>
            {
                a.Tools = new List<ToolDefinition> { revolver };
                a.ReloadMultiplier = 0.55f;
            });
            Attach("extended_mag", "Extended Magazine", "Three more rounds; a slower reload.", AttachmentSlot.Magazine, 550, 41, a =>
            {
                a.Tools = new List<ToolDefinition> { scout, glock, uzi, heavyRifle, intervention };
                a.MagazineAdd = 3;
                a.ReloadMultiplier = 1.1f;
                a.Visual = null; // not shown: the bigger magazine is just more rounds
            });
            Attach("ammo_hollow", "Hollow Points", "Damage +25 %, but it falls off sooner.", AttachmentSlot.Ammo, 650, 50, a =>
            {
                a.Tools = new List<ToolDefinition> { glock, uzi, scout, heavyRifle, intervention };
                a.DamageMultiplier = 1.25f;
                a.RangeMultiplier = 0.8f;
            });
            Attach("ammo_match", "Match Rounds", "Keeps its damage a third further; a tighter group.", AttachmentSlot.Ammo, 550, 51, a =>
            {
                a.Tools = new List<ToolDefinition> { glock, uzi, scout, heavyRifle, intervention, gunlance };
                a.RangeMultiplier = 1.35f;
                a.SpreadMultiplier = 0.85f;
                a.AdsSpreadMultiplier = 0.85f;
            });
            Attach("red_dot", "Red Dot", "A clear dot to aim with and a little zoom.", AttachmentSlot.Optic, 500, 60, a =>
            {
                a.Tools = new List<ToolDefinition> { glock, uzi, sawedOff, heavyRifle, grenadeLauncher };
                a.AdsZoom = 1.35f;
                a.AdsSpreadMultiplier = 0.75f;
                var owned = AssetDatabase.LoadAssetAtPath<GameObject>(DropInWeapons.RedDotPrefab);
                a.Visual = owned != null ? owned : AttachmentModel("RedDot"); // the owner's sight, else the stand-in
                ViewmodelFit sight = owned != null ? owned.GetComponent<ViewmodelFit>() : null;
                if (sight != null) a.SightLift = sight.Sight.y; // the eye lines up with the reticle
            });
            Attach("padded_stock", "Padded Stock", "The gun kicks 30 % less in the hand and settles faster.", AttachmentSlot.Stock, 550, 70, a =>
            {
                a.Tools = new List<ToolDefinition> { grenadeLauncher }; // the shotgun has its own stock now
                a.KickMultiplier = 0.7f;
                a.SpringMultiplier = 1.5f;
                a.Visual = AttachmentModel("PaddedStock");
            });

            // Attachments that are parts of a gun's own model: each gun has its own (an Uzi suppressor is not a scout
            // suppressor). Hidden on the bare gun, shown when fitted.
            void Builtin(string id, string name, string description, AttachmentSlot slot, double cost, int order, ToolDefinition gun, string parts, System.Action<AttachmentDefinition> fx) =>
                Attach(id, name, description, slot, cost, order, a =>
                {
                    a.Tools = new List<ToolDefinition> { gun };
                    a.PartNames = parts.Split(';');
                    a.Visual = null;
                    fx(a);
                });
            Builtin("uzi_suppressor", "Uzi Suppressor", "Prey hear the shot from a third as far.", AttachmentSlot.Muzzle, 650, 200, uzi, "Body14", a => { a.NoiseMultiplier = 0.35f; a.RangeMultiplier = 0.9f; a.RecoilMultiplier = 0.9f; });
            Builtin("uzi_foregrip", "Uzi Foregrip", "A front grip: the gun kicks 20 % less in the hand and wanders less from the hip.", AttachmentSlot.Underbarrel, 400, 201, uzi, "Body19", a => { a.KickMultiplier = 0.8f; a.FreeAimMultiplier = 0.6f; a.MoveSpreadMultiplier = 0.85f; });
            Builtin("uzi_stock", "Uzi Folding Stock", "Braced on the shoulder: the gun kicks 30 % less and settles faster; a tighter spray.", AttachmentSlot.Stock, 450, 202, uzi, "Body15;Body21", a => { a.KickMultiplier = 0.7f; a.SpringMultiplier = 1.5f; a.SpreadMultiplier = 0.85f; });
            Builtin("scout_suppressor", "Scout Suppressor", "Prey barely hear it: a third of the range.", AttachmentSlot.Muzzle, 900, 220, scout, "suppressor ", a => { a.NoiseMultiplier = 0.3f; a.RangeMultiplier = 0.95f; });
            Builtin("scout_buttpad", "Scout Butt Pad", "The gun kicks 25 % less in the hand and settles faster.", AttachmentSlot.Stock, 450, 221, scout, "stock.001_low", a => { a.KickMultiplier = 0.75f; a.SpringMultiplier = 1.3f; });
            Builtin("intervention_bipod", "Intervention Bipod", "Braced: the gun kicks half as much in the hand; half the spread down the scope.", AttachmentSlot.Underbarrel, 800, 230, intervention, "Cube.003_low;Cylinder.034_low;Cylinder.038_low;Cylinder.008_low", a => { a.KickMultiplier = 0.5f; a.AdsSpreadMultiplier = 0.5f; });

            BuildForestFauna();
            BuildDesertFauna();
            BuildNpcsAndQuests();
            BuildDesertNpcsAndQuests();
            BuildRegions();

            // How each tool lies when dropped (Phase 3.5: a net tips over onto its side, a gun lies flat; no floating).
            foreach (string guid in AssetDatabase.FindAssets("t:ToolDefinition", new[] { ContentDir + "/Tools" }))
            {
                var tool = AssetDatabase.LoadAssetAtPath<ToolDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (tool == null || !ItemRestPose.Compute(tool.Viewmodel, "Hand", out Vector3 euler, out Vector3 center, out Vector3 size)) continue;
                tool.RestEuler = euler;
                tool.RestCenter = center;
                tool.RestSize = size;
                EditorUtility.SetDirty(tool);
            }

            // Sounds per weapon, by folder and file name: Art/Weapons/<tool name>/fire*.wav, reload*, dry*, equip*, inspect*.
            foreach (string guid in AssetDatabase.FindAssets("t:ToolDefinition", new[] { ContentDir + "/Tools" }))
            {
                var tool = AssetDatabase.LoadAssetAtPath<ToolDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (tool != null && WeaponSounds.Link(tool)) EditorUtility.SetDirty(tool);
            }

            AssetDatabase.SaveAssets();
            ContentRegistry.Reset();
            Debug.Log("[HowToHunt] Content rebuilt.");
        }

        private static void Attach(string id, string name, string description, AttachmentSlot slot, double cost, int order, System.Action<AttachmentDefinition> fill)
            => Asset<AttachmentDefinition>("Attachments/Attachment_" + id, "attachment." + id, name, description, a =>
            {
                a.Slot = slot;
                a.CostCoins = cost;
                a.SortOrder = order;
                fill(a);
            });

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
