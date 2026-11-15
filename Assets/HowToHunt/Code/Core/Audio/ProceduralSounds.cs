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
