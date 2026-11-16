#nullable enable
using System;
using System.Numerics;

namespace HowToHunt.Sim.Collision
{
    /// <summary>
    /// A regular grid of heights (low-poly terrain). Each cell is split into two triangles along the same diagonal
    /// everywhere; each triangle becomes a solid prism brush, so terrain uses the same Quake-style traces as levels.
    /// Cells marked as holes (NaN at any corner) produce no brushes.
    /// </summary>
    public sealed class Heightfield
    {
        public readonly float OriginX, OriginZ, Cell;
        public readonly int CountX, CountZ; // vertices per axis
        public readonly float[] Heights;    // row-major: z * CountX + x

        public Heightfield(float originX, float originZ, float cell, int countX, int countZ, float[] heights)
        {
            if (heights.Length != countX * countZ) throw new ArgumentException("Heights must have countX * countZ entries.");
            OriginX = originX;
            OriginZ = originZ;
            Cell = cell;
            CountX = countX;
            CountZ = countZ;
            Heights = heights;
        }

        public Vector3 Vertex(int x, int z) => new Vector3(OriginX + x * Cell, Heights[z * CountX + x], OriginZ + z * Cell);

        /// <summary>Height of the triangle surface at (x, z); NaN outside the grid or over a hole.</summary>
        public float Sample(float x, float z)
        {
            float fx = (x - OriginX) / Cell, fz = (z - OriginZ) / Cell;
            int ix = (int)MathF.Floor(fx), iz = (int)MathF.Floor(fz);
            if (ix < 0 || iz < 0 || ix >= CountX - 1 || iz >= CountZ - 1) return float.NaN;
            float u = fx - ix, v = fz - iz;
            float h00 = Heights[iz * CountX + ix], h10 = Heights[iz * CountX + ix + 1];
            float h01 = Heights[(iz + 1) * CountX + ix], h11 = Heights[(iz + 1) * CountX + ix + 1];
            // Diagonal from (0,0) to (1,1): triangle (00, 01, 11) when v >= u, else (00, 11, 10).
            return v >= u ? h00 + (h11 - h01) * u + (h01 - h00) * v : h00 + (h10 - h00) * u + (h11 - h10) * v;
        }

        /// <summary>Adds one prism brush per triangle, reaching <paramref name="depth"/> below the lowest corner.</summary>
        public int AddBrushes(BrushWorld world, float depth = 4f)
        {
            int added = 0;
            for (int z = 0; z < CountZ - 1; z++)
            {
                for (int x = 0; x < CountX - 1; x++)
                {
                    Vector3 v00 = Vertex(x, z), v10 = Vertex(x + 1, z), v01 = Vertex(x, z + 1), v11 = Vertex(x + 1, z + 1);
                    if (float.IsNaN(v00.Y) || float.IsNaN(v10.Y) || float.IsNaN(v01.Y) || float.IsNaN(v11.Y)) continue;
                    float bottom = MathF.Min(MathF.Min(v00.Y, v10.Y), MathF.Min(v01.Y, v11.Y)) - depth;
                    world.Add(Brush.TerrainPrism(v00, v01, v11, bottom));
                    world.Add(Brush.TerrainPrism(v00, v11, v10, bottom));
                    added += 2;
                }
            }
            return added;
        }
    }
}
