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

        private const float CrouchTime = 0.12f;

        private PlayerMotor _target;
        private Transform _visual;
        private Camera _camera;
        private float _eyeHeight = -1f;
        private float _kick;
        private float _kickVelocity;
        private float _roll;
        private float _fovBonus;

        public static PlayerCamera Instance { get; private set; }

        public static void Register(ConsoleSystem console)
        {
            console.Register(RollAngle);
            console.Register(LandKick);
            console.Register(SpeedFov);
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
            _camera.fieldOfView = HorizontalToVerticalFov(CoreCvars.Fov.Value + _fovBonus, _camera.aspect);

            Vector3 center = _visual.position;
            float feetY = center.y - cfg.Hull(s.Ducked).Y;
            transform.SetPositionAndRotation(new Vector3(center.x, feetY + _eyeHeight + _kick, center.z),
                Quaternion.Euler(_target.ViewPitch, _target.ViewYaw, -_roll));

            // Click to recapture the mouse after alt-tab or closing the console.
            if (!Game.UiHasFocus && Cursor.lockState != CursorLockMode.Locked &&
                UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                LockCursor();
        }

        private static float HorizontalToVerticalFov(float horizontalDeg, float aspect)
        {
            float h = horizontalDeg * Mathf.Deg2Rad;
            return 2f * Mathf.Atan(Mathf.Tan(h * 0.5f) / Mathf.Max(0.01f, aspect)) * Mathf.Rad2Deg;
        }
    }
}
