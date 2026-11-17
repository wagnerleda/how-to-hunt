using System;
using UnityEngine;

namespace HowToHunt.Avatar
{
    /// <summary>
    /// A player's tribe mask: a 24 x 28 grid painted from a 16-colour palette, where colour 0 is cut away (so the outline
    /// and eye holes are part of the painting). Packed two cells a byte: 339 bytes on the wire and in config.cfg.
    /// Row 0 is the top of the mask.
    /// </summary>
    public static class MaskData
    {
        public const int Width = 24, Height = 28, Cells = Width * Height;
        public const byte Cut = 0;
        private const byte Version = 1;
        private const int HeaderBytes = 3;
        public const int PackedBytes = HeaderBytes + Cells / 2;

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

        /// <summary>The starting mask: a bone shield with dark eye slits and red swirls.</summary>
        public static byte[] Default()
        {
            var cells = new byte[Cells];
            for (int r = 0; r < Height; r++)
            for (int c = 0; c < Width; c++)
                cells[r * Width + c] = InsideShield(c, r) ? (byte)1 : Cut;
            // Eye slits.
            for (int c = 5; c <= 9; c++) { Set(cells, c, 10, 4); Set(cells, c, 11, 4); }
            for (int c = 14; c <= 18; c++) { Set(cells, c, 10, 4); Set(cells, c, 11, 4); }
            // Red swirls running down the face.
            for (int r = 2; r < Height - 2; r++)
            {
                int a = 6 + Mathf.RoundToInt(2.5f * Mathf.Sin(r * 0.45f));
                int b = 16 + Mathf.RoundToInt(2.5f * Mathf.Sin(r * 0.45f + 2f));
                if (r < 9 || r > 12) { Set(cells, a, r, 6); Set(cells, b, r, 6); }
            }
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

        private static void Set(byte[] cells, int c, int r, byte colour)
        {
            if (c < 0 || c >= Width || r < 0 || r >= Height) return;
            int i = r * Width + c;
            if (cells[i] != Cut) cells[i] = colour;
        }

        public static byte[] Pack(byte[] cells)
        {
            var data = new byte[PackedBytes];
            data[0] = Version;
            data[1] = Width;
            data[2] = Height;
            for (int i = 0; i < Cells; i += 2)
                data[HeaderBytes + i / 2] = (byte)((cells[i] & 0x0F) | ((cells[i + 1] & 0x0F) << 4));
            return data;
        }

        /// <summary>Unpacks into <paramref name="cells"/> (Cells long). False for anything malformed.</summary>
        public static bool Unpack(byte[] data, byte[] cells)
        {
            if (data == null || cells == null || cells.Length < Cells || data.Length != PackedBytes) return false;
            if (data[0] != Version || data[1] != Width || data[2] != Height) return false;
            for (int i = 0; i < Cells; i += 2)
            {
                byte b = data[HeaderBytes + i / 2];
                cells[i] = (byte)(b & 0x0F);
                cells[i + 1] = (byte)(b >> 4);
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
                buffer[(Height - 1 - r) * Width + c] = Palette[cells[r * Width + c] & 0x0F];
            texture.SetPixels32(buffer);
            texture.Apply(false);
        }
    }
}
