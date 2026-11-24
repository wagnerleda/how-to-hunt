using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.UI
{
    /// <summary>
    /// Every menu look-and-feel number in one place: motion (cascade, easing, transitions, hover), colours, the
    /// living background. The asset lives at Resources/UI/MenuTheme (created once by How to Hunt > Rebuild Generated
    /// Assets, then yours to tune in the Inspector); without it the defaults below apply. "Reduce animations"
    /// (ui_reduce_motion) turns every movement into a plain fade.
    /// </summary>
    [CreateAssetMenu(menuName = "How to Hunt/Menu Theme", fileName = "MenuTheme")]
    public sealed class MenuTheme : ScriptableObject
    {
        [Header("Motion")]
        [Tooltip("Delay between one item and the next as a page comes in (s).")]
        public float Stagger = 0.035f;
        public float ItemInSeconds = 0.34f;
        public Ease.Kind ItemInEase = Ease.Kind.OutBack;
        [Tooltip("How far items rise as they come in (px).")]
        public float ItemRise = 18f;
        public float PageOutSeconds = 0.12f;
        public float PanelInSeconds = 0.38f;
        [Tooltip("Hover/focus: scale, sideways nudge (px), how fast it follows.")]
        public float FocusScale = 1.045f;
        public float FocusNudge = 6f;
        public float FocusSharpness = 18f;
        public float PressScale = 0.94f;
        [Tooltip("The focused button breathes a little (0 = still).")]
        public float IdleBreath = 0.008f;

        [Header("Colours")]
        public Color Panel = new Color(0.07f, 0.1f, 0.12f, 0.93f);
        public Color PanelEdge = new Color(1f, 0.62f, 0.15f, 0.45f);
        public Color Title = new Color(1f, 0.62f, 0.15f);
        public Color Text = new Color(0.92f, 0.94f, 0.96f);
        public Color Dim = new Color(0.68f, 0.74f, 0.78f);
        public Color Button = new Color(0.17f, 0.23f, 0.27f);
        public Color ButtonText = new Color(0.94f, 0.95f, 0.96f);
        public Color Focus = new Color(0.97f, 0.62f, 0.2f);
        public Color FocusText = new Color(0.1f, 0.07f, 0.05f);
        public Color Disabled = new Color(0.13f, 0.15f, 0.17f);
        public Color DisabledText = new Color(0.45f, 0.5f, 0.53f);

        [Header("Background")]
        public Color SkyTop = new Color(1f, 0.74f, 0.42f, 0.35f);
        public Color SkyTopAlt = new Color(0.98f, 0.6f, 0.52f, 0.35f);
        [Tooltip("Seconds for the sky tint to drift from one colour to the other and back.")]
        public float SkyCycle = 18f;
        public float Vignette = 0.55f;
        public int Pollen = 42;
        [Tooltip("How far the background layers follow the mouse (px).")]
        public float Parallax = 22f;
        [Tooltip("Pause menu: how dark the game behind gets.")]
        public float PauseDim = 0.45f;

        private static MenuTheme _current;

        public static MenuTheme Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Resources.Load<MenuTheme>("UI/MenuTheme");
                if (_current == null) _current = CreateInstance<MenuTheme>();
                return _current;
            }
        }
    }
}
