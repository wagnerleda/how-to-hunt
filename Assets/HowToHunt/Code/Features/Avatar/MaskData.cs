using System;
using UnityEngine;

namespace HowToHunt.Avatar
{
    /// <summary>
    /// A tribe mask: a 48 x 56 grid (owner, 2026-11-25: twice the first 24 x 28) of colour indices, where 0 is cut
    /// away (so the outline and eye holes are part of the painting). Indices 1-15 are the painting palette; 16-255 a
    /// fixed colour cube, so an imported picture keeps its colours. Packed as runs (run length, colour): a painted mask
    /// is a few hundred bytes on the wire and in config.cfg, a photo at most MaxPackedBytes. Version 1 masks (24 x 28,
    /// palette only) still load, doubled up. Row 0 is the top of the mask.
    /// </summary>
    public static class MaskData
    {
        public const int Width = 48, Height = 56, Cells = Width * Height;
        public const byte Cut = 0;
        private const byte Version = 2;
        private const byte OldVersion = 1;
        private const int OldWidth = 24, OldHeight = 28;
        private const int HeaderBytes = 3;
        /// <summary>The most a packed mask can take (every run one cell long).</summary>
        public const int MaxPackedBytes = HeaderBytes + Cells * 2;

        /// <summary>The painting palette (index = colour; 0 is cut away).</summary>
        public static readonly Color32[] Palette =
        {
            new Color32(0, 0, 0, 0),         // 0 cut away
            new Color32(236, 224, 190, 255), // 1 bone
            new Color32(240, 214, 150, 255), // 2 sand
            new Color32(250, 250, 245, 255), // 3 white
            new Color32(28, 24, 22, 255),    // 4 black
            new Color32(96, 92, 90, 255),    // 5 ash
            new Color32(200, 38, 34, 255),   // 6 red
            new Color32(118, 22, 22, 255),   // 7 blood
            new Color32(232, 128, 40, 255),  // 8 orange
            new Color32(242, 204, 60, 255),  // 9 yellow
            new Color32(76, 162, 70, 255),   // 10 green
            new Color32(32, 92, 46, 255),    // 11 moss
            new Color32(46, 100, 200, 255),  // 12 blue
            new Color32(64, 190, 214, 255),  // 13 sky
            new Color32(122, 62, 172, 255),  // 14 purple
            new Color32(150, 92, 56, 255),   // 15 clay
        };

        // The colour cube after the palette: 6 reds x 8 greens x 5 blues = 240 colours (16..255).
        private const int CubeR = 6, CubeG = 8, CubeB = 5, CubeStart = 16;

        /// <summary>Every colour index (0-255) to its colour.</summary>
        public static readonly Color32[] Colours = BuildColours();

        private static Color32[] BuildColours()
        {
            var all = new Color32[256];
            Array.Copy(Palette, all, Palette.Length);
            int i = CubeStart;
            for (int r = 0; r < CubeR; r++)
            for (int g = 0; g < CubeG; g++)
            for (int b = 0; b < CubeB; b++)
                all[i++] = new Color32((byte)(r * 255 / (CubeR - 1)), (byte)(g * 255 / (CubeG - 1)), (byte)(b * 255 / (CubeB - 1)), 255);
            return all;
        }

        /// <summary>The nearest colour index (1-255, never cut) to a colour, weighted for the eye.</summary>
        public static byte Nearest(Color32 c)
        {
            int best = 1, bestD = int.MaxValue;
            for (int i = 1; i < 256; i++)
            {
                Color32 p = Colours[i];
                int dr = c.r - p.r, dg = c.g - p.g, db = c.b - p.b;
                int d = 3 * dr * dr + 4 * dg * dg + 2 * db * db;
                if (d < bestD) { bestD = d; best = i; }
            }
            return (byte)best;
        }

        /// <summary>The starting mask: a bone shield with dark eye slits and red swirls.</summary>
        public static byte[] Default()
        {
            var cells = Blank(1);
            // Eye slits (in the old 24 x 28 layout, doubled).
            for (int c = 10; c <= 19; c++) for (int r = 20; r <= 23; r++) { Set(cells, c, r, 4); Set(cells, Width - 1 - c, r, 4); }
            // Red swirls running down the face.
            for (int r = 4; r < Height - 4; r++)
            {
                float y = r * 0.5f;
                int a = Mathf.RoundToInt(2f * (6 + 2.5f * Mathf.Sin(y * 0.45f)));
                int b = Mathf.RoundToInt(2f * (16 + 2.5f * Mathf.Sin(y * 0.45f + 2f)));
                if (r < 18 || r > 25) for (int w = 0; w < 2; w++) { Set(cells, a + w, r, 6); Set(cells, b + w, r, 6); }
            }
            return cells;
        }

        /// <summary>The shield shape filled with one colour, the rest cut away.</summary>
        public static byte[] Blank(byte colour)
        {
            var cells = new byte[Cells];
            for (int r = 0; r < Height; r++)
            for (int c = 0; c < Width; c++)
                cells[r * Width + c] = InsideShield(c, r) ? colour : Cut;
            return cells;
        }

        /// <summary>A rounded shield: wide at the brow, narrowing to the chin.</summary>
        public static bool InsideShield(int c, int r)
        {
            float x = (c + 0.5f - Width * 0.5f) / (Width * 0.5f);
            float y = (r + 0.5f) / Height; // 0 top .. 1 bottom
            float half = y < 0.12f ? Mathf.Sqrt(Mathf.Max(0f, 1f - (0.12f - y) / 0.12f * ((0.12f - y) / 0.12f))) * 0.92f
                : y > 0.7f ? 0.92f - (y - 0.7f) / 0.3f * 0.5f : 0.92f;
            return Mathf.Abs(x) <= half;
        }

