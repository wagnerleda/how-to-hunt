using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Sim.Movement;
using UnityEngine;

namespace HowToHunt.Player
{
    /// <summary>
    /// A hunter's health (design: 100 HP). At 0 you are downed for 30 s: you can't move or use anything, and a
    /// teammate can revive you by holding Interact next to you for 3 s (back at 30 HP). If nobody does, you bleed out:
    /// back at camp at full health, and 10 % of your unsold catch is left where you fell (the economy does that part).
    /// Nobody standing who could revive you (alone on the map, or the whole party down): only a few seconds' wait.
    /// The host decides everything; health and the downed state reach everyone through sync vars.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerHealth : NetworkBehaviour
    {
        private const CvarFlags Sv = CvarFlags.Replicated | CvarFlags.Cheat;
        public const float MaxHealth = 100f;

        public static readonly BoolCvar Pvp = new BoolCvar("sv_pvp", true, "Players can hurt each other (friendly fire follows it). The host's choice.", CvarFlags.Replicated);
        public static readonly FloatCvar DownedSeconds = new FloatCvar("sv_downed_time", 30f, "Seconds a downed player has before bleeding out.", Sv, 3f, 120f);
        public static readonly FloatCvar DownedAloneSeconds = new FloatCvar("sv_downed_time_alone", 3f, "Seconds a downed player waits when nobody is left standing to revive them (playing alone): just long enough to see it.", Sv, 0.5f, 120f);
        public static readonly FloatCvar PoisonTick = new FloatCvar("sv_poison_tick", 1f, "Seconds between two poison hits (each does the venom's damage per second times this).", Sv, 0.1f, 5f);
        public static readonly FloatCvar ReviveSeconds = new FloatCvar("sv_revive_time", 3f, "Seconds a teammate holds Interact to revive.", Sv, 0.5f, 15f);
        public static readonly FloatCvar ReviveHealth = new FloatCvar("sv_revive_health", 30f, "Health after a revive.", Sv, 1f, MaxHealth);
        public static readonly FloatCvar ReviveRange = new FloatCvar("sv_revive_range", 2.2f, "How close a reviver must stay, metres.", Sv, 1f, 6f);
        public static readonly FloatCvar ThornDamage = new FloatCvar("sv_thorn_damage", 4f, "Damage from brushing a cactus (once per sv_thorn_interval while touching). 0 = harmless.", Sv, 0f, 50f);
        public static readonly FloatCvar ThornInterval = new FloatCvar("sv_thorn_interval", 1f, "Seconds between two cactus pricks while you keep touching it.", Sv, 0.1f, 10f);
        public static readonly FloatCvar ThornReach = new FloatCvar("sv_thorn_reach", 0.12f, "How close counts as touching a cactus, metres past its trunk.", Sv, 0f, 1f);
        public static readonly FloatCvar ThornPush = new FloatCvar("sv_thorn_push", 2.5f, "Speed a prick shoves you back from the cactus, m/s.", Sv, 0f, 20f);

        public static void Register(ConsoleSystem console)
        {
            console.Register(Pvp);
            console.Register(DownedSeconds);
            console.Register(DownedAloneSeconds);
            console.Register(PoisonTick);
            console.Register(ReviveSeconds);
            console.Register(ReviveHealth);
            console.Register(ReviveRange);
            console.Register(ThornDamage);
            console.Register(ThornInterval);
            console.Register(ThornReach);
            console.Register(ThornPush);
        }

        private readonly SyncVar<float> _health = new SyncVar<float>(MaxHealth);
        private readonly SyncVar<bool> _downed = new SyncVar<bool>();
        private readonly SyncVar<float> _revive = new SyncVar<float>();       // 0..1 while someone revives
        private readonly SyncVar<float> _bleedLeft = new SyncVar<float>();    // seconds left when downed (host's count)
        private readonly SyncVar<float> _poisonLeft = new SyncVar<float>();   // seconds of venom left (host's count)

        private static readonly List<PlayerHealth> AllList = new List<PlayerHealth>(8);
        public static IReadOnlyList<PlayerHealth> All => AllList;
        public static PlayerHealth Local { get; private set; }

        /// <summary>Host: a player went down (by whom: client id, -1 = not a player).</summary>
        public static event Action<PlayerHealth, int> HostDowned;
        /// <summary>Host: a downed player bled out (before being sent back to camp; the economy drops some catch here).</summary>
        public static event Action<PlayerHealth, Vector3> HostBledOut;
        /// <summary>Host: a player was revived (by whom).</summary>
        public static event Action<PlayerHealth, int> HostRevived;
        /// <summary>Host: where a bled-out player comes back (camp). Set by the player module.</summary>
        public static Func<PlayerHealth, Vector3> RespawnPoint;

        public PlayerMotor Motor { get; private set; }
        public float Health => _health.Value;
        public bool Downed => _downed.Value;
        public float ReviveProgress => _revive.Value;
        /// <summary>Venom in the blood (a snake bite, a scorpion sting): health drains until it wears off.</summary>
        public bool Poisoned => PoisonLeft > 0f;
        /// <summary>Seconds of venom left, counted locally from the host's figure.</summary>
        public float PoisonLeft => Mathf.Max(0f, _poisonLeft.Value - (Time.time - _poisonSyncedAt));
        private float _poisonSyncedAt, _poisonDps, _poisonUntil, _nextPoisonTick;

        /// <summary>Seconds left before bleeding out, counted locally from the host's figure.</summary>
        public float BleedLeft => Mathf.Max(0f, _bleedLeft.Value - (Time.time - _bleedSyncedAt));
        private float _bleedSyncedAt;

        // Host bookkeeping.
        private float _bleedUntil;
        private PlayerHealth _reviving;          // the downed player this one is reviving (host side)
        private float _reviveHeartbeat;           // the reviver's last "still holding" (host time)
        private int _lastAttacker = -1;

        private void Awake()
        {
            Motor = GetComponent<PlayerMotor>();
            _bleedLeft.OnChange += (_, __, ___) => _bleedSyncedAt = Time.time;
            _poisonLeft.OnChange += (_, __, ___) => _poisonSyncedAt = Time.time;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!AllList.Contains(this)) AllList.Add(this);
            if (IsOwner) Local = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            AllList.Remove(this);
            if (Local == this) Local = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (!AllList.Contains(this)) AllList.Add(this);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            AllList.Remove(this);
        }

