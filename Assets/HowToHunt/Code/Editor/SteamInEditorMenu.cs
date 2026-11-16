using HowToHunt.Platform;
using UnityEditor;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Menu toggle: use Steam in Editor Play mode (off by default). With it on, Steam closes the Editor whenever another
    /// app-480 instance on the same account exits (test builds, the bots test), so leave it off unless testing invites.
    /// </summary>
    public static class SteamInEditorMenu
    {
        private const string Path = "How to Hunt/Use Steam in Play Mode";

        [MenuItem(Path, priority = 40)]
        private static void Toggle() => EditorPrefs.SetBool(PlatformModule.SteamInEditorPref, !EditorPrefs.GetBool(PlatformModule.SteamInEditorPref, false));

        [MenuItem(Path, true)]
        private static bool Validate()
        {
            Menu.SetChecked(Path, EditorPrefs.GetBool(PlatformModule.SteamInEditorPref, false));
            return true;
        }
    }
}
