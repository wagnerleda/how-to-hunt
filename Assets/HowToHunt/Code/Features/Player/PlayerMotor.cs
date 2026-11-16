using System;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using HowToHunt.Core;
using HowToHunt.Sim.Movement;
using UnityEngine;

namespace HowToHunt.Player
{
    /// <summary>
    /// Network adapter for player movement. The owner predicts with <see cref="PlayerMove"/> (Sim layer) every tick;
    /// the host runs the same code on the received commands and sends back authoritative state; the owner reconciles
    /// and replays. Remote players are smoothed by Fish-Net's tick smoother on the Visual child.
    /// Root transform = center of the collision hull.
    /// </summary>
    public sealed class PlayerMotor : TickNetworkBehaviour
    {
        public struct ReplicateData : IReplicateData
        {
            // Packed to 7 bytes (bandwidth). The owner simulates the unpacked values too, so prediction stays exact.
            public sbyte Forward;
            public sbyte Side;
            public ushort Yaw;   // 0..65535 = 0..360°
            public short Pitch;  // -32767..32767 = -90..90°
            public byte Buttons;
            private uint _tick;

            public ReplicateData(in MoveCmd cmd)
            {
                Forward = (sbyte)Mathf.RoundToInt(Mathf.Clamp(cmd.Forward, -1f, 1f) * 127f);
                Side = (sbyte)Mathf.RoundToInt(Mathf.Clamp(cmd.Side, -1f, 1f) * 127f);
                Yaw = (ushort)(Mathf.RoundToInt(Mathf.Repeat(cmd.Yaw, 360f) / 360f * 65536f) & 0xFFFF);
                Pitch = (short)Mathf.RoundToInt(Mathf.Clamp(cmd.Pitch, -90f, 90f) / 90f * 32767f);
                Buttons = (byte)cmd.Buttons;
                _tick = 0;
            }

            public MoveCmd ToCmd() => new MoveCmd
            {
                Forward = Forward / 127f,
                Side = Side / 127f,
                Yaw = Yaw * (360f / 65536f),
                Pitch = Pitch * (90f / 32767f),
                Buttons = (MoveButtons)Buttons,
            };
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public bool OnGround;
            public bool JumpHeld;
            public bool Ducked;
            public byte Mode;
            public Vector3 CheckpointFeet;
            public float CheckpointYaw;
            private uint _tick;

            public ReconcileData(in MoveState s, Vector3 checkpointFeet, float checkpointYaw)
            {
                CheckpointFeet = checkpointFeet;
                CheckpointYaw = checkpointYaw;
                Position = s.Position.ToUnity();
                Velocity = s.Velocity.ToUnity();
                OnGround = s.OnGround;
                JumpHeld = s.JumpHeld;
                Ducked = s.Ducked;
                Mode = (byte)s.Mode;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>Raised on the owning client when its local player spawns.</summary>
        public static event Action<PlayerMotor> LocalPlayerStarted;

        public static PlayerMotor Local { get; private set; }

        private static readonly System.Collections.Generic.List<PlayerMotor> Spawned = new System.Collections.Generic.List<PlayerMotor>(8);

        /// <summary>All spawned players on this machine (for footsteps, HUD, bots).</summary>
        public static System.Collections.Generic.IReadOnlyList<PlayerMotor> All => Spawned;

        /// <summary>Distance walked since the last footstep sound (audio bookkeeping).</summary>
        [System.NonSerialized] public float FootstepDistance;

        /// <summary>Jumped / landed / ducked events for camera, sound and particles (any player, real ticks only).</summary>
        public static event Action<PlayerMotor, MoveEvents, float> MoveFeedback;

        /// <summary>Owner only: each real tick's command and the resulting state (demo recording, speedometer).</summary>
        public static event Action<PlayerMotor, MoveCmd, MoveState> CmdSimulated;

        private readonly PlayerInputSampler _input = new PlayerInputSampler();
        private MoveState _state;
        private ReplicateData _lastTicked;
        private Vector3 _checkpointFeet;
        private float _checkpointYaw;

        public MoveState State => _state;
        public PlayerInputSampler Input => _input;
        /// <summary>View yaw used for visuals (owner: live mouse, others: last simulated command).</summary>
        public float ViewYaw { get; private set; }
        public float ViewPitch { get; private set; }

        private void Awake()
        {
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!Spawned.Contains(this)) Spawned.Add(this);
            _state = new MoveState { Position = transform.position.ToSim() };
            _checkpointFeet = transform.position - new Vector3(0f, MovementCvars.Config.HullHalfExtents.Y, 0f);
            _checkpointYaw = transform.eulerAngles.y;
            _input.Yaw = transform.eulerAngles.y;
            ViewYaw = _input.Yaw;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            Spawned.Remove(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;
            Local = this;
            LocalPlayerStarted?.Invoke(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local == this) Local = null;
        }

        private void Update()
        {
            if (!IsOwner) return;
            _input.UpdateFrame();
            ViewYaw = _input.Yaw;
            ViewPitch = _input.Pitch;
        }

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(IsOwner ? new ReplicateData(_input.BuildCmd((float)TimeManager.TickDelta)) : default);
        }

