using System;
using System.IO;
using System.Linq;
using HowToHunt.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Windows player build. Called by tools/build-windows (batch mode, or through a running Editor).
    /// The version becomes Application.version, which is also the network protocol version.
    /// </summary>
    public static class BuildScript
    {
        public const string ExeName = "HowToHunt.exe";

        [MenuItem("How to Hunt/Build Windows (development)", priority = 20)]
        public static void BuildFromMenu() => BuildWindows("Builds/Windows", PlayerSettings.bundleVersion, true, false);

        /// <summary>Batch-mode entry: -executeMethod HowToHunt.Editor.BuildScript.BuildWindowsCli -buildOutput dir -buildVersion x.y.z.n [-dev] [-il2cpp]</summary>
        public static void BuildWindowsCli()
        {
            string[] args = Environment.GetCommandLineArgs();
            string output = Arg(args, "-buildOutput") ?? "Builds/Windows";
            string version = Arg(args, "-buildVersion") ?? PlayerSettings.bundleVersion;
            bool dev = args.Contains("-dev");
            bool il2cpp = args.Contains("-il2cpp");
            string error = BuildWindows(output, version, dev, il2cpp);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }

        /// <summary>Returns null on success, otherwise an error message.</summary>
        public static string BuildWindows(string outputDir, string version, bool development, bool il2cpp)
        {
            // The stamped version must not churn ProjectSettings.asset in git: restore it after the build.
            string previousVersion = PlayerSettings.bundleVersion;
            try
            {
                return BuildWindowsInternal(outputDir, version, development, il2cpp);
            }
            finally
            {
                PlayerSettings.bundleVersion = previousVersion;
                AssetDatabase.SaveAssets();
            }
        }

        private static string BuildWindowsInternal(string outputDir, string version, bool development, bool il2cpp)
        {
            ApplyPlayerSettings(version, il2cpp);

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) return Report("No scenes enabled in Build Settings. Run How to Hunt > Rebuild Graybox Scene.");

            string fullOutput = Path.GetFullPath(outputDir);
            if (Directory.Exists(fullOutput)) Directory.Delete(fullOutput, true);
            Directory.CreateDirectory(fullOutput);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(fullOutput, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                return Report($"Build failed: {report.summary.result}, {report.summary.totalErrors} error(s).");

            // Steam needs steam_appid.txt next to the exe until we ship under our own app id via Steam.
            File.WriteAllText(Path.Combine(fullOutput, "steam_appid.txt"), PlatformSettings.Load().SteamAppId.ToString());
            File.WriteAllText(Path.Combine(fullOutput, "version.txt"), version);

            // Debug symbols folders are not shipped.
            foreach (string dir in Directory.GetDirectories(fullOutput, "*DontShip*")) Directory.Delete(dir, true);

            Debug.Log($"[Build] OK {version} ({(il2cpp ? "IL2CPP" : "Mono")}{(development ? ", development" : "")}) -> {fullOutput} " +
                      $"in {report.summary.totalTime.TotalSeconds:0}s, {report.summary.totalSize / (1024 * 1024)} MB");
            return null;
        }

        private static void ApplyPlayerSettings(string version, bool il2cpp)
        {
            PlayerSettings.companyName = "How to Hunt";
            PlayerSettings.productName = "How to Hunt";
            PlayerSettings.bundleVersion = version;
            PlayerSettings.runInBackground = true;   // keep simulating when alt-tabbed (multiplayer)
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.forceSingleInstance = false; // two instances on one PC for loopback testing
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, il2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        }

        private static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static string Report(string error)
        {
            Debug.LogError("[Build] " + error);
            return error;
        }
    }
}
