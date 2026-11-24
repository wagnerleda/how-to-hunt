using TMPro;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// The game's font: The Wildeast (a hand-drawn western brush, completed with punctuation from its own strokes).
    /// Owner, 2026-11-24: every text in the game uses it. It is TextMesh Pro's default font (set by
    /// GeneratedAssets.EnsureDisplayFont, with the plain face as fallback for accents and symbols), and the IMGUI
    /// screens (console, mask editor) use <see cref="ImGui"/>. <see cref="Use"/> still tunes headings (no faux bold,
    /// a little spacing). docs/ASSETS.md: personal-use license until bought.
    /// </summary>
    public static class UiFonts
    {
        public const string DisplayName = "Wildeast SDF";
        public const string SourceName = "TheWildeast-HowToHunt";
        private static Font _imgui;
        private static bool _lookedImGui;

        /// <summary>The font file for IMGUI (GUI.skin.font); null if missing.</summary>
        public static Font ImGui
        {
            get
            {
                if (!_lookedImGui)
                {
                    _lookedImGui = true;
                    _imgui = Resources.Load<Font>("Fonts & Materials/" + SourceName);
                }
                return _imgui;
            }
        }
        private static TMP_FontAsset _display;
        private static bool _looked;

        /// <summary>The display font (null if it hasn't been generated: callers keep the default).</summary>
        public static TMP_FontAsset Display
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    _display = Resources.Load<TMP_FontAsset>("Fonts & Materials/" + DisplayName);
                }
                return _display;
            }
        }

        /// <summary>Set a text in the display font. A brush face carries its own weight: no faux bold.</summary>
        public static T Use<T>(T text) where T : TMP_Text
        {
            TMP_FontAsset font = Display;
            if (text == null || font == null) return text;
            text.font = font;
            text.fontStyle &= ~FontStyles.Bold;
            text.characterSpacing = 2f;
            return text;
        }

        /// <summary>A rich-text span in the display font (for names inside running text).</summary>
        public static string Span(string s) => Display == null ? s : $"<font=\"{DisplayName}\">{s}</font>";
    }
}
