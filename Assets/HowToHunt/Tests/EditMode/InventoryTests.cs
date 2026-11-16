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

        [Test]
        public void ToolsComeFirst_PreyStack_AndFullSlotsRefuse()
        {
            var inv = new Inventory();
            inv.Configure(new uint[] { Net, Knife }, 4);
            Assert.AreEqual(ItemKind.Tool, inv[0].Kind);
            Assert.AreEqual(Knife, inv[1].Content);

            var fly = ItemStack.Prey(Butterfly, true, CarcassQuality.Intact);
            for (int i = 0; i < 12; i++) Assert.AreEqual(0, inv.AddPrey(fly, 10), $"butterfly {i}");
            Assert.AreEqual(10, inv[2].Count, "first stack full");
            Assert.AreEqual(2, inv[3].Count, "overflow starts a second stack");

            var rabbit = ItemStack.Prey(Rabbit, false, CarcassQuality.Intact);
            Assert.IsFalse(inv.CanAdd(rabbit, 1), "no free slot for a rabbit");
            Assert.AreEqual(1, inv.AddPrey(rabbit, 1));

            // Killed and caught butterflies don't share a stack.
            var deadFly = ItemStack.Prey(Butterfly, false, CarcassQuality.Intact);
            Assert.IsTrue(inv.CanAdd(deadFly, 10) == false);
        }

        [Test]
        public void TakeOne_EmptiesSlot_AndCapacityChangeReturnsOverflow()
        {
            var inv = new Inventory();
            inv.Configure(new uint[] { Net }, 3);
            inv.AddPrey(ItemStack.Prey(Rabbit, false, CarcassQuality.Intact), 1);
            inv.AddPrey(ItemStack.Prey(Rabbit, true, CarcassQuality.Intact), 1);
            Assert.IsFalse(inv.TakeOne(0, out _), "tools can't be thrown");
            Assert.IsTrue(inv.TakeOne(1, out ItemStack taken));
            Assert.IsFalse(taken.Captured);
            Assert.IsTrue(inv[1].IsEmpty);
            Assert.AreEqual(1, inv.PreyCount);

            inv.AddPrey(ItemStack.Prey(Rabbit, false, CarcassQuality.Ruined), 1);
            var overflow = inv.Configure(new uint[] { Net, Knife }, 3); // a new tool takes a slot
            Assert.AreEqual(1, overflow.Count, "one prey no longer fits");
            Assert.AreEqual(2, inv.ToolCount);
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
        public void ItemStepAndInventoryAdd_DoNotAllocate()
        {
            BrushWorld world = Floor();
            var half = new Vector3(0.1f, 0.1f, 0.1f);
            var b = new ItemBody { Position = new Vector3(0, 30f, 0), Velocity = new Vector3(1f, 0f, 0f) };
            ItemPhysics.Step(ref b, world, half, 1f / 64f);
            var inv = new Inventory();
            inv.Configure(new uint[] { Net }, 12);
            var fly = ItemStack.Prey(Butterfly, true, CarcassQuality.Intact);
            inv.AddPrey(fly, 10);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                ItemPhysics.Step(ref b, world, half, 1f / 64f);
                inv.AddPrey(fly, 10);
                inv.TakeOne(1, out _);
            }
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
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
            Assert.AreEqual(3, profile.Inventory.PreyCount);
            Assert.AreEqual(2, profile.Inventory.ToolCount, "net and knife are owned from the start");
            Assert.AreEqual(2, profile.Inventory[2].Count, "two caught butterflies share a stack");

            // Round trip through v2.
            string v2 = slice.Save();
            var again = new EconomyProfile();
            new EconomySaveSlice(again, null).Load(v2, 2);
            Assert.AreEqual(3, again.Inventory.PreyCount);
            Assert.AreEqual(profile.Inventory[3].Content, again.Inventory[3].Content);
        }
    }
}
