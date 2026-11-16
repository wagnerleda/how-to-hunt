using HowToHunt.Core;
using HowToHunt.Sim.Hunting;
using TMPro;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// What the hunter is told about a take-down: a one-line toast under the crosshair ("Caught a butterfly!",
    /// "Your net is too flimsy to hold a rabbit. Upgrade it at camp."), a short feed on the right (rewards, sales),
    /// hit markers and sounds. Lines are built only when something happens, never per frame.
    /// </summary>
    public sealed class HuntFeedback : MonoBehaviour
    {
        private const int FeedLines = 5;
        private const float ToastSeconds = 2.6f;
        private const float FeedSeconds = 6f;

        private static HuntFeedback _instance;

        private TextMeshProUGUI _toast;
        private float _toastLife;
        private readonly TextMeshProUGUI[] _feed = new TextMeshProUGUI[FeedLines];
        private readonly float[] _feedLife = new float[FeedLines];
        private AudioClip _chime;

        private static readonly Color Good = new Color(0.62f, 1f, 0.55f);
        private static readonly Color Neutral = new Color(1f, 0.96f, 0.86f);
        private static readonly Color Warning = new Color(1f, 0.68f, 0.3f);

        private void Awake()
        {
            _instance = this;
            _toast = Hud.Text("Hunt Toast", new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), 26f, TextAlignmentOptions.Center, Neutral);
            _toast.rectTransform.sizeDelta = new Vector2(1200f, 60f);
            _toast.fontStyle = FontStyles.Bold;
            _toast.gameObject.SetActive(false);
            for (int i = 0; i < FeedLines; i++)
            {
                TextMeshProUGUI t = Hud.Text($"Hunt Feed {i}", new Vector2(1f, 0.5f), new Vector2(-30f, 40f - i * 34f), 24f, TextAlignmentOptions.Right, Neutral);
                t.rectTransform.sizeDelta = new Vector2(700f, 34f);
                t.rectTransform.pivot = new Vector2(1f, 0.5f);
                t.gameObject.SetActive(false);
                _feed[i] = t;
            }
            _chime = ProceduralSounds.Chime();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>A line under the crosshair. Warnings are orange and play a soft error sound.</summary>
        public static void Message(string text, bool warning = false)
        {
            if (_instance == null) return;
            _instance.ShowToast(text, warning ? Warning : Neutral);
            if (warning) Sfx.Play2D("Kenney/error_004", 0.35f);
        }

        /// <summary>A line in the feed on the right ("+7 coins", "Sold 3 butterflies").</summary>
        public static void Feed(string text, bool good = true)
        {
            if (_instance == null) return;
            HuntFeedback f = _instance;
            for (int i = FeedLines - 1; i > 0; i--)
            {
                f._feed[i].SetText(f._feed[i - 1].text);
                f._feed[i].color = f._feed[i - 1].color;
                f._feedLife[i] = f._feedLife[i - 1];
                f._feed[i].gameObject.SetActive(f._feedLife[i] > 0f);
            }
            f._feed[0].SetText(text);
            f._feed[0].color = good ? Good : Neutral;
            f._feedLife[0] = FeedSeconds;
            f._feed[0].gameObject.SetActive(true);
        }

        /// <summary>The host's verdict on this player's tool use.</summary>
        public static void LocalResult(TakedownOutcome outcome, RejectReason reason, PreyDefinition prey, ToolArchetype tool,
            Vector3 point, float damage, bool weak, CarcassQuality quality)
        {
            if (_instance == null || prey == null) return;
            HuntFeedback f = _instance;
            switch (outcome)
            {
                case TakedownOutcome.Captured:
                    f.ShowToast("Caught!", Good);
                    HitFeedback.Marker(new Color(0.55f, 1f, 0.5f), 0.3f);
                    Sfx.Play2D("Kenney/cloth", 0.7f, Random.Range(0.95f, 1.08f));
                    Sfx.Play2D("Kenney/pluck_001", 0.35f, 1.2f);
                    f.PlayChime();
                    break;
                case TakedownOutcome.Killed:
                    f.ShowToast(quality == CarcassQuality.Ruined ? "Ruined (overkill)" : "Killed!", quality == CarcassQuality.Ruined ? Warning : Neutral);
                    HitFeedback.Confirm(point, damage, weak, true, -1, default, 0f, 0f);
                    if (tool == ToolArchetype.Blade) { Sfx.Play2D(KnifeSounds.Hit, 0.5f, Random.Range(0.9f, 1.05f)); Sfx.Play2D(KnifeSounds.Shing, 0.18f, Random.Range(1.05f, 1.15f)); }
                    else Sfx.Play2D("Kenney/impactPunch_heavy", 0.45f, Random.Range(0.9f, 1.05f));
                    break;
                case TakedownOutcome.Damaged:
                    HitFeedback.Confirm(point, damage, weak, false, -1, default, 0f, 0f);
                    if (tool == ToolArchetype.Blade) Sfx.Play2D(KnifeSounds.Hit, 0.4f, Random.Range(1f, 1.15f));
                    else Sfx.Play2D("Kenney/impactSoft_medium", 0.3f, Random.Range(1f, 1.15f));
                    break;
                case TakedownOutcome.Rejected:
                    Message(RejectLine(reason, prey, tool), warning: true);
                    Sfx.Play2D(tool == ToolArchetype.Net ? "Kenney/impactSoft_heavy" : "Kenney/impactGeneric_light", 0.35f);
                    break;
            }
        }

        /// <summary>Why the tool didn't work, and what to do about it.</summary>
        public static string RejectLine(RejectReason reason, PreyDefinition prey, ToolArchetype tool)
        {
            string noun = prey.Noun;
            switch (reason)
            {
                case RejectReason.TooWeakToHold:
                    return "Net too weak";
                case RejectReason.TooWeakToHurt:
                    return "Too weak to hurt it";
                case RejectReason.MethodNotAllowed:
                    return tool == ToolArchetype.Net || tool == ToolArchetype.Trap
                        ? "Can't be caught"
                        : "Can't be hurt";
                default:
                    return "That didn't work.";
            }
        }

        private void PlayChime()
        {
            if (_chime == null) return;
            Sfx.Play2D(_chime, 0.22f, Random.Range(0.98f, 1.06f));
        }

        private void ShowToast(string text, Color color)
        {
            _toast.SetText(text);
            _toast.color = color;
            _toastLife = ToastSeconds;
            _toast.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (_toastLife > 0f)
            {
                _toastLife -= dt;
                Color c = _toast.color;
                c.a = Mathf.Clamp01(_toastLife / 0.5f);
                _toast.color = c;
                if (_toastLife <= 0f) _toast.gameObject.SetActive(false);
            }
            for (int i = 0; i < FeedLines; i++)
            {
                if (_feedLife[i] <= 0f) continue;
                _feedLife[i] -= dt;
                Color c = _feed[i].color;
                c.a = Mathf.Clamp01(_feedLife[i] / 0.8f);
                _feed[i].color = c;
                if (_feedLife[i] <= 0f) _feed[i].gameObject.SetActive(false);
            }
        }
    }
}
