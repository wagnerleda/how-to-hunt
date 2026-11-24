using System.Collections.Generic;
using HowToHunt.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Npcs
{
    /// <summary>
    /// A speech bubble over whoever speaks (screen space, on the HUD canvas): sized to its text, a tail pointing at
    /// the speaker's head, kept wholly on screen (below the head, tail flipped, when there is no room above), popping
    /// in with a little squash and stretch and fading out. Five styles (DialogueTheme). Types its line out with
    /// pauses on punctuation, a voice blip every few letters and inline effects (BubbleText). One component for every
    /// NPC and every bark, pooled; no allocations per frame.
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        private enum Phase : byte { Hidden, Showing, Hiding }

        private static readonly Stack<SpeechBubble> Pool = new Stack<SpeechBubble>();
        private static readonly Vector3[] Corners = new Vector3[4];

        private RectTransform _rt, _body, _textRt;
        private CanvasGroup _group;
        private Image _fill, _outline, _tail, _tailOutline, _next, _dotA, _dotB;
        private TextMeshProUGUI _text;
        private readonly BubbleText _fx = new BubbleText();

        private Phase _phase;
        private Transform _speaker;
        private DialogueTheme.Style _style;
        private BubbleStyle _kind;
        private float _age, _hideAge, _lifetime;
        private Vector2 _size;
        private float _shownChars, _pauseLeft, _voicePitch = 1f;
        private int _lastVoiced;
        private bool _hurried, _showNext;
        private AudioClip _voice;

        /// <summary>The line is still typing out.</summary>
        public bool Typing => _phase == Phase.Showing && _shownChars < _fx.Count;
        public bool Active => _phase == Phase.Showing;
        /// <summary>Show the bouncing "more" arrow once the line is fully out.</summary>
        public bool ShowNext { set => _showNext = value; }

        // --- pool -------------------------------------------------------------------------------------------------

        /// <summary>
        /// Shows <paramref name="line"/> over <paramref name="speaker"/> (null: narration at the top of the screen).
        /// <paramref name="lifetime"/> > 0 hides it on its own after the line is out (barks). Inline syntax: BubbleText.
        /// </summary>
        public static SpeechBubble Show(Transform speaker, string line, BubbleStyle style, float voicePitch = 1f, AudioClip voice = null, float lifetime = 0f)
        {
            SpeechBubble b = Pool.Count > 0 ? Pool.Pop() : Create();
            b.Begin(speaker, line, style, voicePitch, voice, lifetime);
            return b;
        }

        /// <summary>Pops out, then back to the pool.</summary>
        public void Hide()
        {
            if (_phase != Phase.Showing) return;
            _phase = Phase.Hiding;
            _hideAge = 0f;
        }

        /// <summary>First call: type faster. Second call (or a calm player): show the whole line.</summary>
        public void Hurry()
        {
            if (!Typing) return;
            if (_hurried) _shownChars = _fx.Count;
            _hurried = true;
        }

        public void Finish() => _shownChars = _fx.Count;

        private static SpeechBubble Create()
        {
            var go = new GameObject("Speech Bubble", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(Hud.Root, false);
            var b = go.AddComponent<SpeechBubble>();
            b.Build();
            return b;
        }

        private void Build()
        {
            _rt = (RectTransform)transform;
            _rt.anchorMin = _rt.anchorMax = Vector2.zero;
            _rt.pivot = new Vector2(0.5f, 0f);
            _group = GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _tailOutline = Child<Image>("Tail Outline", _rt);
            _tail = Child<Image>("Tail", _rt);
            _dotA = Child<Image>("Dot A", _rt);
            _dotB = Child<Image>("Dot B", _rt);
            _fill = Child<Image>("Fill", _rt);
            _outline = Child<Image>("Outline", _rt);
            _body = _fill.rectTransform;
            _text = Child<TextMeshProUGUI>("Text", _body);
            _textRt = _text.rectTransform;
            _textRt.anchorMin = _textRt.anchorMax = new Vector2(0.5f, 0.5f); // centred in the body
            _textRt.anchoredPosition = Vector2.zero;
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.alignment = TextAlignmentOptions.Center;
            _text.raycastTarget = false;
            _next = Child<Image>("Next", _body);
            _next.sprite = UiSprites.Tail();
            foreach (Image i in new[] { _fill, _outline, _tail, _tailOutline, _next, _dotA, _dotB }) i.raycastTarget = false;
            _dotA.sprite = _dotB.sprite = UiSprites.Circle();
            gameObject.SetActive(false);
        }

        private static T Child<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return go.AddComponent<T>();
        }

        // --- show -------------------------------------------------------------------------------------------------

        private void Begin(Transform speaker, string line, BubbleStyle kind, float voicePitch, AudioClip voice, float lifetime)
        {
            DialogueTheme theme = DialogueTheme.Current;
            _speaker = speaker;
            _kind = speaker == null ? BubbleStyle.Narrate : kind;
            _style = theme.For(_kind);
            _voicePitch = voicePitch;
            _voice = voice;
            _lifetime = lifetime;
            _phase = Phase.Showing;
            _age = 0f;
            _shownChars = 0f;
            _pauseLeft = 0f;
            _lastVoiced = 0;
            _hurried = false;
            _showNext = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            // Look.
            _fill.sprite = UiSprites.Rounded(_style.Radius);
            _fill.type = Image.Type.Sliced;
            _fill.color = _style.Fill;
            _outline.sprite = UiSprites.Rounded(_style.Radius, _style.OutlineWidth, _style.DashedOutline);
            _outline.type = Image.Type.Sliced;
            _outline.color = _style.Outline;
            _outline.enabled = _style.OutlineWidth > 0;
            _tail.sprite = UiSprites.Tail();
            _tailOutline.sprite = UiSprites.Tail();
            _tail.color = _style.Fill;
            _tailOutline.color = _style.Outline;
            bool pointer = _style.Tail == DialogueTheme.TailKind.Pointer;
            bool dots = _style.Tail == DialogueTheme.TailKind.Dots;
            _tail.enabled = _tailOutline.enabled = pointer;
            _dotA.enabled = _dotB.enabled = dots;
            _dotA.color = _dotB.color = _style.Fill;
            _next.color = _style.Outline;
            _next.enabled = false;
            _text.color = _style.Text;
            _text.fontSize = theme.FontSize * _style.TextScale;
            _text.fontStyle = (_style.Italic ? FontStyles.Italic : FontStyles.Normal) | (_style.Bold ? FontStyles.Bold : FontStyles.Normal);

            // Text (inline effects parsed out) and size: wrapped at MaxWidth, no wider than it needs.
            _fx.Set(_text, line);
            float maxText = theme.MaxWidth - theme.Padding.x * 2f;
            Vector2 pref = _text.GetPreferredValues(_fx.Clean, maxText, 0f);
            float width = Mathf.Min(maxText, Mathf.Ceil(pref.x) + 2f);
            float height = Mathf.Ceil(_text.GetPreferredValues(_fx.Clean, width, 0f).y);
            _textRt.sizeDelta = new Vector2(width, height);
            _size = new Vector2(width + theme.Padding.x * 2f, height + theme.Padding.y * 2f);
            _body.sizeDelta = _size;
            ((RectTransform)_outline.transform).sizeDelta = _size;
            _text.maxVisibleCharacters = 0;
            _fx.Prepare(_text);
            Layout();
        }

        // --- frame ------------------------------------------------------------------------------------------------

        private void LateUpdate()
        {
            if (_phase == Phase.Hidden) return;
            float dt = Time.unscaledDeltaTime;
            DialogueTheme theme = DialogueTheme.Current;
            bool calm = UiFeel.Calm;

            if (_phase == Phase.Showing)
            {
                _age += dt;
                Type(theme, dt);
                if (_lifetime > 0f && !Typing && _age > _lifetime + _fx.Count / Mathf.Max(1f, theme.CharsPerSecond)) Hide();
            }
            else
            {
                _hideAge += dt;
                if (_hideAge >= theme.PopOutSeconds)
                {
                    _phase = Phase.Hidden;
                    gameObject.SetActive(false);
                    Pool.Push(this);
                    return;
                }
            }

            Layout();

            // Pop in (scale with a squash and stretch, fade), pop out (shrink a little, fade). Calm: fades only.
            float alpha, sx, sy;
            if (_phase == Phase.Showing)
            {
                float t = Mathf.Clamp01(_age / Mathf.Max(0.01f, theme.PopInSeconds));
                alpha = Mathf.Clamp01(t * 2.5f);
                float grow = calm ? 1f : Mathf.LerpUnclamped(0.55f, 1f, Ease.Evaluate(theme.PopInEase, t));
                float squash = calm ? 0f : theme.Squash * Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
                sx = grow * (1f + squash);
                sy = grow * (1f - squash);
            }
            else
            {
                float t = Mathf.Clamp01(_hideAge / Mathf.Max(0.01f, theme.PopOutSeconds));
                alpha = 1f - Ease.OutCubic(t);
                float shrink = calm ? 1f : Mathf.Lerp(1f, 0.85f, Ease.OutCubic(t));
                sx = sy = shrink;
            }
            _group.alpha = alpha;
            _rt.localScale = new Vector3(sx, sy, 1f);

            // A shout trembles while it types; the "more" arrow bobs when the line is out.
            Vector2 jitter = Vector2.zero;
            if (_style.Shake > 0f && Typing && !calm)
            {
                float k = Time.unscaledTime * 47f;
                jitter = new Vector2(Mathf.Sin(k * 1.3f), Mathf.Sin(k * 1.7f + 1f)) * _style.Shake;
            }
            _body.anchoredPosition = new Vector2(0f, _size.y * 0.5f) + jitter;
            ((RectTransform)_outline.transform).anchoredPosition = _body.anchoredPosition;

            bool next = _showNext && !Typing && _phase == Phase.Showing;
            if (_next.enabled != next) _next.enabled = next;
            if (next)
            {
                float bob = calm ? 0f : Mathf.Abs(Mathf.Sin(Time.unscaledTime * theme.NextBobHz * Mathf.PI)) * 5f;
                var nrt = _next.rectTransform;
                nrt.sizeDelta = new Vector2(16f, 13f);
                nrt.anchorMin = nrt.anchorMax = new Vector2(1f, 0f);
                nrt.anchoredPosition = new Vector2(-18f, 14f + bob);
            }

            _fx.Animate(_text, calm);
        }

        /// <summary>Reveals letters at the typing speed, pausing on punctuation, with a voice blip every few letters.</summary>
        private void Type(DialogueTheme theme, float dt)
        {
            if (!Typing)
            {
                _text.maxVisibleCharacters = _fx.Count;
                return;
            }
            float speed = theme.CharsPerSecond * _style.SpeedScale * (_hurried ? theme.HurryMultiplier : 1f);
            if (_pauseLeft > 0f && !_hurried)
            {
                _pauseLeft -= dt;
                return;
            }
            int before = (int)_shownChars;
            _shownChars = Mathf.Min(_fx.Count, _shownChars + speed * dt);
            int after = (int)_shownChars;
            for (int i = before; i < after; i++)
            {
                char c = _fx.CharAt(_text, i);
                if (!_hurried)
                {
                    float pause = c == '.' || c == '!' || c == '?' ? theme.PauseSentence : c == ',' || c == ';' || c == ':' ? theme.PauseComma : c == '…' ? theme.PauseEllipsis : 0f;
                    if (pause > 0f && i + 1 < _fx.Count && !char.IsWhiteSpace(_fx.CharAt(_text, i + 1)) && c == '.') pause = 0f; // 3.5, a.m.
                    if (pause > 0f)
                    {
                        _pauseLeft = pause;
                        _shownChars = i + 1;
                        after = i + 1;
                    }
                }
                if (_voice != null && char.IsLetter(c) && i - _lastVoiced >= theme.VoiceEveryChars)
                {
                    _lastVoiced = i;
                    UiFeel.Play(_voice, theme.VoiceVolume, _voicePitch * (1f + Random.Range(-theme.VoicePitchJitter, theme.VoicePitchJitter)));
                }
                if (_pauseLeft > 0f) break;
            }
            _text.maxVisibleCharacters = after;
        }

        /// <summary>Places the bubble over the speaker's head, on screen, and points the tail (or dots) at it.</summary>
        private void Layout()
        {
            DialogueTheme theme = DialogueTheme.Current;
            Canvas canvas = Hud.Root.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            float w = Screen.width / scale, h = Screen.height / scale, m = theme.ScreenMargin;
            float tailH = theme.TailHeight;

            Vector2 anchor;
            bool hasAnchor = _speaker != null && _style.Tail != DialogueTheme.TailKind.None;
            Camera cam = Camera.main;
            if (hasAnchor && cam != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(_speaker.position + Vector3.up * theme.HeadHeight);
                if (sp.z < 0f) sp = new Vector3(Screen.width - sp.x, 0f, 0f); // behind the camera: towards the bottom edge
                anchor = new Vector2(sp.x / scale, sp.y / scale);
            }
            else
            {
                anchor = new Vector2(w * 0.5f, h - m - _size.y - 90f);
                hasAnchor = false;
            }

            // Above the head, kept inside the screen (pinned to the top edge if it doesn't fit). Only when the head itself
            // is off the top of the screen does it go below it, tail flipped, so it never covers the face otherwise.
            bool below = hasAnchor && anchor.y > h - m - tailH;
            float x = Mathf.Clamp(anchor.x, m + _size.x * 0.5f, w - m - _size.x * 0.5f);
            float bottom = !hasAnchor ? anchor.y : below ? anchor.y - tailH - _size.y : anchor.y + tailH;
            bottom = Mathf.Clamp(bottom, m, h - m - _size.y);
            _rt.anchoredPosition = new Vector2(x, bottom);
            _rt.sizeDelta = _size;

            if (!hasAnchor) return;
            // Tail: from the bubble's edge toward the anchor, its root kept within the straight part of that edge.
            float edgeY = below ? _size.y : 0f;
            float inset = _style.Radius + 14f;
            float rootX = Mathf.Clamp(anchor.x - x, -_size.x * 0.5f + inset, _size.x * 0.5f - inset);
            if (_style.Tail == DialogueTheme.TailKind.Pointer)
            {
                float len = Mathf.Clamp(below ? anchor.y - (bottom + _size.y) : bottom - anchor.y, tailH * 0.6f, tailH * 1.6f);
                PlaceTail(_tailOutline.rectTransform, rootX, edgeY, below, len + _style.OutlineWidth * 1.6f, 30f + _style.OutlineWidth * 2f);
                PlaceTail(_tail.rectTransform, rootX, edgeY + (below ? -_style.OutlineWidth : _style.OutlineWidth), below, len, 30f);
            }
            else
            {
                Vector2 from = new Vector2(rootX, below ? _size.y : 0f);
                Vector2 to = new Vector2(anchor.x - x, below ? anchor.y - bottom : anchor.y - bottom);
                PlaceDot(_dotA.rectTransform, Vector2.Lerp(from, to, 0.35f), 16f);
                PlaceDot(_dotB.rectTransform, Vector2.Lerp(from, to, 0.75f), 9f);
            }
        }

        /// <summary>The tail's wide end sits on the bubble's edge (pivot), its tip points away: down, or up when below.</summary>
        private static void PlaceTail(RectTransform t, float x, float y, bool below, float length, float width)
        {
            t.pivot = new Vector2(0.5f, 1f);
            t.sizeDelta = new Vector2(width, length);
            t.anchoredPosition = new Vector2(x, y + (below ? -2f : 2f));
            t.localRotation = below ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
        }

        private static void PlaceDot(RectTransform t, Vector2 at, float size)
        {
            t.sizeDelta = new Vector2(size, size);
            t.anchoredPosition = at;
        }
    }
}
