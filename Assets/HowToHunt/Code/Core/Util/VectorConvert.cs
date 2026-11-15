using System.Runtime.CompilerServices;

namespace HowToHunt.Core
{
    /// <summary>Conversions between UnityEngine and System.Numerics vectors (the Sim layer uses System.Numerics).</summary>
    public static class VectorConvert
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static System.Numerics.Vector3 ToSim(this UnityEngine.Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnityEngine.Vector3 ToUnity(this System.Numerics.Vector3 v) => new UnityEngine.Vector3(v.X, v.Y, v.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static System.Numerics.Quaternion ToSim(this UnityEngine.Quaternion q) => new System.Numerics.Quaternion(q.x, q.y, q.z, q.w);
    }
}
