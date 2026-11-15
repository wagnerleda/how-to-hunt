using System;
using System.IO;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Sim.Movement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HowToHunt.Player
{
    /// <summary>
    /// Movement demos: <c>record name</c> / <c>stop</c> capture every tick's command of the local player;
    /// <c>playdemo name</c> replays it (host only) and checks the end state matches. Demos recorded in the editor
    /// are saved under Tests/Data/Demos and become regression tests automatically.
    /// </summary>
    public static class DemoRecorder
    {
        public const string TestDemosFolder = "Assets/HowToHunt/Tests/Data/Demos";

        private static MoveDemo _recording;
        private static string _recordingName;

        public static bool IsRecording => _recording != null;

        public static string DemoDirectory => Path.Combine(Application.persistentDataPath, "demos");

        public static void Register(ConsoleSystem console)
        {
            console.Register("record", "record <name>: record your movement until 'stop'.", args =>
            {
                if (args.Count == 0) { console.Warn("Usage: record <name>"); return; }
                Start(args[0]);
            });
            console.Register("stop", "Stop recording a demo and save it.", _ => Stop());
            console.Register("playdemo", "playdemo <name>: replay a movement demo (host only).", args =>
            {
                if (args.Count == 0) { console.Warn("Usage: playdemo <name>"); return; }
                Play(args[0]);
            });
            console.Register("demos", "List saved movement demos.", _ =>
            {
                foreach (string dir in DemoFolders())
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (string f in Directory.GetFiles(dir, "*" + MoveDemo.Extension))
                        console.Print($"{Path.GetFileNameWithoutExtension(f)}  ({dir})");
                }
            });
            PlayerMotor.CmdSimulated += OnCmdSimulated;
        }

        public static void Unregister() => PlayerMotor.CmdSimulated -= OnCmdSimulated;

        private static void Start(string name)
        {
            PlayerMotor local = PlayerMotor.Local;
            if (local == null) { Game.Console.Warn("record: no local player (host or join first)."); return; }
            _recording = new MoveDemo
            {
                Map = SceneManager.GetActiveScene().name,
                TickDelta = (float)local.TimeManager.TickDelta,
                Config = MovementCvars.Config.Clone(),
                Start = local.State,
            };
            _recording.Start.Events = MoveEvents.None;
            _recordingName = name;
            Game.Console.Print($"Recording '{name}'... type 'stop' to save.");
        }

        private static void OnCmdSimulated(PlayerMotor motor, MoveCmd cmd, MoveState after)
        {
            if (_recording == null || motor != PlayerMotor.Local) return;
            _recording.Cmds.Add(cmd);
            _recording.End = after;
        }

        private static void Stop()
        {
            if (_recording == null) { Game.Console.Warn("Not recording."); return; }
            MoveDemo demo = _recording;
            _recording = null;
            demo.End.Events = MoveEvents.None;
            demo.End.LandSpeed = 0f;
            string text = demo.Write();
            string path = Save(_recordingName, text);
            Game.Console.Print($"Saved {demo.Cmds.Count} ticks ({demo.Cmds.Count * demo.TickDelta:0.0} s) to {path}");
        }

        private static string Save(string name, string text)
        {
            string file = SanitizeName(name) + MoveDemo.Extension;
            Directory.CreateDirectory(DemoDirectory);
            string path = Path.Combine(DemoDirectory, file);
            File.WriteAllText(path, text);
#if UNITY_EDITOR
            // In the editor, demos also land in the test data so they become replay regression tests.
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string testPath = Path.Combine(projectRoot, TestDemosFolder, file);
            Directory.CreateDirectory(Path.GetDirectoryName(testPath));
            File.WriteAllText(testPath, text);
            UnityEditor.AssetDatabase.Refresh();
            path = testPath;
#endif
            return path;
        }

        private static void Play(string name)
        {
            PlayerMotor local = PlayerMotor.Local;
            if (local == null || !local.IsServerStarted) { Game.Console.Warn("playdemo: host a session first (playback runs on the host)."); return; }
            string path = Find(name);
            if (path == null) { Game.Console.Warn($"No demo named '{name}'. Try 'demos'."); return; }
            MoveDemo demo;
            try { demo = MoveDemo.Read(File.ReadAllText(path)); }
            catch (Exception e) { Game.Console.Error($"Bad demo: {e.Message}"); return; }
            if (demo.Map != SceneManager.GetActiveScene().name)
                Game.Console.Warn($"Demo was recorded on '{demo.Map}', current map is '{SceneManager.GetActiveScene().name}'.");

            local.ServerSetState(demo.Start);
            local.Input.PlaybackFinished -= OnPlaybackFinished;
            local.Input.PlaybackFinished += OnPlaybackFinished;
            local.Input.Playback = demo;
            Game.Console.Print($"Playing '{name}' ({demo.Cmds.Count} ticks).");
        }

        private static void OnPlaybackFinished(MoveDemo demo)
        {
            PlayerMotor local = PlayerMotor.Local;
            if (local == null) return;
            float error = System.Numerics.Vector3.Distance(local.State.Position, demo.End.Position);
            string verdict = error < 0.01f ? "matches the recording" : $"differs from the recording by {error:0.000} m (physics or map changed)";
            Game.Console.Print($"Demo finished: end position {verdict}.");
        }

        private static string Find(string name)
        {
            string file = SanitizeName(name) + MoveDemo.Extension;
            foreach (string dir in DemoFolders())
            {
                string p = Path.Combine(dir, file);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string[] DemoFolders()
        {
#if UNITY_EDITOR
            return new[] { DemoDirectory, Path.Combine(Path.GetDirectoryName(Application.dataPath), TestDemosFolder) };
#else
            return new[] { DemoDirectory };
#endif
        }

        private static string SanitizeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}
