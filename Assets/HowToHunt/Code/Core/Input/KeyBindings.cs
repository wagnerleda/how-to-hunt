using System;
using HowToHunt.Foundation;
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
        };

        private static readonly Key[] Resolved = new Key[Cvars.Length];

        public static int Count => Cvars.Length;

        public static void Register(ConsoleSystem console)
        {
            for (int i = 0; i < Cvars.Length; i++)
            {
                int index = i;
                console.Register(Cvars[i]);
                Cvars[i].Changed += _ => Resolve(index);
                Resolve(i);
            }
        }

        public static string DisplayName(GameAction action)
        {
            string value = Cvars[(int)action].Value;
            if (value == "Mouse0") return "Left mouse";
            Key key = Resolved[(int)action];
            if (key == Key.None) return value;
            Keyboard kb = Keyboard.current;
            // Show what's printed on the user's keyboard (AZERTY users see Z for the W position).
            return kb != null ? kb[key].displayName : value;
        }

        public static void Bind(GameAction action, Key key) => Cvars[(int)action].Value = key.ToString();

        public static void BindMouse(GameAction action) => Cvars[(int)action].Value = "Mouse0";

        public static bool IsPressed(GameAction action)
        {
            int i = (int)action;
            if (Cvars[i].Value == "Mouse0") return Mouse.current != null && Mouse.current.leftButton.isPressed;
            Keyboard kb = Keyboard.current;
            return kb != null && Resolved[i] != Key.None && kb[Resolved[i]].isPressed;
        }

        public static bool WasPressedThisFrame(GameAction action)
        {
            int i = (int)action;
            if (Cvars[i].Value == "Mouse0") return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
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

        private static void Resolve(int i)
        {
            Resolved[i] = Enum.TryParse(Cvars[i].Value, true, out Key key) ? key : Key.None;
        }
    }
}
