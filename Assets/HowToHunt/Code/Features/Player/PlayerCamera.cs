using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Player
{
    /// <summary>
    /// First-person camera for the local player. Follows the smoothed Visual child (not the tick-rate root) so motion
    /// is smooth at any frame rate, and uses the live mouse view so looking around has zero network latency.
    /// </summary>
    public sealed class PlayerCamera : MonoBehaviour
    {
        private PlayerMotor _target;
        private Transform _visual;
        private Camera _camera;

        public static PlayerCamera Instance { get; private set; }

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
            }
            Instance._target = motor;
            Transform visual = motor.transform.Find("Visual");
            Instance._visual = visual != null ? visual : motor.transform;
            // Hide our own body from our own camera.
            foreach (Renderer r in motor.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            LockCursor();
        }

        public static void LockCursor()
        {
            if (Game.UiHasFocus) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            _camera.fieldOfView = HorizontalToVerticalFov(CoreCvars.Fov.Value, _camera.aspect);
            Vector3 hullCenter = _visual.position;
            float eyeFromCenter = MovementCvars.Config.EyeHeight - MovementCvars.Config.HullHalfExtents.Y;
            transform.SetPositionAndRotation(hullCenter + new Vector3(0f, eyeFromCenter, 0f),
                Quaternion.Euler(_target.ViewPitch, _target.ViewYaw, 0f));

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
