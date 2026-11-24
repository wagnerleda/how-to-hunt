using HowToHunt.Core;
using HowToHunt.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Player
{
    /// <summary>
    /// Health bar (bottom left), and while downed a banner with the bleed-out countdown; a revive bar for both the
    /// downed player and the one reviving. Only changes the text when something changed (no allocations per frame).
    /// </summary>
    public sealed class HealthHud : MonoBehaviour
    {
        private const float BarWidth = 220f, BarHeight = 10f, ReviveWidth = 260f;
        private static readonly Color Good = new Color(0.55f, 0.9f, 0.45f, 0.95f), Hurt = new Color(1f, 0.45f, 0.3f, 0.95f);
        private static readonly Color Text = new Color(1f, 0.96f, 0.86f, 0.95f);
        private static readonly Color Venom = new Color(0.55f, 0.95f, 0.2f, 0.95f);

        private Image _back, _fill, _reviveBack, _reviveFill, _venomTint;
        private TextMeshProUGUI _number, _banner, _venom;
        private int _shownVenom = -1;
        private int _shownHealth = -1, _shownSeconds = -1;
        private string _shownBanner;

        private void Awake()
        {
            _back = Hud.Rect("Health Bar", new Vector2(0f, 0f), new Vector2(40f + BarWidth * 0.5f, 36f), new Vector2(BarWidth, BarHeight), new Color(0f, 0f, 0f, 0.45f));
            _fill = Hud.Rect("Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(BarWidth, BarHeight), Good);
            _fill.rectTransform.SetParent(_back.rectTransform, false);
            _fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _number = Hud.Text("Health", new Vector2(0f, 0f), new Vector2(40f, 50f), 22f, TextAlignmentOptions.BottomLeft, Text);
            _number.rectTransform.pivot = new Vector2(0f, 0f);
            _number.rectTransform.sizeDelta = new Vector2(200f, 30f);
            _banner = Hud.Text("Downed", new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), 26f, TextAlignmentOptions.Center, Hurt);
            _banner.rectTransform.sizeDelta = new Vector2(900f, 80f);
            _reviveBack = Hud.Rect("Revive Bar", new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(ReviveWidth, 8f), new Color(0f, 0f, 0f, 0.5f));
            _reviveFill = Hud.Rect("Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 8f), Good);
            _reviveFill.rectTransform.SetParent(_reviveBack.rectTransform, false);
            _reviveFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            // Poisoned: the bar turns venom green, a label counts down and the screen edges pulse green.
            _venom = Hud.Text("Poisoned", new Vector2(0f, 0f), new Vector2(40f + BarWidth + 16f, 26f), 22f, TextAlignmentOptions.BottomLeft, Venom);
            _venom.rectTransform.pivot = new Vector2(0f, 0f);
            _venom.rectTransform.sizeDelta = new Vector2(320f, 30f);
            _venomTint = Hud.Rect("Venom Tint", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.35f, 0.9f, 0.1f, 0f));
            _venomTint.rectTransform.anchorMin = Vector2.zero;
            _venomTint.rectTransform.anchorMax = Vector2.one;
            _venomTint.rectTransform.offsetMin = _venomTint.rectTransform.offsetMax = Vector2.zero;
            _venomTint.rectTransform.SetAsFirstSibling();
            _venom.gameObject.SetActive(false);
            _venomTint.gameObject.SetActive(false);
            foreach (Graphic g in new Graphic[] { _back, _fill, _reviveBack, _reviveFill, _number, _banner, _venom, _venomTint }) g.raycastTarget = false;
            Show(false);
        }

        /// <summary>The venom label and the green pulse at the screen's edges while poisoned (no pulse with reduce motion).</summary>
        private void ShowVenom(float left)
        {
            bool on = left > 0f;
            if (_venom.gameObject.activeSelf != on) _venom.gameObject.SetActive(on);
            if (_venomTint.gameObject.activeSelf != on) _venomTint.gameObject.SetActive(on);
            if (!on) { _shownVenom = -1; return; }
            int seconds = Mathf.CeilToInt(left);
            if (seconds != _shownVenom)
            {
                _shownVenom = seconds;
                _venom.SetText("POISONED {0} s", seconds);
            }
            float pulse = UiFeel.Calm ? 0.07f : 0.05f + 0.06f * (0.5f + 0.5f * Mathf.Sin(Time.time * 4f));
            _venomTint.color = new Color(0.35f, 0.9f, 0.1f, pulse);
        }

        private void Show(bool on)
        {
            if (_back.gameObject.activeSelf != on) _back.gameObject.SetActive(on);
            if (_number.gameObject.activeSelf != on) _number.gameObject.SetActive(on);
            if (!on)
            {
                ShowVenom(0f);
                SetBanner(null);
                if (_reviveBack.gameObject.activeSelf) _reviveBack.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            PlayerHealth me = PlayerHealth.Local;
            if (me == null) { Show(false); return; }
            Show(true);
            int hp = Mathf.CeilToInt(me.Health);
            if (hp != _shownHealth)
            {
                _shownHealth = hp;
                _number.SetText("{0}", hp);
                float k = me.Health / PlayerHealth.MaxHealth;
                _fill.rectTransform.sizeDelta = new Vector2(BarWidth * k, BarHeight);
                _shownVenom = -1; // the colour below decides
            }
            ShowVenom(me.PoisonLeft);
            float health = me.Health / PlayerHealth.MaxHealth;
            _fill.color = me.Poisoned ? Venom : health > 0.35f ? Good : Hurt;

            float revive = 0f;
            if (me.Downed)
            {
                int seconds = Mathf.CeilToInt(me.BleedLeft);
                revive = me.ReviveProgress;
                if (seconds != _shownSeconds || _shownBanner == null)
                {
                    _shownSeconds = seconds;
                    SetBanner(revive > 0f ? "DOWNED: being revived..."
                        : me.AnyoneStanding() ? $"DOWNED: {seconds} s. A teammate can revive you (hold {KeyBindings.DisplayName(GameAction.Interact)} next to you)."
                        : $"DOWNED: back to camp in {seconds} s.");
                }
            }
            else if (PlayerHealth.LocalReviving != null)
            {
                PlayerHealth other = PlayerHealth.LocalReviving;
                revive = other.ReviveProgress;
                string name = PartyRoster.TryGet(other.OwnerId, out PartyMember m) ? m.Name : "your teammate";
                if (_shownBanner == null || _shownSeconds != -2) { _shownSeconds = -2; SetBanner($"Reviving {name}..."); }
            }
            else
            {
                SetBanner(null);
                _shownSeconds = -1;
            }
            bool reviving = revive > 0f;
            if (_reviveBack.gameObject.activeSelf != reviving) _reviveBack.gameObject.SetActive(reviving);
            if (reviving) _reviveFill.rectTransform.sizeDelta = new Vector2(ReviveWidth * revive, 8f);
        }

        private void SetBanner(string text)
        {
            if (text == _shownBanner) return;
            _shownBanner = text;
            bool on = text != null;
            if (_banner.gameObject.activeSelf != on) _banner.gameObject.SetActive(on);
            if (on) _banner.SetText(text);
        }
    }
}
