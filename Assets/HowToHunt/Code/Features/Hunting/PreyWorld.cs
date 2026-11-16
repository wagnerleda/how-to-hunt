using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using HowToHunt.Core;
using HowToHunt.Player;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Hunting;
using UnityEngine;
using Vec3 = System.Numerics.Vector3;

namespace HowToHunt.Hunting
{
    /// <summary>Host → clients: a prey appeared (with its seed) or is gone.</summary>
    public struct PreyLifeBroadcast : IBroadcast
    {
        public ushort Id;
        public bool Alive;
        public uint Seed;
        public Vector3 Position;
    }

    /// <summary>Host → clients: one ground prey's state at a server tick (16 Hz).</summary>
    public struct PreySnapshotBroadcast : IBroadcast
    {
        public ushort Id;
        public uint Tick;
        public Vector3 Position;
        public short Yaw;
        public byte Ai;
        public byte HealthPct;
    }

    /// <summary>One prey slot. Ids come from spawner order, identical on every machine.</summary>
    public sealed class PreyInstance
    {
        private const int BufferSize = 12;

        public ushort Id;
        public PreyDefinition Def;
        public PreySpawner Spawner;
        public bool Alive;
        public uint Seed;
        public float RespawnAt;
        public float Health;

        // Host simulation (ground prey).
        public PreyState State;
        public readonly PoseHistory History = new PoseHistory();

        // Client interpolation buffer (ground prey).
        private readonly uint[] _ticks = new uint[BufferSize];
        private readonly EntityPose[] _poses = new EntityPose[BufferSize];
        private readonly byte[] _ai = new byte[BufferSize];
        private int _count;

        public PreyVisual Visual;
        public bool IsFlyer => Def.Movement == PreyMovement.Flyer;
        public PreyAiState RenderAi { get; private set; }
        public float RenderSpeed { get; private set; }

        public Vector3 FlyerHome => Spawner.transform.position + HomeOffset(Seed, Spawner.Radius * 0.6f);

        public static Vector3 HomeOffset(uint seed, float radius)
        {
            uint r = seed * 2654435761u + 1u;
            float a = PreyBrain.Rand01(ref r) * Mathf.PI * 2f;
            float d = Mathf.Sqrt(PreyBrain.Rand01(ref r)) * radius;
            return new Vector3(Mathf.Sin(a) * d, 0f, Mathf.Cos(a) * d);
        }

        /// <summary>Pose at a (fractional) server tick: flyers from the seed, ground prey from history or snapshots.</summary>
        public bool TryPoseAt(double tick, bool isHost, out EntityPose pose)
        {
            pose = default;
            if (!Alive) return false;
            if (IsFlyer)
            {
                pose = ButterflyFlight.PoseAt(Seed, FlyerHome.ToSim(), Spawner.Radius * 0.35f + 2f, tick, HitClock.TickRate);
                return true;
            }
            if (isHost)
            {
                if (History.TrySample(tick, out pose)) return true;
                pose = new EntityPose { Position = State.Position, Yaw = State.Yaw };
                return true;
            }
            return SampleSnapshots(tick, out pose);
        }

        public void AddSnapshot(in PreySnapshotBroadcast s)
        {
            if (_count > 0 && s.Tick <= _ticks[(_count - 1) % BufferSize]) return; // old/out of order
            int i = _count % BufferSize;
            _ticks[i] = s.Tick;
            _poses[i] = new EntityPose { Position = s.Position.ToSim(), Yaw = s.Yaw * (360f / 65536f) };
            _ai[i] = s.Ai;
            _count++;
            Health = Def.MaxHealth * s.HealthPct / 255f;
        }

        public void ClearSnapshots() => _count = 0;