        // --- host -------------------------------------------------------------------------------------------------

        /// <summary>Host: hurt this player. <paramref name="attacker"/> is a client id (-1: the world, prey, a blast).</summary>
        public void HostDamage(float amount, int attacker)
        {
            if (!IsServerStarted || amount <= 0f || _downed.Value) return;
            _lastAttacker = attacker;
            float left = _health.Value - amount;
            if (left > 0f)
            {
                _health.Value = left;
                return;
            }
            _health.Value = 0f;
            HostCurePoison();
            _downed.Value = true;
            _revive.Value = 0f;
            _bleedUntil = Time.time + DownedSeconds.Value;
            _bleedLeft.Value = DownedSeconds.Value;
            HostDowned?.Invoke(this, attacker);
        }

        /// <summary>
        /// Host: venom (owner, 2026-11-24). Drains dps every second for seconds; a new dose refreshes the time and keeps
        /// the stronger venom, it doesn't stack. It can put you down. Cured by going down, a revive or a respawn.
        /// </summary>
        public void HostPoison(float dps, float seconds)
        {
            if (!IsServerStarted || _downed.Value || dps <= 0f || seconds <= 0f) return;
            bool fresh = Time.time >= _poisonUntil;
            _poisonDps = fresh ? dps : Mathf.Max(_poisonDps, dps);
            _poisonUntil = Mathf.Max(_poisonUntil, Time.time + seconds);
            if (fresh) _nextPoisonTick = Time.time + PoisonTick.Value;
            _poisonLeft.Value = _poisonUntil - Time.time;
        }

        private void HostCurePoison()
        {
            _poisonUntil = 0f;
            if (_poisonLeft.Value != 0f) _poisonLeft.Value = 0f;
        }

        /// <summary>Host: back to full (a fresh spawn).</summary>
        public void HostRestore()
        {
            if (!IsServerStarted) return;
            _health.Value = MaxHealth;
            _downed.Value = false;
            _revive.Value = 0f;
            HostCurePoison();
        }

