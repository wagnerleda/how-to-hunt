using HowToHunt.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Player
{
    /// <summary>
    /// Sprint stamina: a thin bar above the inventory that appears while stamina is not full, turns warm when low,
    /// and fades out once it's full again.
    /// </summary>
    public sealed class StaminaHud : MonoBehaviour
    {
        private const float Width = 220f, Height = 5f, Y = 88f;
        private static readonly Color Full = new Color(0.95f, 0.93f, 0.82f, 0.9f);
        private static readonly Color LowColor = new Color(1f, 0.5f, 0.3f, 0.95f);

        private Image _back, _fill;
        private float _alpha;

        private void Awake()
        {
            _back = Hud.Rect("Stamina", new Vector2(0.5f, 0f), new Vector2(0f, Y), new Vector2(Width + 4f, Height + 4f), new Color(0f, 0f, 0f, 0.4f));
            _fill = Hud.Rect("Fill", new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(Width, Height), Full);
            _fill.rectTransform.SetParent(_back.rectTransform, false);
            _fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _back.raycastTarget = _fill.raycastTarget = false;
            _back.gameObject.SetActive(false);
        }

        private void Update()
        {
            PlayerMotor m = PlayerMotor.Local;
            float max = MovementCvars.Config.SprintStamina;
            float left = m != null && max > 0f ? Mathf.Clamp01(1f - m.State.Fatigue / max) : 1f;
            _alpha = Mathf.MoveTowards(_alpha, left < 0.999f ? 1f : 0f, Time.deltaTime * (left < 0.999f ? 6f : 1.5f));
            bool show = _alpha > 0.01f;
            if (_back.gameObject.activeSelf != show) _back.gameObject.SetActive(show);
            if (!show) return;
            _fill.rectTransform.sizeDelta = new Vector2(Width * left, Height);
            Color c = left < 0.25f ? LowColor : Full;
            c.a *= _alpha;
            _fill.color = c;
            Color b = _back.color;
            b.a = 0.4f * _alpha;
            _back.color = b;
        }
    }
}
