using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Npcs
{
    /// <summary>
    /// What camp characters show and say without being talked to: a floating marker over their head ("!" a quest
    /// for you, "?" a quest to hand in, "..." close enough to talk), bobbing gently, hidden while you talk to them;
    /// and barks, short lines in a bubble when you walk past (NpcDefinition.Barks, one per BarkCooldown each). Markers
    /// are built once per NPC and reused; quest states are refreshed a few times a second; no allocations per frame.
    /// </summary>
    public sealed class NpcPresence : MonoBehaviour
    {
        private const float RefreshSeconds = 0.25f, MarkerSize = 38f;
        private static readonly Color Gold = new Color(1f, 0.8f, 0.25f), Pale = new Color(0.98f, 0.95f, 0.88f), Dark = new Color(0.18f, 0.13f, 0.1f, 0.85f);

        private sealed class Marker
        {
            public RectTransform Root;
            public Image Back;
            public TextMeshProUGUI Glyph;
            public char Shown;
            public float Alpha;
            public float NextBark;
        }

        private readonly Dictionary<Npc, Marker> _markers = new Dictionary<Npc, Marker>();
        private float _nextRefresh;

        private Marker For(Npc npc)
        {
            if (_markers.TryGetValue(npc, out Marker m)) return m;
            m = new Marker();
            var go = new GameObject($"Marker {npc.Definition.DisplayName}", typeof(RectTransform));
            go.transform.SetParent(Hud.Root, false);
            m.Root = (RectTransform)go.transform;
            m.Root.anchorMin = m.Root.anchorMax = Vector2.zero;
            m.Root.sizeDelta = new Vector2(MarkerSize, MarkerSize);
            m.Back = new GameObject("Back", typeof(RectTransform)).AddComponent<Image>();
            m.Back.transform.SetParent(m.Root, false);
            m.Back.rectTransform.sizeDelta = new Vector2(MarkerSize, MarkerSize);
            m.Back.sprite = UiSprites.Circle();
            m.Back.raycastTarget = false;
            m.Glyph = new GameObject("Glyph", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            m.Glyph.transform.SetParent(m.Root, false);
            m.Glyph.rectTransform.sizeDelta = new Vector2(MarkerSize, MarkerSize);
            m.Glyph.alignment = TextAlignmentOptions.Center;
            m.Glyph.fontStyle = FontStyles.Bold;
            m.Glyph.raycastTarget = false;
            m.NextBark = Time.time + Random.Range(3f, DialogueTheme.Current.BarkCooldown * 0.5f);
            go.SetActive(false);
            _markers[npc] = m;
            return m;
        }

        private void LateUpdate()
        {
            PlayerMotor me = PlayerMotor.Local;
            Camera cam = Camera.main;
            IReadOnlyList<Npc> all = Npc.All;
            DialogueTheme theme = DialogueTheme.Current;
            bool refresh = Time.unscaledTime >= _nextRefresh;
            if (refresh) _nextRefresh = Time.unscaledTime + RefreshSeconds;
            Canvas canvas = Hud.Root.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            bool calm = UiFeel.Calm;

            for (int i = 0; i < all.Count; i++)
            {
                Npc npc = all[i];
                if (npc.Definition == null) continue;
                Marker m = For(npc);
                bool talking = DialogueBox.TalkingTo == npc;
                float dist = me != null ? Vector3.Distance(me.transform.position, npc.transform.position) : float.MaxValue;

                // What to show: quest news from afar, "..." once close enough to talk, nothing while talking.
                if (refresh || m.Shown == '\0')
                {
                    char glyph = ' ';
                    if (me != null && !talking && dist <= theme.MarkerRange)
                    {
                        EconomyNet.QuestFrom(npc.Definition, EconomyNet.Local, out QuestState state);
                        glyph = state == QuestState.Ready ? '?' : state == QuestState.Available ? '!' : dist <= npc.TalkRange + 1.5f ? '…' : ' ';
                    }
                    if (glyph != m.Shown)
                    {
                        m.Shown = glyph;
                        bool news = glyph == '?' || glyph == '!';
                        m.Glyph.SetCharArray(Glyph(glyph), 0, 1);
                        m.Glyph.fontSize = news ? 30f : 26f;
                        m.Glyph.color = news ? Dark : Dark;
                        m.Back.color = news ? Gold : Pale;
                    }
                }

                // Fade in/out; bob; follow the head on screen.
                float target = m.Shown != ' ' ? 1f : 0f;
                m.Alpha = Ease.Damp(m.Alpha, target, 10f, Time.unscaledDeltaTime);
                bool visible = m.Alpha > 0.02f && cam != null;
                Vector3 sp = Vector3.zero;
                if (visible)
                {
                    float bob = calm ? 0f : Mathf.Sin(Time.time * theme.MarkerBobHz * Mathf.PI * 2f + i) * theme.MarkerBob;
                    sp = cam.WorldToScreenPoint(npc.transform.position + Vector3.up * (theme.MarkerHeight + bob));
                    visible = sp.z > 0f;
                }
                if (m.Root.gameObject.activeSelf != visible) m.Root.gameObject.SetActive(visible);
                if (visible)
                {
                    m.Root.anchoredPosition = new Vector2(sp.x / scale, sp.y / scale);
                    float pulse = calm || (m.Shown != '!' && m.Shown != '?') ? 1f : 1f + 0.06f * Mathf.Sin(Time.time * 4f);
                    float s = Mathf.Lerp(0.6f, 1f, m.Alpha) * pulse;
                    m.Root.localScale = new Vector3(s, s, 1f);
                    m.Back.canvasRenderer.SetAlpha(m.Alpha);
                    m.Glyph.canvasRenderer.SetAlpha(m.Alpha);
                }

                // Barks: a short line when you walk past, not while you talk to anyone.
                if (me != null && DialogueBox.TalkingTo == null && dist <= theme.BarkRadius && Time.time >= m.NextBark && npc.Definition.Barks.Count > 0)
                {
                    m.NextBark = Time.time + theme.BarkCooldown * Random.Range(0.8f, 1.3f);
                    string raw = npc.Definition.Barks[Random.Range(0, npc.Definition.Barks.Count)];
                    string line = BubbleText.ParseStyle(raw, BubbleStyle.Say, out BubbleStyle style);
                    SpeechBubble.Show(npc.transform, line, style, npc.Definition.VoicePitch, NpcVoices.For(npc.Definition), theme.BarkSeconds);
                }
            }
        }

        private static readonly char[] One = new char[1];

        private static char[] Glyph(char c)
        {
            One[0] = c;
            return One;
        }
    }
}
