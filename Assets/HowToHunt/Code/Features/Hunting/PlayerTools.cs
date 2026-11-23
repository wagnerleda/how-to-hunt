using System;
using FishNet.Connection;
using FishNet.Object;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Player;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Hunting;
using HowToHunt.Sim.Movement;
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
        private static readonly ToolResolver.Target[] _sweep = new ToolResolver.Target[ToolResolver.MaxSweepTargets];

        /// <summary>Host: does this client carry this tool? Set by the economy feature (inventory). Null = upgrades decide.</summary>
        public static Func<int, uint, bool> HostHasTool;

        /// <summary>
        /// Host: a tool was used (tool, origin, direction, how far it reached: the hit point for a shot, the reach for
        /// a swing). The economy feature shoves the world items it passes through.
        /// </summary>
        public static event Action<ToolDefinition, Vector3, Vector3, float> HostToolReached;

        public static event Action<PlayerTools, ToolDefinition, Vector3, Vector3> LocalUse;
        public static event Action<ToolDefinition> LocalEquipped;

        // Test hooks (wpn_autofire): aim at a dummy exactly as drawn here, using the pistol.
        public static int AutoFireTarget = -1;
        public static int AutoFireShots;
        public static int ShotsFired, HitsConfirmed;
        /// <summary>Local: the seed of the last shot (shotgun pellets spread the same way on every machine).</summary>
        public static ushort LastShotSeed;

        public static PlayerTools Local { get; private set; }
        public ToolDefinition Equipped => _equipped >= 0 && _equipped < Loadouts.AllTools.Count ? Loadouts.AllTools[_equipped] : null;

        private void Awake() => _motor = GetComponent<PlayerMotor>();

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;
            Local = this; // the inventory bar selects what is held
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

        /// <summary>The inventory bar selected a tool slot (null = empty hands).</summary>
        public void SelectTool(ToolDefinition tool)
        {
            bool wasHolding = HeldPrey != null;
            HeldPrey = null;
            if (tool == null)
            {
                // Empty hands. (Also after throwing the last prey of a slot: the viewmodel must drop it.)
                if (_equipped < 0 && !wasHolding) return;
                if (_equipped >= 0) _previous = _equipped;
                _equipped = -1;
                LocalEquipped?.Invoke(null);
                return;
            }
            var tools = Loadouts.AllTools;
            for (int i = 0; i < tools.Count; i++)
                if (tools[i] == tool) { if (i != _equipped) Equip(i); else LocalEquipped?.Invoke(tool); return; }
        }

        /// <summary>The inventory bar selected a prey slot: hands hold it (no tool).</summary>
        public void HoldPrey(PreyDefinition prey)
        {
            if (_equipped >= 0) { _previous = _equipped; _equipped = -1; }
            HeldPrey = prey;
            LocalHeld?.Invoke(prey);
        }

        /// <summary>What the local player holds when not holding a tool (null = nothing or a tool).</summary>
        public static PreyDefinition HeldPrey { get; private set; }

        /// <summary>Local: hands now hold this prey (null = none).</summary>
        public static event Action<PreyDefinition> LocalHeld;

        /// <summary>Local: the held prey was thrown (viewmodel animation).</summary>
        public static event Action LocalThrow;

        public static void NotifyThrow() => LocalThrow?.Invoke();

        /// <summary>Local: how charged the throw being held is (0 = not charging, up to 1). The viewmodel winds up.</summary>
        public static float LocalThrowCharge { get; set; }

        private void Equip(int index)
        {
            if (index == _equipped || index < 0 || index >= Loadouts.AllTools.Count) return;
            _previous = _equipped;
            _equipped = index;
            LocalEquipped?.Invoke(Equipped);
        }

        // --- local firearm state (owner) -----------------------------------------------------------------------

        private int _sentHeld = -2;
        private readonly int[] _ammo = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }; // -1 = full
        private float _reloadStart = -1f, _reloadUntil = -1f;
        private int _reloadTool = -1;

        /// <summary>Local: rounds left in the held gun's magazine and its size (0/0 when not holding a gun).</summary>
        public static int LocalAmmo { get; private set; }
        public static int LocalMagazine { get; private set; }
        /// <summary>Local: reload progress 0..1 while reloading, otherwise -1.</summary>
        public static float LocalReload { get; private set; } = -1f;
        /// <summary>Local: aiming down sights (held Aim with a gun, not reloading).</summary>
        public static bool LocalAiming { get; private set; }

        /// <summary>Local: the inspect key was pressed (the viewmodel shows the held item off; any action cancels it).</summary>
        public static event Action LocalInspect;
        /// <summary>Local: a reload started (viewmodel animation, sounds).</summary>
        public static event Action<ToolDefinition> LocalReloadStarted;
        /// <summary>
        /// Local: where the held gun's barrel points (origin, direction), from the viewmodel. Bullets leave along it,
        /// not along the camera: the weapon itself aims. Null or false = use the camera.
        /// </summary>
        public static MuzzleProvider Muzzle;
        public delegate bool MuzzleProvider(ToolDefinition tool, out Vector3 origin, out Vector3 direction);

        private static GunSetup Gun(ToolDefinition tool) => Loadouts.Local.Setup(tool);

        private int AmmoOf(int index, ToolDefinition tool) => _ammo[index] < 0 ? Gun(tool).MagazineSize : _ammo[index];

        private void Update()
        {
            if (!IsOwner) return;
            ToolDefinition tool = Equipped;
            bool gun = tool != null && tool.Archetype == ToolArchetype.Firearm;
            UpdateGunState(tool, gun);
            if (AutoFireShots > 0) { AutoFire(); return; }
            if (PlayerInputSampler.Incapacitated) { LocalAiming = false; LocalCharge = 0f; return; } // downed: hands empty
            if (Game.UiHasFocus || Cursor.lockState != CursorLockMode.Locked) { LocalAiming = gun && HuntingCvars.ForceAim.Value && LocalReload < 0f; return; }

            // Slot keys belong to the inventory bar (economy feature), which calls SelectTool / HoldPrey.
            if (KeyBindings.WasPressedThisFrame(GameAction.Inspect) && (tool != null || HeldPrey != null)) LocalInspect?.Invoke();
            if (tool == null) { LocalAiming = false; return; }
            LocalAiming = gun && (KeyBindings.IsPressed(GameAction.Aim) || HuntingCvars.ForceAim.Value) && LocalReload < 0f && LocalThrowCharge <= 0f;
            if (gun && KeyBindings.WasPressedThisFrame(GameAction.Reload)) StartReload(tool);
            if (gun && tool.ChargeSeconds > 0f) { ChargeGun(tool); return; }

            bool trigger = gun && !tool.Automatic ? KeyBindings.WasPressedThisFrame(GameAction.Fire) : KeyBindings.IsPressed(GameAction.Fire);
            if (!trigger || Time.time < _cooldownUntil[_equipped] || LocalThrowCharge > 0f) return;
            if (!gun)
            {
                Camera cam = Camera.main;
                if (cam != null) Use(cam.transform.position, cam.transform.forward);
                return;
            }
            if (LocalReload >= 0f) return;
            if (AmmoOf(_equipped, tool) <= 0 && !HuntingCvars.InfiniteMag.Value)
            {
                // Dry: click, then reload by itself.
                if (KeyBindings.WasPressedThisFrame(GameAction.Fire))
                {
                    if (tool.DryFireSound != null) Sfx.Play2D(tool.DryFireSound, 0.6f);
                    else Sfx.Play2D("Kenney/click_001", 0.5f, 1.4f);
                }
                StartReload(tool);
                return;
            }
            FireGun(tool);
        }

        /// <summary>Ammo, reload progress and what the rest of the game needs to know about the held item.</summary>
        private void UpdateGunState(ToolDefinition tool, bool gun)
        {
            if (_reloadTool >= 0 && _reloadTool < Loadouts.AllTools.Count && ReloadProgress() >= Gun(Loadouts.AllTools[_reloadTool]).Handling.RefillAt)
            {
                // Past the refill point the new magazine is in (How to Fish): it counts even if you switch away now.
                _ammo[_reloadTool] = -1;
            }
            if (_reloadTool >= 0 && (_reloadTool != _equipped))
            {
                // Switching away cancels a reload.
                _reloadTool = -1;
                _reloadUntil = -1f;
            }
            if (_reloadTool >= 0 && Time.time >= _reloadUntil)
            {
                ToolDefinition reloaded = _reloadTool < Loadouts.AllTools.Count ? Loadouts.AllTools[_reloadTool] : null;
                _ammo[_reloadTool] = -1; // full
                _reloadTool = -1;
                _reloadUntil = -1f;
                if (reloaded == null || reloaded.ReloadSound == null) Sfx.Play2D("Kenney/close_001", 0.4f, 1.15f); // a recorded reload has its own end

            }
            LocalReload = _reloadTool >= 0 ? ReloadProgress() : -1f;
            LocalMagazine = gun ? Gun(tool).MagazineSize : 0;
            LocalAmmo = gun ? AmmoOf(_equipped, tool) : 0;
            PlayerInputSampler.LoadLevel = tool == null ? 0 : gun ? Gun(tool).Weight : tool.Weight;
            if (_equipped != _sentHeld) { _sentHeld = _equipped; CmdHeld((short)_equipped); }
            MovementHud.HideCrosshair = gun || LocalThrowCharge > 0f; // guns aim by the barrel; a charging throw shows its ring instead
            if (!gun || tool.ChargeSeconds <= 0f) LocalCharge = 0f;
            if (!gun) LocalAiming = false;
        }

        private float ReloadProgress() => Mathf.Clamp01((Time.time - _reloadStart) / Mathf.Max(0.01f, _reloadUntil - _reloadStart));

        /// <summary>Local: the reload in progress started with rounds left (no rack at the end).</summary>
        public static bool LocalReloadTactical { get; private set; }

        /// <summary>Local: the last shot's random kick direction (-1..1), shared by the view and the gun model.</summary>
        public static float LocalKickDir { get; private set; }

        private void StartReload(ToolDefinition tool)
        {
            if (_reloadTool >= 0 || _equipped < 0 || AmmoOf(_equipped, tool) >= Gun(tool).MagazineSize) return;
            GunSetup setup = Gun(tool);
            // With rounds still in the gun there's no rack at the end: a quicker reload (How to Fish's Reload vs ReloadLast).
            LocalReloadTactical = AmmoOf(_equipped, tool) > 0;
            _reloadTool = _equipped;
            _reloadStart = Time.time;
            _reloadUntil = Time.time + setup.ReloadSeconds * (LocalReloadTactical ? setup.Handling.TacticalReload : 1f);
            LocalAiming = false;
            if (tool.ReloadSound != null) Sfx.Play2D(tool.ReloadSound, 0.6f);
            else Sfx.Play2D("Kenney/open_001", 0.4f, 1.1f);
            LocalReloadStarted?.Invoke(tool);
            CmdReload((byte)_equipped);
        }

        /// <summary>Local: how charged the held charge weapon is (0..1; the Gunlance opens and its orb grows).</summary>
        public static float LocalCharge { get; private set; }

        /// <summary>Charge weapons: hold Fire to charge; release when full to fire, earlier to cancel.</summary>
        private void ChargeGun(ToolDefinition tool)
        {
            bool empty = AmmoOf(_equipped, tool) <= 0 && !HuntingCvars.InfiniteMag.Value;
            if (LocalReload >= 0f || Time.time < _cooldownUntil[_equipped]) { LocalCharge = 0f; return; }
            if (empty)
            {
                LocalCharge = 0f;
                if (KeyBindings.WasPressedThisFrame(GameAction.Fire)) StartReload(tool);
                return;
            }
            if (KeyBindings.IsPressed(GameAction.Fire))
            {
                LocalCharge = Mathf.Min(1f, LocalCharge + Time.deltaTime / tool.ChargeSeconds);
                return;
            }
            if (LocalCharge >= 1f) FireGun(tool);
            LocalCharge = 0f;
        }

        private void FireGun(ToolDefinition tool)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 origin = cam.transform.position, dir = cam.transform.forward;
            if (Muzzle == null || !Muzzle(tool, out origin, out dir)) { origin = cam.transform.position; dir = cam.transform.forward; }
            // Accuracy: the cone widens with your speed (not for a scout rifle) and tightens down the sights.
            float speed = PlayerMove.HorizontalSpeed(_motor.State.Velocity);
            GunSetup setup = Gun(tool);
            float cone = GunMath.Spread(setup.Ballistics, speed, LocalAiming);
            dir = GunMath.InCone(dir.ToSim(), cone, UnityEngine.Random.value, UnityEngine.Random.value).ToUnity();
            if (!HuntingCvars.InfiniteMag.Value) _ammo[_equipped] = AmmoOf(_equipped, tool) - 1;
            Use(origin, dir);
            // Recoil, How to Fish style: one random direction drives the whole shot (view and gun model). The view
            // kicks up by the full amount and stays there: you pull it back down. Sideways it is random twice over
            // (mostly small, now and then wide). Aiming doesn't soften the view kick, only the gun in the hand.
            LocalKickDir = UnityEngine.Random.Range(-1f, 1f);
            float scale = HuntingCvars.RecoilScale.Value * (LocalAiming ? HuntingCvars.AdsRecoil.Value : 1f);
            float side = UnityEngine.Random.Range(-1f, 1f) * Mathf.Abs(LocalKickDir) * setup.RecoilSide;
            _motor.Input.Kick(setup.RecoilUp * scale, side * scale, setup.RecoilRecover);
            // A shove backwards along the view (the sawed-off, the launchers).
            if (setup.Handling.Push > 0f) _motor.Input.AddPush(setup.Handling.Push * HuntingCvars.PushScale.Value);
        }

        private void AutoFire()
        {
            Camera cam = Camera.main;
            int pistol = -1;
            var tools = Loadouts.AllTools;
            for (int i = 0; i < tools.Count; i++) if (tools[i].Archetype == ToolArchetype.Firearm) { pistol = i; break; }
            if (cam == null || pistol < 0 || AutoFireTarget < 0 || AutoFireTarget >= TargetDummy.All.Count) { AutoFireShots = 0; return; }
            _equipped = pistol;
            ToolDefinition gun = tools[pistol];
            if (Time.time < _cooldownUntil[pistol] || LocalReload >= 0f) return;
            if (AmmoOf(pistol, gun) <= 0 && !HuntingCvars.InfiniteMag.Value) { StartReload(gun); return; }
            AutoFireShots--;
            // Aim from the simulated eye (what the host checks against), not the camera, which may still be easing after a teleport.
            var s = _motor.State;
            Vector3 eye = s.Position.ToUnity() + new Vector3(0f, MovementCvars.Config.EyeOffset(s.Ducked), 0f);
            // Bullets fly: lead the dummy by the flight time, as a player would.
            double view = HitClock.ViewTick();
            EntityPose pose = TargetDummy.All[AutoFireTarget].Path.PoseAt(view, HitClock.TickRate);
            Vector3 target = pose.Position.ToUnity() + new Vector3(0f, 0.8f, 0f);
            for (int i = 0; i < 2; i++)
            {
                double flightTicks = (target - eye).magnitude / gun.BulletSpeed * HitClock.TickRate;
                pose = TargetDummy.All[AutoFireTarget].Path.PoseAt(view + flightTicks, HitClock.TickRate);
                target = pose.Position.ToUnity() + new Vector3(0f, 0.8f, 0f);
            }
            HitLog($"autofire: view tick {view:0.0} rate {HitClock.TickRate} dummy {AutoFireTarget} ({TargetDummy.All[AutoFireTarget].name}) aim {target} from {eye}");
            if (!HuntingCvars.InfiniteMag.Value) _ammo[pistol] = AmmoOf(pistol, gun) - 1;
            Use(eye, (target - eye).normalized);
        }

        private void Use(Vector3 origin, Vector3 dir)
        {
            ToolDefinition tool = Equipped;
            _cooldownUntil[_equipped] = Time.time + tool.Cooldown;
            ShotsFired++;
            double viewTick = HitClock.ViewTick();
            HitFeedback.RememberSeen(viewTick);
            LastShotSeed = (ushort)UnityEngine.Random.Range(0, 65536); // pellets fan out the same way everywhere
            LocalUse?.Invoke(this, tool, origin, dir);
            CmdUse((byte)_equipped, origin, dir, viewTick, PreyClock.RenderTick(), LastShotSeed);
        }

        [ServerRpc]
        private void CmdUse(byte toolIndex, Vector3 origin, Vector3 dir, double viewTick, double preyTick, ushort seed)
        {
            // Prey are drawn a little further behind than the rest (PreyClock): rewind them by that much more.
            double preyLag = HuntingCvars.LagComp.Value ? Math.Max(0.0, Math.Min(32.0, viewTick - preyTick)) : 0.0;
            ToolResolver.PreyLag = preyLag;
            ToolResolver.IgnoreClient = Owner.ClientId;
            var tools = Loadouts.AllTools;
            if (toolIndex >= tools.Count || toolIndex >= MaxTools) { HitLog("bad tool index"); return; }
            ToolDefinition tool = tools[toolIndex];
            Loadout loadout = Loadouts.ForClient(Owner.ClientId);
            // You must carry the tool (tools can be dropped, lost, or picked up by someone else).
            bool carried = HostHasTool != null ? HostHasTool(Owner.ClientId, tool.Id.Value) : loadout.Owns(tool);
            if (!carried) { HitLog("tool not carried"); return; }
            if (Time.time < _serverCooldownUntil[toolIndex]) { HitLog("cooldown"); return; }
            // Some slack: packets bunch up over the internet (full-auto guns fire every few ticks).
            _serverCooldownUntil[toolIndex] = Time.time + tool.Cooldown * (tool.Automatic ? 0.6f : 0.8f);
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
            if (ToolResolver.SweepsSeveral(tool))
            {
                // A net sweep scoops every prey in its arc (up to MaxTargets). Each one is still checked against the
                // net's strength: a stunned or slowed rabbit still needs a net strong enough to hold it.
                int n = ToolResolver.FindAll(tool, range, origin, dir, t, _sweep);
                HitLog($"{tool.Key} sweep -> {n} prey");
                HostToolReached?.Invoke(tool, origin, dir, range);
                for (int i = 0; i < n; i++) ResolvePrey(tool, stats, _sweep[i], (float)(now - t));
                return;
            }
            if (tool.Archetype == ToolArchetype.Firearm)
            {
                // A real bullet: it flies along the barrel at the gun's muzzle speed (a little drop), checked segment by
                // segment against targets rewound to what the shooter saw at that moment of the flight.
                if (!HostTakeRound(toolIndex, tool)) return;
                GunSetup setup = loadout.Setup(tool);
                GunBallistics g = setup.Ballistics;
                RpcShotFx(toolIndex, origin, dir, seed);
                PreyWorld.HostGunshot(origin, setup.NoiseMultiplier);
                int pellets = Mathf.Max(1, tool.Pellets);
                for (int p = 0; p < pellets; p++)
                {
                    Vector3 d = pellets > 1 ? GunMath.PelletDir(dir.ToSim(), tool.PelletSpreadDegrees, seed, p).ToUnity() : dir;
                    var hb = new HostBullet { Tool = tool, Stats = stats, Ballistics = g, Body = GunMath.Fire(origin.ToSim(), d.ToSim(), g, range), Tick = t, PreyLag = preyLag, Start = origin, Dir = d, Radius = tool.ExplosionRadius, Seed = seed };
                    if (StepBullet(ref hb, (float)TimeManager.TickDelta)) _bullets.Add(hb);
                }
                return;
            }
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
                    if (tool.Mode == ToolMode.Kill) HitDummy(stats, target, (float)(now - t));
                    break;
                case ToolResolver.TargetKind.Player:
                    if (tool.Mode == ToolMode.Kill) HitPlayer(stats, target);
                    break;
            }
            HostToolReached?.Invoke(tool, origin, dir, tool.ArcDegrees > 0f ? range : (end - origin).magnitude);
        }

        private void HitDummy(ToolStats stats, ToolResolver.Target target, float rewound)
        {
            TargetDummy d = TargetDummy.All[target.Index];
            float dmg = stats.Damage * (target.WeakPoint ? stats.HeadshotMultiplier : 1f);
            bool killed = d.ApplyDamage(dmg);
            ServerManager.Broadcast(new DummyStateBroadcast { Index = target.Index, Health = d.Health, Down = d.IsDown });
            RpcDummyHit(Owner, target.Point, dmg, target.WeakPoint, killed, target.Index, target.Pose.Position.ToUnity(), target.Pose.Yaw, rewound);
        }

        /// <summary>Host, PvP: another hunter was hit (headshots count double, as on prey).</summary>
        private void HitPlayer(ToolStats stats, ToolResolver.Target target)
        {
            PlayerHealth victim = target.Player != null ? target.Player.GetComponent<PlayerHealth>() : null;
            if (victim == null) return;
            float dmg = stats.Damage * (target.WeakPoint ? stats.HeadshotMultiplier : 1f);
            victim.HostDamage(dmg, Owner.ClientId);
            RpcPlayerHit(Owner, target.Point, dmg, target.WeakPoint, victim.Downed);
            if (HuntingCvars.HitDebug.Value) HitLog($"player {victim.OwnerId} hit for {dmg:0.0}{(target.WeakPoint ? " (head)" : "")}, {victim.Health:0} left");
        }

        [TargetRpc]
        private void RpcPlayerHit(NetworkConnection conn, Vector3 point, float damage, bool head, bool downed)
        {
            HitsConfirmed++;
            HitFeedback.Confirm(point, damage, head, downed, -1, Vector3.zero, 0f, 0f);
        }

        // --- host: magazines and bullets in flight ------------------------------------------------------------

        private struct HostBullet
        {
            public ToolDefinition Tool;
            public ToolStats Stats;
            public GunBallistics Ballistics;
            public Bullet Body;
            public double Tick;     // rewound tick the next segment is checked at (what the shooter saw then)
            public double PreyLag;  // extra rewind for prey (PreyClock)
            public float Radius;    // explosive: blast radius (0 = a plain bullet)
            public bool Bomblet;    // a cluster orb (does not split again)
            public ushort Seed;
            public Vector3 Start, Dir;
        }

        private readonly System.Collections.Generic.List<HostBullet> _bullets = new System.Collections.Generic.List<HostBullet>(8);
        private readonly int[] _serverAmmo = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        private readonly float[] _serverReloadUntil = new float[MaxTools];

        public override void OnStartServer()
        {
            base.OnStartServer();
            TimeManager.OnTick += ServerTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (TimeManager != null) TimeManager.OnTick -= ServerTick;
            _bullets.Clear();
        }

        private void ServerTick()
        {
            float dt = (float)TimeManager.TickDelta;
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                HostBullet hb = _bullets[i];
                if (StepBullet(ref hb, dt)) _bullets[i] = hb;
                else _bullets.RemoveAt(i);
            }
        }

        /// <summary>One flight step. False once the bullet hit something or flew its range.</summary>
        private bool StepBullet(ref HostBullet hb, float dt)
        {
            GunMath.Step(ref hb.Body, hb.Ballistics, dt, out Vec3 from, out Vec3 d, out float length);
            if (length > 0f)
            {
                Vector3 f = from.ToUnity();
                ToolResolver.PreyLag = hb.PreyLag;
                ToolResolver.IgnoreClient = Owner.ClientId;
                ToolResolver.Target target = ToolResolver.Find(hb.Tool, length, f, d.ToUnity(), hb.Tick);
                if (target.Kind != ToolResolver.TargetKind.None && hb.Radius > 0f)
                {
                    Explode(hb, target.Point);
                    return false;
                }
                if (target.Kind != ToolResolver.TargetKind.None)
                {
                    float distance = hb.Body.Travelled - length + (target.Point - f).magnitude;
                    ToolStats st = hb.Stats;
                    st.Damage *= GunMath.Falloff(hb.Ballistics, distance);
                    float rewound = (float)(TimeManager.Tick - hb.Tick);
                    if (HuntingCvars.HitDebug.Value) HitLog($"{hb.Tool.Key} bullet -> {target.Kind} {target.Index}/{target.PreyId} at {distance:0.0} m, x{st.Damage / Mathf.Max(0.001f, hb.Stats.Damage):0.00} damage, rewound {rewound:0.0} ticks");
                    if (target.Kind == ToolResolver.TargetKind.Prey) ResolvePrey(hb.Tool, st, target, rewound);
                    else if (target.Kind == ToolResolver.TargetKind.Dummy) HitDummy(st, target, rewound);
                    else if (target.Kind == ToolResolver.TargetKind.Player) HitPlayer(st, target);
                    HostToolReached?.Invoke(hb.Tool, hb.Start, hb.Dir, distance);
                    return false;
                }
            }
            if (hb.Body.Done)
            {
                if (hb.Radius > 0f) Explode(hb, hb.Body.Position.ToUnity()); // airburst at the end of its range (bomblets: their fuse)
                else HostToolReached?.Invoke(hb.Tool, hb.Start, hb.Dir, hb.Body.Travelled);
                return false;
            }
            hb.Tick = Math.Min(hb.Tick + 1, TimeManager.Tick);
            return true;
        }

        /// <summary>Host: an explosive round goes off. Everything in the radius is hit, harder near the centre.</summary>
        public static event Action<Vector3, float, float> HostExplosion;
        private const float BlastPushReach = 1.5f, BlastLift = 0.6f; // players are thrown a little beyond the damage radius

        private void Explode(HostBullet hb, Vector3 point)
        {
            float radius = hb.Radius;
            float rewound = (float)(TimeManager.Tick - hb.Tick);
            double tick = hb.Tick - hb.PreyLag;
            var prey = PreyWorld.All;
            for (int i = 0; i < prey.Count; i++)
            {
                PreyInstance p = prey[i];
                if (!p.Alive || !p.TryPoseAt(tick, true, out EntityPose pose)) continue;
                Vector3 center = pose.Position.ToUnity() + Vector3.up * 0.2f;
                float share = GunMath.Blast(Vector3.Distance(center, point), radius);
                if (share <= 0f) continue;
                // Walls and hills shelter: no blast through solid ground.
                var los = SimWorld.Current.TraceBox(point.ToSim() + new Vec3(0f, 0.2f, 0f), center.ToSim(), Vec3.Zero);
                if (los.Hit && los.Fraction < 0.98f) continue;
                ToolStats st = hb.Stats;
                st.Damage *= share;
                ResolvePrey(hb.Tool, st, new ToolResolver.Target { Kind = ToolResolver.TargetKind.Prey, PreyId = p.Id, Point = center, Pose = pose }, rewound);
            }
            var dummies = TargetDummy.All;
            for (int i = 0; i < dummies.Count; i++)
            {
                TargetDummy dm = dummies[i];
                if (dm.IsDown) continue;
                if (!dm.History.TrySample(hb.Tick, out EntityPose pose)) pose = dm.Path.PoseAt(hb.Tick, HitClock.TickRate);
                Vector3 center = pose.Position.ToUnity() + Vector3.up;
                float share = GunMath.Blast(Vector3.Distance(center, point), radius);
                if (share <= 0f) continue;
                ToolStats st = hb.Stats;
                st.Damage *= share;
                HitDummy(st, new ToolResolver.Target { Kind = ToolResolver.TargetKind.Dummy, Index = i, Point = center, Pose = pose }, rewound);
            }
            // Players are thrown away from the blast (the shooter too: a grenade at your feet is a rocket jump).
            float blastPush = HuntingCvars.BlastPush.Value;
            if (blastPush > 0f)
            {
                var players = PlayerMotor.All;
                for (int i = 0; i < players.Count; i++)
                {
                    PlayerMotor pm = players[i];
                    if (pm == null) continue;
                    Vector3 center = pm.transform.position;
                    Vector3 away = center - point;
                    float share = GunMath.Blast(away.magnitude, radius * BlastPushReach);
                    if (share <= 0f) continue;
                    var los = SimWorld.Current.TraceBox(point.ToSim() + new Vec3(0f, 0.2f, 0f), center.ToSim(), Vec3.Zero);
                    if (los.Hit && los.Fraction < 0.98f) continue; // sheltered by a wall
                    Vector3 dir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.up;
                    dir = (dir + Vector3.up * BlastLift).normalized; // blasts throw up as well as out
                    pm.HostPush(dir * (blastPush * share));
                    // PvP: a blast hurts other hunters (never its own shooter: blast jumps are free).
                    float hurt = GunMath.Blast(away.magnitude, radius);
                    if (PlayerHealth.Pvp.Value && hurt > 0f && pm.OwnerId != Owner.ClientId)
                        pm.GetComponent<PlayerHealth>()?.HostDamage(hb.Stats.Damage * hurt, Owner.ClientId);
                }
            }
            HostExplosion?.Invoke(point, radius, hb.Tool.Knockback);
            PreyWorld.HostGunshot(point, 1.5f); // a blast scares everything around
            RpcExplosion(point, radius, hb.Tool.PlasmaOrb);
            // Cluster rounds burst into smaller explosive orbs (the same ones on every machine, from the shot seed).
            if (!hb.Bomblet && hb.Tool.ClusterCount > 0)
            {
                Vector3 from = point + Vector3.up * 0.3f;
                for (int i = 0; i < hb.Tool.ClusterCount; i++)
                {
                    Vec3 dir = Cluster.Dir(hb.Seed, i);
                    var b = new HostBullet
                    {
                        Tool = hb.Tool, Stats = hb.Stats, Ballistics = Cluster.Ballistics, Tick = hb.Tick, PreyLag = hb.PreyLag,
                        Body = GunMath.Fire(from.ToSim(), dir, Cluster.Ballistics, Cluster.Range(i)), Start = from, Dir = dir.ToUnity(),
                        Radius = hb.Tool.ClusterRadius, Bomblet = true, Seed = hb.Seed,
                    };
                    b.Stats.Damage = hb.Tool.ClusterDamage;
                    _bullets.Add(b);
                }
                RpcCluster((byte)Cluster.ToolIndex(hb.Tool), point, hb.Seed);
            }
        }

        [ObserversRpc]
        private void RpcExplosion(Vector3 point, float radius, bool plasma) => BulletFx.Explosion(point, radius, plasma);

        [ObserversRpc]
        private void RpcCluster(byte toolIndex, Vector3 point, ushort seed)
        {
            var tools = Loadouts.AllTools;
            if (toolIndex < tools.Count) BulletFx.Cluster(tools[toolIndex], point, seed);
        }

        /// <summary>Host: spend a round of that gun's magazine (false: empty or mid-reload).</summary>
        private bool HostTakeRound(int index, ToolDefinition tool)
        {
            if (HuntingCvars.InfiniteMag.Value) return true;
            if (_serverReloadUntil[index] > 0f)
            {
                if (Time.time < _serverReloadUntil[index]) { HitLog("reloading"); return false; }
                _serverAmmo[index] = -1;
                _serverReloadUntil[index] = 0f;
            }
            int ammo = _serverAmmo[index] < 0 ? Loadouts.ForClient(Owner.ClientId).Setup(tool).MagazineSize : _serverAmmo[index];
            if (ammo <= 0) { HitLog("empty magazine"); return false; }
            _serverAmmo[index] = ammo - 1;
            return true;
        }

        /// <summary>Owner -> host -> everyone else: what this player holds (buffered, so late joiners see it too).</summary>
        [ServerRpc]
        private void CmdHeld(short toolIndex) => RpcHeld(toolIndex);

        [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
        private void RpcHeld(short toolIndex)
        {
            var tools = Loadouts.AllTools;
            ThirdPersonTool view = GetComponent<ThirdPersonTool>();
            if (view == null) view = gameObject.AddComponent<ThirdPersonTool>();
            view.Show(toolIndex >= 0 && toolIndex < tools.Count ? tools[toolIndex] : null);
        }

        [ServerRpc]
        private void CmdReload(byte toolIndex)
        {
            var tools = Loadouts.AllTools;
            if (toolIndex >= tools.Count || toolIndex >= MaxTools || tools[toolIndex].Archetype != ToolArchetype.Firearm) return;
            // A little shorter than the owner's: the owner's next shot must never arrive before the host's reload ends.
            GunSetup setup = Loadouts.ForClient(Owner.ClientId).Setup(tools[toolIndex]);
            float seconds = setup.ReloadSeconds * (_serverAmmo[toolIndex] > 0 ? setup.Handling.TacticalReload : 1f);
            _serverReloadUntil[toolIndex] = Time.time + seconds * 0.8f;
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
                {
                    Vector3 push = target.Point - transform.position;
                    push.y = 0f;
                    push = push.sqrMagnitude > 1e-4f ? push.normalized * tool.Knockback : Vector3.zero;
                    PreyWorld.HostDamage(prey, r.HealthAfter, push, Owner.ClientId);
                    break;
                }
                case TakedownOutcome.Killed:
                case TakedownOutcome.Captured:
                    PreyWorld.HostCredit(prey, Owner.ClientId);
                    PreyWorld.HostRemove(prey);
                    if (prey.Def.IsBoss) EventBus.Publish(new BossDefeated { Prey = prey.Def.Id, Instance = prey.Id, Killer = Owner.ClientId });
                    // A kill flings the carcass away from the hunter: harder hits fling it further.
                    Vector3 away = target.Point - transform.position;
                    away.y = 0f;
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward;
                    float force = 1.5f + Mathf.Min(r.Damage, 60f) * 0.06f;
                    EventBus.Publish(new PreyTakenDown
                    {
                        ClientId = Owner.ClientId,
                        Prey = prey.Def.Id,
                        Captured = r.Outcome == TakedownOutcome.Captured,
                        Quality = r.Quality,
                        Position = target.Pose.Position.ToUnity() + Vector3.up * 0.15f,
                        Fling = away * force + Vector3.up * (2f + force * 0.4f),
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
        private void RpcShotFx(byte toolIndex, Vector3 origin, Vector3 dir, ushort seed)
        {
            var tools = Loadouts.AllTools;
            if (toolIndex < tools.Count) HitFeedback.RemoteShot(transform.position, tools[toolIndex], origin, dir, seed);
            GetComponent<ThirdPersonTool>()?.Kick();
        }
    }

    /// <summary>Host: what a tool use reaches, at the rewound tick.</summary>
    public static class ToolResolver
    {
        public enum TargetKind { None, Prey, Dummy, World, Player }

        public struct Target
        {
            public TargetKind Kind;
            public ushort PreyId;
            public int Index;
            public Vector3 Point;
            public bool WeakPoint;
            public EntityPose Pose;
            /// <summary>Player targets (PvP).</summary>
            public PlayerMotor Player;
            /// <summary>Sweeps: angle from the crosshair (lower = better).</summary>
            public float Score;
        }

        public const int MaxSweepTargets = 8;

        /// <summary>Host: extra ticks the shooter drew prey behind their view tick (PreyClock), set before resolving a shot.</summary>
        public static double PreyLag;

        /// <summary>The shooter's client id: never hits itself. Set before each search (host).</summary>
        public static int IgnoreClient = -1;

        // A hunter's hitboxes from the hull centre: body from the feet to the shoulders, and the head.
        private static readonly HitCapsule[] PlayerHitboxes =
        {
            new HitCapsule(new Vec3(0f, -0.82f, 0f), new Vec3(0f, 0.38f, 0f), 0.24f, HitRegion.Body),
            new HitCapsule(new Vec3(0f, 0.6f, 0f), new Vec3(0f, 0.8f, 0f), 0.17f, HitRegion.Head),
        };

        /// <summary>A player who can be hit right now (PvP on, not the shooter, not already down).</summary>
        private static bool Hittable(PlayerMotor m, double tick, out EntityPose pose)
        {
            pose = default;
            if (m == null || m.OwnerId == IgnoreClient) return false;
            PlayerHealth h = m.GetComponent<PlayerHealth>();
            if (h == null || h.Downed) return false;
            if (!m.History.TrySample(tick, out pose)) pose = new EntityPose { Position = m.transform.position.ToSim() };
            return true;
        }

        /// <summary>Does one use of this tool reach several prey (a net sweep)?</summary>
        public static bool SweepsSeveral(ToolDefinition tool) =>
            tool.MaxTargets > 1 && tool.Archetype != ToolArchetype.Firearm && tool.ArcDegrees > 0f;

        /// <summary>
        /// Every prey inside the arc and reach with line of sight, up to the tool's MaxTargets, nearest the crosshair
        /// first. Allocation free: fills <paramref name="into"/>.
        /// </summary>
        public static int FindAll(ToolDefinition tool, float range, Vector3 origin, Vector3 dir, double tick, Target[] into)
        {
            int max = Mathf.Min(Mathf.Max(1, tool.MaxTargets), into.Length);
            int n = 0;
            var prey = PreyWorld.All;
            for (int i = 0; i < prey.Count; i++)
            {
                PreyInstance p = prey[i];
                if (!p.TryPoseAt(tick - PreyLag, true, out EntityPose pose)) continue;
                Vector3 center = pose.Position.ToUnity() + Vector3.up * CapsuleMidHeight(p.Def.Capsules);
                float angle = tool.ArcDegrees * 0.5f;
                if (!Consider(origin, dir, center, range + 0.3f, ref angle)) continue;
                var t = new Target { Kind = TargetKind.Prey, PreyId = p.Id, Point = center, Pose = pose, Score = angle };
                if (n < max) { into[n++] = t; continue; }
                int worst = 0;
                for (int k = 1; k < n; k++) if (into[k].Score > into[worst].Score) worst = k;
                if (angle < into[worst].Score) into[worst] = t;
            }
            return n;
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
                if (!p.TryPoseAt(tick - PreyLag, true, out EntityPose pose)) continue;
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
            // PvP: other hunters, where the shooter saw them. With PvP off shots pass through teammates.
            if (PlayerHealth.Pvp.Value)
            {
                var players = PlayerMotor.All;
                for (int i = 0; i < players.Count; i++)
                {
                    if (!Hittable(players[i], tick, out EntityPose pose)) continue;
                    if (RayCapsules(o, d, pose, PlayerHitboxes, ref bestDist, out bool weak))
                        best = new Target { Kind = TargetKind.Player, Player = players[i], Index = i, Point = origin + dir * bestDist, WeakPoint = weak, Pose = pose };
                }
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
                if (!p.TryPoseAt(tick - PreyLag, true, out EntityPose pose)) continue;
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
                if (PlayerHealth.Pvp.Value)
                {
                    var players = PlayerMotor.All;
                    for (int i = 0; i < players.Count; i++)
                    {
                        if (!Hittable(players[i], tick, out EntityPose pose)) continue;
                        Vector3 center = pose.Position.ToUnity() + Vector3.up * 0.2f;
                        if (Consider(origin, dir, center, range + 0.35f, ref bestAngle))
                            best = new Target { Kind = TargetKind.Player, Player = players[i], Index = i, Point = center, Pose = pose };
                    }
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
