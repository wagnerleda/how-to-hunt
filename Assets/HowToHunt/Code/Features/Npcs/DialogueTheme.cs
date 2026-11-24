using System;
using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Npcs
{
    /// <summary>How a line is spoken: each has its own bubble shape, colours and text.</summary>
    public enum BubbleStyle : byte { Say, Shout, Whisper, Think, Narrate }

    /// <summary>
    /// Every dialogue look-and-feel number in one place: text speed and punctuation pauses, bubble size and motion,
    /// the five bubble styles, voices, approach markers, barks and reply choices. The asset lives at
    /// Resources/UI/DialogueTheme (created once by How to Hunt > Rebuild Generated Assets, then yours to tune in the
    /// Inspector); without it the defaults below apply.
    /// </summary>
    [CreateAssetMenu(menuName = "How to Hunt/Dialogue Theme", fileName = "DialogueTheme")]
    public sealed class DialogueTheme : ScriptableObject
    {
        [Serializable]
        public sealed class Style
        {
            public BubbleStyle Kind;
            public Color Fill = Color.white;
            public Color Outline = new Color(0.16f, 0.12f, 0.1f);
            public Color Text = new Color(0.16f, 0.12f, 0.1f);
            [Tooltip("Corner radius in pixels (small = sharp corners).")]
            public int Radius = 22;
            public int OutlineWidth = 3;
            public bool DashedOutline;
            [Tooltip("Pointer: a tail to the speaker. Dots: thought bubbles. None: narration.")]
            public TailKind Tail = TailKind.Pointer;
            public float TextScale = 1f;
            public bool Italic;
            public bool Bold;
            [Tooltip("The bubble trembles while the line types (shouts). Off when 'reduce animations' is on.")]
            public float Shake;
            [Tooltip("Multiplies the typing speed (whispers are slower).")]
            public float SpeedScale = 1f;
        }

        public enum TailKind : byte { Pointer, Dots, None }

        [Header("Text")]
        public float CharsPerSecond = 48f;
        [Tooltip("Holding or pressing Interact while a line types speeds it up this much (a second press shows it all).")]
        public float HurryMultiplier = 5f;
        public float PauseComma = 0.09f;
        public float PauseSentence = 0.28f;
        public float PauseEllipsis = 0.45f;
        public float FontSize = 26f;

        [Header("Bubble")]
        public float MaxWidth = 540f;
        public Vector2 Padding = new Vector2(24f, 16f);
        [Tooltip("Metres above the NPC's centre where the tail points (the top of the head).")]
        public float HeadHeight = 1.05f;
        public float TailHeight = 22f;
        public float ScreenMargin = 18f;

        [Header("Motion")]
        public float PopInSeconds = 0.32f;
        public Ease.Kind PopInEase = Ease.Kind.OutBack;
        [Tooltip("Squash and stretch on appear (0 = none).")]
        public float Squash = 0.14f;
        public float PopOutSeconds = 0.16f;
        public float NextBobHz = 2.2f;

        [Header("Voices")]
        [Tooltip("A voice blip every N letters (syllable-ish).")]
        public int VoiceEveryChars = 2;
        public float VoiceVolume = 0.35f;
        public float VoicePitchJitter = 0.07f;

        [Header("Approach marker")]
        public float MarkerHeight = 1.55f;
        public float MarkerBob = 0.08f;
        public float MarkerBobHz = 1.3f;
        public float MarkerRange = 9f;

        [Header("Barks")]
        public float BarkRadius = 7f;
        public float BarkCooldown = 25f;
        public float BarkSeconds = 3.2f;

        [Header("Reply choices")]
        public Color ChoiceFill = new Color(0.99f, 0.96f, 0.9f, 0.96f);
        public Color ChoiceText = new Color(0.2f, 0.15f, 0.12f);
        public Color ChoiceSelected = new Color(0.98f, 0.76f, 0.36f, 1f);
        public Color ChoiceDisabled = new Color(0.75f, 0.72f, 0.68f, 0.75f);
        public float ChoiceStagger = 0.05f;

        public Style[] Styles =
        {
            new Style { Kind = BubbleStyle.Say },
            new Style { Kind = BubbleStyle.Shout, Fill = new Color(1f, 0.95f, 0.82f), Outline = new Color(0.78f, 0.22f, 0.12f), Text = new Color(0.55f, 0.1f, 0.05f), Radius = 8, OutlineWidth = 5, TextScale = 1.15f, Bold = true, Shake = 2.2f, SpeedScale = 1.3f },
            new Style { Kind = BubbleStyle.Whisper, Fill = new Color(1f, 1f, 1f, 0.78f), Outline = new Color(0.45f, 0.42f, 0.4f), Text = new Color(0.4f, 0.37f, 0.35f), Radius = 26, OutlineWidth = 2, DashedOutline = true, TextScale = 0.88f, Italic = true, SpeedScale = 0.75f },
            new Style { Kind = BubbleStyle.Think, Fill = new Color(0.94f, 0.96f, 1f), Outline = new Color(0.45f, 0.5f, 0.62f), Text = new Color(0.27f, 0.3f, 0.4f), Radius = 40, OutlineWidth = 3, Tail = TailKind.Dots, Italic = true },
            new Style { Kind = BubbleStyle.Narrate, Fill = new Color(0.12f, 0.1f, 0.09f, 0.9f), Outline = new Color(0.95f, 0.75f, 0.35f), Text = new Color(0.98f, 0.93f, 0.84f), Radius = 6, OutlineWidth = 2, Tail = TailKind.None, Italic = true },
        };

        public Style For(BubbleStyle kind)
        {
            for (int i = 0; i < Styles.Length; i++) if (Styles[i].Kind == kind) return Styles[i];
            return Styles[0];
        }

        private static DialogueTheme _current;

        /// <summary>The project's theme (Resources/UI/DialogueTheme), or the defaults.</summary>
        public static DialogueTheme Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Resources.Load<DialogueTheme>("UI/DialogueTheme");
                if (_current == null) _current = CreateInstance<DialogueTheme>();
                return _current;
            }
        }
    }
}
