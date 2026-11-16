using System;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Placeholder sound effects synthesized at startup (filtered noise, sines, envelopes). No files, no licenses.
    /// Replaced by recorded/CC0 sounds in the M3 audio pass; the event hooks stay the same.
    /// </summary>
    public static class ProceduralSounds
    {
        private const int Rate = 44100;

        /// <summary>Soft footstep on grass/dirt: two muffled noise taps (heel, toe), no tonal content.</summary>
        public static AudioClip Footstep(int seed)
        {
            var rng = new System.Random(seed);
            float toe = 0.035f + (float)rng.NextDouble() * 0.02f;
            float lp = 0f, lp2 = 0f;
            return Make($"step{seed}", 0.16f, (t, i) =>
            {
                float heel = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 45f);
                float toeEnv = t < toe ? 0f : Mathf.Min(1f, (t - toe) * 300f) * Mathf.Exp(-(t - toe) * 38f) * 0.7f;
                lp += (Noise(rng) - lp) * 0.09f;
                lp2 += (lp - lp2) * 0.12f;
                return lp2 * (heel + toeEnv);
            });
        }

        /// <summary>Barely-there cloth rustle for jumps.</summary>
        public static AudioClip Jump()
        {
            var rng = new System.Random(11);
            float lp = 0f, lp2 = 0f;
            return Make("jump", 0.18f, (t, i) =>
            {
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.18f));
                lp += (Noise(rng) - lp) * 0.06f;
                lp2 += (lp - lp2) * 0.2f;
                return lp2 * env * env;
            });
        }

        /// <summary>Muffled body-weight thud: low filtered noise, no pitch sweep.</summary>
        public static AudioClip Land()
        {
            var rng = new System.Random(23);
            float lp = 0f, lp2 = 0f;
            return Make("land", 0.25f, (t, i) =>
            {
                float env = Mathf.Min(1f, t * 500f) * Mathf.Exp(-t * 20f);
                lp += (Noise(rng) - lp) * 0.035f;
                lp2 += (lp - lp2) * 0.06f;
                return lp2 * env;
            });
        }

        /// <summary>Seamless 3 s wind loop.</summary>
        public static AudioClip Wind()
        {
            var rng = new System.Random(5);
            const float length = 3f;
            int n = (int)(Rate * length);
            var data = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float gust = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / length * 2f) * Mathf.Sin(2f * Mathf.PI * t / length * 3f + 1f);
                lp += (Noise(rng) - lp) * 0.04f;
                lp2 += (lp - lp2) * 0.08f;
                data[i] = lp2 * 6f * gust;
            }
            // Crossfade the tail into the head so the loop has no click.
            int fade = Rate / 4;
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                data[i] = data[i] * a + data[n - fade + i] * (1f - a);
            }
            Array.Resize(ref data, n - fade);
            Normalize(data, 0.7f);
            AudioClip clip = AudioClip.Create("wind", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Placeholder pistol: a short muffled pop with a soft tail (real recordings come in M3).</summary>
        public static AudioClip Shot()
        {
            var rng = new System.Random(41);
            float lp = 0f, lp2 = 0f;
            return Make("shot", 0.3f, (t, i) =>
            {
                float env = Mathf.Min(1f, t * 3000f) * (Mathf.Exp(-t * 40f) + 0.15f * Mathf.Exp(-t * 9f));
                lp += (Noise(rng) - lp) * 0.3f;
                lp2 += (lp - lp2) * 0.25f;
                return lp2 * env;
            });
        }

        /// <summary>Soft confirmation tap for a hit (filtered noise, no beep).</summary>
        public static AudioClip HitTick()
        {
            var rng = new System.Random(43);
            float lp = 0f;
            return Make("hittick", 0.05f, (t, i) =>
            {
                lp += (Noise(rng) - lp) * 0.45f;
                return lp * Mathf.Exp(-t * 90f);
            });
        }

        /// <summary>Low thud for a takedown.</summary>
        public static AudioClip KillThunk()
        {
            var rng = new System.Random(47);
            float lp = 0f, lp2 = 0f;
            return Make("kill", 0.3f, (t, i) =>
            {
                lp += (Noise(rng) - lp) * 0.05f;
                lp2 += (lp - lp2) * 0.08f;
                return lp2 * Mathf.Min(1f, t * 800f) * Mathf.Exp(-t * 12f);
            });
        }

        public static AudioClip Chime()
        {
            return Make("chime", 0.42f, (t, i) =>
            {
                float f = t < 0.12f ? 659.25f : 987.77f;
                float local = t < 0.12f ? t : t - 0.12f;
                float env = Mathf.Min(1f, local * 200f) * Mathf.Exp(-local * 9f);
                return (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * f * t)) * env * 0.5f;
            });
        }

        public static AudioClip Whoosh()
        {
            var rng = new System.Random(31);
            float lp = 0f;
            return Make("whoosh", 0.45f, (t, i) =>
            {
                float cutoff = Mathf.Lerp(0.35f, 0.02f, t / 0.45f);
                lp += (Noise(rng) - lp) * cutoff;
                return lp * Mathf.Sin(Mathf.PI * t / 0.45f) * 2.2f;
            });
        }

        /// <summary>
        /// Blade swing: a bright, airy "fwish" (band-passed noise sweeping up through 3-7 kHz, then down) with a faint
        /// metallic ring underneath, short and high like classic FPS knife swings.
        /// </summary>
        public static AudioClip KnifeSwish()
        {
            // White noise through a resonant band-pass (RBJ biquad) whose centre sweeps up then down, with a bell
            // envelope like the blade's speed: the classic "fwhoosh" of a fast swing. A second, higher band adds the
            // airy "sh". Quarter of a second, peaks two thirds of the way through.
            var rng = new System.Random(57);
            var low = new BandPass();
            var high = new BandPass();
            const float len = 0.26f;
            return Make("knife_swish", len, (t, i) =>
            {
                float u = t / len;
                float speed = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.75f)), 2f); // fast rise, softer tail
                float centre = Mathf.Lerp(900f, 4200f, speed);
                float n = Noise(rng);
                float body = low.Process(n, centre, 2.2f);
                float air = high.Process(n, centre * 2.1f, 1.4f);
                return (body * 1f + air * 0.45f) * speed;
            });
        }

        /// <summary>RBJ cookbook band-pass (constant peak gain), coefficients recomputed per sample for sweeps.</summary>
        private sealed class BandPass
        {
            private float _x1, _x2, _y1, _y2;

            public float Process(float x, float centreHz, float q)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(centreHz, 40f, Rate * 0.45f) / Rate;
                float alpha = Mathf.Sin(w) / (2f * q);
                float a0 = 1f + alpha;
                float b0 = alpha / a0, b2 = -alpha / a0;
                float a1 = -2f * Mathf.Cos(w) / a0, a2 = (1f - alpha) / a0;
                float y = b0 * x + b2 * _x2 - a1 * _y1 - a2 * _y2;
                _x2 = _x1; _x1 = x;
                _y2 = _y1; _y1 = y;
                return y;
            }
        }

        /// <summary>Blade drawn or hitting: a quick metallic "shing" (inharmonic partials with a fast decay).</summary>
        public static AudioClip KnifeShing()
        {
            var rng = new System.Random(58);
            float lp = 0f;
            return Make("knife_shing", 0.35f, (t, i) =>
            {
                float scrape = 0f;
                if (t < 0.07f)
                {
                    float n = Noise(rng);
                    lp += (n - lp) * 0.6f;
                    scrape = (n - lp) * (1f - t / 0.07f) * 0.6f; // high-passed scrape
                }
                float ring = Mathf.Sin(2f * Mathf.PI * 2480f * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * 3710f * t)
                             + 0.45f * Mathf.Sin(2f * Mathf.PI * 5230f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 7110f * t);
                return scrape + ring * Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 14f) * 0.35f;
            });
        }

        /// <summary>Blade hitting flesh: a short wet "thwack" (low thump plus a bright tick).</summary>
        public static AudioClip BladeHit()
        {
            var rng = new System.Random(59);
            float lp = 0f;
            return Make("blade_hit", 0.18f, (t, i) =>
            {
                float n = Noise(rng);
                lp += (n - lp) * 0.12f;
                float thump = lp * Mathf.Exp(-t * 30f) * 2.2f + Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 35f) * 0.5f;
                float tick = (n - lp) * Mathf.Exp(-t * 90f) * 0.5f;
                return thump + tick;
            });
        }

        public static AudioClip Zap()
        {
            return Make("zap", 0.32f, (t, i) =>
            {
                float f = Mathf.Lerp(320f, 980f, t / 0.32f);
                float env = Mathf.Sin(Mathf.PI * t / 0.32f);
                return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f * 1.5f * t) * 0.25f) * env * 0.45f;
            });
        }

        private static AudioClip Make(string name, float seconds, Func<float, int, float> sample, float lowpass = 1f)
        {
            int n = Mathf.CeilToInt(Rate * seconds);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float v = sample(i / (float)Rate, i);
                lp += (v - lp) * lowpass;
                data[i] = lowpass < 1f ? lp : v;
            }
            Normalize(data, 0.8f);
            // 2 ms fade-out to avoid a click at the end.
            int fade = Mathf.Min(n, Rate / 500);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        private static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
    }
}
