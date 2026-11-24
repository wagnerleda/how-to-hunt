using System;
using HowToHunt.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace HowToHunt.Core
{
    public enum GameAction
    {
        Forward,
        Back,
        Left,
        Right,
        Jump,
        Crouch,
        Fire,
        Scoreboard,
        Interact,
        Sprint,
        Throw,
        Aim,
        Reload,
        Inspect,
    }

    /// <summary>
    /// Rebindable keys, stored as archived cvars (<c>bind_forward "W"</c>) so the settings menu and the console share
    /// them. Values are Input System key names (physical layout: on AZERTY, the "W" key is the one labelled Z).
    /// Fire is the left mouse button unless bound to a key.
    /// </summary>
    public static class KeyBindings
    {
        private static readonly StringCvar[] Cvars =
        {
            new StringCvar("bind_forward", nameof(Key.W), "Move forward key.", CvarFlags.Archive),
            new StringCvar("bind_back", nameof(Key.S), "Move back key.", CvarFlags.Archive),
            new StringCvar("bind_left", nameof(Key.A), "Strafe left key.", CvarFlags.Archive),
            new StringCvar("bind_right", nameof(Key.D), "Strafe right key.", CvarFlags.Archive),
            new StringCvar("bind_jump", nameof(Key.Space), "Jump key (mouse wheel always jumps too).", CvarFlags.Archive),
            new StringCvar("bind_crouch", nameof(Key.LeftCtrl), "Crouch key.", CvarFlags.Archive),
            new StringCvar("bind_fire", "Mouse0", "Fire (Mouse0 = left button, or a key name).", CvarFlags.Archive),
            new StringCvar("bind_scoreboard", nameof(Key.Tab), "Show the party scoreboard while held.", CvarFlags.Archive),
            new StringCvar("bind_interact", nameof(Key.E), "Use a camp station (sell, shop).", CvarFlags.Archive),
            new StringCvar("bind_sprint", nameof(Key.LeftShift), "Sprint (a modest boost; bunny hopping is faster).", CvarFlags.Archive),
            new StringCvar("bind_throw", nameof(Key.Q), "Tap to drop what you hold; hold to charge a throw (further the longer you hold).", CvarFlags.Archive),
            new StringCvar("bind_aim", "Mouse1", "Aim down sights while held (Mouse1 = right button, or a key name).", CvarFlags.Archive),
            new StringCvar("bind_reload", nameof(Key.R), "Reload the held gun.", CvarFlags.Archive),
            new StringCvar("bind_inspect", nameof(Key.F), "Inspect what you hold (any action cancels it).", CvarFlags.Archive),
        };

        private static readonly Key[] Resolved = new Key[Cvars.Length];

        public static int Count => Cvars.Length;

        public static void Register(ConsoleSystem console)
        {
            console.Register(PadDeadzone);
            for (int i = 0; i < Cvars.Length; i++)
            {
                int index = i;
                console.Register(Cvars[i]);
                Cvars[i].Changed += _ => Resolve(index);
                Resolve(i);
            }
        }

        // --- gamepad ------------------------------------------------------------------------------------------------
        // A fixed Xbox-style layout (docs/DESIGN.md): every action also answers to a gamepad button, so keyboard,
        // mouse and gamepad can be mixed freely. Movement and looking use the sticks (PlayerInputSampler).

        public static readonly FloatCvar PadDeadzone = new FloatCvar("joy_deadzone", 0.15f, "Gamepad stick dead zone (0..0.6).", CvarFlags.Archive, 0f, 0.6f);
        private const float StickPress = 0.5f; // a stick pushed this far counts as a movement key

        /// <summary>The last input came from a gamepad (prompts show gamepad buttons).</summary>
        public static bool UsingGamepad { get; private set; }
        private static int _deviceFrame = -1;

        private static ButtonControl PadButton(GameAction action)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null) return null;
            switch (action)
            {
                case GameAction.Jump: return pad.buttonSouth;
                case GameAction.Crouch: return pad.buttonEast;
                case GameAction.Fire: return pad.rightTrigger;
                case GameAction.Aim: return pad.leftTrigger;
                case GameAction.Interact: return pad.buttonWest;
                case GameAction.Reload: return pad.buttonNorth;
                case GameAction.Throw: return pad.rightShoulder;
                case GameAction.Sprint: return pad.leftStickButton;
                case GameAction.Inspect: return pad.rightStickButton;
                case GameAction.Scoreboard: return pad.selectButton;
                default: return null;
            }
        }

        private static string PadName(GameAction action)
        {
            switch (action)
            {
                case GameAction.Jump: return "A";
                case GameAction.Crouch: return "B";
                case GameAction.Fire: return "RT";
                case GameAction.Aim: return "LT";
                case GameAction.Interact: return "X";
                case GameAction.Reload: return "Y";
                case GameAction.Throw: return "RB";
                case GameAction.Sprint: return "L-stick click";
                case GameAction.Inspect: return "R-stick click";
                case GameAction.Scoreboard: return "View";
                case GameAction.Forward: case GameAction.Back: case GameAction.Left: case GameAction.Right: return "Left stick";
                default: return "?";
            }
        }

        /// <summary>A movement key's worth of left stick (for code that asks IsPressed(Forward) and friends).</summary>
        private static bool StickHeld(GameAction action)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null) return false;
            Vector2 s = pad.leftStick.ReadValue();
            switch (action)
            {
                case GameAction.Forward: return s.y > StickPress;
                case GameAction.Back: return s.y < -StickPress;
                case GameAction.Right: return s.x > StickPress;
                case GameAction.Left: return s.x < -StickPress;
                default: return false;
            }
        }

        /// <summary>Notes whether the player is on the gamepad or the keyboard and mouse (once per frame).</summary>
        private static void TrackDevice()
        {
            if (_deviceFrame == Time.frameCount) return;
            _deviceFrame = Time.frameCount;
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                bool padActive = pad.wasUpdatedThisFrame && (pad.leftStick.ReadValue().sqrMagnitude > 0.09f || pad.rightStick.ReadValue().sqrMagnitude > 0.09f
                                                              || pad.buttonSouth.isPressed || pad.buttonWest.isPressed || pad.buttonEast.isPressed || pad.buttonNorth.isPressed
                                                              || pad.rightTrigger.isPressed || pad.leftTrigger.isPressed || pad.startButton.isPressed || pad.dpad.ReadValue().sqrMagnitude > 0f);
                if (padActive) UsingGamepad = true;
            }
            else UsingGamepad = false;
            Keyboard kb = Keyboard.current;
            Mouse m = Mouse.current;
            if ((kb != null && kb.anyKey.wasPressedThisFrame) || (m != null && (m.delta.ReadValue().sqrMagnitude > 4f || m.leftButton.wasPressedThisFrame))) UsingGamepad = false;
        }

        public static string DisplayName(GameAction action)
        {
            TrackDevice();
            if (UsingGamepad) return PadName(action);
            string value = Cvars[(int)action].Value;
            if (value == "Mouse0") return "Left mouse";
            if (value == "Mouse1") return "Right mouse";
            if (value == "Mouse2") return "Middle mouse";
            Key key = Resolved[(int)action];
            if (key == Key.None) return value;
            Keyboard kb = Keyboard.current;
            // Show what's printed on the user's keyboard (AZERTY users see Z for the W position).
            return kb != null ? kb[key].displayName : value;
        }

        public static void Bind(GameAction action, Key key) => Cvars[(int)action].Value = key.ToString();

        public static void BindMouse(GameAction action) => Cvars[(int)action].Value = "Mouse0";

        /// <summary>The keyboard/mouse binding alone (no gamepad).</summary>
        public static bool IsKeyPressed(GameAction action)
        {
            int i = (int)action;
            ButtonControl mb = MouseButton(Cvars[i].Value);
            if (mb != null) return mb.isPressed;
            if (Cvars[i].Value.StartsWith("Mouse", StringComparison.Ordinal)) return false;
            Keyboard kb = Keyboard.current;
            return kb != null && Resolved[i] != Key.None && kb[Resolved[i]].isPressed;
        }

        public static bool IsPressed(GameAction action)
        {
            ButtonControl pb = PadButton(action);
            if ((pb != null && pb.isPressed) || StickHeld(action)) return true;
            int i = (int)action;
            ButtonControl mb = MouseButton(Cvars[i].Value);
            if (mb != null) return mb.isPressed;
            if (Cvars[i].Value.StartsWith("Mouse", StringComparison.Ordinal)) return false;
            Keyboard kb = Keyboard.current;
            return kb != null && Resolved[i] != Key.None && kb[Resolved[i]].isPressed;
        }

        public static bool WasPressedThisFrame(GameAction action)
        {
            ButtonControl pb = PadButton(action);
            if (pb != null && pb.wasPressedThisFrame) return true;
            int i = (int)action;
            ButtonControl mb = MouseButton(Cvars[i].Value);
            if (mb != null) return mb.wasPressedThisFrame;
            if (Cvars[i].Value.StartsWith("Mouse", StringComparison.Ordinal)) return false;
            Keyboard kb = Keyboard.current;
            return kb != null && Resolved[i] != Key.None && kb[Resolved[i]].wasPressedThisFrame;
        }

        /// <summary>The key pressed this frame, if any (for rebinding). Allocation-free.</summary>
        public static Key AnyKeyPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.anyKey.wasPressedThisFrame) return Key.None;
            var keys = kb.allKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                KeyControl k = keys[i];
                if (k.wasPressedThisFrame) return k.keyCode;
            }
            return Key.None;
        }

        private static ButtonControl MouseButton(string value)
        {
            Mouse m = Mouse.current;
            if (m == null) return null;
            switch (value)
            {
                case "Mouse0": return m.leftButton;
                case "Mouse1": return m.rightButton;
                case "Mouse2": return m.middleButton;
                default: return null;
            }
        }

        private static void Resolve(int i)
        {
            Resolved[i] = Enum.TryParse(Cvars[i].Value, true, out Key key) ? key : Key.None;
        }
    }
}
