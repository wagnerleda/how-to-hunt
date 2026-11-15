using System.Collections.Generic;
using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>Global access to the few services every feature needs. Kept deliberately small.</summary>
    public static class Game
    {
        private static readonly List<object> UiFocusOwners = new List<object>(4);

        public static ConsoleSystem Console => ConsoleSystem.Global;
        public static ModuleRegistry Modules { get; internal set; }
        public static GameRoot Root { get; internal set; }

        /// <summary>Set by the platform module during Register (Steam if available, Offline otherwise).</summary>
        public static IPlatformServices Platform { get; set; }

        /// <summary>Build version (Player Settings bundle version, set by tools/build-windows).</summary>
        public static string Version => Application.version;

        /// <summary>Builds with a different protocol cannot join each other. Same as the version for now.</summary>
        public static string ProtocolVersion => Application.version;

        /// <summary>True while a UI (console, menu) owns the mouse and keyboard; gameplay input must ignore input.</summary>
        public static bool UiHasFocus => UiFocusOwners.Count > 0;

        public static void PushUiFocus(object owner)
        {
            if (!UiFocusOwners.Contains(owner)) UiFocusOwners.Add(owner);
        }

        public static void PopUiFocus(object owner) => UiFocusOwners.Remove(owner);

        internal static void ResetStatics()
        {
            UiFocusOwners.Clear();
            Modules = null;
            Root = null;
            Platform = null;
        }
    }
}
