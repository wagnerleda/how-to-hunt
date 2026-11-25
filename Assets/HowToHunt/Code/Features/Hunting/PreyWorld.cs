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
        /// <summary>Gone because it went back to sleep in its den (a boss left alone), not taken down.</summary>
        public bool Slept;
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
        public bool SeesThreat;
        /// <summary>Host: client ids of the hunters who hurt it since it spawned (a boss rewards them all).</summary>
        public readonly List<int> Damagers = new List<int>(8);
        public float LastHurtAt = -1000f;
        /// <summary>Host: when a woken boss was last left with no hunter near it (-1: someone is near).</summary>
        public float AloneSince = -1f;
        /// <summary>Not alive because it went back to sleep (a boss left alone), not because it was taken down.</summary>
        public bool Slept;
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
        private const int SightEveryTicks = 6; // ~10 Hz line-of-sight checks per prey

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
            foreach (PreyInstance p in Instances)
            {
                if (p.Spawner.Summoned) { p.Alive = false; p.RespawnAt = 0f; continue; } // waits for its bait
                HostSpawn(p);
            }
        }

        /// <summary>Is one of these (a summoned boss) out of its den right now? Host and clients.</summary>
        public static bool IsOut(PreyDefinition def)
        {
            for (int i = 0; i < Instances.Count; i++)
                if (Instances[i].Def == def && Instances[i].Alive) return true;
            return false;
        }

        /// <summary>
        /// Host: a bait was bought: wake a summoned prey (a boss) in its den. False when there is none in this level or
        /// it is already out.
        /// </summary>
        public static bool HostSummon(PreyDefinition def)
        {
            if (!IsHost || def == null || IsOut(def)) return false;
            for (int i = 0; i < Instances.Count; i++)
            {
                PreyInstance p = Instances[i];
                if (p.Def != def || !p.Spawner.Summoned) continue;
                HostSpawn(p);
                return true;
            }
            return false;
        }

        private static void HostSpawn(PreyInstance p)
        {
            p.Alive = true;
            p.Slept = false;
            p.AloneSince = -1f;
            p.Health = p.Def.MaxHealth;
            p.Damagers.Clear();
            p.LastHurtAt = -1000f;
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
                    Stamina = p.Def.StaminaSeconds,
                    Timer = (float)Rng.NextDouble() * 2f,
                };
                // The spawn point is the spawner's height plus an offset: drop it onto the ground (hills, river bank),
                // or the prey starts buried and can never move or see out.
                PreyBehavior b = p.Def.Behavior;
                PreyBrain.PlaceOnGround(ref p.State, in b, SimWorld.Current);
            }
            Broadcast(p);
        }

        /// <summary>Host: prey taken down (captured or killed).</summary>
        public static void HostRemove(PreyInstance p)
        {
            p.Slept = false;
            p.Alive = false;
            p.RespawnAt = p.Spawner.Summoned ? 0f : Time.time + p.Spawner.RespawnSeconds; // a summoned boss waits for the next bait
            Broadcast(p);
        }

        /// <summary>
        /// Host: a woken boss with no hunter on their feet within sv_boss_sleep_range for sv_boss_sleep_after seconds goes
        /// back to sleep in its den (owner, 2026-11-25: a bear woken and then left, after a death or a trip back to camp,
        /// stayed out for good and its bait could never be bought again). No reward; the bait can be bought again.
        /// </summary>
        private static bool HostSleepIfAlone(PreyInstance p, bool hasThreat, Vec3 threat, float time)
        {
            float range = HuntingCvars.BossSleepRange.Value;
            bool near = hasThreat && Vec3.DistanceSquared(threat, p.State.Position) <= range * range;
            if (near) { p.AloneSince = -1f; return false; }
            if (p.AloneSince < 0f) { p.AloneSince = time; return false; }
            if (time - p.AloneSince < HuntingCvars.BossSleepAfter.Value) return false;
            p.Alive = false;
            p.Slept = true;
            p.Damagers.Clear();
            p.RespawnAt = 0f;
            Broadcast(p);
            return true;
        }

        /// <summary>Host: a gunshot at <paramref name="at"/>. Prey that fear guns and hear it bolt away.</summary>
        public static void HostGunshot(Vector3 at, float loudness = 1f)
        {
            if (!IsHost) return;
            Vec3 source = at.ToSim();
            float time = Time.time;
            for (int i = 0; i < Instances.Count; i++)
            {
                PreyInstance p = Instances[i];
                if (!p.Alive || p.IsFlyer) continue;
                PreyBehavior b = p.Def.Behavior;
                b.HearingRange *= loudness; // a suppressor shrinks how far prey hear the shot
                PreyBrain.Startle(ref p.State, in b, source, time);
            }
        }

        /// <summary>
        /// Host: damaged but alive (ground prey get stunned and shoved by the hit, then flee; aggressive ones turn on
        /// the hunter). <paramref name="attacker"/>: the hunter's client id (-1: unknown), remembered for boss rewards.
        /// </summary>
        public static void HostDamage(PreyInstance p, float healthAfter, Vector3 push = default, int attacker = -1)
        {
            p.Health = healthAfter;
            HostCredit(p, attacker);
            if (!p.IsFlyer)
            {
                p.State.Health = healthAfter;
                PreyBehavior b = p.Def.Behavior;
                PreyBrain.Stun(ref p.State, in b);
                PreyBrain.Knock(ref p.State, push.ToSim());
                PreyBrain.Provoke(ref p.State, in b, Time.time);
            }
        }

        /// <summary>Host: this hunter took part in bringing it down (hurt it or finished it).</summary>
        public static void HostCredit(PreyInstance p, int attacker)
        {
            p.LastHurtAt = Time.time;
            if (attacker >= 0 && !p.Damagers.Contains(attacker)) p.Damagers.Add(attacker);
        }

        /// <summary>
        /// Host: an aggressive animal's strike lands: a swipe on every standing hunter in front of it and in reach, a
        /// slam on every standing hunter on the ground around it (thrown outward).
        /// </summary>
        private static void HostStrike(PreyInstance p, in PreyBehavior b, PreyStrike strike)
        {
            bool slam = strike == PreyStrike.Slam;
            float damage = slam ? p.Def.SlamDamage : p.Def.AttackDamage;
            float pushSpeed = slam ? p.Def.SlamPush : p.Def.AttackPush;
            var players = PlayerMotor.All;
            float hullY = MovementCvars.Config.HullHalfExtents.Y;
            for (int k = 0; k < players.Count; k++)
            {
                PlayerMotor m = players[k];
                PlayerHealth health = m.GetComponent<PlayerHealth>();
                if (health == null || health.Downed) continue;
                Vec3 feet = m.State.Position - new Vec3(0f, hullY, 0f);
                bool reaches = slam
                    ? PreyBrain.SlamReaches(in p.State, in b, feet, HuntingCvars.StrikeReachSlack.Value, HuntingCvars.StrikeHeightSlack.Value)
                    : PreyBrain.StrikeReaches(in p.State, in b, feet, HuntingCvars.StrikeReachSlack.Value, HuntingCvars.StrikeHeightSlack.Value);
                if (!reaches) continue;
                health.HostDamage(damage, -1);
                if (p.Def.PoisonDamagePerSecond > 0f) health.HostPoison(p.Def.PoisonDamagePerSecond, p.Def.PoisonSeconds); // venomous bites and stings
                Vector3 away = (feet - p.State.Position).ToUnity();
                away.y = 0f;
                away = away.sqrMagnitude > 1e-4f ? away.normalized : Quaternion.Euler(0f, p.State.Yaw, 0f) * Vector3.forward;
                m.HostPush(away * pushSpeed + Vector3.up * (pushSpeed * HuntingCvars.StrikeLift.Value));
            }
        }

        /// <summary>Nearest hunter still on their feet (aggressive animals ignore downed ones), or -1.</summary>
        private static int NearestStanding(IReadOnlyList<PlayerMotor> players, Vec3 from, out Vec3 position)
        {
            position = default;
            int nearest = -1;
            float best = float.MaxValue;
            for (int k = 0; k < players.Count; k++)
            {
                PlayerHealth health = players[k].GetComponent<PlayerHealth>();
                if (health != null && health.Downed) continue;
                Vec3 pos = players[k].State.Position;
                float d = Vec3.DistanceSquared(pos, from);
                if (d < best) { best = d; position = pos; nearest = k; }
            }
            return nearest;
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

                PreyBehavior b = p.Def.Behavior;
                bool aggressive = b.Aggression != PreyAggression.None;
                // Nearest player is the threat (fighters pick the nearest one still standing).
                Vec3 threat = default;
                bool hasThreat = false;
                int nearest = -1;
                if (aggressive)
                {
                    nearest = NearestStanding(players, p.State.Position, out threat);
                    hasThreat = nearest >= 0;
                }
                else
                {
                    float best = float.MaxValue;
                    for (int k = 0; k < players.Count; k++)
                    {
                        Vec3 pos = players[k].State.Position;
                        float d = Vec3.DistanceSquared(pos, p.State.Position);
                        if (d < best) { best = d; threat = pos; hasThreat = true; nearest = k; }
                    }
                }
                // Sight: a line-of-sight trace every few ticks per prey (staggered by id), only when in range.
                if (nearest < 0) p.SeesThreat = false;
                else if ((tick + p.Id) % SightEveryTicks == 0)
                {
                    var ms = players[nearest].State;
                    Vec3 eye = ms.Position + new Vec3(0f, MovementCvars.Config.EyeOffset(ms.Ducked), 0f);
                    float scale = ms.Ducked ? HuntingCvars.CrouchSight.Value : 1f;
                    p.SeesThreat = PreyBrain.CanSee(in p.State, in b, eye, scale, SimWorld.Current);
                }
                if (aggressive)
                {
                    PreyStrike strike = PreyBrain.StepAggressive(ref p.State, in b, threat, hasThreat, p.SeesThreat, SimWorld.Current, time, dt);
                    if (strike != PreyStrike.None) HostStrike(p, in b, strike);
                    // Left alone for a while (everyone ran or went down), a fighter licks its wounds back to full; a
                    // boss that heals completely forgets who fought it.
                    if (p.Def.RegenPerSecond > 0f && p.Health < p.Def.MaxHealth && time - p.LastHurtAt > HuntingCvars.RegenDelay.Value)
                    {
                        p.Health = Mathf.Min(p.Def.MaxHealth, p.Health + p.Def.RegenPerSecond * dt);
                        p.State.Health = p.Health;
                        if (p.Health >= p.Def.MaxHealth) p.Damagers.Clear();
                    }
                }
                else PreyBrain.Step(ref p.State, in b, threat, hasThreat, p.SeesThreat, SimWorld.Current, time, dt);
                if (p.Spawner.Summoned && HostSleepIfAlone(p, hasThreat, threat, time)) continue;
                p.History.Record(tick, new EntityPose { Position = p.State.Position, Yaw = p.State.Yaw });
                p.SetHostRender();

                if (snapshot)
                {
                    SendToReady(new PreySnapshotBroadcast
                    {
                        Id = p.Id,
                        Tick = tick,
                        Position = p.State.Position.ToUnity(),
                        Yaw = (short)(ushort)(Mathf.Repeat(p.State.Yaw, 360f) / 360f * 65536f),
                        Ai = (byte)p.State.Ai,
                        HealthPct = (byte)Mathf.Clamp(Mathf.RoundToInt(p.Health / p.Def.MaxHealth * 255f), 0, 255),
                    }, Channel.Unreliable);
                }
            }
        }

        private static void Broadcast(PreyInstance p)
        {
            if (!IsHost) return;
            SendToReady(Life(p), Channel.Reliable);
        }

        /// <summary>To every client that has finished joining (others get the full state when they load in).</summary>
        private static void SendToReady<T>(T msg, Channel channel) where T : struct, IBroadcast
        {
            foreach (NetworkConnection c in _manager.ServerManager.Clients.Values)
                if (c.IsAuthenticated) _manager.ServerManager.Broadcast(c, msg, true, channel);
        }

        private static PreyLifeBroadcast Life(PreyInstance p) => new PreyLifeBroadcast
        {
            Id = p.Id,
            Alive = p.Alive,
            Slept = p.Slept,
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
            p.Slept = msg.Slept;
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
