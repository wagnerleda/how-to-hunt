using HowToHunt.Avatar;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>Tribe masks: what a player paints arrives the same at everyone else, and junk is refused.</summary>
    public sealed class MaskTests
    {
        [Test]
        public void Mask_SurvivesPackingAndText()
        {
            byte[] cells = MaskData.Default();
            cells[5] = 13;
            cells[MaskData.Cells - 1] = 15;
            byte[] packed = MaskData.Pack(cells);
            Assert.AreEqual(MaskData.PackedBytes, packed.Length);
            Assert.LessOrEqual(packed.Length, Net.PartyRoster.MaxMaskBytes, "fits the join message");
            var back = new byte[MaskData.Cells];
            Assert.IsTrue(MaskData.Unpack(packed, back));
            CollectionAssert.AreEqual(cells, back);
            var fromText = new byte[MaskData.Cells];
            Assert.IsTrue(MaskData.FromText(MaskData.ToText(cells), fromText));
            CollectionAssert.AreEqual(cells, fromText);
        }

        [Test]
        public void Mask_RefusesJunk()
        {
            var cells = new byte[MaskData.Cells];
            Assert.IsFalse(MaskData.Unpack(new byte[10], cells));
            Assert.IsFalse(MaskData.Unpack(null, cells));
            Assert.IsFalse(MaskData.FromText("not base64 !!", cells));
            Assert.IsFalse(MaskData.FromText(string.Empty, cells));
        }

        [Test]
        public void DefaultMask_HasAnOutlineAndEyes()
        {
            byte[] cells = MaskData.Default();
            Assert.AreEqual(MaskData.Cut, cells[0], "the corner is cut away");
            Assert.AreEqual(4, cells[10 * MaskData.Width + 6], "a dark eye slit");
            Assert.AreNotEqual(MaskData.Cut, cells[14 * MaskData.Width + MaskData.Width / 2], "the middle of the face is mask");
        }
    }
}
