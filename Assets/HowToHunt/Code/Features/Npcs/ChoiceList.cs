using HowToHunt.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HowToHunt.Npcs
{
    /// <summary>
    /// The player's replies in a conversation: rounded "pills" stacked above the hotbar, sliding in one after the
    /// other. Up/down (keyboard, D-pad, stick) or the mouse select, confirm or a click picks, 1-4 pick directly.
    /// The selected reply is highlighted, a little bigger, with a bobbing arrow. Built once, reused; no allocations
    /// per frame. Colours and stagger come from DialogueTheme.
    /// </summary>
    public sealed class ChoiceList : MonoBehaviour
    {
        public const int MaxChoices = 5;
        private const float RowHeight = 44f, Spacing = 8f, MinWidth = 380f, BottomY = 150f, SlideIn = 0.26f, Padding = 46f;

        private sealed class Row
        {
            public RectTransform Root;
            public Image Fill, Outline;
            public TextMeshProUGUI Label;
            public CanvasGroup Group;
            public bool Enabled;
            public float Highlight;
        }

        private readonly Row[] _rows = new Row[MaxChoices];
        private RectTransform _root;
        private Image _arrow;
        private TextMeshProUGUI _hint;
        private int _count, _selected;
        private float _age;
        private bool _open;
        private System.Action<int> _pick;
        private bool _hintPad;
        private const float InputDelay = 0.15f; // the press that finished the line must not also pick a reply

        public bool Open => _open;

        private void Awake()
        {
            var go = new GameObject("Dialogue Choices", typeof(RectTransform));
            go.transform.SetParent(Hud.Root, false);
            _root = (RectTransform)go.transform;
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            for (int i = 0; i < MaxChoices; i++) _rows[i] = MakeRow(i);
            _arrow = NewChild<Image>("Selector", _root);
            _arrow.sprite = UiSprites.Tail();
            _arrow.raycastTarget = false;
            _arrow.rectTransform.sizeDelta = new Vector2(18f, 14f);
            _arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f); // points right, at the reply
            _hint = NewChild<TextMeshProUGUI>("Hint", _root);
            _hint.fontSize = 17f;
            _hint.alignment = TextAlignmentOptions.Center;
            _hint.color = new Color(1f, 0.96f, 0.88f, 0.8f);
            _hint.raycastTarget = false;
            _hint.rectTransform.sizeDelta = new Vector2(700f, 26f);
            _hint.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            _hint.outlineWidth = 0.2f;
            _hint.outlineColor = new Color32(20, 18, 16, 200);
            go.SetActive(false);
        }

        private Row MakeRow(int i)
        {
            var r = new Row();
            var go = new GameObject($"Choice {i + 1}", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_root, false);
            r.Root = (RectTransform)go.transform;
            r.Root.anchorMin = r.Root.anchorMax = new Vector2(0.5f, 0f);
            r.Root.pivot = new Vector2(0.5f, 0.5f);
            r.Group = go.GetComponent<CanvasGroup>();
            r.Fill = NewChild<Image>("Fill", r.Root, stretch: true);
            r.Fill.sprite = UiSprites.Rounded(18);
            r.Fill.type = Image.Type.Sliced;
            r.Outline = NewChild<Image>("Outline", r.Root, stretch: true);
            r.Outline.sprite = UiSprites.Rounded(18, 2);
            r.Outline.type = Image.Type.Sliced;
            r.Label = NewChild<TextMeshProUGUI>("Label", r.Root, stretch: true);
            r.Label.fontSize = 21f;
            r.Label.alignment = TextAlignmentOptions.MidlineLeft;
            r.Label.margin = new Vector4(22f, 0f, 16f, 0f);
            r.Label.textWrappingMode = TextWrappingModes.NoWrap;
            r.Label.overflowMode = TextOverflowModes.Ellipsis;
            foreach (Graphic g in new Graphic[] { r.Fill, r.Outline, r.Label }) g.raycastTarget = false;
            return r;
        }

        private static T NewChild<T>(string name, Transform parent, bool stretch = false) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            else rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            return go.AddComponent<T>();
        }

        /// <summary>Shows the replies; <paramref name="pick"/> gets the index chosen. Disabled replies can't be picked.</summary>
        public void Show(string[] labels, bool[] enabled, int count, System.Action<int> pick)
        {
            DialogueTheme theme = DialogueTheme.Current;
            _count = Mathf.Min(count, MaxChoices);
            _pick = pick;
            _open = true;
            _age = 0f;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            float width = MinWidth;
            for (int i = 0; i < MaxChoices; i++)
            {
                Row r = _rows[i];
                bool used = i < _count;
                r.Root.gameObject.SetActive(used);
                if (!used) continue;
                r.Enabled = enabled[i];
                r.Label.SetText($"{i + 1}.  {labels[i]}");
                r.Label.color = r.Enabled ? theme.ChoiceText : new Color(theme.ChoiceText.r, theme.ChoiceText.g, theme.ChoiceText.b, 0.5f);
                r.Highlight = 0f;
                width = Mathf.Max(width, r.Label.GetPreferredValues(r.Label.text).x + Padding);
            }
            width = Mathf.Min(width, 820f);
            for (int i = 0; i < _count; i++)
            {
                Row r = _rows[i];
                r.Root.sizeDelta = new Vector2(width, RowHeight);
            }
            _root.anchoredPosition = new Vector2(0f, BottomY);
            _selected = FirstEnabled(0, 1);
            UiFeel.Open();
        }

        public void Hide()
        {
            _open = false;
            _pick = null;
            _root.gameObject.SetActive(false);
        }

        private int FirstEnabled(int from, int step)
        {
            for (int k = 0; k < _count; k++)
            {
                int i = ((from + k * step) % _count + _count) % _count;
                if (_rows[i].Enabled) return i;
            }
            return 0;
        }

        private void Update()
        {
            if (!_open) return;
            DialogueTheme theme = DialogueTheme.Current;
            float dt = Time.unscaledDeltaTime;
            _age += dt;
            bool calm = UiFeel.Calm;

            // Input: navigation, mouse hover/click, number keys, confirm.
            int before = _selected;
            Vector2Int move = UiInput.Move;
            if (move.y != 0) _selected = FirstEnabled(_selected - move.y, -move.y);
            Mouse mouse = Mouse.current;
            int hovered = -1;
            if (mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                for (int i = 0; i < _count; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(_rows[i].Root, mp, null)) hovered = i;
                if (hovered >= 0 && _rows[hovered].Enabled && mouse.delta.ReadValue().sqrMagnitude > 0.01f) _selected = hovered;
            }
            if (_selected != before) UiFeel.Hover();
            int pick = -1;
            Keyboard kb = Keyboard.current;
            if (kb != null)
                for (int i = 0; i < _count && i < 4; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame) pick = i;
            if (UiInput.Confirm) pick = _selected;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && hovered >= 0) pick = hovered;
            if (_age < InputDelay) pick = -1;

            // Look: each row slides in after the one before it; the selected one is lit and a little bigger.
            for (int i = 0; i < _count; i++)
            {
                Row r = _rows[i];
                float t = Mathf.Clamp01((_age - i * theme.ChoiceStagger) / SlideIn);
                r.Group.alpha = Mathf.Clamp01(t * 2f);
                float rise = calm ? 0f : (1f - Ease.OutBack(t)) * -22f;
                int fromBottom = _count - 1 - i;
                r.Root.anchoredPosition = new Vector2(0f, fromBottom * (RowHeight + Spacing) + RowHeight * 0.5f + rise);
                r.Highlight = Ease.Damp(r.Highlight, i == _selected ? 1f : 0f, 16f, dt);
                float scale = calm ? 1f : 1f + 0.05f * r.Highlight;
                r.Root.localScale = new Vector3(scale, scale, 1f);
                r.Fill.color = !r.Enabled ? theme.ChoiceDisabled : Color.Lerp(theme.ChoiceFill, theme.ChoiceSelected, r.Highlight);
                r.Outline.color = Color.Lerp(new Color(0.25f, 0.2f, 0.16f, 0.5f), new Color(0.45f, 0.25f, 0.08f, 1f), r.Highlight);
            }
            if (_count > 0)
            {
                RectTransform sel = _rows[_selected].Root;
                float bob = calm ? 0f : Mathf.Sin(Time.unscaledTime * 6f) * 4f;
                _arrow.rectTransform.anchoredPosition = sel.anchoredPosition + new Vector2(-sel.sizeDelta.x * 0.5f - 16f + bob, 0f);
                _arrow.color = theme.ChoiceSelected;
            }
            bool pad = UiInput.HasGamepad;
            if (pad != _hintPad || _age <= dt)
            {
                _hintPad = pad;
                _hint.SetText(pad ? "Stick / D-pad choose   A confirm   B leave" : "Up/Down or mouse choose   E / Enter confirm   Esc leave");
            }

            if (pick >= 0 && pick < _count)
            {
                if (!_rows[pick].Enabled) { UiFeel.Error(); return; }
                UiFeel.Confirm();
                System.Action<int> callback = _pick;
                Hide();
                callback?.Invoke(pick);
            }
        }
    }
}
