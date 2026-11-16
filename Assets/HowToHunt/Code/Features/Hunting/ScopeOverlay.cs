using HowToHunt.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Looking through a scope: once a scoped gun is fully raised to the eye, the model hides and the screen goes black
    /// around a round sight picture with a thin reticle. The texture is drawn once at startup (no asset).
    /// </summary>
    public sealed class ScopeOverlay : MonoBehaviour
    {
        private const int TextureSize = 512;
        private const float ShowAbove = 0.9f; // aim blend at which the view switches to the scope

        /// <summary>True while the scope picture is shown (the viewmodel hides).</summary>
        public static bool Showing { get; private set; }

        private RawImage _ring;
        private Image _left, _right;

        private void Awake()
        {
            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { name = "Scope", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[TextureSize * TextureSize];
            float c = (TextureSize - 1) * 0.5f, r = TextureSize * 0.47f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // Black outside the lens with a soft edge; a thin cross inside (thicker toward the rim), centre open.
                    float outside = Mathf.Clamp01((d - r) / 6f);
                    bool cross = (Mathf.Abs(dx) < (d > r * 0.55f ? 3f : 0.8f) || Mathf.Abs(dy) < (d > r * 0.55f ? 3f : 0.8f)) && d > 9f;
                    float a = Mathf.Max(outside, cross ? 0.9f : 0f);
                    pixels[y * TextureSize + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            _ring = new GameObject("Scope Ring", typeof(RectTransform)).AddComponent<RawImage>();
            _ring.texture = tex;
            _ring.raycastTarget = false;
            var rt = _ring.rectTransform;
            rt.SetParent(Hud.Root, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.SetAsFirstSibling();
            _left = Bar("Scope Left", 0f);
            _right = Bar("Scope Right", 1f);
            Show(false);
        }

        private Image Bar(string name, float side)
        {
            Image i = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            i.color = Color.black;
            i.raycastTarget = false;
            var rt = i.rectTransform;
            rt.SetParent(Hud.Root, false);
            rt.anchorMin = new Vector2(side, 0f);
            rt.anchorMax = new Vector2(side, 1f);
            rt.pivot = new Vector2(side, 0.5f);
            rt.SetAsFirstSibling();
            return i;
        }

        private void Show(bool on)
        {
            Showing = on;
            if (_ring.gameObject.activeSelf != on)
            {
                _ring.gameObject.SetActive(on);
                _left.gameObject.SetActive(on);
                _right.gameObject.SetActive(on);
            }
        }

        private void LateUpdate()
        {
            ToolDefinition tool = PlayerTools.Local != null ? PlayerTools.Local.Equipped : null;
            bool on = tool != null && tool.Archetype == Sim.Hunting.ToolArchetype.Firearm && Loadouts.Local.Setup(tool).ScopeOverlay && ViewmodelController.AdsBlend >= ShowAbove;
            Show(on);
            if (!on) return;
            // Square lens the height of the screen; black bars fill the sides.
            var root = (RectTransform)Hud.Root;
            float h = root.rect.height, w = root.rect.width;
            _ring.rectTransform.sizeDelta = new Vector2(h, 0f);
            float bar = Mathf.Max(0f, (w - h) * 0.5f) + 2f;
            _left.rectTransform.sizeDelta = new Vector2(bar, 0f);
            _right.rectTransform.sizeDelta = new Vector2(bar, 0f);
        }
    }
}
