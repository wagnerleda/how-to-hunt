using HowToHunt.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HowToHunt.UI
{
    /// <summary>
    /// The living layer over the menus' background (the main menu's 3D sunset is MenuVista). Main menu: a warm sky tint drifting
    /// between two colours, a soft sun that breathes, pollen drifting up in three depths that follow the mouse a
    /// little (parallax), and a vignette. Pause menu: the game behind dimmed, with the vignette. "Reduce animations":
    /// still tint, no pollen. Built once; no allocations per frame.
    /// </summary>
    public sealed class MenuBackdrop
    {
        public enum Mode : byte { Off, Main, Pause }

        private readonly RectTransform _root;
        private readonly RawImage _sky, _vignette;
        private readonly Image _dim, _sun;
        private readonly Image[] _pollen;
        private readonly Vector3[] _seed; // x, y in 0..1, depth 0.3..1
        private Vector2 _parallax;
        private float _alpha;

        public MenuBackdrop(RectTransform parent, MenuTheme theme)
        {
            var go = new GameObject("Backdrop", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _root = Stretch((RectTransform)go.transform);
            _dim = Layer<Image>("Dim");
            _dim.color = Color.black;
            _sky = Layer<RawImage>("Sky");
            _sky.texture = Gradient();
            _sun = Layer<Image>("Sun", stretch: false);
            _sun.sprite = UiSprites.Glow();
            _sun.rectTransform.anchorMin = _sun.rectTransform.anchorMax = new Vector2(0.82f, 0.82f);
            _sun.rectTransform.sizeDelta = new Vector2(520f, 520f);
            _pollen = new Image[Mathf.Max(0, theme.Pollen)];
            _seed = new Vector3[_pollen.Length];
            var rng = new System.Random(5);
            for (int i = 0; i < _pollen.Length; i++)
            {
                Image p = Layer<Image>("Pollen", stretch: false);
                p.sprite = UiSprites.Glow();
                _seed[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), 0.3f + 0.7f * (float)rng.NextDouble());
                float size = Mathf.Lerp(6f, 18f, _seed[i].z);
                p.rectTransform.anchorMin = p.rectTransform.anchorMax = Vector2.zero;
                p.rectTransform.sizeDelta = new Vector2(size, size);
                _pollen[i] = p;
            }
            _vignette = Layer<RawImage>("Vignette");
            _vignette.texture = Vignette();
            go.SetActive(false);
        }

        private T Layer<T>(string name, bool stretch = true) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            if (stretch) Stretch((RectTransform)go.transform);
            T g = go.AddComponent<T>();
            g.raycastTarget = false;
            return g;
        }

        private static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public void Update(Mode mode, MenuTheme theme, float dt, Vector2 canvasSize)
        {
            _alpha = Ease.Damp(_alpha, mode == Mode.Off ? 0f : 1f, 8f, dt);
            bool on = _alpha > 0.01f;
            if (_root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
            if (!on) return;
            bool calm = UiFeel.Calm;
            bool main = mode == Mode.Main;
            float time = Time.unscaledTime;

            _dim.color = new Color(0f, 0f, 0f, (main ? 0.12f : theme.PauseDim) * _alpha);
            float drift = calm ? 0f : 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / Mathf.Max(1f, theme.SkyCycle));
            Color sky = Color.Lerp(theme.SkyTop, theme.SkyTopAlt, drift);
            _sky.color = new Color(sky.r, sky.g, sky.b, sky.a * _alpha * (main ? 1f : 0.35f));
            _vignette.color = new Color(1f, 1f, 1f, theme.Vignette * _alpha);

            // Parallax: layers lean away from the mouse, the nearer the more.
            Mouse mouse = Mouse.current;
            Vector2 target = Vector2.zero;
            if (mouse != null && !calm)
            {
                Vector2 m = mouse.position.ReadValue();
                target = new Vector2(m.x / Mathf.Max(1f, Screen.width) - 0.5f, m.y / Mathf.Max(1f, Screen.height) - 0.5f) * -2f;
            }
            _parallax = Vector2.Lerp(_parallax, target, 1f - Mathf.Exp(-3f * dt));

            float sunPulse = calm ? 1f : 1f + 0.04f * Mathf.Sin(time * 0.9f);
            _sun.enabled = false; // the main menu shows the real sunset now (MenuVista); the drawn sun is kept for later
            _sun.color = new Color(1f, 0.86f, 0.55f, 0.55f * _alpha);
            _sun.rectTransform.localScale = new Vector3(sunPulse, sunPulse, 1f);
            _sun.rectTransform.anchoredPosition = _parallax * theme.Parallax * 0.3f;

            bool pollen = main && !calm;
            for (int i = 0; i < _pollen.Length; i++)
            {
                Image p = _pollen[i];
                if (p.enabled != pollen) p.enabled = pollen;
                if (!pollen) continue;
                Vector3 s = _seed[i];
                float speed = Mathf.Lerp(8f, 26f, s.z);
                float y = Mathf.Repeat(s.y * canvasSize.y + time * speed, canvasSize.y + 40f) - 20f;
                float x = s.x * canvasSize.x + Mathf.Sin(time * (0.4f + s.z) + i) * 18f * s.z;
                p.rectTransform.anchoredPosition = new Vector2(x, y) + _parallax * theme.Parallax * s.z;
                p.color = new Color(1f, 0.95f, 0.75f, (0.25f + 0.45f * s.z) * _alpha);
            }
        }

        private static Texture2D Gradient()
        {
            var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 64; y++)
            {
                float k = y / 63f;
                t.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, k))));
            }
            t.Apply(false, true);
            return t;
        }

        private static Texture2D Vignette()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f).magnitude / 0.7071f;
                    t.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d))));
                }
            t.Apply(false, true);
            return t;
        }
    }
}
