using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>Easing curves for UI motion (t in 0..1). Pure functions, no allocations. Never linear.</summary>
    public static class Ease
    {
        public enum Kind : byte { OutCubic, OutBack, OutElastic, InBack, InOutSine, OutQuint }

        public static float Evaluate(Kind kind, float t)
        {
            switch (kind)
            {
                case Kind.OutBack: return OutBack(t);
                case Kind.OutElastic: return OutElastic(t);
                case Kind.InBack: return InBack(t);
                case Kind.InOutSine: return InOutSine(t);
                case Kind.OutQuint: return OutQuint(t);
                default: return OutCubic(t);
            }
        }

        public static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * t + 1f;
        }

        public static float OutQuint(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * t * t * t + 1f;
        }

        /// <summary>Overshoots a little, then settles: buttons and bubbles that "pop".</summary>
        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
        }

        public static float InBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t);
            return t * t * ((overshoot + 1f) * t - overshoot);
        }

        /// <summary>A springy wobble that settles (squash and stretch).</summary>
        public static float OutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f || t >= 1f) return t;
            const float p = 0.35f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
        }

        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) * 0.5f;

        /// <summary>Frame-rate independent approach toward a target (critically damped feel).</summary>
        public static float Damp(float current, float target, float sharpness, float dt) => Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
    }
}
