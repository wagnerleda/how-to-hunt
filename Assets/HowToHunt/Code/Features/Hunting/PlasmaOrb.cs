using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// A Gunlance plasma orb: a white-hot core inside see-through low-poly shells in different blues, each turning,
    /// breathing and drifting at its own rate, so the layers slide over one another like plasma. Used for the charge
    /// at the muzzle, the thrown orb and its bomblets. Built once, animated without allocations.
    /// </summary>
    public sealed class PlasmaOrb : MonoBehaviour
    {
        private struct Layer
        {
            public Color Color;
            public float Scale, Breathe, BreatheHz, Spin, Drift;
            public Vector3 Axis;
        }

        // Outer haze first (drawn first), core last.
        private static readonly Layer[] Layers =
        {
            new Layer { Color = new Color(0.08f, 0.2f, 0.9f, 0.16f), Scale = 1f, Breathe = 0.08f, BreatheHz = 2.1f, Spin = 70f, Drift = 0.05f, Axis = new Vector3(0.3f, 1f, 0.2f) },
            new Layer { Color = new Color(0.3f, 0.25f, 1f, 0.2f), Scale = 0.88f, Breathe = 0.1f, BreatheHz = 3.3f, Spin = -110f, Drift = 0.04f, Axis = new Vector3(1f, 0.2f, 0.5f) },
            new Layer { Color = new Color(0.1f, 0.55f, 1f, 0.32f), Scale = 0.72f, Breathe = 0.09f, BreatheHz = 4.7f, Spin = 150f, Drift = 0.03f, Axis = new Vector3(0.2f, 0.6f, 1f) },
            new Layer { Color = new Color(0.35f, 0.9f, 1f, 0.5f), Scale = 0.52f, Breathe = 0.12f, BreatheHz = 6.5f, Spin = -220f, Drift = 0.02f, Axis = new Vector3(0.8f, 1f, 0.1f) },
            new Layer { Color = new Color(0.9f, 1f, 1f, 0.95f), Scale = 0.3f, Breathe = 0.15f, BreatheHz = 9f, Spin = 300f, Drift = 0f, Axis = new Vector3(0.5f, 0.5f, 1f) },
        };

        private readonly Transform[] _layers = new Transform[Layers.Length];
        private float _seed;

        /// <summary>Diameter in metres.</summary>
        public float Size { get; set; } = 0.1f;

        /// <summary>0..1: a fuller charge breathes faster.</summary>
        public float Energy { get; set; } = 1f;

        public static PlasmaOrb Create(string name, Transform parent)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            PlasmaOrb orb = root.AddComponent<PlasmaOrb>();
            orb.Build();
            return orb;
        }

        private void Build()
        {
            _seed = Random.value * 100f;
            Mesh mesh = ExplosionFx.OrbMesh;
            Material plasma = Resources.Load<Material>("FX/M_Plasma");
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < Layers.Length; i++)
            {
                var g = new GameObject("Layer " + i);
                g.transform.SetParent(transform, false);
                g.transform.localRotation = Random.rotation;
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = g.AddComponent<MeshRenderer>();
                if (plasma != null) r.sharedMaterial = plasma;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sortingOrder = i; // nested shells share a centre: draw outside in
                block.SetColor("_BaseColor", Layers[i].Color);
                r.SetPropertyBlock(block);
                _layers[i] = g.transform;
            }
        }

        private void LateUpdate()
        {
            float t = Time.time + _seed, dt = Time.deltaTime, rate = 0.6f + 0.6f * Energy;
            transform.localScale = Vector3.one * Size;
            for (int i = 0; i < Layers.Length; i++)
            {
                ref readonly Layer l = ref Layers[i];
                Transform tr = _layers[i];
                tr.Rotate(l.Axis, l.Spin * rate * dt, Space.Self);
                tr.localScale = Vector3.one * (l.Scale * (1f + l.Breathe * Mathf.Sin(t * l.BreatheHz * rate * Mathf.PI * 2f)));
                tr.localPosition = new Vector3(Mathf.Sin(t * 2.3f + i), Mathf.Sin(t * 1.7f + i * 2f), Mathf.Sin(t * 2.9f + i * 3f)) * l.Drift;
            }
        }
    }
}
