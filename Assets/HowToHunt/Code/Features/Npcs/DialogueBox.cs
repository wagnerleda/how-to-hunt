using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Player;
using HowToHunt.Sim.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HowToHunt.Npcs
{
    /// <summary>
    /// Talking to a camp character. What they say appears in a speech bubble over their head (SpeechBubble), typed
    /// out; a long line comes in pages (a blank line in the text starts a new one). Interact, Enter, Space, a click or
    /// gamepad A hurries the typing, then moves on; once they have said their piece, your replies appear (ChoiceList).
    /// Short and to the point (owner, 2026-11-24: the quests are set, so no questions to click through): talking to a
    /// character settles their quest at once in one line (a new one is taken on, a finished one handed in and the
    /// next one taken, one in progress shows how far you are), then the only replies are their stall's business
    /// (sell, browse, guns) and goodbye. Escape, gamepad B or walking away ends it. Lines come from NpcDefinition and QuestDefinition and
    /// may use the bubble markup (BubbleText: [shout], &lt;shake&gt;...).
    /// </summary>
    public sealed class DialogueBox : MonoBehaviour
    {
        private enum Act : byte { None, Bye, Sell, Shop, Workshop }

        private static DialogueBox _instance;
        /// <summary>The character the local player is talking to (null: nobody).</summary>
        public static Npc TalkingTo => _instance != null ? _instance._npc : null;

        private Npc _npc;
        private readonly List<string> _pages = new List<string>(4);
        private int _page;
        private SpeechBubble _bubble;
        private ChoiceList _list;
        private readonly string[] _labels = new string[ChoiceList.MaxChoices];
        private readonly Act[] _acts = new Act[ChoiceList.MaxChoices];
        private readonly bool[] _enabled = new bool[ChoiceList.MaxChoices];
        private int _choiceCount;
        private int _openedFrame;
        private System.Action<int> _onPick;

        private void Awake()
        {
            _instance = this;
            _onPick = i => Choose(_acts[i]);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public static bool Open(Npc npc)
        {
            if (_instance == null || npc == null || npc.Definition == null) return false;
            _instance.Begin(npc);
            return true;
        }

        private void Begin(Npc npc)
        {
            if (_list == null) _list = GetComponent<ChoiceList>();
            _npc = npc;
            _openedFrame = Time.frameCount;
            Game.PushUiFocus(this);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UiFeel.Open();
            var greetings = npc.Definition.Greetings;
            Root(greetings.Count > 0 ? greetings[Random.Range(0, greetings.Count)] : "...");
        }

        private void End()
        {
            if (_bubble != null) _bubble.Hide();
            _bubble = null;
            if (_list != null && _list.Open) _list.Hide();
            if (_npc != null) UiFeel.Back();
            _npc = null;
            Game.PopUiFocus(this);
            PlayerCamera.LockCursor();
        }

        // --- what they say ----------------------------------------------------------------------------------

        /// <summary>A new line from the character: split into pages, the first one shown; replies wait for the last.</summary>
        private void Say(string text)
        {
            _pages.Clear();
            _choiceCount = 0;
            text ??= string.Empty;
            int start = 0;
            while (start <= text.Length)
            {
                int cut = text.IndexOf("\n\n", start, System.StringComparison.Ordinal);
                string page = (cut < 0 ? text.Substring(start) : text.Substring(start, cut - start)).Trim();
                if (page.Length > 0) _pages.Add(page);
                if (cut < 0) break;
                start = cut + 2;
            }
            if (_pages.Count == 0) _pages.Add("...");
            if (_list != null && _list.Open) _list.Hide();
            ShowPage(0);
        }

        private void ShowPage(int index)
        {
            _page = index;
            if (_bubble != null) _bubble.Hide();
            NpcDefinition def = _npc.Definition;
            string body = BubbleText.ParseStyle(_pages[index], BubbleStyle.Say, out BubbleStyle style);
            // Name plate on the first page: who is talking.
            string line = index == 0 ? $"<size=70%><b>{def.DisplayName}</b>, {def.Role}</size>\n{body}" : body;
            _bubble = SpeechBubble.Show(_npc.transform, line, style, def.VoicePitch, NpcVoices.For(def));
            _bubble.ShowNext = true;
        }

        private void Add(string label, Act act, bool enabled = true)
        {
            if (_choiceCount >= ChoiceList.MaxChoices) return;
            _labels[_choiceCount] = label;
            _acts[_choiceCount] = act;
            _enabled[_choiceCount] = enabled;
            _choiceCount++;
        }

        private void Root(string line)
        {
            EconomyProfile me = EconomyNet.Local;
            QuestDefinition quest = EconomyNet.QuestFrom(_npc.Definition, me, out QuestState state);
            if (quest != null && state == QuestState.Ready)
            {
                // Handed in on the spot; the next job from them (if any) is taken on in the same breath.
                EconomyNet.RequestTurnInQuest(quest);
                line = $"{quest.Done} {Reward(quest)}";
                QuestDefinition next = NextFrom(quest);
                if (next != null)
                {
                    EconomyNet.RequestAcceptQuest(next);
                    line += $"\n{Job(next)}";
                }
            }
            else if (quest != null && state == QuestState.Available)
            {
                EconomyNet.RequestAcceptQuest(quest);
                line = $"{quest.Offer}\n{Job(quest)}";
            }
            else if (quest != null && state == QuestState.Active)
                line = $"{quest.Waiting}\n<b>{quest.Task}</b>: {EconomyNet.ProgressOf(me, quest)} / {quest.Count}";
            else if (!string.IsNullOrEmpty(_npc.Definition.Idle) && Random.value < 0.5f) line = _npc.Definition.Idle;
            Say(line);
            if (_npc.Definition.HasStation)
            {
                switch (_npc.Definition.Station)
                {
                    case CampStationKind.Sell:
                        int n = me.Inventory.PreyCount;
                        if (n > 0) Add($"Sell my {n} {(n == 1 ? "catch" : "catches")}.", Act.Sell);
                        break;
                    case CampStationKind.Shop: Add("Show me what you've got.", Act.Shop); break;
                    case CampStationKind.Workshop: Add("Let's look at the guns.", Act.Workshop); break;
                }
            }
            Add("Goodbye.", Act.Bye);
        }

        private static string Job(QuestDefinition q) =>
            $"<b>{q.DisplayName}</b>: {q.Task}. <color=#b8741a><b>{((BigDouble)q.RewardCoins).ToShortString()} coins</b></color>";

        private static string Reward(QuestDefinition q) =>
            $"<color=#b8741a><b>+{((BigDouble)q.RewardCoins).ToShortString()} coins</b></color>";

        /// <summary>Their next quest in the chain right after this one (they hand it over as you finish), or null.</summary>
        private QuestDefinition NextFrom(QuestDefinition done)
        {
            foreach (QuestDefinition q in ContentRegistry.All<QuestDefinition>())
                if (q.Giver == _npc.Definition && q.Requires == done) return q;
            return null;
        }

        private void Choose(Act act)
        {
            switch (act)
            {
                case Act.Bye: End(); break;
                case Act.Sell:
                    EconomyNet.RequestSell();
                    End();
                    break;
                case Act.Shop:
                {
                    CampStation s = _npc.Station;
                    End();
                    if (s != null) ShopPanel.Open(s);
                    break;
                }
                case Act.Workshop:
                {
                    CampStation s = _npc.Station;
                    End();
                    if (s != null) WorkshopPanel.Open(s);
                    break;
                }
            }
        }

        // --- frame ---------------------------------------------------------------------------------------------

        private void Update()
        {
            if (_npc == null) return;
            PlayerMotor me = PlayerMotor.Local;
            if (me == null || (me.transform.position - _npc.transform.position).sqrMagnitude > (_npc.TalkRange + 1.5f) * (_npc.TalkRange + 1.5f)) { End(); return; }
            if (UiInput.Back) { End(); return; }
            if (_list != null && _list.Open) return; // the replies take the input
            if (Time.frameCount == _openedFrame || _bubble == null) return;

            Mouse mouse = Mouse.current;
            bool advance = UiInput.Confirm || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            bool last = _page >= _pages.Count - 1;
            _bubble.ShowNext = !last || _choiceCount == 0;
            if (_bubble.Typing)
            {
                if (advance) _bubble.Hurry();
                return;
            }
            if (!last)
            {
                if (advance) { UiFeel.Play(UiFeel.HoverSound, 0.5f); ShowPage(_page + 1); }
                return;
            }
            if (_choiceCount > 0) _list.Show(_labels, _enabled, _choiceCount, _onPick);
            else if (advance) End();
        }
    }
}
