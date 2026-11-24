using System.Text;
using TMPro;
using UnityEngine;

namespace HowToHunt.Npcs
{
    /// <summary>
    /// The little markup of dialogue lines, and the effects it drives. A line may start with a style:
    /// <c>[shout]</c>, <c>[whisper]</c>, <c>[think]</c>, <c>[narrate]</c> (default: normal speech). Inside it,
    /// TextMeshPro's own tags work (<c>&lt;b&gt;</c>, <c>&lt;i&gt;</c>, <c>&lt;color=#c33&gt;</c>), plus two of ours:
    /// <c>&lt;shake&gt;scared&lt;/shake&gt;</c> (letters tremble) and <c>&lt;wave&gt;hello&lt;/wave&gt;</c> (letters ripple).
    /// Parsing allocates once per line; animating moves the letters' vertices in place each frame (no garbage).
    /// </summary>
    public sealed class BubbleText
    {
        private const string ShakeOpen = "<shake>", ShakeClose = "</shake>", WaveOpen = "<wave>", WaveClose = "</wave>";
        private const float ShakePixels = 1.6f, WavePixels = 3.2f, WaveSpeed = 7f, WaveSpread = 0.55f;
        private enum Fx : byte { None, Shake, Wave }

        private readonly StringBuilder _sb = new StringBuilder(256);
        private Fx[] _sourceFx = new Fx[256]; // per character of Clean
        private bool _any;

        /// <summary>The line with our tags removed (TextMeshPro tags kept): what the text component shows.</summary>
        public string Clean { get; private set; } = string.Empty;
        /// <summary>Visible characters once laid out.</summary>
        public int Count { get; private set; }

        /// <summary>Splits off a leading style marker: "[shout] Get out!" -> Shout, "Get out!".</summary>
        public static string ParseStyle(string line, BubbleStyle fallback, out BubbleStyle style)
        {
            style = fallback;
            if (string.IsNullOrEmpty(line) || line[0] != '[') return line ?? string.Empty;
            int end = line.IndexOf(']');
            if (end < 0) return line;
            string tag = line.Substring(1, end - 1).Trim().ToLowerInvariant();
            switch (tag)
            {
                case "say": style = BubbleStyle.Say; break;
                case "shout": style = BubbleStyle.Shout; break;
                case "whisper": style = BubbleStyle.Whisper; break;
                case "think": style = BubbleStyle.Think; break;
                case "narrate": style = BubbleStyle.Narrate; break;
                default: return line; // not a style: keep the text as written
            }
            return line.Substring(end + 1).TrimStart();
        }

        /// <summary>Strips our tags and remembers which characters they wrapped; sets the text.</summary>
        public void Set(TMP_Text text, string line)
        {
            _sb.Clear();
            _any = false;
            line ??= string.Empty;
            if (_sourceFx.Length < line.Length + 1) _sourceFx = new Fx[line.Length + 32];
            Fx current = Fx.None;
            for (int i = 0; i < line.Length;)
            {
                if (line[i] == '<')
                {
                    if (Match(line, i, ShakeOpen)) { current = Fx.Shake; i += ShakeOpen.Length; _any = true; continue; }
                    if (Match(line, i, WaveOpen)) { current = Fx.Wave; i += WaveOpen.Length; _any = true; continue; }
                    if (Match(line, i, ShakeClose)) { current = Fx.None; i += ShakeClose.Length; continue; }
                    if (Match(line, i, WaveClose)) { current = Fx.None; i += WaveClose.Length; continue; }
                }
                _sourceFx[_sb.Length] = current;
                _sb.Append(line[i]);
                i++;
            }
            Clean = _sb.ToString();
            text.text = Clean;
        }

        /// <summary>Lays the text out (after sizing) so characters can be counted and revealed.</summary>
        public void Prepare(TMP_Text text)
        {
            text.ForceMeshUpdate();
            Count = text.textInfo.characterCount;
        }

        public char CharAt(TMP_Text text, int i) => i >= 0 && i < text.textInfo.characterCount ? text.textInfo.characterInfo[i].character : ' ';

        /// <summary>Moves the letters inside &lt;shake&gt; and &lt;wave&gt; for this frame. Calm: still.</summary>
        public void Animate(TMP_Text text, bool calm)
        {
            if (!_any || calm) return;
            text.ForceMeshUpdate();
            TMP_TextInfo info = text.textInfo;
            float t = Time.unscaledTime;
            bool moved = false;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible) continue;
                int source = c.index;
                Fx fx = source >= 0 && source < Clean.Length ? _sourceFx[source] : Fx.None;
                if (fx == Fx.None) continue;
                Vector3 offset = fx == Fx.Shake
                    ? new Vector3(Mathf.Sin(t * 53f + i * 1.7f), Mathf.Sin(t * 61f + i * 2.3f), 0f) * ShakePixels
                    : new Vector3(0f, Mathf.Sin(t * WaveSpeed + i * WaveSpread) * WavePixels, 0f);
                Vector3[] verts = info.meshInfo[c.materialReferenceIndex].vertices;
                int v = c.vertexIndex;
                verts[v] += offset;
                verts[v + 1] += offset;
                verts[v + 2] += offset;
                verts[v + 3] += offset;
                moved = true;
            }
            if (moved) text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        private static bool Match(string s, int at, string tag) => string.CompareOrdinal(s, at, tag, 0, tag.Length) == 0;
    }
}