        protected override void TimeManager_OnPostTick() => CreateReconcile();

        public override void CreateReconcile()
        {
            // The host sends corrections every Nth tick (bandwidth); clients always keep their local copy.
            if (IsServerStarted && TimeManager.Tick % (uint)MovementCvars.ReconcileInterval.Value != 0) return;
            PerformReconcile(new ReconcileData(in _state, _checkpointFeet, _checkpointYaw));
        }

        [Replicate]
        private void PerformReplicate(ReplicateData rd, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            // Spectated players on a non-host client: we don't know their future inputs yet.
            // Repeat their last known input for one tick, then coast, to keep them close to real time.
            if (!IsServerStarted && !IsOwner)
            {
                if (state.ContainsTicked())
                {
                    _lastTicked = rd;
                }
                else if (state.IsFuture())
                {
                    rd = rd.GetTick() - _lastTicked.GetTick() > 1 ? default : _lastTicked;
                    rd.Buttons &= unchecked((byte)~(byte)MoveButtons.Jump);
                }
            }

            MoveCmd cmd = rd.ToCmd();
            PlayerMove.Step(ref _state, in cmd, MovementCvars.Config, SimWorld.Current, (float)TimeManager.TickDelta);
            ZoneEvent zoneEvent = ApplyZones(out float newYaw);
            // Feedback only for real ticks: replays during reconciliation would double the sounds and camera kicks.
            if (state.ContainsTicked() && !state.ContainsReplayed())
            {
                if (zoneEvent != ZoneEvent.None)
                {
                    if (IsOwner && zoneEvent != ZoneEvent.Checkpoint) _input.Yaw = newYaw;
                    ZoneFeedback?.Invoke(this, zoneEvent);
                }
                if (IsOwner) CmdSimulated?.Invoke(this, cmd, _state);
                if (_state.Events != MoveEvents.None) MoveFeedback?.Invoke(this, _state.Events, _state.LandSpeed);
            }
            transform.SetPositionAndRotation(_state.Position.ToUnity(), Quaternion.Euler(0f, cmd.Yaw, 0f));
            if (!IsOwner)
            {
                ViewYaw = cmd.Yaw;
                ViewPitch = cmd.Pitch;
            }
        }

        [Reconcile]
        private void PerformReconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            _state.Position = rd.Position.ToSim();
            _state.Velocity = rd.Velocity.ToSim();
            _state.OnGround = rd.OnGround;
            _state.JumpHeld = rd.JumpHeld;
            _state.Ducked = rd.Ducked;
            _state.Mode = (MoveMode)rd.Mode;
            _checkpointFeet = rd.CheckpointFeet;
            _checkpointYaw = rd.CheckpointYaw;
            transform.position = rd.Position;
        }

