using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Hunting;
using HowToHunt.Player;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Travel
{
    /// <summary>
    /// The totem's journey (owner, 2026-11-25: "a cool transition"): a golden glow closes in from the edges while the
    /// view stretches wide and leans, it all burns to warm white, the host moves you while the screen is white (and the
    /// new land streams in), then the light opens out from the centre as the view settles from a narrow squeeze.
    /// With reduce motion it is a plain fade. You can't move during it. No allocations after Awake.
    /// </summary>
    public sealed class TravelTransition : MonoBehaviour
    {
        public static readonly FloatCvar Speed = new FloatCvar("cl_travel_speed", 1f,
            "Speed of the totem travel transition (2 = twice as quick).", CvarFlags.Archive, 0.5f, 4f);

        private const float InSeconds = 0.75f, HoldSeconds = 0.4f, OutSeconds = 0.85f, WaitLimit = 4f;
        private const float StretchFov = 38f, SqueezeFov = -14f, LeanIn = 12f, LeanOut = -8f;
        private const float VignetteWide = 2.8f;   // the glow's scale when open (its clear middle covers the screen)
        private static readonly Color Glow = new Color(1f, 0.78f, 0.36f), Flash = new Color(1f, 0.97f, 0.88f);

        private enum Phase { Idle, In, Waiting, Hold, Out }

        private static TravelTransition _instance;
        private Image _vignette, _flash;
        private Phase _phase;
        private float _t;
        private RegionDefinition _region;
        private bool _arrived;
        private AudioClip _whoosh, _chime;

        public static bool Busy => _instance != null && _instance._phase != Phase.Idle;

        private void Awake()
        {
            _instance = this;
            _flash = Full("Travel Flash");
            _flash.color = new Color(Flash.r, Flash.g, Flash.b, 0f);
            _vignette = Full("Travel Glow");
            _vignette.sprite = GlowSprite();
            _vignette.color = new Color(Glow.r, Glow.g, Glow.b, 0f);
            _vignette.transform.SetAsLastSibling();
            _flash.transform.SetAsLastSibling();
            _vignette.gameObject.SetActive(false);
            _flash.gameObject.SetActive(false);
            TravelNet.Result += OnResult;
        }

        private void OnDestroy()
        {
            TravelNet.Result -= OnResult;
            if (_phase != Phase.Idle) Finish();
            if (_instance == this) _instance = null;
        }

        /// <summary>Start the journey to a region: the host is asked once the screen is white.</summary>
        public static bool Begin(RegionDefinition region)
        {
            if (_instance == null || _instance._phase != Phase.Idle || region == null) return false;
            TravelTransition s = _instance;
            s._region = region;
            s._arrived = false;
            s._phase = Phase.In;
            s._t = 0f;
            s._vignette.gameObject.SetActive(true);
            s._flash.gameObject.SetActive(true);
            s._vignette.transform.SetAsLastSibling();
            s._flash.transform.SetAsLastSibling();
            Game.PushUiFocus(s);
            if (s._whoosh == null) { s._whoosh = ProceduralSounds.Whoosh(); s._chime = ProceduralSounds.Chime(); }
            Sfx.Play2D(s._whoosh, 0.55f, 0.7f);
            Sfx.Play2D(s._chime, 0.35f, 0.75f);
            return true;
        }

        private void OnResult(TravelResultBroadcast msg)
        {
            if (_phase != Phase.Waiting && _phase != Phase.In) return;
            var why = (TravelRefusal)msg.Refusal;
            _arrived = why == TravelRefusal.None;
            if (!_arrived) HuntFeedback.Message(TravelNet.Explain(why), warning: true);
            if (_phase == Phase.Waiting) { _phase = _arrived ? Phase.Hold : Phase.Out; _t = 0f; }
        }

        private void Update()
        {
            if (_phase == Phase.Idle) return;
            float dt = Time.unscaledDeltaTime * Speed.Value;
            _t += dt;
            bool calm = UiFeel.Calm;
            switch (_phase)
            {
                case Phase.In:
                {
                    float k = Mathf.Clamp01(_t / InSeconds);
                    Show(k, k, calm);
                    if (k >= 1f)
                    {
                        _phase = Phase.Waiting;
                        _t = 0f;
                        if (!TravelNet.Request(_region)) { _phase = Phase.Out; HuntFeedback.Message(TravelNet.Explain(TravelRefusal.Unknown), warning: true); }
                    }
                    break;
                }
                case Phase.Waiting:
                    Show(1f, 1f, calm);
                    if (_t > WaitLimit) { _phase = Phase.Out; _t = 0f; }
                    break;
                case Phase.Hold:
                    Show(1f, 1f, calm);
                    if (_t >= HoldSeconds)
                    {
                        _phase = Phase.Out;
                        _t = 0f;
                        Sfx.Play2D(_chime, 0.45f, 1.1f);
                        Sfx.Play2D("bong_001", 0.35f, 0.7f);
                    }
                    break;
                case Phase.Out:
                {
                    float k = 1f - Mathf.Clamp01(_t / OutSeconds);
                    Show(k, k, calm, arriving: _arrived);
                    if (k <= 0f) Finish();
                    break;
                }
            }
        }

        /// <summary>glow 0..1 (closed at 1), flash: how white. Going in, the view stretches and leans; coming out, it squeezes the other way.</summary>
        private void Show(float glow, float k, bool calm, bool arriving = false)
        {
            float g = Ease.InOutSine(glow);
            float white = calm ? k : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, k));
            _flash.color = new Color(Flash.r, Flash.g, Flash.b, white);
            _vignette.color = new Color(Glow.r, Glow.g, Glow.b, calm ? 0f : Mathf.Min(1f, g * 1.4f));
            float scale = Mathf.Lerp(VignetteWide, 1f, g);
            _vignette.rectTransform.localScale = new Vector3(scale, scale, 1f);
            if (calm) { PlayerCamera.EffectFov = 0f; PlayerCamera.EffectRoll = 0f; return; }
            float push = g * g;
            PlayerCamera.EffectFov = push * (arriving ? SqueezeFov : StretchFov);
            PlayerCamera.EffectRoll = push * (arriving ? LeanOut : LeanIn);
        }

        private void Finish()
        {
            _phase = Phase.Idle;
            PlayerCamera.EffectFov = 0f;
            PlayerCamera.EffectRoll = 0f;
            _vignette.gameObject.SetActive(false);
            _flash.gameObject.SetActive(false);
            Game.PopUiFocus(this);
        }

        private static Image Full(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(Hud.Root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Image i = go.AddComponent<Image>();
            i.raycastTarget = false;
            return i;
        }

        /// <summary>A soft ring: clear in the middle, glowing toward the edges (stretched over the screen).</summary>
        private static Sprite GlowSprite()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Travel Glow", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421356f; // 0 centre .. 1 corner
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.75f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
