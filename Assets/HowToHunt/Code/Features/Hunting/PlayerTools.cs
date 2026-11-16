using System;
using FishNet.Connection;
using FishNet.Object;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Player;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Hunting;
using UnityEngine;
using UnityEngine.InputSystem;
using Vec3 = System.Numerics.Vector3;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// The player's hunting tools (hotbar 1-9, Q = previous). The owner plays the swing or shot instantly and sends
    /// the tool, origin, direction and view tick; the host validates ownership, cooldown and origin, rewinds every
    /// target to that tick, picks what the tool reaches (net/blade arc, firearm ray) and resolves it with the
    /// take-down rules. Results go back to the user; takedown effects go to everyone.
    /// </summary>
    public sealed class PlayerTools : NetworkBehaviour
    {
        private const ushort NoPrey = ushort.MaxValue;
        private const int MaxTools = 16;

        private PlayerMotor _motor;
        private readonly float[] _cooldownUntil = new float[MaxTools];
        private readonly float[] _serverCooldownUntil = new float[MaxTools];
        private int _equipped = -1;
        private int _previous = -1;

        public static event Action<PlayerTools, ToolDefinition, Vector3, Vector3> LocalUse;
        public static event Action<ToolDefinition> LocalEquipped;

        // Test hooks (wpn_autofire): aim at a dummy exactly as drawn here, using the pistol.
        public static int AutoFireTarget = -1;
        public static int AutoFireShots;
        public static int ShotsFired, HitsConfirmed;

        public static PlayerTools Local { get; private set; }
        public ToolDefinition Equipped => _equipped >= 0 && _equipped < Loadouts.AllTools.Count ? Loadouts.AllTools[_equipped] : null;

        private void Awake() => _motor = GetComponent<PlayerMotor>();

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;
            Local = this;
            EquipFirstOwned();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local == this) Local = null;
        }

        public void EquipFirstOwned()
        {
            var tools = Loadouts.AllTools;
            for (int i = 0; i < tools.Count; i++)
                if (Loadouts.Local.Owns(tools[i])) { Equip(i); return; }
        }

        private void Equip(int index)
        {
            if (index == _equipped || index < 0 || index >= Loadouts.AllTools.Count) return;
            if (!Loadouts.Local.Owns(Loadouts.AllTools[index]))
            {
                HuntFeedback.Message($"You don't own the {Loadouts.AllTools[index].DisplayName} yet. Buy it at camp.", warning: true);
                return;
            }
            _previous = _equipped;
            _equipped = index;
            LocalEquipped?.Invoke(Equipped);
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (AutoFireShots > 0) { AutoFire(); return; }
            if (Game.UiHasFocus || Cursor.lockState != CursorLockMode.Locked) return;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                for (int slot = 1; slot <= 9; slot++)
                {
                    if (!kb[Key.Digit1 + (slot - 1)].wasPressedThisFrame) continue;
                    var tools = Loadouts.AllTools;
                    for (int i = 0; i < tools.Count; i++) if (tools[i].Slot == slot) Equip(i);
                }
                if (kb.qKey.wasPressedThisFrame && _previous >= 0) Equip(_previous);
            }

            ToolDefinition tool = Equipped;
            if (tool == null) return;
            bool trigger = tool.Archetype == ToolArchetype.Firearm ? KeyBindings.WasPressedThisFrame(GameAction.Fire) : KeyBindings.IsPressed(GameAction.Fire);
            if (!trigger || Time.time < _cooldownUntil[_equipped]) return;
            Camera cam = Camera.main;
            if (cam != null) Use(cam.transform.position, cam.transform.forward);
        }

        private void AutoFire()
        {
            Camera cam = Camera.main;
            int pistol = -1;
            var tools = Loadouts.AllTools;
            for (int i = 0; i < tools.Count; i++) if (tools[i].Archetype == ToolArchetype.Firearm) pistol = i;
            if (cam == null || pistol < 0 || AutoFireTarget < 0 || AutoFireTarget >= TargetDummy.All.Count) { AutoFireShots = 0; return; }
            _equipped = pistol;
            if (Time.time < _cooldownUntil[pistol]) return;
            AutoFireShots--;
            EntityPose pose = TargetDummy.All[AutoFireTarget].Path.PoseAt(HitClock.ViewTick(), HitClock.TickRate);
            Vector3 target = pose.Position.ToUnity() + new Vector3(0f, 0.8f, 0f);
            // Aim from the simulated eye (what the host checks against), not the camera, which may still be easing after a teleport.
            var s = _motor.State;
            Vector3 eye = s.Position.ToUnity() + new Vector3(0f, MovementCvars.Config.EyeOffset(s.Ducked), 0f);
            HitLog($"autofire: view tick {HitClock.ViewTick():0.0} rate {HitClock.TickRate} dummy {AutoFireTarget} ({TargetDummy.All[AutoFireTarget].name}) aim {target} from {eye}");
            Use(eye, (target - eye).normalized);
        }

        private void Use(Vector3 origin, Vector3 dir)
        {
            ToolDefinition tool = Equipped;
            _cooldownUntil[_equipped] = Time.time + tool.Cooldown;
            ShotsFired++;
            double viewTick = HitClock.ViewTick();
            HitFeedback.RememberSeen(viewTick);
            LocalUse?.Invoke(this, tool, origin, dir);
            CmdUse((byte)_equipped, origin, dir, viewTick);
        }

        [ServerRpc]
        private void CmdUse(byte toolIndex, Vector3 origin, Vector3 dir, double viewTick)
        {
            var tools = Loadouts.AllTools;
            if (toolIndex >= tools.Count || toolIndex >= MaxTools) { HitLog("bad tool index"); return; }
            ToolDefinition tool = tools[toolIndex];
            Loadout loadout = Loadouts.ForClient(Owner.ClientId);
            if (!loadout.Owns(tool)) { HitLog("tool not owned"); return; }
            if (Time.time < _serverCooldownUntil[toolIndex]) { HitLog("cooldown"); return; }
            _serverCooldownUntil[toolIndex] = Time.time + tool.Cooldown * 0.8f;
            if (dir.sqrMagnitude < 0.5f || float.IsNaN(dir.x)) return;
            dir.Normalize();

            var cfg = MovementCvars.Config;
            var s = _motor.State;
            Vector3 eye = s.Position.ToUnity() + new Vector3(0f, cfg.EyeOffset(s.Ducked), 0f);
            if ((origin - eye).sqrMagnitude > 2.5f * 2.5f) origin = eye;

            uint now = TimeManager.Tick;
            double t = HuntingCvars.LagComp.Value ? viewTick : now;
            double oldest = now - HuntingCvars.MaxUnlag.Value * TimeManager.TickRate;
            if (t < oldest) t = oldest;
            if (t > now) t = now;

            ToolStats stats = loadout.StatsFor(tool, out float range);
            ToolResolver.Target target = ToolResolver.Find(tool, range, origin, dir, t);
            Vector3 end = target.Kind == ToolResolver.TargetKind.None ? origin + dir * range : target.Point;
            if (HuntingCvars.HitDebug.Value)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"{tool.Key} -> {target.Kind} {target.Index}/{target.PreyId} at {(target.Point - origin).magnitude:0.0} m, rewound {now - t:0.0} ticks; origin {origin}, eye {eye}");
                for (int i = 0; i < TargetDummy.All.Count; i++)
                {
                    TargetDummy dm = TargetDummy.All[i];
                    bool sampled = dm.History.TrySample(t, out EntityPose p);
                    if (!sampled) p = dm.Path.PoseAt(t, HitClock.TickRate);
                    Vector3 c = p.Position.ToUnity() + Vector3.up * 0.8f;
                    float miss = Vector3.Cross(dir, c - origin).magnitude;
                    sb.Append($"; dummy {i} {(sampled ? "hist" : "path")} {c} ray passes {miss:0.00} m");
                }
                HitLog(sb.ToString());
            }

            switch (target.Kind)
            {
                case ToolResolver.TargetKind.Prey:
                    ResolvePrey(tool, stats, target, (float)(now - t));
                    break;
                case ToolResolver.TargetKind.Dummy:
                    if (tool.Mode == ToolMode.Kill)
                    {
                        TargetDummy d = TargetDummy.All[target.Index];
                        float dmg = stats.Damage * (target.WeakPoint ? stats.HeadshotMultiplier : 1f);
                        bool killed = d.ApplyDamage(dmg);
                        ServerManager.Broadcast(new DummyStateBroadcast { Index = target.Index, Health = d.Health, Down = d.IsDown });
                        RpcDummyHit(Owner, target.Point, dmg, target.WeakPoint, killed, target.Index, target.Pose.Position.ToUnity(), target.Pose.Yaw, (float)(now - t));
                    }
                    break;
            }
            if (tool.Archetype == ToolArchetype.Firearm) RpcShotFx(origin, end);
        }

        private static void HitLog(string line)
        {
            if (HuntingCvars.HitDebug.Value) Game.Console.Print("[hitdebug] " + line);
        }

        private void ResolvePrey(ToolDefinition tool, ToolStats stats, ToolResolver.Target target, float rewound)
        {
            PreyInstance prey = PreyWorld.Get(target.PreyId);
            if (prey == null || !prey.Alive) return;
            PreyStats ps = prey.Def.Stats;
            TakedownResult r = Takedown.Resolve(ps, prey.Health, stats, target.WeakPoint);
            switch (r.Outcome)
            {
                case TakedownOutcome.Damaged:
                    PreyWorld.HostDamage(prey, r.HealthAfter);
                    break;
                case TakedownOutcome.Killed:
                case TakedownOutcome.Captured:
                    PreyWorld.HostRemove(prey);
                    EventBus.Publish(new PreyTakenDown
                    {
                        ClientId = Owner.ClientId,
                        Prey = prey.Def.Id,
                        Captured = r.Outcome == TakedownOutcome.Captured,
                        Quality = r.Quality,
                    });
                    break;
            }
            RpcToolResult(Owner, (byte)r.Outcome, (byte)r.Reason, prey.Def.Id.Value, (byte)tool.Archetype, target.Point, r.Damage, target.WeakPoint, (byte)r.Quality);
            if (r.Outcome != TakedownOutcome.Rejected)
                RpcTakedownFx((byte)r.Outcome, prey.Def.Id.Value, target.Point, target.Point - transform.position, (byte)r.Quality, r.Damage);
        }

        [TargetRpc]
        private void RpcToolResult(NetworkConnection conn, byte outcome, byte reason, uint preyId, byte archetype, Vector3 point, float damage, bool weak, byte quality)
        {
            HitsConfirmed++;
            HuntFeedback.LocalResult((TakedownOutcome)outcome, (RejectReason)reason, ContentRegistry.Get<PreyDefinition>(new ContentId(preyId)),
                (ToolArchetype)archetype, point, damage, weak, (CarcassQuality)quality);
        }

        [TargetRpc]
        private void RpcDummyHit(NetworkConnection conn, Vector3 point, float damage, bool head, bool killed, int target, Vector3 checkedFeet, float checkedYaw, float rewoundTicks)
        {
            HitsConfirmed++;
            HitFeedback.Confirm(point, damage, head, killed, target, checkedFeet, checkedYaw, rewoundTicks);
        }

        [ObserversRpc]
        private void RpcTakedownFx(byte outcome, uint preyId, Vector3 point, Vector3 direction, byte quality, float damage)
        {
            TakedownFx.Play((TakedownOutcome)outcome, ContentRegistry.Get<PreyDefinition>(new ContentId(preyId)), point, direction, (CarcassQuality)quality, damage);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void RpcShotFx(Vector3 origin, Vector3 end) => HitFeedback.RemoteShot(transform.position, origin, end);
    }

    /// <summary>Host: what a tool use reaches, at the rewound tick.</summary>
    public static class ToolResolver
    {
        public enum TargetKind { None, Prey, Dummy, World }

        public struct Target
        {
            public TargetKind Kind;
            public ushort PreyId;
            public int Index;
            public Vector3 Point;
            public bool WeakPoint;
            public EntityPose Pose;
        }

        public static Target Find(ToolDefinition tool, float range, Vector3 origin, Vector3 dir, double tick)
        {
            return tool.Archetype == ToolArchetype.Firearm || tool.ArcDegrees <= 0f
                ? Ray(tool, range, origin, dir, tick)
                : Arc(tool, range, origin, dir, tick);
        }

        private static Target Ray(ToolDefinition tool, float range, Vector3 origin, Vector3 dir, double tick)
        {
            var best = new Target { Kind = TargetKind.None, Point = origin + dir * range };
            float bestDist = range;
            Vec3 o = origin.ToSim(), d = dir.ToSim();
            var wall = SimWorld.Current.TraceBox(o, o + d * range, Vec3.Zero);
            if (wall.Hit && !wall.StartSolid)
            {
                bestDist = wall.Fraction * range;
                best = new Target { Kind = TargetKind.World, Point = origin + dir * bestDist };
            }

            var prey = PreyWorld.All;
            for (int i = 0; i < prey.Count; i++)
            {
                PreyInstance p = prey[i];
                if (!p.TryPoseAt(tick, true, out EntityPose pose)) continue;
                if (RayCapsules(o, d, pose, p.Def.Capsules, ref bestDist, out bool weak))
                    best = new Target { Kind = TargetKind.Prey, PreyId = p.Id, Point = origin + dir * bestDist, WeakPoint = weak, Pose = pose };
            }

            var dummies = TargetDummy.All;
            for (int i = 0; i < dummies.Count; i++)
            {
                TargetDummy dm = dummies[i];
                if (dm.IsDown) continue;
                if (!dm.History.TrySample(tick, out EntityPose pose)) pose = dm.Path.PoseAt(tick, HitClock.TickRate);
                if (RayCapsules(o, d, pose, TargetDummyPath.Hitboxes, ref bestDist, out bool weak))
                    best = new Target { Kind = TargetKind.Dummy, Index = i, Point = origin + dir * bestDist, WeakPoint = weak, Pose = pose };
            }
            return best;
        }

        private static bool RayCapsules(Vec3 o, Vec3 d, EntityPose pose, HitCapsule[] caps, ref float bestDist, out bool weak)
        {
            weak = false;
            bool hit = false;
            var rot = System.Numerics.Quaternion.CreateFromAxisAngle(Vec3.UnitY, pose.Yaw * Mathf.Deg2Rad);
            for (int c = 0; c < caps.Length; c++)
            {
                Vec3 a = pose.Position + Vec3.Transform(caps[c].A, rot);
                Vec3 b = pose.Position + Vec3.Transform(caps[c].B, rot);
                if (HitScan.RayCapsule(o, d, a, b, caps[c].Radius, out float dist) && dist < bestDist)
                {
                    bestDist = dist;
                    weak = caps[c].Region == HitRegion.Head;
                    hit = true;
                }
            }
            return hit;
        }

        /// <summary>Melee: the target nearest the crosshair inside the arc and reach, with line of sight.</summary>
        private static Target Arc(ToolDefinition tool, float range, Vector3 origin, Vector3 dir, double tick)
        {
            var best = new Target { Kind = TargetKind.None };
            float bestAngle = tool.ArcDegrees * 0.5f;
            var prey = PreyWorld.All;
            for (int i = 0; i < prey.Count; i++)
            {
                PreyInstance p = prey[i];
                if (!p.TryPoseAt(tick, true, out EntityPose pose)) continue;
                Vector3 center = pose.Position.ToUnity() + Vector3.up * CapsuleMidHeight(p.Def.Capsules);
                if (Consider(origin, dir, center, range + 0.3f, ref bestAngle))
                    best = new Target { Kind = TargetKind.Prey, PreyId = p.Id, Point = center, Pose = pose };
            }
            if (tool.Mode == ToolMode.Kill)
            {
                var dummies = TargetDummy.All;
                for (int i = 0; i < dummies.Count; i++)
                {
                    TargetDummy dm = dummies[i];
                    if (dm.IsDown) continue;
                    if (!dm.History.TrySample(tick, out EntityPose pose)) pose = dm.Path.PoseAt(tick, HitClock.TickRate);
                    Vector3 center = pose.Position.ToUnity() + Vector3.up * 1f;
                    if (Consider(origin, dir, center, range + 0.35f, ref bestAngle))
                        best = new Target { Kind = TargetKind.Dummy, Index = i, Point = center, Pose = pose };
                }
            }
            return best;
        }

        private static bool Consider(Vector3 origin, Vector3 dir, Vector3 center, float reach, ref float bestAngle)
        {
            Vector3 to = center - origin;
            float dist = to.magnitude;
            if (dist > reach || dist < 1e-3f) return false;
            float angle = Vector3.Angle(dir, to);
            // Very close targets are easier to hit (they fill more of the view).
            angle -= Mathf.Clamp01(1f - dist / reach) * 10f;
            if (angle > bestAngle) return false;
            var los = SimWorld.Current.TraceBox(origin.ToSim(), center.ToSim(), Vec3.Zero);
            if (los.Hit && los.Fraction < 0.98f) return false;
            bestAngle = angle;
            return true;
        }

        private static float CapsuleMidHeight(HitCapsule[] caps)
        {
            if (caps.Length == 0) return 0.3f;
            return (caps[0].A.Y + caps[0].B.Y) * 0.5f;
        }
    }
}
