using HowToHunt.Avatar;
using HowToHunt.Npcs;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>Camp characters' masks (owner, 2026-11-25): the sign of their trade, and never any eyes.</summary>
    public sealed class NpcMaskTests
    {
        [TestCase(NpcMasks.Emblem.None)]
        [TestCase(NpcMasks.Emblem.Dollar)]
        [TestCase(NpcMasks.Emblem.Bullet)]
        [TestCase(NpcMasks.Emblem.Bag)]
        [TestCase(NpcMasks.Emblem.Wheel)]
        public void NpcMask_HasNoEyeHolesAndShowsItsEmblem(NpcMasks.Emblem emblem)
        {
            var cells = new byte[MaskData.Cells];
            NpcMasks.Make("npc.test", emblem, cells);
            for (int r = 0; r < MaskData.Height; r++)
            for (int c = 0; c < MaskData.Width; c++)
                if (MaskData.InsideShield(c, r)) Assert.AreNotEqual(MaskData.Cut, cells[r * MaskData.Width + c], $"a hole at {c},{r}");
            var plain = new byte[MaskData.Cells];
            NpcMasks.Make("npc.test", NpcMasks.Emblem.None, plain);
            int differ = 0;
            for (int i = 0; i < MaskData.Cells; i++) if (cells[i] != plain[i]) differ++;
            if (emblem == NpcMasks.Emblem.None) Assert.AreEqual(0, differ);
            else Assert.Greater(differ, 40, "the emblem is drawn big enough to read");
        }
    }
}
