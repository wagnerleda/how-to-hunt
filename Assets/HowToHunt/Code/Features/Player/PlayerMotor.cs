using System;
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
            public float Forward;
            public float Side;
            public float Yaw;
            public float Pitch;
            public byte Buttons;
            private uint _tick;

            public ReplicateData(in MoveCmd cmd)
            {
                Forward = cmd.Forward;
                Side = cmd.Side;
                Yaw = cmd.Yaw;
                Pitch = cmd.Pitch;
                Buttons = (byte)cmd.Buttons;
                _tick = 0;
            }

            public MoveCmd ToCmd() => new MoveCmd { Forward = Forward, Side = Side, Yaw = Yaw, Pitch = Pitch, Buttons = (MoveButtons)Buttons };
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
            private uint _tick;

            public ReconcileData(in MoveState s)
            {
                Position = s.Position.ToUnity();
                Velocity = s.Velocity.ToUnity();
                OnGround = s.OnGround;
                JumpHeld = s.JumpHeld;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>Raised on the owning client when its local player spawns.</summary>
        public static event Action<PlayerMotor> LocalPlayerStarted;

        public static PlayerMotor Local { get; private set; }

        private readonly PlayerInputSampler _input = new PlayerInputSampler();
        private MoveState _state;
        private ReplicateData _lastTicked;

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
            _state = new MoveState { Position = transform.position.ToSim() };
            _input.Yaw = transform.eulerAngles.y;
            ViewYaw = _input.Yaw;
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
            PerformReplicate(IsOwner ? new ReplicateData(_input.BuildCmd()) : default);
        }

        protected override void TimeManager_OnPostTick() => CreateReconcile();

        public override void CreateReconcile()
        {
            PerformReconcile(new ReconcileData(in _state));
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
            transform.position = rd.Position;
        }

        /// <summary>Server-side teleport (respawn, checkpoints).</summary>
        public void ServerTeleport(Vector3 hullCenter)
        {
            _state.Position = hullCenter.ToSim();
            _state.Velocity = System.Numerics.Vector3.Zero;
            transform.position = hullCenter;
        }
    }
}
