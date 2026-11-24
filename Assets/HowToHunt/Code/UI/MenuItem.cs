using System;
using HowToHunt.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.UI
{
    public enum MenuItemKind : byte { Text, Small, Header, Button, Toggle, Slider, Choice, KeyBind, Spacer }

    /// <summary>
    /// One line of a menu page: a button (big, normal, small, half width, or with a caption on its left), a toggle,
    /// a slider, a row of choices, a key binding, or text. Plain data plus its uGUI objects; SessionMenu lays the page
    /// out, moves the focus and animates every item in one loop (no MonoBehaviour per button). Pages are rebuilt
    /// when they change (menus may allocate; gameplay doesn't).
    /// </summary>
    public sealed class MenuItem
    {
        public MenuItemKind Kind;
        public string Key;
        public bool Enabled = true;
        public bool Half;
        public float Height;
        public float ButtonWidth; // > 0: a compact button on the right, Caption text on the left
        public string Caption;

        public Action OnActivate;
        public Func<bool> GetToggle;
        public Action<bool> SetToggle;
        public Func<float> GetValue;
        public Action<float> SetValue;
        public float Min, Max;
        public string Format;
        public int[] ChoiceValues;
        public string[] ChoiceLabels;
        public Func<int> GetChoice;
        public Action<int> SetChoice;
        public Func<string> Live;

        // uGUI
        public RectTransform Root, Body;
        public CanvasGroup Group;
        public Image Fill, Outline, Glow, Track, TrackFill, Knob, Box, Check;
        public TextMeshProUGUI Label, Value, CaptionText;
        public Image[] OptionBacks;
        public TextMeshProUGUI[] Options;

        // Animation
        public float Delay, Age, Highlight, Press;
        private float _shownValue = float.NaN;
        public Vector2 Home;

        public bool Focusable => Enabled && (Kind == MenuItemKind.Button || Kind == MenuItemKind.Toggle || Kind == MenuItemKind.Slider || Kind == MenuItemKind.Choice || Kind == MenuItemKind.KeyBind);

        public string Text
        {
            set
            {
                if (Label != null) Label.SetText(value ?? string.Empty);
            }
        }

        /// <summary>Animates the item: cascade in, focus (colour, glow, scale, nudge, breathing), press squash, live values.</summary>
        public void Animate(MenuTheme theme, float dt, bool focused, bool calm, float outT)
        {
            Age += dt;
            float t = Mathf.Clamp01((Age - Delay) / Mathf.Max(0.01f, theme.ItemInSeconds));
            float alpha = Mathf.Clamp01(t * 2.2f) * (1f - outT);
            Group.alpha = alpha;
            float rise = calm ? 0f : (1f - Ease.Evaluate(theme.ItemInEase, t)) * -theme.ItemRise + outT * 10f;

            Highlight = Ease.Damp(Highlight, focused ? 1f : 0f, theme.FocusSharpness, dt);
            Press = Mathf.MoveTowards(Press, 0f, dt * 6f);
            float nudge = calm ? 0f : Highlight * theme.FocusNudge;
            Root.anchoredPosition = Home + new Vector2(nudge, rise);
            float scale = 1f;
            if (!calm && Focusable)
            {
                float breath = focused ? Mathf.Sin(Time.unscaledTime * 3.1f) * theme.IdleBreath : 0f;
                scale = Mathf.Lerp(1f, theme.FocusScale, Highlight) + breath;
                scale *= Mathf.Lerp(1f, theme.PressScale, Ease.OutCubic(Press));
            }
            Body.localScale = new Vector3(scale, scale, 1f);

            if (Fill != null)
            {
                Fill.color = !Enabled ? theme.Disabled : Color.Lerp(theme.Button, theme.Focus, Highlight);
                if (Label != null && Kind != MenuItemKind.Toggle && Kind != MenuItemKind.Slider && Kind != MenuItemKind.Choice)
                    Label.color = !Enabled ? theme.DisabledText : Color.Lerp(theme.ButtonText, theme.FocusText, Highlight);
            }
            if (Outline != null) Outline.color = Color.Lerp(new Color(1f, 1f, 1f, 0.08f), new Color(1f, 0.85f, 0.5f, 0.9f), Highlight);
            if (Glow != null) Glow.color = new Color(theme.Focus.r, theme.Focus.g, theme.Focus.b, (calm ? 0.25f : 0.45f) * Highlight);

            switch (Kind)
            {
                case MenuItemKind.Toggle:
                {
                    bool on = GetToggle != null && GetToggle();
                    Check.color = new Color(theme.Focus.r, theme.Focus.g, theme.Focus.b, on ? 1f : 0f);
                    Box.color = Color.Lerp(new Color(1f, 1f, 1f, 0.35f), Color.white, Highlight);
                    Label.color = Color.Lerp(theme.Text, theme.Focus, Highlight);
                    break;
                }
                case MenuItemKind.Slider:
                {
                    float v = GetValue != null ? GetValue() : 0f;
                    float k = Mathf.InverseLerp(Min, Max, v);
                    float w = Track.rectTransform.rect.width;
                    TrackFill.rectTransform.sizeDelta = new Vector2(w * k, TrackFill.rectTransform.sizeDelta.y);
                    Knob.rectTransform.anchoredPosition = new Vector2(w * k, 0f);
                    float knob = Mathf.Lerp(16f, 22f, Highlight);
                    Knob.rectTransform.sizeDelta = new Vector2(knob, knob);
                    TrackFill.color = Color.Lerp(theme.Dim, theme.Focus, 0.5f + 0.5f * Highlight);
                    Label.color = Color.Lerp(theme.Text, theme.Focus, Highlight);
                    if (!Mathf.Approximately(v, _shownValue))
                    {
                        _shownValue = v;
                        Value.SetText(Format == "%" ? $"{v * 100f:0}%" : v.ToString(Format));
                    }
                    break;
                }
                case MenuItemKind.Choice:
                {
                    int current = GetChoice != null ? GetChoice() : 0;
                    for (int i = 0; i < Options.Length; i++)
                    {
                        bool on = ChoiceValues[i] == current;
                        OptionBacks[i].color = on ? theme.Focus : Color.Lerp(theme.Button, theme.Button * 1.3f, Highlight);
                        Options[i].color = on ? theme.FocusText : theme.ButtonText;
                    }
                    Label.color = Color.Lerp(theme.Text, theme.Focus, Highlight);
                    break;
                }
            }
        }

        /// <summary>The choice under a screen point, or -1 (choice rows).</summary>
        public int OptionAt(Vector2 screen)
        {
            if (OptionBacks == null) return -1;
            for (int i = 0; i < OptionBacks.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(OptionBacks[i].rectTransform, screen, null)) return i;
            return -1;
        }

        public void StepChoice(int dir)
        {
            if (GetChoice == null || SetChoice == null) return;
            int current = GetChoice(), index = 0;
            for (int i = 0; i < ChoiceValues.Length; i++) if (ChoiceValues[i] == current) index = i;
            index = Mathf.Clamp(index + dir, 0, ChoiceValues.Length - 1);
            SetChoice(ChoiceValues[index]);
        }

        public void StepSlider(int dir)
        {
            if (GetValue == null || SetValue == null) return;
            float step = (Max - Min) / 20f;
            SetValue(Mathf.Clamp(GetValue() + dir * step, Min, Max));
        }
    }
}
