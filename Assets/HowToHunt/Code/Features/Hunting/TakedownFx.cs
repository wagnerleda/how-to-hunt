using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Sim.Hunting;
using UnityEngine;
using Vec3 = System.Numerics.Vector3;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Stylized gore, always on (docs/DESIGN.md): chunky blood cubes sprayed along the shot, fur/wing chunks that
    /// bounce and settle on the ground, flat blood splats that stay a while; overkill (ruined carcass) is three times
    /// messier. Captures get a sparkle puff instead. Everything is pooled and placed with the sim's own collision,
    /// so it costs no allocations after warm-up and looks the same for every player.
    /// </summary>
    public sealed class TakedownFx : MonoBehaviour
    {
        private const int GibPool = 96;
        private const int SplatPool = 48;
        private const float Gravity = 16f;
        private const float GibLifetime = 9f;
        private const float SplatLifetime = 30f;

        private static TakedownFx _instance;

        private ParticleSystem _spray;
        private ParticleSystem _sparkle;
        private ParticleSystem.EmitParams _emit;

        private Transform[] _gibs;
        private MeshRenderer[] _gibRenderers;
        private Vector3[] _gibVelocity;
        private Vector3[] _gibSpin;
        private float[] _gibLife;
        private float[] _gibSize;
        private bool[] _gibResting;
        private int _nextGib;

        private Transform[] _splats;
        private MeshRenderer[] _splatRenderers;
        private float[] _splatLife;
        private float[] _splatSize;
        private int _nextSplat;

        private Material _gibMaterial;
        private readonly Dictionary<Color32, Material> _tinted = new Dictionary<Color32, Material>();

        private void Awake()
        {
            _instance = this;
            Mesh cube = CubeMesh();
            _gibMaterial = Resources.Load<Material>("FX/M_Gib");
            Material particles = Resources.Load<Material>("FX/M_Gore");

            _spray = MakeParticles("Blood Spray", cube, particles, 512, 1f);
            _sparkle = MakeParticles("Capture Sparkle", cube, particles, 256, -0.15f);

            var gibRoot = new GameObject("Gibs").transform;
            gibRoot.SetParent(transform, false);
            _gibs = new Transform[GibPool];
            _gibRenderers = new MeshRenderer[GibPool];
            _gibVelocity = new Vector3[GibPool];
            _gibSpin = new Vector3[GibPool];
            _gibLife = new float[GibPool];
            _gibSize = new float[GibPool];
            _gibResting = new bool[GibPool];
            for (int i = 0; i < GibPool; i++)
            {
                _gibs[i] = MakeBlock($"Gib {i}", gibRoot, cube, out _gibRenderers[i]);
                _gibRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            var splatRoot = new GameObject("Splats").transform;
            splatRoot.SetParent(transform, false);
            _splats = new Transform[SplatPool];
            _splatRenderers = new MeshRenderer[SplatPool];
            _splatLife = new float[SplatPool];
            _splatSize = new float[SplatPool];
            for (int i = 0; i < SplatPool; i++)
            {
                _splats[i] = MakeBlock($"Splat {i}", splatRoot, cube, out _splatRenderers[i]);
                _splatRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            foreach (Material m in _tinted.Values) Destroy(m);
        }

        private static Mesh CubeMesh()
        {
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh m = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            return m;
        }

        private static Transform MakeBlock(string name, Transform parent, Mesh mesh, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            go.SetActive(false);
            return go.transform;
        }

        private ParticleSystem MakeParticles(string name, Mesh mesh, Material material, int max, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true;
            main.gravityModifier = gravity;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = mesh;
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        private Material Tinted(Color color)
        {
            Color32 key = color;
            if (_tinted.TryGetValue(key, out Material m)) return m;
            m = _gibMaterial != null ? new Material(_gibMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            _tinted[key] = m;
            return m;
        }

        /// <summary>Plays the effect of a take-down (or a wounding hit) on this machine.</summary>
        public static void Play(TakedownOutcome outcome, PreyDefinition prey, Vector3 point, Vector3 direction, CarcassQuality quality, float damage)
        {
            if (_instance == null || prey == null) return;
            if (direction.sqrMagnitude < 1e-4f) direction = Vector3.forward;
            direction.Normalize();
            switch (outcome)
            {
                case TakedownOutcome.Captured:
                    _instance.Sparkle(point);
                    Sfx.PlayAt("cloth", point, 0.6f);
                    break;
                case TakedownOutcome.Damaged:
                    _instance.Gore(prey, point, direction, 0.35f);
                    PreyVisual.FlashNear(prey, point);
                    PreySounds.PlayHit(prey, point, killed: false);
                    break;
                case TakedownOutcome.Killed:
                    bool ruined = quality == CarcassQuality.Ruined;
                    _instance.Gore(prey, point, direction, ruined ? 3f : 1f);
                    PreySounds.PlayHit(prey, point, killed: true);
                    Sfx.PlayAt(ruined ? "impactPunch_heavy" : "impactSoft_heavy", point, ruined ? 0.9f : 0.6f, Random.Range(0.85f, 1f));
                    break;
            }
        }

        private void Gore(PreyDefinition prey, Vector3 point, Vector3 direction, float amount)
        {
            float scale = Mathf.Clamp(prey.VisualScale, 0.4f, 3f);
            Color blood = prey.BloodColor;

            int drops = Mathf.RoundToInt(16f * amount) + 4;
            for (int i = 0; i < drops; i++)
            {
                Vector3 v = (direction * Random.Range(0.6f, 1.6f) + Random.insideUnitSphere * 0.9f + Vector3.up * 0.8f) * Random.Range(1.5f, 4f);
                _emit.position = point;
                _emit.velocity = v;
                _emit.startSize = Random.Range(0.03f, 0.08f) * scale;
                _emit.startLifetime = Random.Range(0.35f, 0.7f);
                _emit.startColor = Color.Lerp(blood, blood * 0.6f, Random.value);
                _emit.rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, 0f);
                _spray.Emit(_emit, 1);
            }

            if (amount >= 1f)
            {
                int gibs = Mathf.Max(1, Mathf.RoundToInt(prey.GibCount * amount));
                for (int i = 0; i < gibs; i++)
                {
                    Color c = (i & 1) == 0 ? prey.GibColor : Color.Lerp(blood, prey.GibColor, 0.25f);
                    SpawnGib(point, direction, Tinted(c), Random.Range(0.06f, 0.13f) * scale);
                }
            }

            int splats = amount >= 3f ? 5 : amount >= 1f ? 2 : 1;
            for (int i = 0; i < splats; i++)
            {
                Vector3 at = point + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f)) * amount + direction * (0.3f * i);
                float size = Random.Range(0.25f, 0.55f) * scale * (amount >= 3f ? 1.5f : amount >= 1f ? 1f : 0.6f);
                SpawnSplat(at, Tinted(blood * 0.8f), size);
            }
        }

        private void SpawnGib(Vector3 point, Vector3 direction, Material material, float size)
        {
            int i = _nextGib;
            _nextGib = (_nextGib + 1) % GibPool;
            Transform t = _gibs[i];
            t.position = point;
            t.rotation = Random.rotation;
            t.localScale = Vector3.one * size;
            _gibRenderers[i].sharedMaterial = material;
            _gibVelocity[i] = direction * Random.Range(1f, 3f) + Random.insideUnitSphere * 2f + Vector3.up * Random.Range(2.5f, 5f);
            _gibSpin[i] = Random.insideUnitSphere * 720f;
            _gibLife[i] = GibLifetime + Random.value * 3f;
            _gibSize[i] = size;
            _gibResting[i] = false;
            t.gameObject.SetActive(true);
        }

        private void SpawnSplat(Vector3 at, Material material, float size)
        {
            Vec3 start = (at + Vector3.up * 0.5f).ToSim();
            var tr = SimWorld.Current.TraceBox(start, start - new Vec3(0f, 6f, 0f), Vec3.Zero);
            if (!tr.Hit || tr.StartSolid) return;
            int i = _nextSplat;
            _nextSplat = (_nextSplat + 1) % SplatPool;
            Transform t = _splats[i];
            Vector3 normal = tr.Normal.ToUnity();
            Vector3 ground = tr.EndPosition.ToUnity() + normal * (0.006f + 0.0004f * i);
            t.SetPositionAndRotation(ground, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.value * 360f, 0f));
            t.localScale = new Vector3(size, 0.008f, size * Random.Range(0.6f, 1f));
            _splatRenderers[i].sharedMaterial = material;
            _splatLife[i] = SplatLifetime;
            _splatSize[i] = size;
            t.gameObject.SetActive(true);
        }

        private void Sparkle(Vector3 point)
        {
            for (int i = 0; i < 26; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                _emit.position = point + dir * 0.1f;
                _emit.velocity = dir * Random.Range(1f, 2.6f);
                _emit.startSize = Random.Range(0.03f, 0.07f);
                _emit.startLifetime = Random.Range(0.45f, 0.9f);
                _emit.startColor = Color.Lerp(new Color(1f, 0.97f, 0.75f), new Color(0.7f, 1f, 0.6f), Random.value);
                _emit.rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, 0f);
                _sparkle.Emit(_emit, 1);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < GibPool; i++)
            {
                if (_gibLife[i] <= 0f) continue;
                Transform t = _gibs[i];
                _gibLife[i] -= dt;
                if (_gibLife[i] <= 0f) { t.gameObject.SetActive(false); continue; }
                if (_gibLife[i] < 0.6f) t.localScale = Vector3.one * (_gibSize[i] * _gibLife[i] / 0.6f);
                if (_gibResting[i]) continue;

                Vector3 v = _gibVelocity[i];
                v.y -= Gravity * dt;
                Vector3 from = t.position;
                Vector3 to = from + v * dt;
                var tr = SimWorld.Current.TraceBox(from.ToSim(), to.ToSim(), Vec3.Zero);
                if (tr.Hit && !tr.StartSolid)
                {
                    Vector3 n = tr.Normal.ToUnity();
                    to = tr.EndPosition.ToUnity() + n * (_gibSize[i] * 0.5f);
                    v = Vector3.Reflect(v, n) * 0.35f;
                    _gibSpin[i] *= 0.5f;
                    if (n.y > 0.6f && v.sqrMagnitude < 1f)
                    {
                        _gibResting[i] = true;
                        v = Vector3.zero;
                    }
                }
                _gibVelocity[i] = v;
                t.position = to;
                t.Rotate(_gibSpin[i] * dt, Space.World);
            }

            for (int i = 0; i < SplatPool; i++)
            {
                if (_splatLife[i] <= 0f) continue;
                _splatLife[i] -= dt;
                Transform t = _splats[i];
                if (_splatLife[i] <= 0f) { t.gameObject.SetActive(false); continue; }
                if (_splatLife[i] < 2f)
                {
                    float k = _splatLife[i] / 2f;
                    Vector3 s = t.localScale;
                    t.localScale = new Vector3(_splatSize[i] * k, s.y, s.z * (_splatSize[i] * k) / Mathf.Max(s.x, 1e-4f));
                }
            }
        }
    }
}
