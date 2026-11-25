using HowToHunt.Avatar;
using HowToHunt.Economy;
using UnityEngine;

namespace HowToHunt.Npcs
{
    /// <summary>
    /// Tribe masks for the camp characters, made from their key (each one different, always the same one), with no
    /// eyes, and the sign of their trade in the middle (owner, 2026-11-25): a dollar sign for traders, a bullet for the
    /// gunsmith, a bag or a cart wheel for the other shops. Characters about camp wear the pattern alone.
    /// </summary>
    public static class NpcMasks
    {
        public enum Emblem : byte { None, Dollar, Bullet, Bag, Wheel }

        private static readonly byte[] Bases = { 1, 2, 3, 9, 13, 15, 5 };
        private static readonly byte[] Paints = { 6, 7, 4, 12, 11, 14, 8 };
        private const byte Dark = 4, Light = 3;

        public static Emblem EmblemFor(NpcDefinition npc)
        {
            if (npc == null || !npc.HasStation) return Emblem.None;
            switch (npc.Station)
            {
                case CampStationKind.Sell: return Emblem.Dollar;
                case CampStationKind.Workshop: return Emblem.Bullet;
                default: return (Hash(npc.Key) & 1) == 0 ? Emblem.Bag : Emblem.Wheel;
            }
        }

        public static void Make(NpcDefinition npc, byte[] cells) => Make(npc != null ? npc.Key : string.Empty, EmblemFor(npc), cells);

        public static void Make(string key, Emblem emblem, byte[] cells)
        {
            var rng = new System.Random(Hash(key));
            byte baseColour = Bases[rng.Next(Bases.Length)], paint = Paints[rng.Next(Paints.Length)];
            if (paint == baseColour) paint = Dark;
            int style = rng.Next(3);
            const int w = MaskData.Width, h = MaskData.Height;
            for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                int i = r * w + c;
                if (!MaskData.InsideShield(c, r)) { cells[i] = MaskData.Cut; continue; }
                int mx = c < w / 2 ? c : w - 1 - c; // mirrored column
                // Patterns round the edge, leaving the middle for the emblem.
                bool edge = mx < 9 || r < 6 || r > h - 9;
                bool mark = style switch
                {
                    0 => (mx + r) % 10 < 2,                    // diagonal stripes
                    1 => r > 36 && (r - 36) % 7 < 2,           // bands under the emblem
                    _ => mx % 8 == 4 && r % 8 == 4,            // dots
                } && (emblem == Emblem.None || edge);
                cells[i] = mark ? paint : baseColour;
            }
            byte ink = Contrast(baseColour);
            switch (emblem)
            {
                case Emblem.Dollar: Dollar(cells, ink); break;
                case Emblem.Bullet: Bullet(cells, ink); break;
                case Emblem.Bag: Bag(cells, ink); break;
                case Emblem.Wheel: Wheel(cells, ink); break;
            }
        }

        /// <summary>Dark ink on light masks, white on dark ones.</summary>
        private static byte Contrast(byte colour)
        {
            Color32 c = MaskData.Colours[colour];
            return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f > 120f ? Dark : Light;
        }

        // --- emblems, drawn in cells round the middle of the face (centre column 24, row ~26) -----------------------

        private const float Cx = MaskData.Width * 0.5f, Cy = 26f;

        private static void Fill(byte[] cells, byte colour, System.Func<float, float, bool> inside)
        {
            for (int r = 0; r < MaskData.Height; r++)
            for (int c = 0; c < MaskData.Width; c++)
                if (inside(c + 0.5f - Cx, r + 0.5f - Cy)) MaskData.Set(cells, c, r, colour);
        }

        /// <summary>
        /// An S made of two rings touching at the middle (the top one without its lower right quarter, the bottom one
        /// without its upper left), with a bar through it.
        /// </summary>
        private static void Dollar(byte[] cells, byte ink)
        {
            const float radius = 5.5f, thick = 2.4f;
            Fill(cells, ink, (x, y) =>
            {
                float uy = y + radius, ly = y - radius;
                bool upper = Mathf.Abs(Mathf.Sqrt(x * x + uy * uy) - radius) < thick * 0.5f && !(x > 0f && uy > 0f);
                bool lower = Mathf.Abs(Mathf.Sqrt(x * x + ly * ly) - radius) < thick * 0.5f && !(x < 0f && ly < 0f);
                bool bar = Mathf.Abs(x) < 1f && Mathf.Abs(y) < radius * 2f + 3f;
                return upper || lower || bar;
            });
        }

        /// <summary>An upright cartridge: pointed bullet, a band, the case and its rim.</summary>
        private static void Bullet(byte[] cells, byte ink)
        {
            const float half = 3.5f, tip = -13f, neck = -4f, rim = 12f;
            Fill(cells, ink, (x, y) =>
            {
                if (y < tip || y > rim + 1.5f) return false;
                if (y < neck)
                {
                    float t = (y - tip) / (neck - tip); // 0 at the tip
                    return Mathf.Abs(x) < half * Mathf.Sqrt(t);
                }
                if (y > rim) return Mathf.Abs(x) < half + 1.2f; // rim
                return Mathf.Abs(x) < half;
            });
            byte band = ink == Dark ? (byte)5 : (byte)2; // the crimp line, a shade off
            Fill(cells, band, (x, y) => Mathf.Abs(x) < 3.5f && y >= -4f && y < -2.5f);
        }

        /// <summary>A tied sack: round belly, a pinched neck, two ears of cloth.</summary>
        private static void Bag(byte[] cells, byte ink)
        {
            Fill(cells, ink, (x, y) =>
            {
                float by = y - 3f;
                bool belly = x * x / (9f * 9f) + by * by / (8.5f * 8.5f) < 1f;
                bool neck = y > -9f && y < -4f && Mathf.Abs(x) < 2.5f;
                bool ears = y > -13f && y <= -9f && Mathf.Abs(Mathf.Abs(x) - 2.5f) < 1.6f + (y + 9f) * 0.25f;
                return belly || neck || ears;
            });
            byte tie = ink == Dark ? (byte)15 : (byte)6;
            Fill(cells, tie, (x, y) => y > -8f && y < -6f && Mathf.Abs(x) < 3.5f);
        }

        /// <summary>A cart wheel: rim, hub and eight spokes.</summary>
        private static void Wheel(byte[] cells, byte ink)
        {
            const float outer = 10f, rimThick = 2f, hub = 2.4f, spoke = 0.75f;
            Fill(cells, ink, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d > outer) return false;
                if (d > outer - rimThick || d < hub) return true;
                float a = Mathf.Atan2(y, x);
                float nearest = Mathf.Round(a / (Mathf.PI / 4f)) * (Mathf.PI / 4f); // the closest of eight spokes
                return d * Mathf.Abs(Mathf.Sin(a - nearest)) < spoke;
            });
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
