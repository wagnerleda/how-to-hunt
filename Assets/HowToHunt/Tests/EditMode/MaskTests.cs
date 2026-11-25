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
            cells[700] = 200; // a colour from the cube (imported pictures)
            Assert.LessOrEqual(MaskData.MaxPackedBytes, Net.PartyRoster.MaxMaskBytes, "even a photo fits the join message");
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
            Assert.AreEqual(4, cells[21 * MaskData.Width + 13], "a dark eye slit");
            Assert.AreNotEqual(MaskData.Cut, cells[28 * MaskData.Width + MaskData.Width / 2], "the middle of the face is mask");
        }

        [Test]
        public void OldMask_LoadsDoubled()
        {
            // A first-version mask: 24 x 28 cells, two a byte. Cell (5, 3) red, the rest bone.
            var old = new byte[3 + 24 * 28 / 2];
            old[0] = 1; old[1] = 24; old[2] = 28;
            for (int i = 3; i < old.Length; i++) old[i] = 0x11;
            int k = 3 * 24 + 5;
            old[3 + k / 2] = (byte)((k & 1) == 0 ? 0x16 : 0x61);
            var cells = new byte[MaskData.Cells];
            Assert.IsTrue(MaskData.Unpack(old, cells));
            Assert.AreEqual(6, cells[6 * MaskData.Width + 10]);
            Assert.AreEqual(6, cells[7 * MaskData.Width + 11]);
            Assert.AreEqual(1, cells[6 * MaskData.Width + 12]);
        }

        [Test]
        public void PastedImage_KeepsTheOutlineAndSeeThroughParts()
        {
            byte[] cells = MaskData.Default();
            byte eye = cells[21 * MaskData.Width + 13];
            // A 96 x 112 picture: left half pure red, right half see-through.
            const int w = 96, h = 112;
            var pixels = new UnityEngine.Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = x < w / 2 ? new UnityEngine.Color32(200, 38, 34, 255) : new UnityEngine.Color32(0, 0, 0, 0);
            MaskData.PasteImage(pixels, w, h, cells);
            Assert.AreEqual(MaskData.Cut, cells[0], "the outline stays cut");
            Assert.AreEqual(6, cells[28 * MaskData.Width + 10], "red lands on the palette red");
            Assert.AreEqual(6, cells[21 * MaskData.Width + 13], "the left eye slit was paint, so the picture covers it");
            Assert.AreEqual(eye, cells[21 * MaskData.Width + 34], "the right eye slit is under see-through pixels");
            Assert.AreEqual(1, cells[30 * MaskData.Width + 40], "see-through pixels keep the bone under them");
        }
    }
}
