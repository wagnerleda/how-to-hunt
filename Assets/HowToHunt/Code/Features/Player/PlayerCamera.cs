using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Sim.Movement;
using UnityEngine;

namespace HowToHunt.Player
{
    /// <summary>
    /// First-person camera for the local player. Follows the smoothed Visual child (not the tick-rate root) so motion
    /// is smooth at any frame rate, and uses the live mouse view so looking around has zero network latency.
    /// Feel: smooth crouch, landing dip scaled by impact, slight strafe roll, small FOV boost at high speed.
    /// </summary>
    public sealed class PlayerCamera : MonoBehaviour
    {
        public static readonly FloatCvar RollAngle = new FloatCvar("cl_rollangle", 1.2f, "Camera roll when strafing, degrees (0 = off).", CvarFlags.Archive, 0f, 6f);
        public static readonly FloatCvar LandKick = new FloatCvar("cl_landkick", 1f, "Landing camera dip strength (0 = off).", CvarFlags.Archive, 0f, 3f);
        public static readonly FloatCvar SpeedFov = new FloatCvar("cl_speedfov", 6f, "Extra FOV at very high speed, degrees (0 = off).", CvarFlags.Archive, 0f, 20f);
        public static readonly FloatCvar ViewBob = new FloatCvar("cl_viewbob", 1f, "Head bob while walking and running on the ground (How to Fish), strength (0 = off).", CvarFlags.Archive, 0f, 2f);
        public static readonly FloatCvar FallTilt = new FloatCvar("cl_falltilt", 0.25f, "Camera pitch per m/s of vertical speed (How to Fish: 0.25; 0 = off).", CvarFlags.Archive, 0f, 1f);
        public static readonly FloatCvar HurtTilt = new FloatCvar("cl_hurt_tilt", 4f, "Camera tilt when you get hurt, degrees (0 = off; off with ui_reduce_motion).", CvarFlags.Archive, 0f, 15f);
        public static readonly FloatCvar HurtTime = new FloatCvar("cl_hurt_time", 0.45f, "How long the hurt tilt lasts, seconds.", CvarFlags.Archive, 0.1f, 2f);

        // How to Fish's head bob: up to 3 strides a second at 7 m/s, a sideways sway and a dip per step, smoothed.
        private const float BobMaxSpeed = 7f, BobStridesPerSecond = 3f, BobSide = 0.022f, BobDip = 0.03f, BobSmooth = 0.08f, FallTiltSmooth = 0.08f;
        private float _bobTime, _fallTilt, _fallTiltVel;
        private Vector3 _bob, _bobVel;

        private const float CrouchTime = 0.12f;

        private PlayerMotor _target;
        private Transform _visual;
        private Camera _camera;
        private float _eyeHeight = -1f;
        private float _kick;
        private float _kickVelocity;
        private float _roll;
        private float _fovBonus;
        private float _recoil;

        /// <summary>Visual recoil (view kick that settles back; aim is unaffected).</summary>
        public void Kick(float degrees) => _recoil = Mathf.Min(_recoil + degrees, 6f);

        public static PlayerCamera Instance { get; private set; }

        // A framed shot (the Gunsmith's counter): the camera glides from the eyes to a set pose and back.
        private static bool _shot;
        private static Vector3 _shotPosition;
        private static Quaternion _shotRotation = Quaternion.identity;
        private static float _shotFov = 30f;
        private float _shotWeight;
        // Where the shot is right now: it glides to a new pose (another gun on the counter) instead of cutting.
        private Vector3 _shotNowPosition;
        private Quaternion _shotNowRotation = Quaternion.identity;
        private float _shotNowFov;
        private const float ShotGlideRate = 5f;
        private const float ShotBlendRate = 7f;

        /// <summary>A framed shot is on (or still blending out): the first-person gun hides.</summary>
        public static bool Framed => Instance != null && Instance._shotWeight > 0.001f;

        /// <summary>Glide the camera to this pose (vertical FOV, degrees) until <see cref="EndShot"/>.</summary>
        public static void FrameShot(Vector3 position, Quaternion rotation, float verticalFov)
        {
            _shot = true;
            _shotPosition = position;
            _shotRotation = rotation;
            _shotFov = verticalFov;
        }

        public static void EndShot() => _shot = false;

        /// <summary>The camera itself (null before a player spawns).</summary>
        public static Camera Camera => Instance != null ? Instance._camera : null;

