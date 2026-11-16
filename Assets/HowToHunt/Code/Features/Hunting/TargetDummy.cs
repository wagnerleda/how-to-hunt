using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Sim.Combat;
using FishNet.Broadcast;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>Host → everyone: a dummy's health changed.</summary>
    public struct DummyStateBroadcast : IBroadcast
    {
        public int Index;
        public float Health;
        public bool Down;
    }

    /// <summary>
    /// A shooting-range target that walks back and forth on a deterministic path (pure function of the server tick).
    /// The host records its pose every tick for lag compensation; every machine draws it at its own view tick.
    /// Health is host-authoritative and announced with a broadcast. Place in a scene; no code needed.
    /// </summary>
    public sealed class TargetDummy : MonoBehaviour
    {
        [SerializeField] private Vector3 _pathStart;
        [SerializeField] private Vector3 _pathEnd;
        [SerializeField] private float _speed = 4f;
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _respawnSeconds = 2.5f;

        private static readonly List<TargetDummy> Registered = new List<TargetDummy>(8);
        private static readonly System.Comparison<TargetDummy> ByName = (a, b) => string.CompareOrdinal(a.name, b.name);

        private readonly PoseHistory _history = new PoseHistory();
        private Renderer[] _renderers;
        private Color[] _baseColors;
        private MaterialPropertyBlock _block;
        private float _flash;
        private Color _flashColor;
        private bool _down;
        private float _downUntil;

        public static IReadOnlyList<TargetDummy> All => Registered;
        public TargetDummyPath Path => new TargetDummyPath(_pathStart.ToSim(), _pathEnd.ToSim(), _speed);
        public PoseHistory History => _history;
        public float Health { get; private set; }
        public float MaxHealth => _maxHealth;
        public bool IsDown => _down;
        public int Index => Registered.IndexOf(this);

        public void Configure(Vector3 start, Vector3 end, float speed, float maxHealth = 100f)
        {
            _pathStart = start;
            _pathEnd = end;
            _speed = speed;
            _maxHealth = maxHealth;
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _baseColors[i] = _renderers[i].sharedMaterial != null && _renderers[i].sharedMaterial.HasProperty("_BaseColor") ? _renderers[i].sharedMaterial.GetColor("_BaseColor") : Color.white;
            _block = new MaterialPropertyBlock();
            Health = _maxHealth;
        }

        private void OnEnable()
        {
            Registered.Add(this);
            Registered.Sort(ByName);
        }

        private void OnDisable() => Registered.Remove(this);

        /// <summary>Host: record this tick's pose (called from the tick loop).</summary>
        public void RecordTick(uint tick, int tickRate)
        {
            EntityPose pose = Path.PoseAt(tick, tickRate);
            _history.Record(tick, pose);
            if (_down && Time.time >= _downUntil) SetState(_maxHealth, false);
        }

        /// <summary>Host: apply damage. Returns true if this hit took it down.</summary>
        public bool ApplyDamage(float amount)
        {
            if (_down) return false;
            float health = Mathf.Max(0f, Health - amount);
            bool killed = health <= 0f;
            SetState(health, killed);
            return killed;
        }

        /// <summary>Everyone: apply the host's state.</summary>
        public void SetState(float health, bool down)
        {
            bool wasDown = _down;
            float previous = Health;
            Health = health;
            _down = down;
            if (down && !wasDown) _downUntil = Time.time + _respawnSeconds;
            if (health < previous) Flash(down ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 1f, 1f));
            StateChanged?.Invoke(this);
        }

        public static event System.Action<TargetDummy> StateChanged;

        private void Flash(Color c)
        {
            _flash = 1f;
            _flashColor = c;
        }

        private void LateUpdate()
        {
            double tick = NetSessionTick();
            EntityPose pose = Path.PoseAt(tick, HitClock.TickRate);
            transform.SetPositionAndRotation(pose.Position.ToUnity(), Quaternion.Euler(0f, pose.Yaw, 0f));

            // Knocked down: lie flat until respawn.
            transform.localScale = _down ? new Vector3(1f, 0.15f, 1f) : Vector3.one;

            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - Time.deltaTime * 6f);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_flash <= 0f) { _renderers[i].SetPropertyBlock(null); continue; }
                    _block.SetColor("_BaseColor", Color.Lerp(_baseColors[i], _flashColor, _flash * 0.8f));
                    _renderers[i].SetPropertyBlock(_block);
                }
            }
        }

        private static double NetSessionTick()
        {
            double t = HitClock.ViewTick();
            return t > 0 ? t : Time.timeAsDouble * Foundation.GameIdentity.TickRate;
        }
    }
}
