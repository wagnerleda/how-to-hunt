using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Player;
using HowToHunt.Sim.Hunting;
using HowToHunt.Sim.Movement;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// The tool in your hand. Everything moves on springs so it never looks robotic:
    /// - body inertia: the arm is pushed by your real acceleration (dips when you land, sinks as you jump, lags when you
    ///   strafe or turn, floats a little in the air) and settles with a slight overshoot;
    /// - walk bob, raise on equip;
    /// - use animations: the net sweeps across the upper screen in a curve, right to left then left to right when
    ///   spammed; the knife slashes diagonally, alternating; firearms recoil.
    /// Models (tool + glove) come from ToolDefinition.Viewmodel and are instantiated once per tool.
    /// </summary>
    public sealed class ViewmodelController : MonoBehaviour
    {
        private const float RaiseSeconds = 0.22f;
        private static readonly Vector3 Rest = new Vector3(0.18f, -0.17f, 0.40f);
        private static readonly Vector3 Shoulder = new Vector3(0.24f, -0.42f, -0.12f);
        private const float ShoulderShare = 1f; // the whole turn is the arm, so the sleeve always points back at the shoulder

        // Inertia spring (position, metres in camera space) and tilt spring (degrees).
        private const float Stiffness = 140f, Damping = 15f, InertiaScale = 0.0016f, MaxOffset = 0.07f;
        private const float TiltStiffness = 90f, TiltDamping = 12f;

        private readonly Dictionary<ToolDefinition, GameObject> _models = new Dictionary<ToolDefinition, GameObject>();
        private Transform _holder;
        private Camera _camera;
        private ToolDefinition _tool;
        private GameObject _shown;
        private float _raise;
        private float _useTime = -10f;
        private float _bobPhase;
        private float _bobAmount;
        private Quaternion _lag = Quaternion.identity;
        private Quaternion _lastCameraRotation = Quaternion.identity;

        private Vector3 _lastVelocity;
        private bool _hasLastVelocity;
        private Vector3 _springPos, _springVel;
        private Vector3 _tilt, _tiltVel;

        // Guns: the kick spring (degrees up, degrees right, metres back), aim-down-sights blend, inspect.
        private const float KickStiffness = 260f, KickDamping = 20f, KickImpulse = 30f, KickBackImpulse = 0.9f;
        private const float AdsSeconds = 0.16f, AdsForward = 0.24f, InspectSeconds = 2.6f;
        private Vector3 _kick, _kickVel;
        private float _ads;

        /// <summary>Local: how far the gun is raised to the eye (0 hip .. 1 aiming down sights).</summary>
        public static float AdsBlend { get; private set; }
        private float _inspectStart = -10f;

        private void Awake()
        {
            _holder = new GameObject("Viewmodel").transform;
            PlayerTools.LocalEquipped += OnEquipped;
            PlayerTools.LocalHeld += OnHeld;
            PlayerTools.LocalThrow += OnThrow;
            PlayerTools.LocalUse += OnUse;
            PlayerMotor.MoveFeedback += OnMoveFeedback;
            PlayerTools.LocalInspect += OnInspect;
            PlayerTools.LocalReloadStarted += OnReload;
            PlayerTools.Muzzle = TryMuzzle;
        }

        private void OnDestroy()
        {
            PlayerTools.LocalEquipped -= OnEquipped;
            PlayerTools.LocalHeld -= OnHeld;
            PlayerTools.LocalThrow -= OnThrow;
            PlayerTools.LocalUse -= OnUse;
            PlayerMotor.MoveFeedback -= OnMoveFeedback;
            PlayerTools.LocalInspect -= OnInspect;
            PlayerTools.LocalReloadStarted -= OnReload;
            if (PlayerTools.Muzzle == TryMuzzle) PlayerTools.Muzzle = null;
            PlayerCamera.Zoom = 1f;
            PlayerInputSampler.SensitivityScale = 1f;
            if (_holder != null) Destroy(_holder.gameObject);
        }

        // --- held prey ----------------------------------------------------------------------------------------
        private readonly Dictionary<PreyDefinition, GameObject> _preyModels = new Dictionary<PreyDefinition, GameObject>();
        private PreyDefinition _heldPrey;
        private float _throwTime = -10f;

        private void OnHeld(PreyDefinition prey)
        {
            _tool = null;
            _heldPrey = prey;
            if (_shown != null) _shown.SetActive(false);
            _shown = null;
            _raise = 0f;
            if (prey == null || prey.Visual == null) return;
            if (!_preyModels.TryGetValue(prey, out GameObject model) || model == null)
            {
                model = Instantiate(prey.Visual, _holder, false);
                // Held up in front, a bit smaller than life so it doesn't fill the screen.
                model.transform.localScale = Vector3.one * prey.VisualScale * (prey.VisualScale > 1.2f ? 0.7f : 0.55f);
                model.transform.localPosition = new Vector3(0.04f, -0.12f, 0.12f);
                model.transform.localRotation = Quaternion.Euler(-10f, 235f, -12f); // held by the scruff, side-on
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _preyModels[prey] = model;
            }
            model.SetActive(true);
            _shown = model;
            Sfx.Play2D("Kenney/handleSmallLeather", 0.25f, Random.Range(0.95f, 1.05f));
        }

        private void OnThrow() => _throwTime = Time.time;

        /// <summary>Throw: pull back and up, then fling forward; the model is hidden at release until the bar refreshes.</summary>
        private void ThrowPose(out Vector3 offset, out Quaternion rotation)
        {
            float t = (Time.time - _throwTime) / 0.32f;
            offset = Vector3.zero;
            rotation = Quaternion.identity;
            if (t < 0f || t >= 1f) return;
            float back = t < 0.35f ? Smooth(t / 0.35f) : 1f - Smooth((t - 0.35f) / 0.25f);
            float fling = t < 0.35f ? 0f : Mathf.Sin(Mathf.Clamp01((t - 0.35f) / 0.65f) * Mathf.PI);
            offset = new Vector3(0.04f * back, 0.08f * back - 0.04f * fling, -0.12f * back + 0.25f * fling);
            rotation = Quaternion.Euler(-35f * back + 40f * fling, 0f, 0f);
        }

        private void OnInspect() => _inspectStart = Time.time;

        private void OnReload(ToolDefinition tool) => _inspectStart = -10f;

        /// <summary>Where the held gun's barrel points, for PlayerTools: bullets leave along it (the weapon aims, not the camera).</summary>
        private bool TryMuzzle(ToolDefinition tool, out Vector3 origin, out Vector3 direction)
        {
            origin = direction = Vector3.zero;
            if (tool != _tool || _camera == null || !_holder.gameObject.activeInHierarchy) return false;
            origin = _holder.TransformPoint(tool.MuzzleOffset);
            direction = _holder.forward;
            // Never shoot from the far side of a wall the barrel pokes into: start at the wall instead.
            Vector3 eye = _camera.transform.position;
            var tr = SimWorld.Current.TraceBox(eye.ToSim(), origin.ToSim(), System.Numerics.Vector3.Zero);
            if (tr.Hit || tr.StartSolid) origin = eye + (origin - eye) * Mathf.Max(0f, tr.Fraction - 0.05f);
            return true;
        }

        private void OnEquipped(ToolDefinition tool)
        {
            _inspectStart = -10f;
            _kick = _kickVel = Vector3.zero;
            _heldPrey = null;
            _tool = tool;
            if (_shown != null) _shown.SetActive(false);
            _shown = null;
            _raise = 0f;
            _side = 1f;
            if (tool == null || tool.Viewmodel == null) return;
            if (!_models.TryGetValue(tool, out GameObject model) || model == null)
            {
                model = Instantiate(tool.Viewmodel, _holder, false); // tool + gloved hand, posed in Blender
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _models[tool] = model;
            }
            model.SetActive(true);
            _shown = model;
            if (tool.Archetype == ToolArchetype.Blade) Sfx.Play2D(KnifeSounds.Shing, 0.22f, Random.Range(0.97f, 1.04f));
            else Sfx.Play2D("Kenney/cloth", 0.25f, Random.Range(0.95f, 1.05f));
        }

        private void OnUse(PlayerTools user, ToolDefinition tool, Vector3 origin, Vector3 dir)
        {
            _useTime = Time.time;
            _inspectStart = -10f;
            if (tool.Archetype == ToolArchetype.Firearm)
            {
                // The gun jumps in the hand first (the view follows loosely, see PlayerInputSampler.Kick).
                float scale = HuntingCvars.RecoilScale.Value * (PlayerTools.LocalAiming ? HuntingCvars.AdsRecoil.Value : 1f);
                _kickVel.x += tool.RecoilUp * KickImpulse * scale;
                _kickVel.y += Random.Range(-0.5f, 1f) * tool.RecoilSide * KickImpulse * scale;
                _kickVel.z += KickBackImpulse * scale;
            }
            if (tool.Archetype != ToolArchetype.Firearm) StartSweep();
            if (tool.Archetype == ToolArchetype.Net) Sfx.Play2D("Kenney/cloth", 0.35f, Random.Range(1.05f, 1.2f));
            else if (tool.Archetype == ToolArchetype.Blade) Sfx.Play2D(KnifeSounds.Swish, 0.28f, Random.Range(0.9f, 1.12f));
        }

        /// <summary>A hard landing gives the arm an extra downward kick on top of the inertia (it reads better).</summary>
        private void OnMoveFeedback(PlayerMotor motor, MoveEvents events, float landSpeed)
        {
            if (motor != PlayerMotor.Local) return;
            if ((events & MoveEvents.Landed) != 0) _springVel.y -= Mathf.Clamp(landSpeed, 0f, 16f) * 0.045f;
            if ((events & MoveEvents.Jumped) != 0) _springVel.y -= 0.25f;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != _camera)
            {
                _camera = cam;
                if (cam != null)
                {
                    _holder.SetParent(cam.transform, false);
                    _lastCameraRotation = cam.transform.rotation;
                }
            }
            // Aim-down-sights blend first: it keeps running while a scope picture hides the model.
            bool gunHeld = _tool != null && _tool.Archetype == ToolArchetype.Firearm;
            _ads = Mathf.MoveTowards(_ads, gunHeld && PlayerTools.LocalAiming ? 1f : 0f, Mathf.Min(Time.deltaTime, 0.05f) / AdsSeconds);
            AdsBlend = _ads;
            bool visible = cam != null && _shown != null && PlayerMotor.Local != null && PlayerMotor.Local.State.Mode != MoveMode.Noclip && !ScopeOverlay.Showing;
            if (_holder.gameObject.activeSelf != visible) _holder.gameObject.SetActive(visible);
            if (!visible || (_tool == null && _heldPrey == null)) { _hasLastVelocity = false; return; }

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;
            _raise = Mathf.Min(1f, _raise + dt / RaiseSeconds);
            float raise = 1f - (1f - _raise) * (1f - _raise);
            MoveState s = PlayerMotor.Local.State;
            Transform ct = cam.transform;

            // Body inertia: the arm is "left behind" by acceleration, in camera space.
            Vector3 velocity = s.Velocity.ToUnity();
            if (!_hasLastVelocity) { _lastVelocity = velocity; _hasLastVelocity = true; }
            Vector3 accel = (velocity - _lastVelocity) / dt;
            _lastVelocity = velocity;
            Vector3 localAccel = ct.InverseTransformDirection(accel);
            Vector3 force = -localAccel * InertiaScale * Stiffness;
            // In the air the arm floats up a little while rising and sinks while falling.
            Vector3 target = s.OnGround ? Vector3.zero : new Vector3(0f, Mathf.Clamp(-velocity.y * 0.002f, -0.02f, 0.02f), 0f);
            _springVel += (force + (target - _springPos) * Stiffness - _springVel * Damping) * dt;
            _springPos += _springVel * dt;
            _springPos = Vector3.ClampMagnitude(_springPos, MaxOffset);

            // Tilt: roll into strafes, pitch with vertical speed; springy too.
            Vector3 localVel = ct.InverseTransformDirection(velocity);
            var tiltTarget = new Vector3(Mathf.Clamp(-velocity.y * 0.35f, -6f, 6f), 0f, Mathf.Clamp(-localVel.x * 0.9f, -7f, 7f));
            _tiltVel += ((tiltTarget - _tilt) * TiltStiffness - _tiltVel * TiltDamping) * dt;
            _tilt += _tiltVel * dt;

            // Walk bob while grounded.
            float speed = s.OnGround ? new Vector2(velocity.x, velocity.z).magnitude : 0f;
            _bobAmount = Mathf.Lerp(_bobAmount, Mathf.Clamp01(speed / 7f), dt * 8f);
            _bobPhase += dt * Mathf.Lerp(6f, 11f, Mathf.Clamp01(speed / 10f));
            Vector3 bob = new Vector3(Mathf.Sin(_bobPhase) * 0.012f, -Mathf.Abs(Mathf.Cos(_bobPhase)) * 0.014f, 0f) * _bobAmount;

            // Look lag: the model trails view rotation a little, then catches up.
            Quaternion camRot = ct.rotation;
            Quaternion delta = Quaternion.Inverse(camRot) * _lastCameraRotation;
            _lastCameraRotation = camRot;
            _lag = Quaternion.Slerp(_lag * delta, Quaternion.identity, dt * 12f);
            _lag = Quaternion.RotateTowards(Quaternion.identity, _lag, 6f);

            Vector3 offset;
            Quaternion use;
            float shoulderYaw = 0f;
            float charge = PlayerTools.LocalThrowCharge;
            if (charge > 0f)
            {
                // Winding up a throw (any held item): pulled back and up, more the longer it's held.
                float c = Smooth(charge);
                offset = new Vector3(0.04f * c, 0.08f * c, -0.14f * c);
                use = Quaternion.Euler(-38f * c, 0f, 0f);
            }
            else if (_tool == null || Time.time - _throwTime < 0.32f)
            {
                ThrowPose(out offset, out use);
                float since = Time.time - _throwTime;
                if (_shown != null) _shown.SetActive(since < 0.13f || since > 0.45f); // released mid-throw
            }
            else if (_tool.Archetype == ToolArchetype.Firearm)
            {
                GunPose(dt, out offset, out use);
            }
            else if (_tool.Archetype == ToolArchetype.Net)
            {
                NetSwoop(cam, out offset, out use, out shoulderYaw);
            }
            else
            {
                Sweep(dt, out offset, out use);
            }

            // Big sweeps turn the whole arm around the shoulder (below-right of the eye) like a real arm, not around the grip.
            Quaternion shoulder = Quaternion.Euler(0f, shoulderYaw, 0f);
            Vector3 grip = Shoulder + shoulder * (Rest + offset - Shoulder);
            Vector3 position = grip + bob + _springPos + new Vector3(0f, -0.25f * (1f - raise), 0f);
            Quaternion rotation = _lag * Quaternion.Euler(_tilt) * shoulder * use * Quaternion.Euler(25f * (1f - raise), 0f, 0f);

            // Inspect: bring it up and turn it over to show it off (any action cancels it).
            float it = (Time.time - _inspectStart) / InspectSeconds;
            if (it >= 0f && it < 1f && _ads < 0.01f)
            {
                float e = Smooth(Mathf.Clamp01(it * 5f)) * Smooth(Mathf.Clamp01((1f - it) * 5f));
                float turn = Mathf.Sin(it * Mathf.PI);
                position += new Vector3(-0.13f, 0.06f, -0.06f) * e;
                rotation *= Quaternion.Euler(-12f * e, 75f * turn * e, -35f * e + 25f * Mathf.Sin(it * Mathf.PI * 2f) * e);
            }

            // Aim down sights: the sight comes to the eye on the view axis; sway and bob mostly settle.
            bool gun = gunHeld;
            float a = Smooth(_ads);
            if (a > 0f)
            {
                Quaternion adsRot = Quaternion.Slerp(Quaternion.identity, _lag, 0.25f) * use;
                Vector3 adsPos = new Vector3(0f, 0f, AdsForward) - adsRot * _tool.SightOffset + (bob + _springPos) * 0.2f;
                position = Vector3.Lerp(position, adsPos, a);
                rotation = Quaternion.Slerp(rotation, adsRot, a);
            }
            float zoom = gun ? Mathf.Lerp(1f, Mathf.Max(1f, _tool.AdsZoom), a) : 1f;
            PlayerCamera.Zoom = zoom;
            PlayerInputSampler.SensitivityScale = 1f / zoom;
            // The viewmodel is drawn by the zoomed camera too: shrink it toward the eye by the zoom so it keeps its size
            // on screen (smaller and nearer looks identical once the field of view narrows by the same factor).
            _holder.localPosition = position / zoom;
            _holder.localRotation = rotation;
            _holder.localScale = Vector3.one / zoom;
        }

        /// <summary>
        /// Gun in hand: the kick spring (shots push it up, aside and back; it springs home with a little overshoot), a
        /// reload dip (tilted down and in while PlayerTools.LocalReload runs).
        /// </summary>
        private void GunPose(float dt, out Vector3 offset, out Quaternion rotation)
        {
            _kickVel += (-_kick * KickStiffness - _kickVel * KickDamping) * dt;
            _kick += _kickVel * dt;
            offset = new Vector3(0f, 0.004f * _kick.x, -_kick.z);
            rotation = Quaternion.Euler(-_kick.x, _kick.y, 0f);
            float r = PlayerTools.LocalReload;
            if (r >= 0f)
            {
                float w = Smooth(Mathf.Clamp01(r * 5f)) * Smooth(Mathf.Clamp01((1f - r) * 5f));
                offset += new Vector3(-0.06f, -0.1f, -0.04f) * w;
                rotation *= Quaternion.Euler(30f * w, -25f * w, 45f * w + 8f * Mathf.Sin(r * Mathf.PI * 6f) * w);
            }
        }

        // --- melee sweeps -----------------------------------------------------------------------------------
        // A continuous "side" value: +1 = tool held to the right (rest), -1 = to the left. Each use sweeps to the other
        // side, so spamming alternates right->left, left->right with no snapping; idle drifts back to the right.
        private float _side = 1f;
        private float _sweepFrom, _sweepTo;
        private float _sweepStart = -10f, _sweepSeconds = 0.3f;

        // --- net swoops (owner's schema) ----------------------------------------------------------------------
        // Rest: held upright, hoop above the hand. Swoop 1: wind up to the right, then sweep through a dipping curve
        // all the way past the left edge of the screen, the hoop rolled so its opening leads. Swoop 2: the mirror, left
        // to right. Spamming alternates 1, 2, 1...; stopping eases back to rest. The sweep angle comes from the real
        // field of view, so it crosses the whole screen at any FOV.
        private struct Pose
        {
            public Vector3 Offset;
            public Vector3 Euler; // pitch (down +), yaw (right +), roll

            public static Pose Lerp(in Pose a, in Pose b, float t) => new Pose
            {
                Offset = Vector3.LerpUnclamped(a.Offset, b.Offset, t),
                Euler = new Vector3(Mathf.LerpUnclamped(a.Euler.x, b.Euler.x, t), Mathf.LerpUnclamped(a.Euler.y, b.Euler.y, t), Mathf.LerpUnclamped(a.Euler.z, b.Euler.z, t)),
            };
        }

        private static readonly Pose NetRest = new Pose { Offset = new Vector3(0.05f, -0.03f, 0f), Euler = new Vector3(-42f, 28f, -18f) }; // angled off to the right, out of the way
        private Pose _netPose = NetRest, _netFrom, _netVia, _netTo;
        private bool _netHasVia;
        private float _netDir = 1f;   // direction of the last swoop: -1 = moved left, +1 = moved right
        private float _netEndTime = -10f;

        private void StartNetSwoop(Camera cam)
        {
            float dir = _netDir > 0f ? -1f : 1f; // alternate; the first swoop from rest goes right -> left
            float yawEnd = HalfFovDegrees(cam) + 2f; // just past the screen edge
            _netFrom = _netPose;
            // Coming from rest (or from the wrong side): wind up to the starting side first.
            _netHasVia = Mathf.Sign(_netPose.Euler.y + 0.001f) != -dir || _netPose.Euler.x < -25f;
            _netVia = new Pose { Offset = new Vector3(-dir * 0.03f, 0.01f, 0f), Euler = new Vector3(-24f, -dir * yawEnd * 0.55f, dir * 35f) };
            _netTo = new Pose { Offset = new Vector3(dir * 0.04f, 0f, 0.02f), Euler = new Vector3(-22f, dir * yawEnd, -dir * 75f) }; // opening faces the way it moves
            _netDir = dir;
            _sweepStart = Time.time;
            _sweepSeconds = Mathf.Clamp(_tool.Cooldown * 0.85f, 0.3f, 0.6f);
            _netEndTime = _sweepStart + _sweepSeconds;
        }

        private static float HalfFovDegrees(Camera cam)
        {
            float v = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
            return Mathf.Atan(Mathf.Tan(v) * cam.aspect) * Mathf.Rad2Deg;
        }

        private void NetSwoop(Camera cam, out Vector3 offset, out Quaternion rotation, out float shoulderYaw)
        {
            float t = (Time.time - _sweepStart) / _sweepSeconds;
            if (t >= 0f && t < 1f)
            {
                const float wind = 0.22f;
                if (_netHasVia && t < wind)
                {
                    _netPose = Pose.Lerp(_netFrom, _netVia, Smooth(t / wind));
                }
                else
                {
                    float k = _netHasVia ? (t - wind) / (1f - wind) : t;
                    Pose start = _netHasVia ? _netVia : _netFrom;
                    _netPose = Pose.Lerp(start, _netTo, Smooth(k));
                    // The U-shaped path: the hoop dips through the middle of the screen, then rises at the far side.
                    float dip = Mathf.Sin(k * Mathf.PI);
                    _netPose.Offset.y -= 0.035f * dip;
                    _netPose.Euler.x += 8f * dip;
                }
            }
            else
            {
                // Hold the end of the swoop briefly (so spamming chains), then ease back to the upright rest.
                float idle = Time.time - _netEndTime - 0.25f;
                if (idle > 0f) _netPose = Pose.Lerp(_netPose, NetRest, Mathf.Clamp01(Time.deltaTime * 7f));
                if (idle > 0.6f) _netDir = 1f; // after a real pause, the next swoop starts from the right again
            }
            offset = _netPose.Offset;
            shoulderYaw = _netPose.Euler.y * ShoulderShare;
            // Mid-swoop the wrist leads: the hoop runs ahead of the hand, so the net crosses the view side-on.
            float lead = (Time.time - _sweepStart) / _sweepSeconds is float tt && tt >= 0f && tt < 1f ? Mathf.Sin(Mathf.Clamp01(tt) * Mathf.PI) * _netDir * 32f : 0f;
            rotation = Quaternion.Euler(_netPose.Euler.x, _netPose.Euler.y * (1f - ShoulderShare) + lead, _netPose.Euler.z);
        }

        private static float Smooth(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * k * (k * (k * 6f - 15f) + 10f);
        }

        private void StartSweep()
        {
            if (_tool != null && _tool.Archetype == ToolArchetype.Net && _camera != null)
            {
                StartNetSwoop(_camera);
                return;
            }
            _sweepFrom = _side;
            _sweepTo = _side > 0f ? -1f : 1f;
            _sweepStart = Time.time;
            bool net = _tool != null && _tool.Archetype == ToolArchetype.Net;
            _sweepSeconds = Mathf.Clamp((_tool != null ? _tool.Cooldown : 0.5f) * (net ? 0.8f : 0.7f), 0.18f, 0.6f);
        }

        private void Sweep(float dt, out Vector3 offset, out Quaternion rotation)
        {
            bool net = _tool.Archetype == ToolArchetype.Net;
            float t = (Time.time - _sweepStart) / _sweepSeconds;
            float arc = 0f;       // 0 at the ends of a sweep, 1 in the middle
            float direction = 0f; // which way we're moving (for leaning into the motion)
            if (t >= 0f && t < 1f)
            {
                float u;
                if (!net && t < 0.2f) u = -0.25f * Mathf.Sin(t / 0.2f * Mathf.PI * 0.5f); // knife wind-up
                else
                {
                    float k = net ? t : (t - 0.2f) / 0.8f;
                    u = Mathf.Lerp(net ? 0f : -0.25f, 1f, k * k * k * (k * (k * 6f - 15f) + 10f)); // smootherstep
                }
                _side = Mathf.LerpUnclamped(_sweepFrom, _sweepTo, u);
                arc = Mathf.Sin(Mathf.Clamp01(net ? t : (t - 0.2f) / 0.8f) * Mathf.PI);
                direction = Mathf.Sign(_sweepTo - _sweepFrom);
            }
            else if (Time.time - _sweepStart > _sweepSeconds + 0.8f)
            {
                _side = Mathf.MoveTowards(_side, 1f, dt * 1.6f); // back to resting on the right
            }

            float s = _side;
            if (net)
            {
                // Owner's sketch + note: hoop held on the right at eye level; a swing carries it across the middle of
                // the screen in a gentle curve (a little higher mid-swing), leaning into the direction of travel.
                offset = new Vector3(s * 0.11f - 0.05f, 0.03f + 0.02f * arc, 0.05f * arc);
                rotation = Quaternion.Euler(-13f - 4f * arc, s * 42f - 12f, -s * 10f + direction * 14f * arc);
            }
            else
            {
                // Diagonal slashes: high on the right, low on the left; held blade-up at rest.
                offset = new Vector3(s * 0.16f - 0.03f, s * 0.05f - 0.03f * arc + 0.02f, 0.1f * arc);
                rotation = Quaternion.Euler(-38f + 55f * arc - s * 10f, s * 32f - 8f, -s * 45f + 12f);
            }
        }

    }
}
