using System;
using System.Numerics;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Hunting;
using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Hunting;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>Inventory slots and stacking, world-item physics, and the v1 bag → v2 inventory save migration.</summary>
    public sealed class InventoryTests
    {
        private const uint Net = 1, Knife = 2, Butterfly = 10, Rabbit = 11;

        private static Inventory WithTools(int capacity, params uint[] tools)
        {
            var inv = new Inventory();
            inv.SetCapacity(capacity);
            foreach (uint t in tools) inv.Add(ItemStack.Tool(t), 1);
            return inv;
        }

        [Test]
        public void PreyStack_ToolsDont_AndFullSlotsRefuse()
        {
            Inventory inv = WithTools(4, Net, Knife);
            Assert.AreEqual(ItemKind.Tool, inv[0].Kind);
            Assert.AreEqual(Knife, inv[1].Content);
            Assert.AreEqual(0, inv.Add(ItemStack.Tool(Net), 10));
            Assert.AreEqual(1, inv[0].Count, "tools never stack...");
            Assert.AreEqual(Net, inv[2].Content, "...a second net takes its own slot");

            var fly = ItemStack.Prey(Butterfly, true, CarcassQuality.Intact);
            Inventory inv2 = WithTools(4, Net, Knife);
            for (int i = 0; i < 12; i++) Assert.AreEqual(0, inv2.Add(fly, 10), $"butterfly {i}");
            Assert.AreEqual(10, inv2[2].Count, "first stack full");
            Assert.AreEqual(2, inv2[3].Count, "overflow starts a second stack");

            var rabbit = ItemStack.Prey(Rabbit, false, CarcassQuality.Intact);
            Assert.IsFalse(inv2.CanAdd(rabbit, 1), "no free slot for a rabbit");
            Assert.AreEqual(1, inv2.Add(rabbit, 1));

            // Killed and caught butterflies don't share a stack.
            Assert.IsFalse(inv2.CanAdd(ItemStack.Prey(Butterfly, false, CarcassQuality.Intact), 10));
        }

        [Test]
        public void ToolsCanBeDroppedAndPickedUp_SellingKeepsThem()
        {
            Inventory inv = WithTools(3, Net);
            inv.Add(ItemStack.Prey(Rabbit, false, CarcassQuality.Intact), 1);
            Assert.IsTrue(inv.TakeOne(0, out ItemStack tool), "tools can be dropped");
            Assert.AreEqual(ItemKind.Tool, tool.Kind);
            Assert.IsFalse(inv.HasTool(Net));
            Assert.AreEqual(0, inv.Add(tool, 1), "picked back up into the free slot");
            Assert.IsTrue(inv.HasTool(Net));

            inv.Add(ItemStack.Prey(Rabbit, true, CarcassQuality.Intact), 1);
            Assert.AreEqual(2, inv.TakeAllPrey().Count, "selling takes the prey");
            Assert.IsTrue(inv.HasTool(Net), "and leaves the tools");

            inv.Add(ItemStack.Prey(Rabbit, false, CarcassQuality.Ruined), 1);
            inv.Add(ItemStack.Prey(Rabbit, false, CarcassQuality.Intact), 1);
            var overflow = inv.SetCapacity(2);
            Assert.AreEqual(1, overflow.Count, "the item in the removed slot is returned");
        }

        private static BrushWorld Floor()
        {
            var w = new BrushWorld();
            w.Add(Brush.Box(new Vector3(0, -0.5f, 0), new Vector3(50, 0.5f, 50)));
            w.Add(Brush.Box(new Vector3(0, 2f, 6f), new Vector3(50, 2f, 0.5f))); // a wall at z = 5.5
            return w;
        }

        [Test]
        public void ThrownItem_BouncesOffWall_AndComesToRestOnTheGround()
        {
            BrushWorld world = Floor();
            var half = new Vector3(0.15f, 0.15f, 0.15f);
            var b = new ItemBody { Position = new Vector3(0, 1.5f, 0), Velocity = new Vector3(0, 2f, 13f) };
            for (int i = 0; i < 64 * 6 && !b.Resting; i++) ItemPhysics.Step(ref b, world, half, 1f / 64f);
            Assert.IsTrue(b.Resting, "settled");
            Assert.Less(b.Position.Z, 5.5f - 0.1f, "didn't pass through the wall");
            Assert.AreEqual(0.15f, b.Position.Y, 0.05f, "lying on the floor");
        }

        [Test]
        public void ThrownCarcass_StopsWhereItLands_InsteadOfRolling()
        {
            var world = new BrushWorld();
            world.Add(Brush.Box(new Vector3(0, -0.5f, 0), new Vector3(100, 0.5f, 100)));
            var half = new Vector3(0.3f, 0.18f, 0.2f);
            var b = new ItemBody { Position = new Vector3(0, 2.5f, 0), Velocity = new Vector3(0, 1.5f, 13f) };
            float landedZ = float.NaN;
            for (int i = 0; i < 64 * 5 && !b.Resting; i++)
            {
                ItemPhysics.Step(ref b, world, half, 1f / 64f);
                if (float.IsNaN(landedZ) && b.Position.Y <= half.Y + 0.01f) landedZ = b.Position.Z;
            }
            Assert.IsTrue(b.Resting);
            Assert.Less(b.Position.Z - landedZ, 1f, $"slid {b.Position.Z - landedZ:0.00} m after landing");
        }

        [Test]
        public void ItemStepAndInventoryAdd_DoNotAllocate()
        {
            BrushWorld world = Floor();
            var half = new Vector3(0.1f, 0.1f, 0.1f);
            var b = new ItemBody { Position = new Vector3(0, 30f, 0), Velocity = new Vector3(1f, 0f, 0f) };
            ItemPhysics.Step(ref b, world, half, 1f / 64f);
            Inventory inv = WithTools(12, Net);
            var fly = ItemStack.Prey(Butterfly, true, CarcassQuality.Intact);
            inv.Add(fly, 10);
            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 200; i++)
                {
                    ItemPhysics.Step(ref b, world, half, 1f / 64f);
                    inv.Add(fly, 10);
                    inv.TakeOne(1, out _);
                }
            });
        }

        [Test]
        public void SaveV1Bag_MigratesToInventoryStacks()
        {
            ContentRegistry.Reset();
            Loadouts.Reset();
            HuntingCvars.AllTools.Value = false; // a play session may have left the cheat on
            var profile = new EconomyProfile();
            var slice = new EconomySaveSlice(profile, null);
            const string v1 = "{\"coins\":\"4.2;1\",\"trophies\":1,\"upgrades\":[],\"bagPrey\":[\"prey.butterfly\",\"prey.butterfly\",\"prey.rabbit\"],\"bagFlags\":[1,1,0]}";
            slice.Load(v1, 1);
            Assert.AreEqual(42.0, profile.Coins.ToDouble(), 1e-6);
            // Three slots (Phase 3.5): net, knife and the butterfly stack fit; the rabbit has no room left.
            Assert.AreEqual(2, profile.Inventory.PreyCount);
            Assert.IsTrue(profile.Inventory.HasTool(HowToHunt.Core.ContentRegistry.Get<ToolDefinition>("tool.net").Id.Value), "old saves get the starting tools");
            Assert.IsTrue(profile.Inventory.HasTool(HowToHunt.Core.ContentRegistry.Get<ToolDefinition>("tool.knife").Id.Value));
            Assert.AreEqual(2, profile.Inventory[2].Count, "two caught butterflies share a stack");

            // Round trip through v3 (tools and prey in their slots).
            string v3 = slice.Save();
            var again = new EconomyProfile();
            new EconomySaveSlice(again, null).Load(v3, 3);
            Assert.IsTrue(again.Inventory.HasTool(HowToHunt.Core.ContentRegistry.Get<ToolDefinition>("tool.net").Id.Value));
            Assert.AreEqual(2, again.Inventory.PreyCount);
            Assert.AreEqual(profile.Inventory[3].Content, again.Inventory[3].Content);
        }
    }
}
