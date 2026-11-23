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
        public static readonly IntCvar FpsMax = new IntCvar("fps_max", 0, "Frame rate limit (0 = none). A limit replaces vertical sync, which can't cap below the screen's rate.", CvarFlags.Archive, 0, 1000);
        public static readonly BoolCvar VSync = new BoolCvar("mat_vsync", true, "Vertical sync (when fps_max is 0).", CvarFlags.Archive);
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
            console.Register(ScatterRenderer.Density);
            console.Register(ScatterRenderer.FadeStart);
            RenderQuality.Register(console);

            Volume.Changed += _ => UnityEngine.AudioListener.volume = Volume.Value;
            FpsMax.Changed += _ => ApplyFrameRate();
            VSync.Changed += _ => ApplyFrameRate();
            UnityEngine.AudioListener.volume = Volume.Value;
            ApplyFrameRate();
        }

        /// <summary>
        /// Applies mat_vsync and fps_max. Fish-Net sets Application.targetFrameRate to its own maximum (9999) whenever a
        /// server or client starts or stops, which silently undid fps_max; the network session calls this after it.
        /// </summary>
        public static void ApplyFrameRate()
        {
            // Unity ignores targetFrameRate while vSyncCount > 0, so with vsync on (the default) fps_max did nothing.
            // A set limit wins, like fps_max in Source: vsync off, capped at the limit.
            bool limited = FpsMax.Value > 0;
#if UNITY_EDITOR
            // Play mode writes QualitySettings into the project: put the project's value back when it ends.
            if (_editorVSync < 0)
            {
                _editorVSync = UnityEngine.QualitySettings.vSyncCount;
                UnityEngine.Application.quitting += () => { UnityEngine.QualitySettings.vSyncCount = _editorVSync; _editorVSync = -1; };
            }
#endif
            UnityEngine.QualitySettings.vSyncCount = !limited && VSync.Value ? 1 : 0;
            UnityEngine.Application.targetFrameRate = limited ? FpsMax.Value : -1;
        }

#if UNITY_EDITOR
        private static int _editorVSync = -1;
#endif
    }
}
