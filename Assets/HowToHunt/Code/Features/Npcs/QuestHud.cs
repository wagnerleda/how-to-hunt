using System.Collections.Generic;
using System.Text;
using HowToHunt.Core;
using HowToHunt.Economy;
using TMPro;
using UnityEngine;

namespace HowToHunt.Npcs
{
    /// <summary>The player's quests in progress, top left: "Fresh Stock: sell 3 catches to Tomas (1/3)". Rebuilt only
    /// when the quest log changes.</summary>
    public sealed class QuestHud : MonoBehaviour
    {
        private static readonly Color Text = new Color(1f, 0.95f, 0.82f, 0.95f);
        private TextMeshProUGUI _list;
        private int _signature = int.MinValue;
        private readonly StringBuilder _sb = new StringBuilder(256);

        private void Awake()
        {
            _list = Hud.Text("Quests", new Vector2(0f, 1f), new Vector2(24f, -96f), 18f, TextAlignmentOptions.TopLeft, Text);
            _list.rectTransform.pivot = new Vector2(0f, 1f);
            _list.rectTransform.sizeDelta = new Vector2(520f, 200f);
            _list.raycastTarget = false;
            _list.richText = true;
        }

        private void Update()
        {
            EconomyProfile me = EconomyNet.Local;
            int sig = me.Quests.Count * 7919 + me.QuestsDone.Count;
            foreach (KeyValuePair<uint, int> kv in me.Quests) sig = sig * 31 + (int)kv.Key + kv.Value;
            if (sig == _signature) return;
            _signature = sig;
            List<QuestDefinition> active = EconomyNet.ActiveQuests(me);
            _sb.Clear();
            foreach (QuestDefinition q in active)
            {
                int n = EconomyNet.ProgressOf(me, q);
                bool ready = n >= q.Count;
                _sb.Append("<b>").Append(q.DisplayName).Append("</b>: ").Append(q.Task);
                if (q.Count > 1 && !ready) _sb.Append(" (").Append(n).Append('/').Append(q.Count).Append(')');
                if (ready) _sb.Append("  <color=#9BE07A>done: see ").Append(q.Giver != null ? q.Giver.DisplayName : "them").Append("</color>");
                _sb.Append('\n');
            }
            _list.SetText(_sb);
            _list.gameObject.SetActive(active.Count > 0);
        }
    }
}