        private void Update()
        {
            if (IsOwner) UpdateOwner();
            if (IsServerStarted && !_downed.Value && _poisonUntil > 0f) HostPoisonTick();
            if (IsServerStarted && !_downed.Value) HostThorns();
            if (!IsServerStarted || !_downed.Value) return;

            // Revive: any standing teammate close by who is holding Interact on this player.
            PlayerHealth reviver = null;
            for (int i = 0; i < AllList.Count; i++)
            {
                PlayerHealth p = AllList[i];
                if (p == this || p._downed.Value || p._reviving != this || Time.time - p._reviveHeartbeat > 0.4f) continue;
                if ((p.transform.position - transform.position).sqrMagnitude > ReviveRange.Value * ReviveRange.Value) continue;
                reviver = p;
                break;
            }
            if (reviver != null)
            {
                _revive.Value = Mathf.Min(1f, _revive.Value + Time.deltaTime / ReviveSeconds.Value);
                if (_revive.Value >= 1f)
                {
                    _downed.Value = false;
                    _revive.Value = 0f;
                    _health.Value = ReviveHealth.Value;
                    HostRevived?.Invoke(this, reviver.OwnerId);
                }
                return; // bleeding pauses while someone is reviving you
            }
            if (_revive.Value > 0f) _revive.Value = 0f;
            // Alone (or everyone else is down too): nobody can come, so don't make you wait the full time.
            if (!AnyoneStanding() && _bleedUntil - Time.time > DownedAloneSeconds.Value)
            {
                _bleedUntil = Time.time + DownedAloneSeconds.Value;
                _bleedLeft.Value = DownedAloneSeconds.Value;
            }
            if (Time.time >= _bleedUntil)
            {
                Vector3 where = Motor.transform.position;
                HostBledOut?.Invoke(this, where);
                if (RespawnPoint != null) Motor.ServerTeleport(RespawnPoint(this));
                HostRestore();
            }
        }

        private float _nextThorn;

        /// <summary>Host: brushing a cactus pricks (a little damage, a small shove away), at most once per interval.</summary>
        private void HostThorns()
        {
            float now = Time.time;
            if (now < _nextThorn || ThornDamage.Value <= 0f || Motor == null) return;
            MoveState s = Motor.State;
            Vector3 center = s.Position.ToUnity();
            Vector3 half = MovementCvars.Config.Hull(s.Ducked).ToUnity();
            if (!ThornField.Touches(center, half, ThornReach.Value, out Vector3 thorn)) return;
            _nextThorn = now + ThornInterval.Value;
            HostDamage(ThornDamage.Value, -1);
            Vector3 away = center - thorn;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward;
            Motor.HostPush(away * ThornPush.Value);
        }

        private void HostPoisonTick()
        {
            float now = Time.time;
            if (now >= _nextPoisonTick)
            {
                float tick = PoisonTick.Value;
                _nextPoisonTick = now + tick;
                HostDamage(_poisonDps * tick, -1); // may put you down (that cures it)
            }
            if (now >= _poisonUntil) HostCurePoison();
        }

        /// <summary>Another hunter on their feet, who could come and revive this one (host and clients).</summary>
        public bool AnyoneStanding()
        {
            for (int i = 0; i < AllList.Count; i++)
                if (AllList[i] != this && !AllList[i]._downed.Value) return true;
            return false;
        }

        // --- owner ------------------------------------------------------------------------------------------------

        /// <summary>The downed teammate this player is reviving right now (owner side), for the HUD.</summary>
        public static PlayerHealth LocalReviving { get; private set; }

        private float _nextHeartbeat;

        private void UpdateOwner()
        {
            // Downed: no moving or using anything (the input sampler and the tools read this).
            PlayerInputSampler.Incapacitated = _downed.Value;
            PlayerHealth target = null;
            if (!_downed.Value && Core.KeyBindings.IsPressed(Core.GameAction.Interact) && !Core.Game.UiHasFocus) target = NearestDowned();
            if (target != LocalReviving || (target != null && Time.time >= _nextHeartbeat))
            {
                LocalReviving = target;
                _nextHeartbeat = Time.time + 0.15f;
                CmdReviving(target);
            }
        }

        private PlayerHealth NearestDowned()
        {
            float best = ReviveRange.Value * ReviveRange.Value;
            PlayerHealth found = null;
            for (int i = 0; i < AllList.Count; i++)
            {
                PlayerHealth p = AllList[i];
                if (p == this || !p._downed.Value) continue;
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d <= best) { best = d; found = p; }
            }
            return found;
        }

        [ServerRpc]
        private void CmdReviving(PlayerHealth target)
        {
            _reviving = target;
            _reviveHeartbeat = Time.time;
        }
    }
}
