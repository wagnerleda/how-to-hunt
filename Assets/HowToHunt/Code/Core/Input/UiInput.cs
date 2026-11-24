using UnityEngine;
using UnityEngine.InputSystem;

namespace HowToHunt.Core
{
    /// <summary>
    /// Menu and dialogue navigation from the keyboard and any gamepad: up/down/left/right (arrows, W/S/A/D, D-pad,
    /// left stick with auto-repeat), confirm (Enter, Space, the Interact key, gamepad South/A), back (Escape, gamepad
    /// East/B). Read once per frame, whoever asks first; no allocations.
    /// </summary>
    public static class UiInput
    {
        private const float StickThreshold = 0.55f, RepeatDelay = 0.38f, RepeatRate = 0.09f;

        private static int _frame = -1;
        private static Vector2Int _move;
        private static bool _confirm, _back;
        private static Vector2Int _held;
        private static float _repeatAt;

        /// <summary>-1, 0 or +1 per axis this frame (y: +1 is up).</summary>
        public static Vector2Int Move { get { Sample(); return _move; } }
        public static bool Confirm { get { Sample(); return _confirm; } }
        public static bool Back { get { Sample(); return _back; } }
        /// <summary>A gamepad is plugged in (show its prompts).</summary>
        public static bool HasGamepad => Gamepad.current != null;

        /// <summary>
        /// uGUI panels (shop, workshop): with a gamepad plugged in, keep one of the panel's buttons selected so the
        /// D-pad/stick and A drive it through the EventSystem. Cheap when nothing needs to change.
        /// </summary>
        public static void KeepGamepadSelection(Transform panel)
        {
            if (!HasGamepad || panel == null) return;
            UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;
            GameObject current = es.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy && current.transform.IsChildOf(panel)) return;
            UnityEngine.UI.Selectable first = panel.GetComponentInChildren<UnityEngine.UI.Selectable>();
            if (first != null) es.SetSelectedGameObject(first.gameObject);
        }

        private static void Sample()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            _move = Vector2Int.zero;
            _confirm = _back = false;

            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) _move.y = 1;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) _move.y = -1;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) _move.x = -1;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) _move.x = 1;
                _confirm = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame
                           || KeyBindings.WasPressedThisFrame(GameAction.Interact);
                _back = kb.escapeKey.wasPressedThisFrame;
            }
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame) _move.y = 1;
                if (pad.dpad.down.wasPressedThisFrame) _move.y = -1;
                if (pad.dpad.left.wasPressedThisFrame) _move.x = -1;
                if (pad.dpad.right.wasPressedThisFrame) _move.x = 1;
                _confirm |= pad.buttonSouth.wasPressedThisFrame;
                _back |= pad.buttonEast.wasPressedThisFrame;

                // Left stick: one step on push, then auto-repeat while held.
                Vector2 s = pad.leftStick.ReadValue();
                var dir = new Vector2Int(Mathf.Abs(s.x) > StickThreshold ? (int)Mathf.Sign(s.x) : 0, Mathf.Abs(s.y) > StickThreshold ? (int)Mathf.Sign(s.y) : 0);
                if (dir != _held)
                {
                    _held = dir;
                    if (dir != Vector2Int.zero) { _move = dir; _repeatAt = Time.unscaledTime + RepeatDelay; }
                }
                else if (dir != Vector2Int.zero && Time.unscaledTime >= _repeatAt)
                {
                    _move = dir;
                    _repeatAt = Time.unscaledTime + RepeatRate;
                }
            }
        }
    }
}