        private bool SampleSnapshots(double tick, out EntityPose pose)
        {
            pose = default;
            if (_count == 0) return false;
            int n = Mathf.Min(_count, BufferSize);
            int newest = (_count - 1) % BufferSize;
            // Walk back from the newest to find the pair around 'tick'.
            for (int k = 0; k < n - 1; k++)
            {
                int i1 = (newest - k + BufferSize) % BufferSize;
                int i0 = (i1 - 1 + BufferSize) % BufferSize;
                if (_ticks[i0] <= tick && tick <= _ticks[i1])
                {
                    float f = (float)((tick - _ticks[i0]) / Mathf.Max(1, _ticks[i1] - _ticks[i0]));
                    pose.Position = Vec3.Lerp(_poses[i0].Position, _poses[i1].Position, f);
                    pose.Yaw = Mathf.LerpAngle(_poses[i0].Yaw, _poses[i1].Yaw, f);
                    RenderAi = (PreyAiState)_ai[i1];
                    RenderSpeed = Vec3.Distance(_poses[i0].Position, _poses[i1].Position) / Mathf.Max(1e-3f, (_ticks[i1] - _ticks[i0]) / (float)HitClock.TickRate);
                    return true;
                }
            }
            // Outside the buffer: hold the newest (or oldest) known pose.
            int pick = tick > _ticks[newest] ? newest : (newest - n + 1 + BufferSize) % BufferSize;
            pose = _poses[pick];
            RenderAi = (PreyAiState)_ai[pick];
            RenderSpeed = 0f;
            return true;
        }

        public void SetHostRender()
        {
            RenderAi = State.Ai;
            RenderSpeed = State.Speed;
        }
    }

    /// <summary>
    /// All prey in the current level. The host simulates ground prey (bounded AI), decides life and death, records
    /// pose history for lag compensation and streams compact snapshots. Flyers (butterflies) are pure functions of
    /// seed + tick: only their life changes are sent.
    /// </summary>
    public static class PreyWorld
    {
        private const int SnapshotEveryTicks = 4; // 16 Hz

        private static readonly List<PreyInstance> Instances = new List<PreyInstance>(64);
        private static NetworkManager _manager;
        private static readonly System.Random Rng = new System.Random();

        public static IReadOnlyList<PreyInstance> All => Instances;

        private static bool IsHost => _manager != null && _manager.IsServerStarted;

        /// <summary>Builds the prey slots from the scene's spawners (same order everywhere).</summary>
        public static void Build()
        {
            foreach (PreyInstance p in Instances) if (p.Visual != null) Object.Destroy(p.Visual.gameObject);
            Instances.Clear();
            ushort id = 0;
            foreach (PreySpawner s in PreySpawner.All)
            {
                if (s.Prey == null) continue;
                for (int i = 0; i < s.Count; i++)
                {
                    var p = new PreyInstance { Id = id++, Def = s.Prey, Spawner = s, Health = s.Prey.MaxHealth };
                    p.Visual = PreyVisual.Create(p);
                    Instances.Add(p);
                }
            }
        }

        public static void Attach(NetworkManager m)
        {
            _manager = m;
            m.TimeManager.OnTick += OnTick;
            m.ClientManager.RegisterBroadcast<PreyLifeBroadcast>(OnLife);
            m.ClientManager.RegisterBroadcast<PreySnapshotBroadcast>(OnSnapshot);
            m.SceneManager.OnClientLoadedStartScenes += OnClientLoaded;
            m.ServerManager.OnServerConnectionState += args =>
            {
                if (args.ConnectionState == LocalConnectionState.Started) HostSpawnAll();
            };
        }

        public static void Detach()
        {
            _manager = null;
            foreach (PreyInstance p in Instances)
            {
                p.Alive = false;
                p.ClearSnapshots();
                if (p.Visual != null) p.Visual.gameObject.SetActive(false);
            }
        }

        public static PreyInstance Get(ushort id) => id < Instances.Count ? Instances[id] : null;

        // --- host -------------------------------------------------------------------------------------------

        private static void HostSpawnAll()
        {
            if (Instances.Count == 0) Build();
            foreach (PreyInstance p in Instances) HostSpawn(p);
        }

        private static void HostSpawn(PreyInstance p)
        {
            p.Alive = true;
            p.Health = p.Def.MaxHealth;
            p.Seed = (uint)Rng.Next(1, int.MaxValue);
            Vector3 home = p.Spawner.transform.position + PreyInstance.HomeOffset(p.Seed, p.Spawner.Radius);
            if (!p.IsFlyer)
            {
                p.State = new PreyState
                {
                    Position = home.ToSim(),
                    Home = p.Spawner.transform.position.ToSim(),
                    Yaw = (float)Rng.NextDouble() * 360f,
                    Rng = p.Seed,
                    Health = p.Def.MaxHealth,
                    Ai = PreyAiState.Idle,
                    Timer = (float)Rng.NextDouble() * 2f,
                };
            }
            Broadcast(p);
        }

