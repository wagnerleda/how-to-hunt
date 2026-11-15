#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace HowToHunt.Sim.Collision
{
    /// <summary>
    /// Text snapshot of a map's collision brushes, exported from the scene by the editor so tests and bots can
    /// load the world without Unity. One brush per block: "brush flags minX minY minZ maxX maxY maxZ" then
    /// "plane nx ny nz d" lines.
    /// </summary>
    public static class BrushWorldCodec
    {
        public const string Extension = ".brushes";
        private const string Magic = "hthbrushes 1";

        public static string Write(IEnumerable<Brush> brushes)
        {
            var sb = new StringBuilder();
            sb.Append(Magic).Append('\n');
            foreach (Brush b in brushes)
            {
                sb.Append("brush ").Append(((int)b.Surface).ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(V(b.Min)).Append(' ').Append(V(b.Max)).Append('\n');
                foreach (Plane3 p in b.Planes)
                    sb.Append("plane ").Append(V(p.Normal)).Append(' ').Append(F(p.Distance)).Append('\n');
            }
            return sb.ToString();
        }

        public static BrushWorld Read(string text)
        {
            var world = new BrushWorld();
            var reader = new StringReader(text);
            string? line = reader.ReadLine();
            if (line == null || line.Trim() != Magic) throw new FormatException("Not a brush world file.");
            var planes = new List<Plane3>();
            SurfaceFlags flags = SurfaceFlags.None;
            Vector3 min = default, max = default;
            bool open = false;
            while ((line = reader.ReadLine()) != null)
            {
                string[] p = line.Split(' ');
                if (p[0] == "brush")
                {
                    if (open) world.Add(new Brush(planes.ToArray(), min, max, flags));
                    planes.Clear();
                    flags = (SurfaceFlags)int.Parse(p[1], CultureInfo.InvariantCulture);
                    min = new Vector3(P(p[2]), P(p[3]), P(p[4]));
                    max = new Vector3(P(p[5]), P(p[6]), P(p[7]));
                    open = true;
                }
                else if (p[0] == "plane")
                {
                    planes.Add(new Plane3(new Vector3(P(p[1]), P(p[2]), P(p[3])), P(p[4])));
                }
            }
            if (open) world.Add(new Brush(planes.ToArray(), min, max, flags));
            return world;
        }

        private static string V(Vector3 v) => F(v.X) + " " + F(v.Y) + " " + F(v.Z);
        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static float P(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
