using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Explosions made of low-poly orbs (no textures, no transparency, in the game's flat-shaded style): a bright core
    /// that flashes and fades, fire balls bursting outward and cooling from yellow to deep red, smoke puffs that swell,
    /// rise and shrink away, plus a short light flash. A plasma blast (the Gunlance) runs the same in cyan and violet.
    /// Everything pooled and coloured per orb with a shared property block: no allocations per blast.
    /// </summary>
    public sealed class ExplosionFx : MonoBehaviour
    {
        private const int OrbPool = 220;
        private const int LightPool = 6;

        private struct Orb
        {
            public Vector3 Velocity;
            public float Life, MaxLife, Size, Rise, Drag;
            public Color From, To;
            public bool Active;
        }

        private static ExplosionFx _instance;
        private readonly Transform[] _transforms = new Transform[OrbPool];
        private readonly MeshRenderer[] _renderers = new MeshRenderer[OrbPool];
        private readonly Orb[] _orbs = new Orb[OrbPool];
        private int _next;
        private readonly Light[] _lights = new Light[LightPool];
        private readonly float[] _lightLife = new float[LightPool];
        private readonly float[] _lightPeak = new float[LightPool];
        private int _nextLight;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _instance = this;
            _block = new MaterialPropertyBlock();
            Mesh orb = LowPolyOrb();
            Material fire = Resources.Load<Material>("FX/M_Fire"), smoke = Resources.Load<Material>("FX/M_Smoke");
            for (int i = 0; i < OrbPool; i++)
            {
                var go = new GameObject("Orb");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = orb;
                var r = go.AddComponent<MeshRenderer>();
                // The first third of the pool is lit smoke, the rest glowing fire.
                r.sharedMaterial = i < OrbPool / 3 ? smoke : fire;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _transforms[i] = go.transform;
                _renderers[i] = r;
            }
            for (int i = 0; i < LightPool; i++)
            {
                var go = new GameObject("Blast Light");
                go.transform.SetParent(transform, false);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.enabled = false;
                _lights[i] = l;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public static void Play(Vector3 point, float radius, bool plasma)
        {
            if (_instance == null) return;
            _instance.Blast(point, Mathf.Max(0.5f, radius), plasma);
        }

        private void Blast(Vector3 p, float r, bool plasma)
        {
            Color core = plasma ? new Color(0.85f, 1f, 1f) : new Color(1f, 0.95f, 0.7f);
            Color hot = plasma ? new Color(0.25f, 0.95f, 1f) : new Color(1f, 0.72f, 0.18f);
            Color cool = plasma ? new Color(0.35f, 0.15f, 0.75f) : new Color(0.62f, 0.12f, 0.05f);
            Color smokeA = plasma ? new Color(0.42f, 0.38f, 0.52f) : new Color(0.5f, 0.48f, 0.45f);
            Color smokeB = plasma ? new Color(0.7f, 0.68f, 0.8f) : new Color(0.78f, 0.76f, 0.72f);

            // Core flash.
            Spawn(false, p, Vector3.zero, r * 0.55f, 0.28f, 0f, 0f, core, hot);
            // Fire balls burst out and cool.
            int fires = Mathf.Clamp(Mathf.RoundToInt(8f + r * 3f), 10, 26);
            for (int i = 0; i < fires; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.1f;
                Spawn(false, p + dir * r * 0.15f, dir.normalized * Random.Range(r * 1.6f, r * 3.2f), r * Random.Range(0.12f, 0.24f),
                    Random.Range(0.4f, 0.7f), 1.2f, 3.5f, hot, cool);
            }
            // Smoke swells and rises after the fire.
            int smokes = Mathf.Clamp(Mathf.RoundToInt(8f + r * 3f), 10, 26);
            for (int i = 0; i < smokes; i++)
            {
                Vector3 dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.2f;
                Spawn(true, p + dir * r * 0.4f, dir * Random.Range(r * 0.4f, r * 1.2f), r * Random.Range(0.16f, 0.3f),
                    Random.Range(1.1f, 1.8f), 1.4f, 1.6f, smokeA, smokeB);
            }
            Light l = _lights[_nextLight];
            _lightLife[_nextLight] = 0.35f;
            _lightPeak[_nextLight] = 6f + r;
            _nextLight = (_nextLight + 1) % LightPool;
            l.transform.position = p + Vector3.up * 0.5f;
            l.color = hot;
            l.range = r * 5f;
            l.intensity = 6f + r;
            l.enabled = true;
        }

        private void Spawn(bool smoke, Vector3 position, Vector3 velocity, float size, float life, float rise, float drag, Color from, Color to)
        {
            // Smoke uses the lit third of the pool, fire the rest.
            int start = smoke ? 0 : OrbPool / 3, count = smoke ? OrbPool / 3 : OrbPool - OrbPool / 3;
            int i = start + (_next++ % count);
            _orbs[i] = new Orb { Velocity = velocity, Life = 0f, MaxLife = life, Size = size, Rise = rise, Drag = drag, From = from, To = to, Active = true };
            _transforms[i].position = position;
            _transforms[i].rotation = Random.rotation;
            _transforms[i].localScale = Vector3.zero;
            _transforms[i].gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = 0; i < OrbPool; i++)
            {
                if (!_orbs[i].Active) continue;
                ref Orb o = ref _orbs[i];
                o.Life += dt;
                float t = o.Life / o.MaxLife;
                if (t >= 1f)
                {
                    o.Active = false;
                    _transforms[i].gameObject.SetActive(false);
                    continue;
                }
                o.Velocity *= Mathf.Max(0f, 1f - o.Drag * dt);
                o.Velocity.y += o.Rise * dt;
                Transform tr = _transforms[i];
                tr.position += o.Velocity * dt;
                // Grow fast, hold, shrink away at the end.
                float grow = 1f - (1f - Mathf.Clamp01(t / 0.18f)) * (1f - Mathf.Clamp01(t / 0.18f));
                float shrink = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
                tr.localScale = Vector3.one * (o.Size * grow * (0.4f + 0.6f * shrink) * (shrink > 0f ? 1f : 0f));
                _block.SetColor(BaseColor, Color.Lerp(o.From, o.To, Mathf.Clamp01(t * 1.4f)));
                _renderers[i].SetPropertyBlock(_block);
            }
            for (int i = 0; i < LightPool; i++)
            {
                if (_lightLife[i] <= 0f) continue;
                _lightLife[i] -= dt;
                _lights[i].intensity = _lightPeak[i] * Mathf.Clamp01(_lightLife[i] / 0.35f);
                if (_lightLife[i] <= 0f) _lights[i].enabled = false;
            }
        }

        /// <summary>An icosahedron with one normal per face: flat-shaded, 20 faces.</summary>
        private static Mesh LowPolyOrb()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] v =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var verts = new Vector3[f.Length];
            var tris = new int[f.Length];
            for (int i = 0; i < f.Length; i++)
            {
                verts[i] = v[f[i]].normalized * 0.5f;
                tris[i] = i;
            }
            var mesh = new Mesh { name = "Low-poly orb", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
