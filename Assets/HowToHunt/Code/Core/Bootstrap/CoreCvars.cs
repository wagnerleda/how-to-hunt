using HowToHunt.Foundation;

namespace HowToHunt.Core
{
    /// <summary>Player settings and engine-level tunables. Settings are archived cvars, so the console and the settings UI share one source of truth.</summary>
    public static class CoreCvars
    {
        public static readonly FloatCvar Sensitivity = new FloatCvar("sensitivity", 1.5f, "Mouse sensitivity (degrees per count x 0.022 x value, like Source).", CvarFlags.Archive, 0.05f, 20f);
        public static readonly FloatCvar Fov = new FloatCvar("fov", 90f, "Horizontal field of view in degrees.", CvarFlags.Archive, 60f, 130f);
        public static readonly FloatCvar Volume = new FloatCvar("volume", 0.8f, "Master volume 0..1.", CvarFlags.Archive, 0f, 1f);
        public static readonly BoolCvar InvertY = new BoolCvar("m_invert_y", false, "Invert mouse Y.", CvarFlags.Archive);
        public static readonly IntCvar FpsMax = new IntCvar("fps_max", 0, "Frame rate cap (0 = uncapped, follows vsync).", CvarFlags.Archive, 0, 1000);
        public static readonly BoolCvar VSync = new BoolCvar("mat_vsync", true, "Vertical sync.", CvarFlags.Archive);
        public static readonly StringCvar PlayerName = new StringCvar("name", "", "Display name override (empty = Steam name).", CvarFlags.Archive);

        internal static void Register(ConsoleSystem console)
        {
            console.Register(Sensitivity);
            console.Register(Fov);
            console.Register(Volume);
            console.Register(InvertY);
            console.Register(FpsMax);
            console.Register(VSync);
            console.Register(PlayerName);

            Volume.Changed += _ => UnityEngine.AudioListener.volume = Volume.Value;
            FpsMax.Changed += _ => ApplyFrameRate();
            VSync.Changed += _ => ApplyFrameRate();
            UnityEngine.AudioListener.volume = Volume.Value;
            ApplyFrameRate();
        }

        private static void ApplyFrameRate()
        {
            UnityEngine.QualitySettings.vSyncCount = VSync.Value ? 1 : 0;
            UnityEngine.Application.targetFrameRate = FpsMax.Value <= 0 ? -1 : FpsMax.Value;
        }
    }
}
