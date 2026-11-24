using HowToHunt.Core;
using HowToHunt.Sim.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HowToHunt.Player
{
    /// <summary>
    /// Samples local input. Mouse look is applied every frame (lowest latency); movement keys are sampled per tick.
    /// Presses that happen between two ticks are latched so a quick tap is never lost.
    /// Keys come from <see cref="KeyBindings"/> (rebindable in Settings or with bind_* cvars).
    /// </summary>
    public sealed class PlayerInputSampler
    {
        // Source's m_yaw/m_pitch: 0.022 degrees per mouse count, times sensitivity.
        private const float DegreesPerCount = 0.022f;

        // Gamepad look: degrees per second at full right-stick deflection, the response curve (1 = linear, 2 =
        // gentle near the centre for fine aim), and its own invert. Movement uses the left stick, analog.
        public static readonly Foundation.FloatCvar JoySensitivity = new Foundation.FloatCvar("joy_sensitivity", 220f, "Gamepad look speed, degrees per second at full right-stick deflection.", Foundation.CvarFlags.Archive, 30f, 720f);
        public static readonly Foundation.FloatCvar JoyCurve = new Foundation.FloatCvar("joy_curve", 2f, "Gamepad look response curve: 1 linear, 2 or more finer near the centre.", Foundation.CvarFlags.Archive, 1f, 4f);
        public static readonly Foundation.BoolCvar JoyInvertY = new Foundation.BoolCvar("joy_invert_y", false, "Invert the gamepad's vertical look.", Foundation.CvarFlags.Archive);

        public static void Register(Foundation.ConsoleSystem console)
        {
            console.Register(JoySensitivity);
            console.Register(JoyCurve);
            console.Register(JoyInvertY);
        }

        /// <summary>A stick with a radial dead zone, rescaled so movement starts at 0 just past it.</summary>
        public static Vector2 Stick(Vector2 raw)
        {
            float dz = KeyBindings.PadDeadzone.Value;
            float m = raw.magnitude;
            if (m <= dz) return Vector2.zero;
            return raw / m * Mathf.Clamp01((m - dz) / Mathf.Max(0.01f, 1f - dz));
        }

        private bool _jumpLatched;
        private int _playIndex;
        private MoveDemo _playback;

        /// <summary>When set, commands come from this demo instead of the keyboard and mouse.</summary>
        public MoveDemo Playback
        {
            get => _playback;
            set { _playback = value; _playIndex = 0; }
        }

        public event System.Action<MoveDemo> PlaybackFinished;

        public float Yaw { get; set; }
        public float Pitch { get; set; }

        // Recoil the view still has to follow, degrees.
        private float _followPitch, _followYaw;

        /// <summary>Held weapon weight class 0..3, set by the tools feature (heavy weapons slow you down).</summary>
        public static int LoadLevel { get; set; }

        /// <summary>Mouse sensitivity multiplier (aiming down a zoomed sight turns slower).</summary>
        public static float SensitivityScale { get; set; } = 1f;

        /// <summary>
        /// Weapon kick, physical style: the gun jumps in the hand (the viewmodel does that) and the view follows
        /// loosely. Only the part that doesn't recover (1 - <paramref name="recover"/>) moves the view, eased in over a
        /// moment, so fast fire climbs smoothly instead of snapping the camera.
        /// </summary>
        public void Kick(float up, float side, float recover)
        {
            float keep = 1f - Mathf.Clamp01(recover);
            _followPitch += up * keep;
            _followYaw += side * keep;
        }

        /// <summary>Downed: no moving, no jumping (set by PlayerHealth for the local player).</summary>
        public static bool Incapacitated { get; set; }

        private float _push;

        /// <summary>A gun shove (m/s backwards along the view) to send with the next command.</summary>
        public void AddPush(float speed) => _push = Mathf.Max(_push, speed);

        private float TakePush()
        {
            float p = _push;
            _push = 0f;
            return p;
        }

        /// <summary>Call every frame.</summary>
        public void UpdateFrame()
        {
            if (Playback != null) return;
            if (_followPitch != 0f || _followYaw != 0f)
            {
                float k = 1f - Mathf.Exp(-MovementCvars.RecoilSettleSpeed.Value * Time.deltaTime);
                float p = _followPitch * k, y = _followYaw * k;
                Pitch = Mathf.Clamp(Pitch - p, -89f, 89f);
                Yaw = Mathf.Repeat(Yaw + y, 360f);
                _followPitch -= p;
                _followYaw -= y;
                if (Mathf.Abs(_followPitch) < 0.01f && Mathf.Abs(_followYaw) < 0.01f) _followPitch = _followYaw = 0f;
            }
            if (Game.UiHasFocus || Cursor.lockState != CursorLockMode.Locked) return;
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                float k = DegreesPerCount * CoreCvars.Sensitivity.Value * SensitivityScale;
                Yaw = Mathf.Repeat(Yaw + delta.x * k, 360f);
                float dy = delta.y * k * (CoreCvars.InvertY.Value ? 1f : -1f);
                Pitch = Mathf.Clamp(Pitch + dy, -89f, 89f);
                // Mouse-wheel jump: the classic bunny hop bind.
                if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f) _jumpLatched = true;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 look = Stick(pad.rightStick.ReadValue());
                float m = look.magnitude;
                if (m > 0f)
                {
                    look = look / m * Mathf.Pow(m, JoyCurve.Value);
                    float rate = JoySensitivity.Value * SensitivityScale * Time.deltaTime;
                    Yaw = Mathf.Repeat(Yaw + look.x * rate, 360f);
                    Pitch = Mathf.Clamp(Pitch + look.y * rate * (JoyInvertY.Value ? 1f : -1f), -89f, 89f);
                }
            }
            if (KeyBindings.WasPressedThisFrame(GameAction.Jump)) _jumpLatched = true;
        }

        /// <summary>Call once per tick; returns the command for this tick.</summary>
        public MoveCmd BuildCmd(float tickDelta)
        {
            if (Playback != null)
            {
                if (_playIndex < Playback.Cmds.Count)
                {
                    MoveCmd played = Playback.Cmds[_playIndex++];
                    Yaw = played.Yaw;
                    Pitch = played.Pitch;
                    return played;
                }
                MoveDemo finished = Playback;
                Playback = null;
                PlaybackFinished?.Invoke(finished);
            }

            if (MovementCvars.BotForward.Value != 0f || MovementCvars.BotSide.Value != 0f || MovementCvars.BotYawSpeed.Value != 0f || MovementCvars.BotJump.Value)
            {
                MoveCmd bot = BuildBotCmd(tickDelta);
                bot.Push = TakePush();
                return bot;
            }

            var cmd = new MoveCmd { Yaw = Yaw, Pitch = Pitch, Push = TakePush() };
            if (Game.UiHasFocus)
            {
                _jumpLatched = false;
                return cmd;
            }
            if (Incapacitated)
            {
                // Downed: low on the ground, going nowhere.
                cmd.Buttons = MoveButtons.Duck;
                _jumpLatched = false;
                return cmd;
            }
            // Keys give full steps; the left stick is analog (walk slowly with a light push).
            Vector2 move = Gamepad.current != null ? Stick(Gamepad.current.leftStick.ReadValue()) : Vector2.zero;
            float keysForward = (KeysHeld(GameAction.Forward) ? 1f : 0f) - (KeysHeld(GameAction.Back) ? 1f : 0f);
            float keysSide = (KeysHeld(GameAction.Right) ? 1f : 0f) - (KeysHeld(GameAction.Left) ? 1f : 0f);
            cmd.Forward = Mathf.Clamp(keysForward + move.y, -1f, 1f);
            cmd.Side = Mathf.Clamp(keysSide + move.x, -1f, 1f);
            if (KeyBindings.IsPressed(GameAction.Jump) || _jumpLatched) cmd.Buttons |= MoveButtons.Jump;
            if (KeyBindings.IsPressed(GameAction.Crouch)) cmd.Buttons |= MoveButtons.Duck;
            if (KeyBindings.IsPressed(GameAction.Sprint)) cmd.Buttons |= MoveButtons.Sprint;
            cmd.Buttons |= MoveCmd.LoadButtons(LoadLevel);
            if (MovementCvars.ClientAutoHop.Value) cmd.Buttons |= MoveButtons.AutoHop;
            _jumpLatched = false;
            return cmd;
        }

        /// <summary>A movement key on the keyboard (the stick is read analog above, not as a key).</summary>
        private static bool KeysHeld(GameAction action) => KeyBindings.IsKeyPressed(action);

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
