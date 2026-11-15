using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Platform configuration, loaded from Resources/PlatformSettings. The build script writes steam_appid.txt
    /// from <see cref="SteamAppId"/>. 480 (Spacewar) is Valve's shared development app id.
    /// </summary>
    [CreateAssetMenu(menuName = "How to Hunt/Platform Settings", fileName = "PlatformSettings")]
    public sealed class PlatformSettings : ScriptableObject
    {
        [Tooltip("Steam app id. 480 = Spacewar (development). Replace with our own id after Steam Direct.")]
        [SerializeField] private uint _steamAppId = 480;

        [Tooltip("Relaunch through Steam when started outside it. Keep off while using 480.")]
        [SerializeField] private bool _restartThroughSteam;

        public uint SteamAppId => _steamAppId;
        public bool RestartThroughSteam => _restartThroughSteam;

        public static PlatformSettings Load()
        {
            var settings = Resources.Load<PlatformSettings>("PlatformSettings");
            return settings != null ? settings : CreateInstance<PlatformSettings>();
        }
    }
}