        /// <summary>Optical zoom while aiming down sights (1 = none), set by the tools feature.</summary>
        public static float Zoom { get; set; } = 1f;

        public static void Register(ConsoleSystem console)
        {
            console.Register(RollAngle);
            console.Register(LandKick);
            console.Register(SpeedFov);
            console.Register(ViewBob);
            console.Register(FallTilt);
            console.Register(HurtTilt);
            console.Register(HurtTime);
            HurtSounds.Register(console);
            RenderDistance.Register(console);
        }

        public static void Attach(PlayerMotor motor)
        {
            if (Instance == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                    cam = go.GetComponent<Camera>();
                }
                Instance = cam.gameObject.TryGetComponent(out PlayerCamera existing) ? existing : cam.gameObject.AddComponent<PlayerCamera>();
                Instance._camera = cam;
                PlayerMotor.MoveFeedback += Instance.OnMoveFeedback;
                PlayerMotor.ZoneFeedback += Instance.OnZoneFeedback;
                PlayerHealth.LocalHurt += Instance.OnHurt;
            }
            Instance._target = motor;
            Instance._eyeHeight = -1f;
            Transform visual = motor.transform.Find("Visual");
            Instance._visual = visual != null ? visual : motor.transform;
            // Hide our own body from our own camera (it still casts a shadow).
            foreach (Renderer r in motor.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            LockCursor();
        }

        private void OnDestroy()
        {
            PlayerMotor.MoveFeedback -= OnMoveFeedback;
            PlayerMotor.ZoneFeedback -= OnZoneFeedback;
            PlayerHealth.LocalHurt -= OnHurt;
            if (Instance == this) Instance = null;
        }

        public static void LockCursor()
        {
            if (Game.UiHasFocus) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnMoveFeedback(PlayerMotor motor, MoveEvents events, float landSpeed)
        {
            if (motor != _target || (events & MoveEvents.Landed) == 0) return;
            float strength = Mathf.Clamp01((landSpeed - 2f) / 12f);
            _kickVelocity -= strength * 2.2f * LandKick.Value;
        }

        // Hurt tilt (Minecraft's hurt cam): 1 when hit, down to 0 over cl_hurt_time; alternates sides.
        private float _hurt;
        private float _hurtSide = 1f;

        private void OnHurt(float amount)
        {
            HurtSounds.Play(amount);
            if (HurtTilt.Value <= 0f || UiFeel.Calm) return;
            _hurt = 1f;
            _hurtSide = -_hurtSide;
        }

        /// <summary>Roll for the hurt tilt: a quick lean that eases back (sin of t^4 * pi, t going 1 to 0).</summary>
        private float HurtRoll(float dt)
        {
            if (_hurt <= 0f) return 0f;
            _hurt = Mathf.Max(0f, _hurt - dt / HurtTime.Value);
            float t = _hurt * _hurt;
            return Mathf.Sin(t * t * Mathf.PI) * HurtTilt.Value * _hurtSide;
        }

        private void OnZoneFeedback(PlayerMotor motor, PlayerMotor.ZoneEvent e)
        {
            if (motor != _target || e == PlayerMotor.ZoneEvent.Checkpoint) return;
            _kick = 0f;
            _kickVelocity = 0f;
            _eyeHeight = -1f;
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            float dt = Time.deltaTime;
            MoveConfig cfg = MovementCvars.Config;
            MoveState s = _target.State;

            // Eye height: smooth when crouching on the ground; in the air the hull change keeps the eye in place already.
            float targetEye = s.Ducked ? cfg.DuckEyeHeight : cfg.EyeHeight;
            if (_eyeHeight < 0f || !s.OnGround) _eyeHeight = targetEye;
            else _eyeHeight = Mathf.MoveTowards(_eyeHeight, targetEye, dt * (cfg.EyeHeight - cfg.DuckEyeHeight) / CrouchTime);

            // Landing dip: a stiff, damped spring.
            _kickVelocity += (-_kick * 140f - _kickVelocity * 20f) * dt;
            _kick = Mathf.Clamp(_kick + _kickVelocity * dt, -0.35f, 0.1f);

            // Strafe roll and speed FOV, smoothed.
            Vector3 vel = s.Velocity.ToUnity();
            Vector3 right = Quaternion.Euler(0f, _target.ViewYaw, 0f) * Vector3.right;
            float side = Vector3.Dot(new Vector3(vel.x, 0f, vel.z), right);
            float targetRoll = Mathf.Clamp(side / cfg.MaxSpeed, -1f, 1f) * RollAngle.Value;
            _roll = Mathf.Lerp(_roll, targetRoll, 1f - Mathf.Exp(-10f * dt));
            float speed = new Vector2(vel.x, vel.z).magnitude;
            float targetFov = Mathf.Clamp01((speed - cfg.MaxSpeed * 1.4f) / 18f) * SpeedFov.Value;
            _fovBonus = Mathf.Lerp(_fovBonus, targetFov, 1f - Mathf.Exp(-4f * dt));
            float vfov = HorizontalToVerticalFov(CoreCvars.Fov.Value + _fovBonus, _camera.aspect);
            // Aiming down sights magnifies (true zoom: the tangent of the half angle shrinks).
            if (Zoom > 1.001f) vfov = 2f * Mathf.Atan(Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad) / Zoom) * Mathf.Rad2Deg;
            _camera.fieldOfView = vfov;
            RenderDistance.Apply(_camera);

            // How to Fish: the head bobs with your steps on the ground and tips with your vertical speed.
            Vector3 bobTarget = Vector3.zero;
            if (s.OnGround && speed > 0.1f && ViewBob.Value > 0f)
            {
                float k = Mathf.Clamp01(speed / BobMaxSpeed);
                _bobTime = Mathf.Repeat(_bobTime + dt * Mathf.Lerp(BobStridesPerSecond * 0.5f, BobStridesPerSecond, k), 1f);
                float a = _bobTime * Mathf.PI * 2f;
                bobTarget = new Vector3(Mathf.Sin(a) * BobSide, -Mathf.Abs(Mathf.Sin(a)) * BobDip, 0f) * (k * ViewBob.Value);
            }
            else
            {
                _bobTime = 0f;
            }
            _bob = Vector3.SmoothDamp(_bob, bobTarget, ref _bobVel, BobSmooth, Mathf.Infinity, dt);
            _fallTilt = Mathf.SmoothDamp(_fallTilt, Mathf.Clamp(vel.y, -20f, 20f) * FallTilt.Value, ref _fallTiltVel, FallTiltSmooth, Mathf.Infinity, dt);

            Vector3 center = _visual.position;
            float feetY = center.y - cfg.Hull(s.Ducked).Y;
            Quaternion view = Quaternion.Euler(_target.ViewPitch - _recoil + _fallTilt, _target.ViewYaw, -_roll + HurtRoll(dt));
            transform.SetPositionAndRotation(new Vector3(center.x, feetY + _eyeHeight + _kick, center.z) + view * _bob, view);
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * (6f + _recoil * 8f));