        /// <summary>Paints a cell if it is part of the mask (cut cells stay cut).</summary>
        public static void Set(byte[] cells, int c, int r, byte colour)
        {
            if (c < 0 || c >= Width || r < 0 || r >= Height) return;
            int i = r * Width + c;
            if (cells[i] != Cut) cells[i] = colour;
        }

        public static byte[] Pack(byte[] cells)
        {
            var buffer = new byte[MaxPackedBytes];
            buffer[0] = Version;
            buffer[1] = Width;
            buffer[2] = Height;
            int at = HeaderBytes;
            for (int i = 0; i < Cells;)
            {
                byte v = cells[i];
                int run = 1;
                while (i + run < Cells && run < 255 && cells[i + run] == v) run++;
                buffer[at++] = (byte)run;
                buffer[at++] = v;
                i += run;
            }
            var data = new byte[at];
            Buffer.BlockCopy(buffer, 0, data, 0, at);
            return data;
        }

        /// <summary>Unpacks into <paramref name="cells"/> (Cells long). False for anything malformed.</summary>
        public static bool Unpack(byte[] data, byte[] cells)
        {
            if (data == null || cells == null || cells.Length < Cells || data.Length < HeaderBytes) return false;
            if (data[0] == OldVersion) return UnpackOld(data, cells);
            if (data[0] != Version || data[1] != Width || data[2] != Height || data.Length > MaxPackedBytes || (data.Length - HeaderBytes) % 2 != 0) return false;
            int i = 0;
            for (int at = HeaderBytes; at < data.Length; at += 2)
            {
                int run = data[at];
                if (run == 0 || i + run > Cells) return false;
                byte v = data[at + 1];
                for (int k = 0; k < run; k++) cells[i++] = v;
            }
            return i == Cells;
        }

        /// <summary>A first-version mask (24 x 28, two palette cells a byte), each cell doubled both ways.</summary>
        private static bool UnpackOld(byte[] data, byte[] cells)
        {
            const int oldCells = OldWidth * OldHeight;
            if (data.Length != HeaderBytes + oldCells / 2 || data[1] != OldWidth || data[2] != OldHeight) return false;
            for (int i = 0; i < oldCells; i++)
            {
                byte b = data[HeaderBytes + i / 2];
                byte v = (byte)((i & 1) == 0 ? b & 0x0F : b >> 4);
                int c = (i % OldWidth) * 2, r = (i / OldWidth) * 2;
                cells[r * Width + c] = cells[r * Width + c + 1] = cells[(r + 1) * Width + c] = cells[(r + 1) * Width + c + 1] = v;
            }
            return true;
        }

        public static string ToText(byte[] cells) => Convert.ToBase64String(Pack(cells));

        public static bool FromText(string text, byte[] cells)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            try { return Unpack(Convert.FromBase64String(text.Trim()), cells); }
            catch (FormatException) { return false; }
        }

        /// <summary>A point-filtered texture for a mask (row 0 at the top).</summary>
        public static Texture2D NewTexture(string name)
        {
            return new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
        }

        public static void Paint(Texture2D texture, byte[] cells, Color32[] buffer)
        {
            for (int r = 0; r < Height; r++)
            for (int c = 0; c < Width; c++)
                buffer[(Height - 1 - r) * Width + c] = Colours[cells[r * Width + c]];
            texture.SetPixels32(buffer);
            texture.Apply(false);
        }

        /// <summary>
        /// Pastes a picture over the mask (owner, 2026-11-25: import an image onto your mask). It covers the whole
        /// mask, centred, keeping its proportions; the mask's outline and eye holes stay cut, and the picture's
        /// see-through pixels leave the paint under them.
        /// </summary>
        public static void PasteImage(Color32[] pixels, int width, int height, byte[] cells)
        {
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length < width * height) return;
            // Cover: the picture's span (in its pixels) that one mask cell takes, the same both ways.
            float scale = Mathf.Min(width / (float)Width, height / (float)Height);
            float x0 = (width - Width * scale) * 0.5f, y0 = (height - Height * scale) * 0.5f;
            for (int r = 0; r < Height; r++)
            for (int c = 0; c < Width; c++)
            {
                int i = r * Width + c;
                if (cells[i] == Cut) continue;
                // Average the pixels under this cell (row 0 is the top; textures start at the bottom).
                int sx0 = Mathf.FloorToInt(x0 + c * scale), sx1 = Mathf.Max(sx0 + 1, Mathf.FloorToInt(x0 + (c + 1) * scale));
                int top = Mathf.FloorToInt(y0 + r * scale), bottom = Mathf.Max(top + 1, Mathf.FloorToInt(y0 + (r + 1) * scale));
                long sr = 0, sg = 0, sb = 0, sa = 0, n = 0;
                for (int sy = top; sy < bottom; sy++)
                {
                    int row = (height - 1 - Mathf.Clamp(sy, 0, height - 1)) * width;
                    for (int sx = sx0; sx < sx1; sx++)
                    {
                        Color32 p = pixels[row + Mathf.Clamp(sx, 0, width - 1)];
                        sr += p.r * p.a; sg += p.g * p.a; sb += p.b * p.a; sa += p.a; n++;
                    }
                }
                if (n == 0 || sa < n * 128) continue; // mostly see-through: keep the paint
                cells[i] = Nearest(new Color32((byte)(sr / sa), (byte)(sg / sa), (byte)(sb / sa), 255));
            }
        }
    }
}
