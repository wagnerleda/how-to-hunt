using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Shared UI feel settings: the UI sound volume, "reduce animations" (accessibility: no shake, flashes, particles
    /// or overshoot, only fades), and the menu sounds. Sound clips are the Kenney interface pack already in the
    /// project; a family that has no clip simply plays nothing (a clean slot to fill later).
    /// </summary>
    public static class UiFeel
    {
        public static readonly FloatCvar Volume = new FloatCvar("snd_ui", 0.6f, "Menu and dialogue sound volume (0..1).", CvarFlags.Archive, 0f, 1f);
        public static readonly BoolCvar ReduceMotion = new BoolCvar("ui_reduce_motion", false, "Reduce animations: no shake, flashes, particles or bounces in menus and dialogue (fades only).", CvarFlags.Archive);

        // Sound slots (Resources/Audio/<family>): hover, confirm, back, open, error.
        public const string HoverSound = "Kenney/tick_001", ConfirmSound = "Kenney/confirmation_001", BackSound = "Kenney/drop_001";
        public const string OpenSound = "Kenney/maximize_004", ErrorSound = "Kenney/error_004", ToggleSound = "Kenney/toggle_001";

        internal static void Register(ConsoleSystem console)
        {
            console.Register(Volume);
            console.Register(ReduceMotion);
        }

        public static bool Calm => ReduceMotion.Value;

        public static void Play(string family, float volume = 1f, float pitch = 1f)
        {
            float v = Volume.Value * volume;
            if (v <= 0.001f) return;
            Sfx.Play2D(family, v, pitch);
        }

        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            float v = Volume.Value * volume;
            if (clip == null || v <= 0.001f) return;
            Sfx.Play2D(clip, v, pitch);
        }

        public static void Hover() => Play(HoverSound, 0.45f, Random.Range(0.97f, 1.05f));
        public static void Confirm() => Play(ConfirmSound, 0.7f);
        public static void Back() => Play(BackSound, 0.6f);
        public static void Open() => Play(OpenSound, 0.5f);
        public static void Error() => Play(ErrorSound, 0.6f);
    }
}
