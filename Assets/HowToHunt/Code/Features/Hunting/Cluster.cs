using HowToHunt.Sim.Combat;
using Vec3 = System.Numerics.Vector3;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Cluster orbs (the Gunlance's plasma orb bursts into these): thrown up and out from the blast in a fan that
    /// depends only on the shot seed, so host (damage) and clients (what they see) fly the same ones. Each goes off
    /// when its fuse (its flight length) runs out or when it hits something.
    /// </summary>
    public static class Cluster
    {
        public static readonly GunBallistics Ballistics = new GunBallistics { BulletSpeed = 9f, BulletGravity = 9.8f };
        private const float SpreadDegrees = 65f;

        public static Vec3 Dir(ushort seed, int index) => GunMath.PelletDir(Vec3.UnitY, SpreadDegrees, (uint)seed * 7919u + 13u, index + 1);

        /// <summary>Flight length (the fuse): 0.55 to 0.95 s, staggered so the orbs pop one after another.</summary>
        public static float Range(int index) => Ballistics.BulletSpeed * (0.55f + 0.08f * (index % 6));

        public static int ToolIndex(ToolDefinition tool)
        {
            var tools = Loadouts.AllTools;
            for (int i = 0; i < tools.Count; i++) if (tools[i] == tool) return i;
            return 0;
        }
    }
}