        /// <summary>Cheat: toggle noclip. Validated on the host (sv_cheats).</summary>
        [ServerRpc]
        public void CmdToggleNoclip()
        {
            if (!HowToHunt.Foundation.ConsoleSystem.Global.Cheats.Value) return;
            _state.Mode = _state.Mode == MoveMode.Noclip ? MoveMode.Walk : MoveMode.Noclip;
            _state.Velocity = System.Numerics.Vector3.Zero;
        }

        /// <summary>Cheat: teleport (feet position). Validated on the host (sv_cheats).</summary>
        [ServerRpc]
        public void CmdSetPos(Vector3 feet)
        {
            if (!HowToHunt.Foundation.ConsoleSystem.Global.Cheats.Value) return;
            ServerTeleport(feet + new Vector3(0f, MovementCvars.Config.Hull(_state.Ducked).Y, 0f));
        }

        public enum ZoneEvent : byte { None, Checkpoint, Respawned, Teleported }

        /// <summary>Checkpoint reached / respawned after a hazard / teleported (any player, real ticks only).</summary>
        public static event Action<PlayerMotor, ZoneEvent> ZoneFeedback;

        /// <summary>
        /// Checkpoint, hazard and teleport zones. Runs inside the predicted tick on both host and owner so the outcome
        /// is predicted (no snap). Allocation-free: a linear scan over a handful of zones.
        /// </summary>
        private ZoneEvent ApplyZones(out float yaw)
        {
            yaw = 0f;
            if (_state.Mode == MoveMode.Noclip) return ZoneEvent.None;
            Vector3 center = _state.Position.ToUnity();
            Vector3 half = MovementCvars.Config.Hull(_state.Ducked).ToUnity();
            ZoneEvent result = ZoneEvent.None;
            var zones = Zone.All;
            for (int i = 0; i < zones.Count; i++)
            {
                Zone z = zones[i];
                if (!z.Overlaps(center, half)) continue;
                z.GetSpawn(out Vector3 feet, out float spawnYaw);
                switch (z.Kind)
                {
                    case ZoneKind.Checkpoint:
                        if ((feet - _checkpointFeet).sqrMagnitude > 0.01f)
                        {
                            _checkpointFeet = feet;
                            _checkpointYaw = spawnYaw;
                            result = ZoneEvent.Checkpoint;
                        }
                        break;
                    case ZoneKind.Hazard:
                        PlaceAt(_checkpointFeet);
                        yaw = _checkpointYaw;
                        return ZoneEvent.Respawned;
                    case ZoneKind.Teleport:
                        _checkpointFeet = feet;
                        _checkpointYaw = spawnYaw;
                        PlaceAt(feet);
                        yaw = spawnYaw;
                        return ZoneEvent.Teleported;
                }
            }
            return result;
        }

        private void PlaceAt(Vector3 feet)
        {
            _state.Ducked = false;
            _state.Position = (feet + new Vector3(0f, MovementCvars.Config.HullHalfExtents.Y, 0f)).ToSim();
            _state.Velocity = System.Numerics.Vector3.Zero;
            _state.OnGround = false;
        }

        /// <summary>Host only: replace the whole movement state (demo playback).</summary>
        public void ServerSetState(in MoveState s)
        {
            _state = s;
            _state.Events = MoveEvents.None;
            transform.position = s.Position.ToUnity();
        }

        /// <summary>Server-side teleport (respawn, checkpoints).</summary>
        public void ServerTeleport(Vector3 hullCenter)
        {
            _state.Position = hullCenter.ToSim();
            _state.Velocity = System.Numerics.Vector3.Zero;
            transform.position = hullCenter;
            // Tell a remote owner directly: its prediction would otherwise keep simulating from the old spot
            // (a reconcile alone did not move it in the M3 lag-compensation test).
            if (IsServerStarted && Owner.IsValid && !Owner.IsLocalClient) RpcTeleported(Owner, hullCenter);
        }

        [FishNet.Object.TargetRpc]
        private void RpcTeleported(FishNet.Connection.NetworkConnection conn, Vector3 hullCenter)
        {
            _state.Position = hullCenter.ToSim();
            _state.Velocity = System.Numerics.Vector3.Zero;
            _state.OnGround = false;
            transform.position = hullCenter;
        }
    }
}
