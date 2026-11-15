using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Core
{
    /// <summary>
    /// The shared screen-space HUD canvas (uGUI + TextMeshPro). Text is updated with TMP SetText overloads that
    /// format numbers without allocating, so the HUD costs zero garbage per frame. Layout is for 1920x1080 and scales.
    /// </summary>
    public static class Hud
    {
        private static Canvas _canvas;

        public static RectTransform Root
        {
            get
            {
                if (_canvas == null) Create();
                return (RectTransform)_canvas.transform;
            }
        }

        private static void Create()
        {
            var go = new GameObject("[HUD]", typeof(Canvas), typeof(CanvasScaler));
            Object.DontDestroyOnLoad(go);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            _canvas.pixelPerfect = false;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
        }

        /// <summary>Creates a text element anchored at <paramref name="anchor"/> (0..1) with a pixel offset.</summary>
        public static TextMeshProUGUI Text(string name, Vector2 anchor, Vector2 offset, float size, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(Root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(900, size * 2.4f);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(20, 18, 16, 200);
            return t;
        }

        /// <summary>Plain colored rectangle (crosshair lines, bars).</summary>
        public static Image Rect(string name, Vector2 anchor, Vector2 offset, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(Root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
