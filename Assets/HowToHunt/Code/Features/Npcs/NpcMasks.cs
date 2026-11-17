using HowToHunt.Avatar;
using UnityEngine;

namespace HowToHunt.Npcs
{
    /// <summary>Tribe masks for the camp characters, made from their key: each one different, always the same one.</summary>
    public static class NpcMasks
    {
        private static readonly byte[] Bases = { 1, 2, 3, 9, 13, 15, 5 };
        private static readonly byte[] Paints = { 6, 7, 4, 12, 11, 14, 8 };

        public static void Make(string key, byte[] cells)
        {
            var rng = new System.Random(Hash(key));
            byte baseColour = Bases[rng.Next(Bases.Length)], paint = Paints[rng.Next(Paints.Length)];
            if (paint == baseColour) paint = 4;
            int style = rng.Next(3);
            int eyeRow = 9 + rng.Next(3), eyeWidth = 3 + rng.Next(3);
            for (int r = 0; r < MaskData.Height; r++)
            for (int c = 0; c < MaskData.Width; c++)
            {
                int i = r * MaskData.Width + c;
                if (!MaskData.InsideShield(c, r)) { cells[i] = MaskData.Cut; continue; }
                int mx = c < MaskData.Width / 2 ? c : MaskData.Width - 1 - c; // mirrored column
                bool mark = style switch
                {
                    0 => (mx + r) % 6 == 0,                       // diagonal stripes
                    1 => r > eyeRow + 4 && (r - eyeRow) % 4 == 0, // bands below the eyes
                    _ => (mx % 5 == 2 && r % 5 == 2),             // dots
                };
                cells[i] = mark ? paint : baseColour;
            }
            // Eyes: dark slits, mirrored.
            for (int dc = 0; dc < eyeWidth; dc++)
            for (int dr = 0; dr < 2; dr++)
            {
                int c = 9 - dc, r = eyeRow + dr;
                cells[r * MaskData.Width + c] = 4;
                cells[r * MaskData.Width + (MaskData.Width - 1 - c)] = 4;
            }
        }

        private static int Hash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in s ?? string.Empty) h = h * 31 + ch;
                return h;
            }
        }
    }
}
