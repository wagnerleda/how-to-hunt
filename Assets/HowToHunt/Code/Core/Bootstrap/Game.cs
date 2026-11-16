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

        /// <summary>Launched with -nocfg (bots, automated tests): no config or profile writes.</summary>
        public static bool TestInstance { get; internal set; }

        /// <summary>
        /// Build version: VERSION file + git commit count (e.g. 0.1.0.42), stamped by tools/build-windows.
        /// In the editor it is computed the same way, so the editor and a fresh build of the same commit can play together.
        /// </summary>
#if UNITY_EDITOR
        public static string Version => _editorVersion ??= ComputeEditorVersion();
        private static string _editorVersion;

        private static string ComputeEditorVersion()
        {
            try
            {
                string root = System.IO.Path.GetDirectoryName(Application.dataPath);
                string baseVersion = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "VERSION")).Trim();
                var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-list --count HEAD")
                {
                    WorkingDirectory = root,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = System.Diagnostics.Process.Start(psi);
                string count = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(5000);
                return string.IsNullOrEmpty(count) ? baseVersion : $"{baseVersion}.{count}";
            }
            catch
            {
                return Application.version;
            }
        }
#else
        public static string Version => Application.version;
#endif

        /// <summary>Builds with a different protocol cannot join each other. Same as the version for now.</summary>
        public static string ProtocolVersion => Version;

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
#if UNITY_EDITOR
            _editorVersion = null; // recompute (VERSION + commit count) on every Play
#endif
        }
    }
}
