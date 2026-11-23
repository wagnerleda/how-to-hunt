using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HowToHunt.Core
{
    /// <summary>
    /// Draws thousands of small decorations (grass tufts, flowers, pebbles) with GPU instancing instead of one
    /// GameObject each. Instances are baked by editor code into chunks; each frame only chunks inside the view
    /// frustum and draw distance are submitted. No allocations per frame. No collision (decoration only).
    /// Ground clutter thins out with distance (a layer's FarDensity): full density up close, fewer and fewer instances
    /// towards the edge of its reach, where fog hides the difference. Nothing is drawn past r_drawdistance.
    /// </summary>
    [ExecuteAlways]
    public sealed class ScatterRenderer : MonoBehaviour
    {
        [Serializable]
        public sealed class Layer
        {
            public Mesh Mesh;
            public Material[] Materials = Array.Empty<Material>();
            public float DrawDistance = 90f;
            [Tooltip("Share of the instances still drawn at the edge of the draw distance (1 = no thinning; grass ~0.15).")]
            [Range(0f, 1f)] public float FarDensity = 1f;
            public bool CastShadows;
            /// <summary>Instances sorted by chunk.</summary>
            public Matrix4x4[] Instances = Array.Empty<Matrix4x4>();
            public Chunk[] Chunks = Array.Empty<Chunk>();
        }

        [Serializable]
        public struct Chunk
        {
            public Bounds Bounds;
            public int Start;
            public int Count;
        }

        public static readonly Foundation.FloatCvar Density = new Foundation.FloatCvar("r_scatter_density", 1f,
            "Grass and flower density (0 = none, 1 = full).", Foundation.CvarFlags.Archive, 0f, 1f);
        public static readonly Foundation.FloatCvar FadeStart = new Foundation.FloatCvar("r_scatter_fade", 0.35f,
            "Where grass and flowers start thinning out, as a share of their draw distance (1 = never). Lower is faster.", Foundation.CvarFlags.Archive, 0.05f, 1f);

        [SerializeField] private List<Layer> _layers = new List<Layer>();
        private readonly Plane[] _planes = new Plane[6];

        public List<Layer> Layers => _layers;

        private void LateUpdate()
        {
            // Headless (-nographics, dedicated tests) or very old GPUs: nothing to draw with.
            if (!SystemInfo.supportsInstancing || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Camera cam = Camera.main;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null) cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            if (cam == null) return;
            float density = Application.isPlaying ? Density.Value : 1f;
            if (density <= 0f) return;
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            Vector3 eye = cam.transform.position;
            float worldReach = Application.isPlaying ? RenderDistance.DrawDistance.Value : float.MaxValue;
            float fade = Application.isPlaying ? FadeStart.Value : 1f;

            for (int l = 0; l < _layers.Count; l++)
            {
                Layer layer = _layers[l];
                if (layer.Mesh == null || layer.Instances.Length == 0) continue;
                float reach = Mathf.Min(layer.DrawDistance * RenderDistance.ScatterScale.Value, worldReach);
                float maxSq = reach * reach;
                float thinFrom = reach * fade;
                float thinSpan = Mathf.Max(0.01f, reach - thinFrom);
                for (int c = 0; c < layer.Chunks.Length; c++)
                {
                    Chunk chunk = layer.Chunks[c];
                    float sq = chunk.Bounds.SqrDistance(eye);
                    if (sq > maxSq) continue;
                    if (!GeometryUtility.TestPlanesAABB(_planes, chunk.Bounds)) continue;
                    // Instances in a chunk are in random order, so drawing the first part thins it out evenly.
                    float far = layer.FarDensity >= 1f || sq <= thinFrom * thinFrom ? 1f
                        : Mathf.Lerp(1f, layer.FarDensity, (Mathf.Sqrt(sq) - thinFrom) / thinSpan);
                    int count = Mathf.Max(1, Mathf.RoundToInt(chunk.Count * density * far));
                    for (int m = 0; m < layer.Materials.Length && m < layer.Mesh.subMeshCount; m++)
                    {
                        Material mat = layer.Materials[m];
                        if (mat == null) continue;
                        var rp = new RenderParams(mat)
                        {
                            shadowCastingMode = layer.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                            receiveShadows = true,
                            worldBounds = chunk.Bounds,
                            layer = gameObject.layer,
                        };
                        Graphics.RenderMeshInstanced(rp, layer.Mesh, m, layer.Instances, count, chunk.Start);
                    }
                }
            }
        }

        /// <summary>Editor: replace a layer's instances, grouping them into square chunks for culling.</summary>
        public void SetLayer(Mesh mesh, Material[] materials, List<Matrix4x4> instances, float chunkSize, float drawDistance, bool castShadows, float farDensity = 1f)
        {
            var buckets = new SortedDictionary<long, List<Matrix4x4>>();
            foreach (Matrix4x4 m in instances)
            {
                Vector3 p = m.GetColumn(3);
                long key = ((long)Mathf.FloorToInt(p.x / chunkSize) << 32) ^ (uint)Mathf.FloorToInt(p.z / chunkSize);
                if (!buckets.TryGetValue(key, out List<Matrix4x4> list)) buckets[key] = list = new List<Matrix4x4>();
                list.Add(m);
            }
            var all = new List<Matrix4x4>(instances.Count);
            var chunks = new List<Chunk>();
            Bounds meshBounds = mesh.bounds;
            foreach (List<Matrix4x4> list in buckets.Values)
            {
                // RenderMeshInstanced draws at most 1023 matrices per call with arrays: split big buckets.
                for (int start = 0; start < list.Count; start += 1023)
                {
                    int n = Mathf.Min(1023, list.Count - start);
                    var b = new Bounds(list[start].MultiplyPoint3x4(meshBounds.center), Vector3.zero);
                    for (int i = start; i < start + n; i++)
                    {
                        Matrix4x4 m = list[i];
                        b.Encapsulate(m.MultiplyPoint3x4(meshBounds.min));
                        b.Encapsulate(m.MultiplyPoint3x4(meshBounds.max));
                    }
                    chunks.Add(new Chunk { Bounds = b, Start = all.Count, Count = n });
                    for (int i = start; i < start + n; i++) all.Add(list[i]);
                }
            }
            _layers.Add(new Layer
            {
                Mesh = mesh,
                Materials = materials,
                DrawDistance = drawDistance,
                FarDensity = farDensity,
                CastShadows = castShadows,
                Instances = all.ToArray(),
                Chunks = chunks.ToArray(),
            });
        }
    }
}
