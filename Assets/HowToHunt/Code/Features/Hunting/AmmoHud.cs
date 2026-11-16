using HowToHunt.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Bottom-right ammo counter while holding a gun: rounds left / magazine size, a reload bar, and a hint when the
    /// magazine is empty. Text changes only when the numbers do.
    /// </summary>
    public sealed class AmmoHud : MonoBehaviour
    {
        private const float BarWidth = 150f, BarHeight = 5f;
        private static readonly Color Normal = new Color(1f, 0.96f, 0.86f, 0.95f);
        private static readonly Color Low = new Color(1f, 0.45f, 0.3f, 0.95f);

        private TextMeshProUGUI _count;
        private TextMeshProUGUI _hint;
        private Image _barBack, _barFill;
        private int _shownAmmo = -1, _shownMag = -1;
        private bool _shownReload, _shownEmpty;

        private void Awake()
        {
            _count = Hud.Text("Ammo", new Vector2(1f, 0f), new Vector2(-40f, 40f), 44f, TextAlignmentOptions.BottomRight, Normal);
            _count.rectTransform.pivot = new Vector2(1f, 0f);
            _count.rectTransform.sizeDelta = new Vector2(260f, 56f);
            _count.fontStyle = FontStyles.Bold;
            _hint = Hud.Text("Ammo Hint", new Vector2(1f, 0f), new Vector2(-40f, 98f), 20f, TextAlignmentOptions.BottomRight, Low);
            _hint.rectTransform.pivot = new Vector2(1f, 0f);
            _hint.rectTransform.sizeDelta = new Vector2(320f, 26f);
            _barBack = Hud.Rect("Reload Bar", new Vector2(1f, 0f), new Vector2(-40f - BarWidth * 0.5f, 32f), new Vector2(BarWidth, BarHeight), new Color(0f, 0f, 0f, 0.45f));
            _barFill = Hud.Rect("Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, BarHeight), Normal);
            _barFill.rectTransform.SetParent(_barBack.rectTransform, false);
            _barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _barBack.raycastTarget = _barFill.raycastTarget = false;
            Show(false);
        }

        private void Show(bool on)
        {
            if (_count.gameObject.activeSelf != on) _count.gameObject.SetActive(on);
            if (!on)
            {
                if (_hint.gameObject.activeSelf) _hint.gameObject.SetActive(false);
                if (_barBack.gameObject.activeSelf) _barBack.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            int mag = PlayerTools.LocalMagazine;
            if (mag <= 0 || PlayerTools.Local == null) { Show(false); _shownMag = -1; return; }
            Show(true);
            int ammo = PlayerTools.LocalAmmo;
            bool infinite = HuntingCvars.InfiniteMag.Value;
            if (ammo != _shownAmmo || mag != _shownMag)
            {
                _shownAmmo = ammo;
                _shownMag = mag;
                if (infinite) _count.SetText("INF");
                else _count.SetText("{0}<size=60%> / {1}</size>", (float)ammo, (float)mag);
                _count.color = !infinite && ammo <= Mathf.Max(1, mag / 4) ? Low : Normal;
            }
            float reload = PlayerTools.LocalReload;
            bool reloading = reload >= 0f;
            if (_barBack.gameObject.activeSelf != reloading) _barBack.gameObject.SetActive(reloading);
            if (reloading) _barFill.rectTransform.sizeDelta = new Vector2(BarWidth * reload, BarHeight);
            bool empty = !infinite && ammo == 0 && !reloading;
            if (_hint.gameObject.activeSelf != (empty || reloading)) _hint.gameObject.SetActive(empty || reloading);
            if (reloading != _shownReload || empty != _shownEmpty)
            {
                _shownReload = reloading;
                _shownEmpty = empty;
                _hint.SetText(reloading ? "Reloading" : $"Empty · {KeyBindings.DisplayName(GameAction.Reload)} to reload");
            }
        }
    }
}
