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
    /// They offer their stall's business (sell, browse, attachments), their quest (offer, how it's going, turn it in),
    /// and goodbye. Escape, gamepad B or walking away ends it. Lines come from NpcDefinition and QuestDefinition and
    /// may use the bubble markup (BubbleText: [shout], &lt;shake&gt;...).
    /// </summary>
    public sealed class DialogueBox : MonoBehaviour
    {
        private enum Act : byte { None, Bye, Back, Sell, Shop, Workshop, AskWork, Accept, TurnIn, AskProgress }

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
        private QuestDefinition _quest;
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
            _quest = EconomyNet.QuestFrom(_npc.Definition, me, out QuestState state);
            if (_quest == null && !string.IsNullOrEmpty(_npc.Definition.Idle) && Random.value < 0.5f) line = _npc.Definition.Idle;
            Say(line);
            if (_quest != null)
            {
                if (state == QuestState.Ready) Add($"It's done: {_quest.Task}.", Act.TurnIn);
                else if (state == QuestState.Available) Add("Got any work for me?", Act.AskWork);
                else if (state == QuestState.Active) Add($"About \"{_quest.DisplayName}\"...", Act.AskProgress);
            }
            if (_npc.Definition.HasStation)
            {
                switch (_npc.Definition.Station)
                {
                    case CampStationKind.Sell:
                        int n = me.Inventory.PreyCount;
                        Add(n > 0 ? $"I've got {n} {(n == 1 ? "catch" : "catches")} to sell." : "(Nothing to sell yet.)", Act.Sell, n > 0);
                        break;
                    case CampStationKind.Shop: Add("Show me what you've got.", Act.Shop); break;
                    case CampStationKind.Workshop: Add("Let's look at my guns.", Act.Workshop); break;
                }
            }
            Add("Goodbye.", Act.Bye);
        }

        private void Choose(Act act)
        {
            switch (act)
            {
                case Act.Bye: End(); break;
                case Act.Back: Root(_npc.Definition.Greetings.Count > 0 ? _npc.Definition.Greetings[0] : "..."); break;
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
                case Act.AskWork:
                    Say($"{_quest.Offer}\n\n<b>{_quest.DisplayName}</b>: {_quest.Task}.\nReward: <color=#b8741a><b>{((BigDouble)_quest.RewardCoins).ToShortString()} coins</b></color>.");
                    Add("I'll do it.", Act.Accept);
                    Add("Not now.", Act.Back);
                    break;
                case Act.Accept:
                    EconomyNet.RequestAcceptQuest(_quest);
                    Say($"Good. {_quest.Task}, then come back to me.");
                    Add("Goodbye.", Act.Bye);
                    break;
                case Act.AskProgress:
                    Say($"{_quest.Waiting}\n\n{_quest.Task}: <b>{EconomyNet.ProgressOf(EconomyNet.Local, _quest)} / {_quest.Count}</b>.");
                    Add("Back.", Act.Back);
                    Add("Goodbye.", Act.Bye);
                    break;
                case Act.TurnIn:
                    EconomyNet.RequestTurnInQuest(_quest);
                    Say(_quest.Done);
                    Add("Goodbye.", Act.Bye);
                    break;
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
