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
        private readonly bool[] _explosive = new bool[BulletPool]; // the host announces the blast; no bullet impact
        private readonly float[] _orbSize = new float[BulletPool];   // plasma orb size (0 = a plain bullet)
        private readonly PlasmaOrb[] _orbs = new PlasmaOrb[BulletPool];
        private readonly float[] _ballSize = new float[BulletPool];  // an iron ball (the hand cannon), 0 = none
        private readonly Transform[] _balls = new Transform[BulletPool];
        private MaterialPropertyBlock _ballColor;
        private const float PlasmaOrbSize = 0.3f, BombletSize = 0.14f;
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

        /// <summary>One shot: a single tracer, or a fan of pellets (the same fan as the host's, from the shot seed).</summary>
        public static void FireVolley(ToolDefinition tool, Vector3 origin, Vector3 dir, ushort seed)
        {
            if (tool == null) return;
            int pellets = Mathf.Max(1, tool.Pellets);
            if (pellets == 1) { Fire(tool, origin, dir); return; }
            for (int p = 0; p < pellets; p++) Fire(tool, origin, GunMath.PelletDir(dir.ToSim(), tool.PelletSpreadDegrees, seed, p).ToUnity());
        }

        /// <summary>A shot left this muzzle in this direction: fly its tracer (with the gun's speed and drop).</summary>
        public static void Fire(ToolDefinition tool, Vector3 origin, Vector3 dir)
        {
            if (tool == null) return;
            FireWith(tool.Ballistics, origin, dir, tool.Range, tool.ExplosionRadius > 0f, tool.PlasmaOrb ? PlasmaOrbSize : 0f, tool.IronBall);
        }

        /// <summary>Plasma rounds are layered plasma orbs (PlasmaOrb), pooled with the bullets.</summary>
        private void ShowOrb(int i, Vector3 at)
        {
            if (_orbSize[i] <= 0f) { if (_orbs[i] != null) _orbs[i].gameObject.SetActive(false); return; }
            if (_orbs[i] == null) _orbs[i] = PlasmaOrb.Create("Plasma Orb", transform);
            _orbs[i].transform.position = at;
            _orbs[i].Size = _orbSize[i];
            _orbs[i].gameObject.SetActive(true);
        }

        /// <summary>The hand cannon's iron ball: a dark low-poly ball, pooled with the bullets.</summary>
        private void ShowBall(int i, Vector3 at)
        {
            if (_ballSize[i] <= 0f) { if (_balls[i] != null) _balls[i].gameObject.SetActive(false); return; }
            if (_balls[i] == null)
            {
                var g = new GameObject("Iron Ball");
                g.transform.SetParent(transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = ExplosionFx.OrbMesh;
                var r = g.AddComponent<MeshRenderer>();
                Material smoke = Resources.Load<Material>("FX/M_Smoke"); // lit, coloured per renderer
                if (smoke != null) r.sharedMaterial = smoke;
                _ballColor ??= new MaterialPropertyBlock();
                _ballColor.SetColor("_BaseColor", new Color(0.13f, 0.13f, 0.14f));
                r.SetPropertyBlock(_ballColor);
                _balls[i] = g.transform;
            }
            _balls[i].position = at;
            _balls[i].localScale = Vector3.one * _ballSize[i];
            _balls[i].gameObject.SetActive(true);
        }

        /// <summary>A cluster round burst: its orbs fly the same fan as the host's (from the shot seed).</summary>
        public static void Cluster(ToolDefinition tool, Vector3 point, ushort seed)
        {
            if (tool == null) return;
            Vector3 from = point + Vector3.up * 0.3f;
            for (int i = 0; i < tool.ClusterCount; i++)
                FireWith(Hunting.Cluster.Ballistics, from, Hunting.Cluster.Dir(seed, i).ToUnity(), Hunting.Cluster.Range(i), true, tool.PlasmaOrb ? BombletSize : 0f);
        }

        private static void FireWith(GunBallistics g, Vector3 origin, Vector3 dir, float range, bool explosive, float orb, float ball = 0f)
        {
            if (_instance == null) return;
            BulletFx f = _instance;
            int i = f._nextBullet;
            f._nextBullet = (i + 1) % BulletPool;
            f._ballistics[i] = g;
            f._bullets[i] = GunMath.Fire(origin.ToSim(), dir.ToSim(), g, range);
            f._live[i] = true;
            f._explosive[i] = explosive;
            f._orbSize[i] = orb;
            f._ballSize[i] = ball;
            f.ShowBall(i, origin);
            f._lines[i].enabled = false; // shown from the first step (no zero-length line)
            f.ShowOrb(i, origin);
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
                        if (!_explosive[i]) Impact(tr.EndPosition.ToUnity(), tr.Normal.ToUnity(), d.ToUnity());
                        end = true;
                    }
                }
                Vector3 head = _bullets[i].Position.ToUnity();
                float streak = Mathf.Min(_ballistics[i].BulletSpeed * StreakSeconds, MaxStreak, _bullets[i].Travelled);
                LineRenderer lr = _lines[i];
                lr.SetPosition(0, head - d.ToUnity() * streak);
                lr.SetPosition(1, head);
                lr.enabled = _ballSize[i] <= 0f; // an iron ball is seen, not streaked
                if (_balls[i] != null && _ballSize[i] > 0f)
                {
                    _balls[i].position = head;
                    _balls[i].Rotate(720f * dt, 0f, 0f, Space.Self); // tumbling
                }
                if (_orbs[i] != null && _orbSize[i] > 0f)
                {
                    _orbs[i].transform.position = head;
                    _orbs[i].Size = _orbSize[i];
                }
                if (end)
                {
                    _live[i] = false;
                    lr.enabled = false;
                    if (_orbs[i] != null) _orbs[i].gameObject.SetActive(false);
                    if (_balls[i] != null) _balls[i].gameObject.SetActive(false);
                }
            }

            for (int i = 0; i < MarkPool; i++)
            {
                if (_markLife[i] <= 0f) continue;
                _markLife[i] -= dt;
                if (_markLife[i] <= 0f) _marks[i].gameObject.SetActive(false);
            }
        }

        /// <summary>An explosive round went off: a burst of dust and debris, a boom, and a shake when it's close.</summary>
        public static void Explosion(Vector3 point, float radius, bool plasma = false)
        {
            if (_instance == null) return;
            BulletFx f = _instance;
            ExplosionFx.Play(point, radius, plasma); // fire, smoke and flash
            int count = Mathf.Clamp(Mathf.RoundToInt(radius * 6f), 8, 50); // plus thrown dust and grit
            for (int k = 0; k < count; k++)
            {
                Vector3 dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 1.4f + 0.2f;
                f._emit.position = point + Random.insideUnitSphere * 0.3f;
                f._emit.velocity = dir.normalized * Random.Range(2f, 4f + radius * 1.6f);
                f._emit.startSize = Random.Range(0.25f, 0.6f) * Mathf.Max(1f, radius * 0.35f);
                f._emit.startLifetime = Random.Range(0.6f, 1.4f);
                f._emit.startColor = Color.Lerp(new Color(0.55f, 0.5f, 0.42f), new Color(0.32f, 0.29f, 0.25f), Random.value);
                f._dust.Emit(f._emit, 1);
            }
            Sfx.PlayAt("impactPunch_heavy", point, 1f, Random.Range(0.45f, 0.6f));
            Camera cam = Camera.main;
            if (cam != null)
            {
                float near = 1f - Mathf.Clamp01(Vector3.Distance(cam.transform.position, point) / (radius * 6f));
                if (near > 0f && HowToHunt.Player.PlayerCamera.Instance != null) HowToHunt.Player.PlayerCamera.Instance.Kick(4f * near);
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
            Sfx.PlayAt("impactGeneric_light", point, 0.35f, Random.Range(1.1f, 1.4f));
        }
    }
}
