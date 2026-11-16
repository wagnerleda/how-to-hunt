using HowToHunt.Core;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Collision;
using UnityEngine;
using Vec3 = System.Numerics.Vector3;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Bullets you can see: a short bright streak flying at the gun's muzzle speed (not a laser line), and where it
    /// meets the level a puff of dust, a chip sound and a small mark that stays a while. Cosmetic: every machine flies
    /// its own copy from the shot message; the host decides hits. Pooled, no allocations per shot.
    /// </summary>
    public sealed class BulletFx : MonoBehaviour
    {
        private const int BulletPool = 32;
        private const int MarkPool = 96;
        private const float MarkLifetime = 25f;
        private const float StreakSeconds = 0.018f; // streak length = muzzle speed x this (capped)
        private const float MaxStreak = 6f;

        private static BulletFx _instance;

        private readonly Bullet[] _bullets = new Bullet[BulletPool];
        private readonly GunBallistics[] _ballistics = new GunBallistics[BulletPool];
        private readonly LineRenderer[] _lines = new LineRenderer[BulletPool];
        private readonly bool[] _live = new bool[BulletPool];
        private int _nextBullet;

        private readonly Transform[] _marks = new Transform[MarkPool];
        private readonly float[] _markLife = new float[MarkPool];
        private int _nextMark;

        private ParticleSystem _dust;
        private ParticleSystem.EmitParams _emit;

        private void Awake()
        {
            _instance = this;
            Material tracer = Resources.Load<Material>("FX/M_Tracer");
            for (int i = 0; i < BulletPool; i++)
            {
                var go = new GameObject($"Bullet {i}");
                go.transform.SetParent(transform, false);
                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.012f;
                lr.endWidth = 0.035f; // fat head, thin tail
                lr.sharedMaterial = tracer;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.enabled = false;
                _lines[i] = lr;
            }

            Material markMat = Resources.Load<Material>("FX/M_Gib");
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.08f));
            var markRoot = new GameObject("Bullet Marks").transform;
            markRoot.SetParent(transform, false);
            for (int i = 0; i < MarkPool; i++)
            {
                var go = new GameObject($"Mark {i}");
                go.transform.SetParent(markRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var r = go.AddComponent<MeshRenderer>();
                if (markMat != null) r.sharedMaterial = markMat;
                r.SetPropertyBlock(block);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _marks[i] = go.transform;
            }

            var dustGo = new GameObject("Impact Dust");
            dustGo.transform.SetParent(transform, false);
            _dust = dustGo.AddComponent<ParticleSystem>();
            _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _dust.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.15f;
            ParticleSystem.EmissionModule emission = _dust.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _dust.shape;
            shape.enabled = false;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = _dust.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 4f;
            drag.multiplyDragByParticleVelocity = true;
            ParticleSystem.SizeOverLifetimeModule size = _dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));
            ParticleSystem.ColorOverLifetimeModule fade = _dust.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var pr = dustGo.GetComponent<ParticleSystemRenderer>();
            Material dust = Resources.Load<Material>("FX/M_Dust");
            if (dust != null) pr.sharedMaterial = dust;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>A shot left this muzzle in this direction: fly its tracer (with the gun's speed and drop).</summary>
        public static void Fire(ToolDefinition tool, Vector3 origin, Vector3 dir)
        {
            if (_instance == null || tool == null) return;
            BulletFx f = _instance;
            int i = f._nextBullet;
            f._nextBullet = (i + 1) % BulletPool;
            f._ballistics[i] = tool.Ballistics;
            f._bullets[i] = GunMath.Fire(origin.ToSim(), dir.ToSim(), f._ballistics[i], tool.Range);
            f._live[i] = true;
            f._lines[i].enabled = false; // shown from the first step (no zero-length line)
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            ICollisionWorld world = SimWorld.Current;
            for (int i = 0; i < BulletPool; i++)
            {
                if (!_live[i]) continue;
                GunMath.Step(ref _bullets[i], _ballistics[i], dt, out Vec3 from, out Vec3 d, out float length);
                bool end = _bullets[i].Done;
                if (world != null && length > 0f)
                {
                    TraceResult tr = world.TraceBox(from, from + d * length, Vec3.Zero);
                    if (tr.Hit && !tr.StartSolid)
                    {
                        _bullets[i].Position = tr.EndPosition;
                        Impact(tr.EndPosition.ToUnity(), tr.Normal.ToUnity(), d.ToUnity());
                        end = true;
                    }
                }
                Vector3 head = _bullets[i].Position.ToUnity();
                float streak = Mathf.Min(_ballistics[i].BulletSpeed * StreakSeconds, MaxStreak, _bullets[i].Travelled);
                LineRenderer lr = _lines[i];
                lr.SetPosition(0, head - d.ToUnity() * streak);
                lr.SetPosition(1, head);
                lr.enabled = true;
                if (end)
                {
                    _live[i] = false;
                    lr.enabled = false;
                }
            }

            for (int i = 0; i < MarkPool; i++)
            {
                if (_markLife[i] <= 0f) continue;
                _markLife[i] -= dt;
                if (_markLife[i] <= 0f) _marks[i].gameObject.SetActive(false);
            }
        }

        /// <summary>A bullet met the level: dust kicked back out of the surface, a chip sound, a mark.</summary>
        private void Impact(Vector3 point, Vector3 normal, Vector3 dir)
        {
            if (normal.sqrMagnitude < 0.5f) normal = -dir;
            Vector3 bounce = Vector3.Reflect(dir, normal);
            for (int k = 0; k < 7; k++)
            {
                _emit.position = point + normal * 0.03f;
                _emit.velocity = (normal * Random.Range(0.8f, 2.4f) + bounce * Random.Range(0f, 1.2f) + Random.insideUnitSphere * 0.6f);
                _emit.startSize = Random.Range(0.06f, 0.14f);
                _emit.startLifetime = Random.Range(0.45f, 0.9f);
                _emit.startColor = Color.Lerp(new Color(0.78f, 0.7f, 0.56f), new Color(0.55f, 0.5f, 0.42f), Random.value);
                _dust.Emit(_emit, 1);
            }
            Transform m = _marks[_nextMark];
            _markLife[_nextMark] = MarkLifetime;
            _nextMark = (_nextMark + 1) % MarkPool;
            m.SetPositionAndRotation(point + normal * 0.012f, Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.value * 360f));
            m.localScale = Vector3.one * Random.Range(0.06f, 0.09f);
            m.gameObject.SetActive(true);
            Sfx.PlayAt("Kenney/impactGeneric_light", point, 0.35f, Random.Range(1.1f, 1.4f));
        }
    }
}