        /// <summary>Host: prey taken down (captured or killed).</summary>
        public static void HostRemove(PreyInstance p)
        {
            p.Alive = false;
            p.RespawnAt = Time.time + p.Spawner.RespawnSeconds;
            Broadcast(p);
        }

        /// <summary>Host: damaged but alive (ground prey get stunned, then flee).</summary>
        public static void HostDamage(PreyInstance p, float healthAfter)
        {
            p.Health = healthAfter;
            if (!p.IsFlyer)
            {
                p.State.Health = healthAfter;
                PreyBrain.Stun(ref p.State, p.Def.Behavior);
            }
        }

        private static void OnTick()
        {
            if (!IsHost) return;
            uint tick = _manager.TimeManager.Tick;
            float dt = (float)_manager.TimeManager.TickDelta;
            float time = Time.time;
            bool snapshot = tick % SnapshotEveryTicks == 0;
            var players = PlayerMotor.All;

            for (int i = 0; i < Instances.Count; i++)
            {
                PreyInstance p = Instances[i];
                if (!p.Alive)
                {
                    if (time >= p.RespawnAt && p.RespawnAt > 0f) HostSpawn(p);
                    continue;
                }
                if (p.IsFlyer) continue;

                // Nearest player is the threat.
                Vec3 threat = default;
                bool hasThreat = false;
                float best = float.MaxValue;
                for (int k = 0; k < players.Count; k++)
                {
                    Vec3 pos = players[k].State.Position;
                    float d = Vec3.DistanceSquared(pos, p.State.Position);
                    if (d < best) { best = d; threat = pos; hasThreat = true; }
                }
                PreyBehavior b = p.Def.Behavior;
                PreyBrain.Step(ref p.State, in b, threat, hasThreat, SimWorld.Current, time, dt);
                p.History.Record(tick, new EntityPose { Position = p.State.Position, Yaw = p.State.Yaw });
                p.SetHostRender();

                if (snapshot)
                {
                    _manager.ServerManager.Broadcast(new PreySnapshotBroadcast
                    {
                        Id = p.Id,
                        Tick = tick,
                        Position = p.State.Position.ToUnity(),
                        Yaw = (short)(ushort)(Mathf.Repeat(p.State.Yaw, 360f) / 360f * 65536f),
                        Ai = (byte)p.State.Ai,
                        HealthPct = (byte)Mathf.Clamp(Mathf.RoundToInt(p.Health / p.Def.MaxHealth * 255f), 0, 255),
                    }, true, Channel.Unreliable);
                }
            }
        }

        private static void Broadcast(PreyInstance p)
        {
            if (!IsHost) return;
            _manager.ServerManager.Broadcast(Life(p));
        }

        private static PreyLifeBroadcast Life(PreyInstance p) => new PreyLifeBroadcast
        {
            Id = p.Id,
            Alive = p.Alive,
            Seed = p.Seed,
            Position = p.IsFlyer ? Vector3.zero : p.State.Position.ToUnity(),
        };

        private static void OnClientLoaded(NetworkConnection conn, bool asServer)
        {
            if (!asServer || conn.IsLocalClient) return;
            foreach (PreyInstance p in Instances) _manager.ServerManager.Broadcast(conn, Life(p));
        }

        // --- clients ----------------------------------------------------------------------------------------

        private static void OnLife(PreyLifeBroadcast msg, Channel channel)
        {
            if (IsHost) return;
            if (Instances.Count == 0) Build();
            PreyInstance p = Get(msg.Id);
            if (p == null) return;
            p.Alive = msg.Alive;
            p.Seed = msg.Seed;
            p.Health = p.Def.MaxHealth;
            p.ClearSnapshots();
            if (msg.Alive && !p.IsFlyer)
                p.AddSnapshot(new PreySnapshotBroadcast { Id = msg.Id, Tick = 0, Position = msg.Position, HealthPct = 255 });
        }

        private static void OnSnapshot(PreySnapshotBroadcast msg, Channel channel)
        {
            if (IsHost) return;
            PreyInstance p = Get(msg.Id);
            if (p != null && p.Alive) p.AddSnapshot(msg);
        }
    }
}