            // A framed shot: blend from the eyes to its pose (smooth in and out, respects reduce motion).
            _shotWeight = UiFeel.Calm ? (_shot ? 1f : 0f) : Mathf.MoveTowards(_shotWeight, _shot ? 1f : 0f, dt * ShotBlendRate * 0.5f);
            if (_shotWeight > 0.001f)
            {
                // Entering the shot: start from its pose; already in it: glide to the new pose.
                float g = UiFeel.Calm || _shotNowFov <= 0f ? 1f : 1f - Mathf.Exp(-ShotGlideRate * dt);
                _shotNowPosition = Vector3.Lerp(_shotNowPosition, _shotPosition, g);
                _shotNowRotation = Quaternion.Slerp(_shotNowRotation, _shotRotation, g);
                _shotNowFov = Mathf.Lerp(_shotNowFov, _shotFov, g);
                float w = _shotWeight * _shotWeight * (3f - 2f * _shotWeight);
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, _shotNowPosition, w), Quaternion.Slerp(transform.rotation, _shotNowRotation, w));
                _camera.fieldOfView = Mathf.Lerp(vfov, _shotNowFov, w);
            }
            else _shotNowFov = 0f; // the next shot starts from its own pose

            // Click to recapture the mouse after alt-tab or closing the console.
            if (!Game.UiHasFocus && Cursor.lockState != CursorLockMode.Locked &&
                ((UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                 || (UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.wasUpdatedThisFrame && UnityEngine.InputSystem.Gamepad.current.buttonSouth.wasPressedThisFrame)))
                LockCursor();
        }

        private static float HorizontalToVerticalFov(float horizontalDeg, float aspect)
        {
            float h = horizontalDeg * Mathf.Deg2Rad;
            return 2f * Mathf.Atan(Mathf.Tan(h * 0.5f) / Mathf.Max(0.01f, aspect)) * Mathf.Rad2Deg;
        }
    }
}
