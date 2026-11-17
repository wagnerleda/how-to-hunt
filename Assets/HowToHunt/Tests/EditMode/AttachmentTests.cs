using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Hunting;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>Phase 5: attachments change a gun's numbers, fit one per slot, and survive a save.</summary>
    public sealed class AttachmentTests
    {
        private static T Get<T>(string key) where T : ContentDefinition => ContentRegistry.Get<T>(key);

        [SetUp]
        public void Fresh()
        {
            ContentRegistry.Reset();
            Loadouts.Reset();
            HuntingCvars.AllTools.Value = false;
        }

        [Test]
        public void Attachments_ChangeTheNumbers_AndStack()
        {
            ToolDefinition glock = Get<ToolDefinition>("tool.glock"), revolver = Get<ToolDefinition>("tool.revolver");
            GunSetup bare = GunSetup.For(glock, null);
            var kit = new List<AttachmentDefinition>
            {
                Get<AttachmentDefinition>("attachment.muzzle_brake"),
                Get<AttachmentDefinition>("attachment.ammo_hollow"),
                Get<AttachmentDefinition>("attachment.red_dot"),
            };
            GunSetup kitted = GunSetup.For(glock, kit);
            GunSetup loaded = GunSetup.For(revolver, new List<AttachmentDefinition> { Get<AttachmentDefinition>("attachment.speed_loader") });
            Assert.AreEqual(bare.RecoilUp * 0.8f, kitted.RecoilUp, 1e-4f, "muzzle brake (view kick -20 %)");
            Assert.AreEqual(GunSetup.For(revolver, null).ReloadSeconds * 0.55f, loaded.ReloadSeconds, 1e-4f, "speed loader");
            Assert.AreEqual(1.25f, kitted.DamageMultiplier, 1e-4f, "hollow points");
            Assert.Less(kitted.Ballistics.FalloffEnd, bare.Ballistics.FalloffEnd, "hollow points fall off sooner");
            Assert.AreEqual(1.35f, kitted.AdsZoom, 1e-4f, "red dot zoom");
            Assert.Greater(kitted.SightOffset.y, bare.SightOffset.y, "aiming looks through the red dot, above the iron sights");
            Assert.Greater(kitted.Control, bare.Control);
            Assert.Greater(loaded.Mobility, GunSetup.For(revolver, null).Mobility, "a faster reload is more mobile");
        }

        [Test]
        public void Attachments_OnlyFitGunsWithThatMount()
        {
            ToolDefinition revolver = Get<ToolDefinition>("tool.revolver"), scout = Get<ToolDefinition>("tool.scout"), knife = Get<ToolDefinition>("tool.knife");
            Assert.IsTrue(Get<AttachmentDefinition>("attachment.red_dot").Fits(Get<ToolDefinition>("tool.glock")));
            Assert.IsFalse(Get<AttachmentDefinition>("attachment.red_dot").Fits(revolver), "the old revolver: a laser or a speed loader, nothing else");
            Assert.IsTrue(Get<AttachmentDefinition>("attachment.laser").Fits(revolver));
            Assert.IsFalse(Get<AttachmentDefinition>("attachment.muzzle_brake").Fits(revolver));
            Assert.IsFalse(Get<AttachmentDefinition>("attachment.laser").Fits(Get<ToolDefinition>("tool.handcannon")), "the cannon takes nothing");
            Assert.IsFalse(Get<AttachmentDefinition>("attachment.red_dot").Fits(scout), "the scout's scope is built in");
            Assert.IsTrue(Get<AttachmentDefinition>("attachment.scout_buttpad").Fits(scout), "the scout has its own butt pad");
            Assert.IsFalse(Get<AttachmentDefinition>("attachment.padded_stock").Fits(scout), "so not the stand-in");
            Assert.IsFalse(Get<AttachmentDefinition>("attachment.padded_stock").Fits(revolver));
            ToolDefinition uzi = Get<ToolDefinition>("tool.uzi");
            Assert.IsTrue(Get<AttachmentDefinition>("attachment.uzi_suppressor").Fits(uzi), "each gun has its own parts");
            Assert.IsFalse(Get<AttachmentDefinition>("attachment.uzi_suppressor").Fits(scout), "an Uzi suppressor is not a scout suppressor");
            Assert.IsFalse(Get<AttachmentDefinition>("attachment.suppressor").Fits(uzi), "the stand-in suppressor is not offered where a real one exists");
            Assert.IsFalse(Get<AttachmentDefinition>("attachment.suppressor").Fits(knife));
        }

        [Test]
        public void Profile_FitsOnePerSlot_AndKeepsThemThroughASave()
        {
            var p = new EconomyProfile();
            ToolDefinition revolver = Get<ToolDefinition>("tool.glock"); // a gun that takes a muzzle device and an optic
            AttachmentDefinition brake = Get<AttachmentDefinition>("attachment.muzzle_brake"), can = Get<AttachmentDefinition>("attachment.suppressor"), dot = Get<AttachmentDefinition>("attachment.red_dot");
            foreach (AttachmentDefinition a in new[] { brake, can, dot }) p.Attachments.Add(a.Id.Value);
            p.Fit(revolver.Id.Value, brake);
            p.Fit(revolver.Id.Value, dot);
            p.Fit(revolver.Id.Value, can); // replaces the brake
            Assert.AreEqual(can, p.FittedIn(revolver.Id.Value, AttachmentSlot.Muzzle));
            Assert.AreEqual(dot, p.FittedIn(revolver.Id.Value, AttachmentSlot.Optic));
            Assert.AreEqual(2, p.FittedTools.Count);

            var slice = new EconomySaveSlice(p, null);
            var again = new EconomyProfile();
            new EconomySaveSlice(again, null).Load(slice.Save(), slice.Version);
            Assert.AreEqual(3, again.Attachments.Count);
            Assert.AreEqual(can, again.FittedIn(revolver.Id.Value, AttachmentSlot.Muzzle));
            Assert.AreEqual(dot, again.FittedIn(revolver.Id.Value, AttachmentSlot.Optic));

            var loadout = new Loadout();
            again.ApplyTo(loadout);
            Assert.AreEqual(0.35f, loadout.Setup(revolver).NoiseMultiplier, 1e-4f, "the suppressor quiets the gun");
            Assert.IsTrue(again.Unfit(revolver.Id.Value, AttachmentSlot.Muzzle));
            Assert.IsNull(again.FittedIn(revolver.Id.Value, AttachmentSlot.Muzzle));
        }
    }
}
