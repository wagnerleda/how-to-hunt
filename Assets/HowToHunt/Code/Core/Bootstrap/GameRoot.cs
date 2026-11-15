using System;
using System.Collections.Generic;
using System.IO;
using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Boots the game before the first scene loads: console, config, platform, feature modules.
    /// Any scene can be played directly from the editor.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class GameRoot : MonoBehaviour
    {
        private const string ConfigFile = "config.cfg";
        private float _saveConfigAt = -1f;

        public static string ConfigDirectory => Path.Combine(Application.persistentDataPath, "cfg");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Game.Root != null) return;
            var go = new GameObject("[GameRoot]");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        private void Awake()
        {
            Game.ResetStatics();
            EventBus.ClearAll();
            ConsoleSystem.ResetGlobal();
            Game.Root = this;

            ConsoleSystem console = ConsoleSystem.Global;
            console.ConfigLoader = LoadConfigText;
            console.ExceptionHandler = Debug.LogException;
            console.Output += EchoToUnityLog;
            CoreCvars.Register(console);
            KeyBindings.Register(console);
            RegisterCoreCommands(console);

            Game.Modules = new ModuleRegistry();
            Game.Modules.DiscoverAndRegister();
            if (Game.Platform == null) Game.Platform = new OfflinePlatform("No platform module.");

            // -nocfg: neither read nor write config.cfg (several instances on one PC: tests, bots).
            NoConfig = Array.IndexOf(Environment.GetCommandLineArgs(), "-nocfg") >= 0;
            if (!NoConfig)
            {
                ExecIfExists(console, ConfigFile);
                ExecIfExists(console, "autoexec.cfg");
            }
            foreach (Cvar cvar in console.Cvars)
                if ((cvar.Flags & CvarFlags.Archive) != 0) cvar.Changed += _ => ScheduleConfigSave();

            Game.Modules.InitializeAll();
            gameObject.AddComponent<DevConsoleView>();

            console.Print($"How to Hunt {Game.Version} ({Application.platform}), platform: {Game.Platform.Name}" +
                          (Game.Platform.IsOnline ? $" as {Game.Platform.LocalUserName}" : $" ({Game.Platform.OfflineReason})"));
            ExecuteCommandLine(console);
        }

        private void Update()
        {
            Game.Platform?.Tick();
            if (_saveConfigAt > 0f && Time.unscaledTime >= _saveConfigAt)
            {
                _saveConfigAt = -1f;
                SaveConfig();
            }
        }

        private void OnApplicationQuit()
        {
            SaveConfig();
            Game.Modules?.ShutdownAll();
            Game.Platform?.Shutdown();
            ConsoleSystem.Global.Output -= EchoToUnityLog;
        }

        private void OnDestroy()
        {
            if (Game.Root == this) Game.Root = null;
        }

        private static void ExecIfExists(ConsoleSystem console, string file)
        {
            string text = LoadConfigText(file);
            if (text != null) console.Execute(text);
        }

        private void ScheduleConfigSave() => _saveConfigAt = Time.unscaledTime + 1f;

        private static bool NoConfig;

        private static void SaveConfig()
        {
            if (NoConfig) return;
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                using var writer = new StreamWriter(Path.Combine(ConfigDirectory, ConfigFile));
                ConsoleSystem.Global.WriteArchive(writer);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Config] Could not save: {e.Message}");
            }
        }

        private static string LoadConfigText(string name)
        {
            if (!name.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase)) name += ".cfg";
            foreach (string dir in new[] { ConfigDirectory, Path.Combine(Application.streamingAssetsPath, "cfg") })
            {
                string path = Path.Combine(dir, name);
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            return null;
        }

        /// <summary>Source-style command line: <c>HowToHunt.exe +host +sv_cheats 1</c> runs "host" then "sv_cheats 1".</summary>
        private static void ExecuteCommandLine(ConsoleSystem console)
        {
            string[] args = Environment.GetCommandLineArgs();
            var current = new List<string>();
            void Flush()
            {
                if (current.Count > 0) console.Execute(string.Join(" ", current));
                current.Clear();
            }
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("+", StringComparison.Ordinal))
                {
                    Flush();
                    current.Add(a.Substring(1));
                }
                else if (current.Count > 0)
                {
                    current.Add(a.Contains(" ") ? $"\"{a}\"" : a);
                }
            }
            Flush();
        }

        private static void RegisterCoreCommands(ConsoleSystem console)
        {
            console.Register("quit", "Quit the game.", _ =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
            console.Register("host_writeconfig", "Save config.cfg now.", _ => { SaveConfig(); console.Print($"Saved to {ConfigDirectory}"); });
            console.Register("version", "Print build and protocol version.", _ => console.Print($"version {Game.Version}, protocol {Game.ProtocolVersion}, Unity {Application.unityVersion}"));
            console.Register("clear", "Clear the console.", _ => DevConsoleView.ClearLog());
            console.Register("delay", "delay <seconds> <command...>: run a command later (scripted tests).", args =>
            {
                if (args.Count < 2 || !float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds))
                {
                    console.Warn("Usage: delay <seconds> <command...>");
                    return;
                }
                string rest = args.ArgString.Substring(args[0].Length).Trim();
                Game.Root.StartCoroutine(Delayed(seconds, rest));
            });
            console.Register("smoke_quit", "smoke_quit <seconds>: log SMOKE OK and quit after a delay (launch tests).", args =>
            {
                float seconds = args.Count > 0 && float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s) ? s : 5f;
                Game.Root.StartCoroutine(SmokeQuit(seconds));
            });
        }

        private static System.Collections.IEnumerator Delayed(float seconds, string command)
        {
            yield return new WaitForSecondsRealtime(seconds);
            ConsoleSystem.Global.Execute(command);
        }

        private static System.Collections.IEnumerator SmokeQuit(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Debug.Log($"SMOKE OK version={Game.Version} platform={Game.Platform?.Name} frames={Time.frameCount} modules={Game.Modules?.Modules.Count}");
            Application.Quit(0);
        }

        private static void EchoToUnityLog(ConsoleLevel level, string line)
        {
            // Console lines are also written to the player log (useful for remote bug reports).
            DevConsoleView.Append(level, line);
            string tagged = DevConsoleView.EchoPrefix + line;
            if (level == ConsoleLevel.Error) Debug.LogError(tagged);
            else if (level == ConsoleLevel.Warning) Debug.LogWarning(tagged);
            else Debug.Log(tagged);
        }
    }
}
