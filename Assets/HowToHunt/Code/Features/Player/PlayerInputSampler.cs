using HowToHunt.Core;
using HowToHunt.Sim.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HowToHunt.Player
{
    /// <summary>
    /// Samples local input. Mouse look is applied every frame (lowest latency); movement keys are sampled per tick.
    /// Presses that happen between two ticks are latched so a quick tap is never lost.
    /// Key bindings become rebindable in M2 (settings); these are the defaults.
    /// </summary>
    public sealed class PlayerInputSampler
    {
        // Source's m_yaw/m_pitch: 0.022 degrees per mouse count, times sensitivity.
        private const float DegreesPerCount = 0.022f;

        private bool _jumpLatched;

        public float Yaw { get; set; }
        public float Pitch { get; set; }

        /// <summary>Call every frame.</summary>
        public void UpdateFrame()
        {
            if (Game.UiHasFocus || Cursor.lockState != CursorLockMode.Locked) return;
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                float k = DegreesPerCount * CoreCvars.Sensitivity.Value;
                Yaw = Mathf.Repeat(Yaw + delta.x * k, 360f);
                float dy = delta.y * k * (CoreCvars.InvertY.Value ? 1f : -1f);
                Pitch = Mathf.Clamp(Pitch + dy, -89f, 89f);
                // Mouse-wheel jump: the classic bunny hop bind.
                if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f) _jumpLatched = true;
            }
            if (kb != null && kb.spaceKey.wasPressedThisFrame) _jumpLatched = true;
        }

        /// <summary>Call once per tick; returns the command for this tick.</summary>
        public MoveCmd BuildCmd(float tickDelta)
        {
            if (MovementCvars.BotForward.Value != 0f || MovementCvars.BotSide.Value != 0f || MovementCvars.BotYawSpeed.Value != 0f || MovementCvars.BotJump.Value)
                return BuildBotCmd(tickDelta);

            var cmd = new MoveCmd { Yaw = Yaw, Pitch = Pitch };
            Keyboard kb = Keyboard.current;
            if (kb == null || Game.UiHasFocus)
            {
                _jumpLatched = false;
                return cmd;
            }
            cmd.Forward = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            cmd.Side = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            if (kb.spaceKey.isPressed || _jumpLatched) cmd.Buttons |= MoveButtons.Jump;
            if (kb.leftShiftKey.isPressed) cmd.Buttons |= MoveButtons.Walk;
            if (kb.leftCtrlKey.isPressed) cmd.Buttons |= MoveButtons.Duck;
            if (MovementCvars.ClientAutoHop.Value) cmd.Buttons |= MoveButtons.AutoHop;
            _jumpLatched = false;
            return cmd;
        }

        /// <summary>Scripted input for automated tests and bots (cl_bot_* cvars).</summary>
        private MoveCmd BuildBotCmd(float tickDelta)
        {
            Yaw = Mathf.Repeat(Yaw + MovementCvars.BotYawSpeed.Value * tickDelta, 360f);
            var cmd = new MoveCmd
            {
                Yaw = Yaw,
                Pitch = Pitch,
                Forward = MovementCvars.BotForward.Value,
                Side = MovementCvars.BotSide.Value,
                Buttons = MoveButtons.AutoHop | (MovementCvars.BotJump.Value ? MoveButtons.Jump : MoveButtons.None),
            };
            _jumpLatched = false;
            return cmd;
        }
    }
}
