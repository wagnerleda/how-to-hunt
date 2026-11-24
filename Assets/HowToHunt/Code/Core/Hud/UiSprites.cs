using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Procedural UI sprites, made once and cached: rounded panels (9-sliced, optional outline and dashes), a bubble
    /// tail, a soft circle and a glow. Flat colours in the art direction's spirit; tinted per use by Image.color.
    /// The fill is white and the outline is stored in a second sprite, so fill and outline take separate colours.
    /// </summary>
    public static class UiSprites
    {
        private static readonly Dictionary<long, Sprite> Cache = new Dictionary<long, Sprite>();

        /// <summary>A rounded rectangle, 9-sliced. outline 0: the filled shape; outline > 0: only a ring that thick (dashed: a dotted ring).</summary>
        public static Sprite Rounded(int radius, int outline = 0, bool dashed = false)
        {
            long key = (long)(uint)radius | ((long)(uint)outline << 16) | ((dashed ? 1L : 0L) << 32);
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
            int size = radius * 2 + 4;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Distance to the rounded box (positive outside), anti-aliased edges.
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float d = new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude - (r - 1f);
                    float a = Mathf.Clamp01(0.5f - d);
                    if (outline > 0)
                    {
                        float inner = Mathf.Clamp01(0.5f - (d + outline));
                        a = Mathf.Clamp01(a - inner);
                        if (dashed && ((x / 5 + y / 5) & 1) == 0) a = 0f;
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            int border = radius + 1;
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            s.name = $"Rounded{radius}_{outline}{(dashed ? "d" : "")}";
            Cache[key] = s;
            return s;
        }

        /// <summary>A downward-pointing bubble tail (tip at the bottom centre). Rotate the Image to point elsewhere.</summary>
        public static Sprite Tail(int outline = 0)
        {
            long key = 1L << 40 | (long)outline;
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
            const int w = 48, h = 40;
            var tex = NewTexture(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Triangle: wide at the top (y = h), tip at the bottom centre; a slight curve to the side.
                    float t = (y + 0.5f) / h;
                    float half = t * (w * 0.5f - 1f);
                    float centre = w * 0.5f + (1f - t) * (1f - t) * 6f;
                    float d = Mathf.Abs(x + 0.5f - centre) - half;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (outline > 0) a = Mathf.Clamp01(a - Mathf.Clamp01(0.5f - (d + outline))) * (y < h - 2 ? 1f : 0f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
            s.name = $"Tail_{outline}";
            Cache[key] = s;
            return s;
        }

        /// <summary>A filled circle (thought bubbles, dots, the selector), anti-aliased.</summary>
        public static Sprite Circle(int radius = 32, float softness = 1f)
        {
            long key = 2L << 40 | (long)radius | ((long)(softness * 100f) << 20);
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
            int size = radius * 2;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - radius, y + 0.5f - radius).magnitude - (radius - 1f - softness);
                    float a = Mathf.Clamp01(0.5f - d / Mathf.Max(1f, softness));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            s.name = $"Circle{radius}";
            Cache[key] = s;
            return s;
        }

        /// <summary>A soft radial glow (hover halos, the menu's sun, pollen).</summary>
        public static Sprite Glow() => Circle(32, 24f);

        private static Texture2D NewTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
    }
}
