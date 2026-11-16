using HowToHunt.Core;
using HowToHunt.Player;
using HowToHunt.Sim.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Shot and hit feedback: tracer, recoil, shot sound, hit marker (red for headshots and kills), floating damage
    /// numbers, kill thunk, and the cl_showhits debug view (blue = where you saw the target, green = where the host
    /// checked it after rewinding; they should overlap). Everything pooled.
    /// </summary>
    public sealed class HitFeedback : MonoBehaviour
    {
        private const int TracerCount = 16;
        private const int NumberCount = 12;

        private static HitFeedback _instance;
        private static double _lastSeenTick;

        private LineRenderer[] _tracers;
        private float[] _tracerLife;
        private int _nextTracer;
        private TextMeshPro[] _numbers;
        private float[] _numberLife;
        private int _nextNumber;
        private Image[] _marker;
        private float _markerLife;
        private Transform _debugSeen, _debugChecked;
        private float _debugLife;
        private AudioSource _audio;
        private AudioSource _remoteAudio;
        private AudioClip _shot, _tick, _kill;

        private void Awake()
        {
            _instance = this;
            Material tracerMat = Resources.Load<Material>("FX/M_Tracer");
            _tracers = new LineRenderer[TracerCount];
            _tracerLife = new float[TracerCount];
            for (int i = 0; i < TracerCount; i++)
            {
                var go = new GameObject($"Tracer {i}");
                go.transform.SetParent(transform, false);
                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.025f;
                lr.endWidth = 0.01f;
                lr.sharedMaterial = tracerMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.enabled = false;
                _tracers[i] = lr;
            }

            _numbers = new TextMeshPro[NumberCount];
            _numberLife = new float[NumberCount];
            for (int i = 0; i < NumberCount; i++)
            {
                var go = new GameObject($"Damage {i}");
                go.transform.SetParent(transform, false);
                TextMeshPro t = go.AddComponent<TextMeshPro>();
                t.fontSize = 3f;
                t.alignment = TextAlignmentOptions.Center;
                t.fontStyle = FontStyles.Bold;
                t.outlineWidth = 0.22f;
                t.outlineColor = new Color32(30, 20, 15, 255);
                t.rectTransform.sizeDelta = new Vector2(3f, 1f);
                go.SetActive(false);
                _numbers[i] = t;
            }

            var white = new Color(1f, 1f, 1f, 0.95f);
            _marker = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + 90f * i) * Mathf.Deg2Rad;
                _marker[i] = Hud.Rect($"HitMarker {i}", new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 14f, new Vector2(10f, 2.5f), white);
                _marker[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
                _marker[i].gameObject.SetActive(false);
            }

            _debugSeen = MakeDebugCapsule("Debug Seen", Resources.Load<Material>("FX/M_DebugSeen"));
            _debugChecked = MakeDebugCapsule("Debug Checked", Resources.Load<Material>("FX/M_DebugChecked"));

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            var remote = new GameObject("Remote Shot Audio");
            remote.transform.SetParent(transform, false);
            _remoteAudio = remote.AddComponent<AudioSource>();
            _remoteAudio.spatialBlend = 1f;
            _remoteAudio.minDistance = 3f;
            _remoteAudio.maxDistance = 80f;
            _shot = ProceduralSounds.Shot();
            _tick = ProceduralSounds.HitTick();
            _kill = ProceduralSounds.KillThunk();

            PlayerTools.LocalUse += OnLocalUse;
        }

        private void OnDestroy()
        {
            PlayerTools.LocalUse -= OnLocalUse;
            if (_instance == this) _instance = null;
        }

        private Transform MakeDebugCapsule(string name, Material m)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            foreach (HitCapsule cap in TargetDummyPath.Hitboxes)
            {
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, false);
                Vector3 a = cap.A.ToUnity(), b = cap.B.ToUnity();
                float len = (b - a).magnitude + cap.Radius * 2f;
                g.transform.localPosition = (a + b) * 0.5f;
                g.transform.localScale = new Vector3(cap.Radius * 2f, len * 0.5f, cap.Radius * 2f);
                if (m != null) g.GetComponent<MeshRenderer>().sharedMaterial = m;
                g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            root.gameObject.SetActive(false);
            return root;
        }

        public static void RememberSeen(double viewTick) => _lastSeenTick = viewTick;

        private void OnLocalUse(PlayerTools user, ToolDefinition tool, Vector3 origin, Vector3 dir)
        {
            if (tool.Archetype != Sim.Hunting.ToolArchetype.Firearm) return;
            Vector3 muzzle = origin + dir * 0.6f + Camera.main.transform.right * 0.18f - Camera.main.transform.up * 0.12f;
            // Cosmetic tracer end: stop at level geometry (the host decides what was really hit).
            Vector3 end = origin + dir * tool.Range;
            var tr = SimWorld.Current.TraceBox(origin.ToSim(), end.ToSim(), System.Numerics.Vector3.Zero);
            if (tr.Hit) end = tr.EndPosition.ToUnity();
            Tracer(muzzle, end);
            _audio.pitch = Random.Range(0.96f, 1.04f);
            _audio.PlayOneShot(_shot, 0.35f);
            if (PlayerCamera.Instance != null) PlayerCamera.Instance.Kick(1.2f);
        }

        public static void RemoteShot(Vector3 shooter, Vector3 origin, Vector3 end)
        {
            if (_instance == null) return;
            _instance.Tracer(origin, end);
            _instance._remoteAudio.transform.position = shooter;
            _instance._remoteAudio.PlayOneShot(_instance._shot, 0.4f);
        }

        public static void Confirm(Vector3 point, float damage, bool head, bool killed, int target, Vector3 checkedFeet, float checkedYaw, float rewoundTicks)
        {
            if (_instance == null) return;
            HitFeedback f = _instance;
            Marker(killed || head ? new Color(1f, 0.3f, 0.25f) : Color.white, killed ? 0.35f : 0.18f);
            f._audio.PlayOneShot(killed ? f._kill : f._tick, killed ? 0.45f : 0.3f);
            f.ShowNumber(point, damage, head || killed);

            if (HuntingCvars.ShowHits.Value && target >= 0 && target < TargetDummy.All.Count)
            {
                EntityPose seen = TargetDummy.All[target].Path.PoseAt(_lastSeenTick, HitClock.TickRate);
                f._debugSeen.SetPositionAndRotation(seen.Position.ToUnity(), Quaternion.Euler(0f, seen.Yaw, 0f));
                f._debugChecked.SetPositionAndRotation(checkedFeet, Quaternion.Euler(0f, checkedYaw, 0f));
                f._debugSeen.gameObject.SetActive(true);
                f._debugChecked.gameObject.SetActive(true);
                f._debugLife = 1.5f;
                Game.Console.Print($"[hit] rewound {rewoundTicks:0.0} ticks ({rewoundTicks * 1000f / HitClock.TickRate:0} ms), seen vs checked offset {(seen.Position.ToUnity() - checkedFeet).magnitude * 100f:0.0} cm");
            }
        }

        /// <summary>Flashes the crosshair hit marker.</summary>
        public static void Marker(Color color, float seconds)
        {
            if (_instance == null) return;
            foreach (Image i in _instance._marker)
            {
                i.color = color;
                i.gameObject.SetActive(true);
            }
            _instance._markerLife = seconds;
        }

        private void Tracer(Vector3 from, Vector3 to)
        {
            LineRenderer lr = _tracers[_nextTracer];
            _tracerLife[_nextTracer] = 0.07f;
            _nextTracer = (_nextTracer + 1) % TracerCount;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.enabled = true;
        }

        private void ShowNumber(Vector3 point, float damage, bool strong)
        {
            TextMeshPro t = _numbers[_nextNumber];
            _numberLife[_nextNumber] = 0.8f;
            _nextNumber = (_nextNumber + 1) % NumberCount;
            t.transform.position = point + Vector3.up * 0.2f;
            t.color = strong ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.95f, 0.8f);
            t.SetText("{0}", Mathf.Round(damage));
            t.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < TracerCount; i++)
            {
                if (_tracerLife[i] <= 0f) continue;
                _tracerLife[i] -= dt;
                if (_tracerLife[i] <= 0f) _tracers[i].enabled = false;
            }

            Camera cam = Camera.main;
            for (int i = 0; i < NumberCount; i++)
            {
                if (_numberLife[i] <= 0f) continue;
                _numberLife[i] -= dt;
                TextMeshPro t = _numbers[i];
                if (_numberLife[i] <= 0f) { t.gameObject.SetActive(false); continue; }
                t.transform.position += Vector3.up * (dt * 0.9f);
                if (cam != null) t.transform.rotation = Quaternion.LookRotation(t.transform.position - cam.transform.position);
                Color c = t.color;
                c.a = Mathf.Clamp01(_numberLife[i] / 0.3f);
                t.color = c;
            }

            if (_markerLife > 0f)
            {
                _markerLife -= dt;
                if (_markerLife <= 0f) foreach (Image i in _marker) i.gameObject.SetActive(false);
            }

            if (_debugLife > 0f)
            {
                _debugLife -= dt;
                if (_debugLife <= 0f)
                {
                    _debugSeen.gameObject.SetActive(false);
                    _debugChecked.gameObject.SetActive(false);
                }
            }
        }
    }
}
