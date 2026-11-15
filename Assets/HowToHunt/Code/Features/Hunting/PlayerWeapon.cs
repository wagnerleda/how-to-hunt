using System;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Object;
using HowToHunt.Core;
using HowToHunt.Player;
using HowToHunt.Sim.Combat;
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
    /// M2 dev pistol (hitscan). The owner fires locally for instant feedback and sends origin, direction and the tick
    /// it was looking at. The host validates, rewinds targets to that tick, resolves the hit against the same Sim
    /// hitboxes, then confirms to the shooter and shows the shot to everyone else.
    /// </summary>
    public sealed class PlayerWeapon : NetworkBehaviour
    {
        private const int MaxTargets = 16;
        private PlayerMotor _motor;
        private float _nextFire;
        private float _serverNextFire;

        public static event Action<PlayerWeapon, Vector3, Vector3> LocalShot;

        private void Awake() => _motor = GetComponent<PlayerMotor>();

        private void Update()
        {
            if (!IsOwner || Game.UiHasFocus || Cursor.lockState != CursorLockMode.Locked) return;
            if (!KeyBindings.WasPressedThisFrame(GameAction.Fire) || Time.time < _nextFire) return;
            _nextFire = Time.time + 1f / HuntingCvars.FireRate.Value;

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 origin = cam.transform.position;
            Vector3 dir = cam.transform.forward;
            double viewTick = HitClock.ViewTick();
            LocalShot?.Invoke(this, origin, dir);
            HitFeedback.RememberSeen(viewTick);
            CmdFire(origin, dir, viewTick);
        }

        [ServerRpc]
        private void CmdFire(Vector3 origin, Vector3 dir, double viewTick)
        {
            // Rate limit (with slack for jitter).
            if (Time.time < _serverNextFire) return;
            _serverNextFire = Time.time + 0.8f / HuntingCvars.FireRate.Value;
            if (dir.sqrMagnitude < 0.5f || float.IsNaN(dir.x)) return;
            dir.Normalize();

            // The shot must come from near the shooter's eyes as the host knows them.
            var cfg = MovementCvars.Config;
            var s = _motor.State;
            Vector3 eye = s.Position.ToUnity() + new Vector3(0f, cfg.EyeOffset(s.Ducked), 0f);
            if ((origin - eye).sqrMagnitude > 2.5f * 2.5f) origin = eye;

            // Lag compensation: rewind to the tick the shooter saw, capped by sv_maxunlag.
            uint now = TimeManager.Tick;
            int rate = TimeManager.TickRate;
            double t = HuntingCvars.LagComp.Value ? viewTick : now;
            double oldest = now - HuntingCvars.MaxUnlag.Value * rate;
            if (t < oldest) t = oldest;
            if (t > now) t = now;

            var dummies = TargetDummy.All;
            int count = Math.Min(dummies.Count, MaxTargets);
            Span<EntityPose> poses = stackalloc EntityPose[count];
            for (int i = 0; i < count; i++)
            {
                TargetDummy d = dummies[i];
                if (d.IsDown) { poses[i] = new EntityPose { Position = new System.Numerics.Vector3(0f, -1000f, 0f) }; continue; }
                if (!d.History.TrySample(t, out poses[i])) poses[i] = d.Path.PoseAt(t, rate);
            }

            float range = HuntingCvars.Range.Value;
            bool hit = HitScan.Raycast(origin.ToSim(), dir.ToSim(), range, SimWorld.Current, poses, TargetDummyPath.Hitboxes, out RayHit result);
            Vector3 end = hit ? result.Point.ToUnity() : origin + dir * range;

            if (hit && result.Target >= 0)
            {
                TargetDummy target = dummies[result.Target];
                bool head = result.Region == HitRegion.Head;
                float damage = HuntingCvars.Damage.Value * (head ? HuntingCvars.HeadMultiplier.Value : 1f);
                bool killed = target.ApplyDamage(damage);
                ServerManager.Broadcast(new DummyStateBroadcast { Index = result.Target, Health = target.Health, Down = target.IsDown });
                RpcHitConfirm(Owner, end, damage, head, killed, result.Target, poses[result.Target].Position.ToUnity(), poses[result.Target].Yaw, (float)(now - t));
            }
            RpcShotFx(origin, end);
        }

        [TargetRpc]
        private void RpcHitConfirm(NetworkConnection conn, Vector3 point, float damage, bool head, bool killed, int target, Vector3 checkedFeet, float checkedYaw, float rewoundTicks)
        {
            HitFeedback.Confirm(point, damage, head, killed, target, checkedFeet, checkedYaw, rewoundTicks);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void RpcShotFx(Vector3 origin, Vector3 end)
        {
            HitFeedback.RemoteShot(transform.position, origin, end);
        }
    }
}
