using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Player;
using HowToHunt.Sim.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HowToHunt.Npcs
{
    /// <summary>
    /// Talking to a camp character: their name and role, what they say (typed out), and numbered choices (keys 1-4
    /// or the mouse). They offer their stall's business (sell, browse, attachments), their quest (offer, how it's
    /// going, turn it in), and goodbye. Esc or walking away ends it.
    /// </summary>
    public sealed class DialogueBox : MonoBehaviour
    {
        private enum Act : byte { None, Bye, Back, Sell, Shop, Workshop, AskWork, Accept, TurnIn, AskProgress }

        private struct Choice
        {
            public string Label;
            public Act Act;
            public bool Enabled;
        }

        private const float PanelW = 780f, PanelH = 236f, CharsPerSecond = 70f;

        private static DialogueBox _instance;
        /// <summary>The character the local player is talking to (null: nobody).</summary>
        public static Npc TalkingTo => _instance != null ? _instance._npc : null;

        private Npc _npc;
        private string _text = string.Empty;
        private float _shownChars;
        private readonly Choice[] _choices = new Choice[5];
        private int _choiceCount;
        private QuestDefinition _quest;
        private GUIStyle _name, _role, _body, _button, _hint;
        private Texture2D _white;

        private void Awake() => _instance = this;

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
            _npc = npc;
            Game.PushUiFocus(this);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var greetings = npc.Definition.Greetings;
            Root(greetings.Count > 0 ? greetings[Random.Range(0, greetings.Count)] : "...");
        }

        private void End()
        {
            _npc = null;
            Game.PopUiFocus(this);
            PlayerCamera.LockCursor();
        }

        // --- pages --------------------------------------------------------------------------------------------

        private void Say(string text)
        {
            _text = text ?? string.Empty;
            _shownChars = 0f;
            _choiceCount = 0;
        }

        private void Add(string label, Act act, bool enabled = true)
        {
            if (_choiceCount < _choices.Length) _choices[_choiceCount++] = new Choice { Label = label, Act = act, Enabled = enabled };
        }

        private void Root(string line)
        {
            Say(line);
            EconomyProfile me = EconomyNet.Local;
            _quest = EconomyNet.QuestFrom(_npc.Definition, me, out QuestState state);
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
            if (_quest == null && !string.IsNullOrEmpty(_npc.Definition.Idle) && _text == line && Random.value < 0.5f) _text = _npc.Definition.Idle;
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
                    Say($"{_quest.Offer}\n\n<b>{_quest.DisplayName}</b>: {_quest.Task}.  Reward: {((BigDouble)_quest.RewardCoins).ToShortString()} coins.");
                    Add("I'll do it.", Act.Accept);
                    Add("Not now.", Act.Back);
                    break;
                case Act.Accept:
                    EconomyNet.RequestAcceptQuest(_quest);
                    Say($"Good. {_quest.Task}, then come back to me.");
                    Add("Goodbye.", Act.Bye);
                    break;
                case Act.AskProgress:
                    Say($"{_quest.Waiting}\n\n{_quest.Task}: {EconomyNet.ProgressOf(EconomyNet.Local, _quest)} / {_quest.Count}.");
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
            _shownChars = Mathf.Min(_text.Length, _shownChars + Time.unscaledDeltaTime * CharsPerSecond);
            Keyboard kb = Keyboard.current;
            PlayerMotor me = PlayerMotor.Local;
            if (me == null || (me.transform.position - _npc.transform.position).sqrMagnitude > (_npc.TalkRange + 1.5f) * (_npc.TalkRange + 1.5f)) { End(); return; }
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { End(); return; }
            if (kb.spaceKey.wasPressedThisFrame) _shownChars = _text.Length;
            for (int i = 0; i < _choiceCount && i < 4; i++)
            {
                Key k = Key.Digit1 + i;
                if (kb[k].wasPressedThisFrame && _choices[i].Enabled) { Choose(_choices[i].Act); return; }
            }
        }

        private void OnGUI()
        {
            if (_npc == null) return;
            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.height / 720f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;
            var panel = new Rect(Mathf.Round((w - PanelW) * 0.5f), h - PanelH - 24f, PanelW, PanelH);
            GUI.color = new Color(0.1f, 0.09f, 0.08f, 0.92f);
            GUI.DrawTexture(panel, _white);
            GUI.color = new Color(0.95f, 0.75f, 0.35f, 1f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3f), _white);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + 22f, panel.y + 12f, 400f, 30f), _npc.Definition.DisplayName, _name);
            GUI.Label(new Rect(panel.x + 22f, panel.y + 40f, 400f, 20f), _npc.Definition.Role, _role);
            string shown = _text.Length <= (int)_shownChars ? _text : _text.Substring(0, (int)_shownChars);
            GUI.Label(new Rect(panel.x + 22f, panel.y + 64f, panel.width - 44f, 90f), shown, _body);

            float y = panel.y + PanelH - 22f - _choiceCount * 30f;
            float x = panel.x + panel.width - 22f - 360f;
            for (int i = 0; i < _choiceCount; i++)
            {
                GUI.enabled = _choices[i].Enabled;
                if (GUI.Button(new Rect(x, y + i * 30f, 360f, 26f), $"{i + 1}.  {_choices[i].Label}", _button)) { GUI.enabled = true; Choose(_choices[i].Act); break; }
                GUI.enabled = true;
            }
            GUI.Label(new Rect(panel.x + 22f, panel.yMax - 26f, 300f, 20f), "1-4 choose, Space skip, Esc leave", _hint);
            GUI.matrix = previous;
        }

        private void EnsureStyles()
        {
            if (_white != null) return;
            _white = Texture2D.whiteTexture;
            _name = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.92f, 0.75f) } };
            _role = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.85f, 0.7f, 0.45f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, richText = true, normal = { textColor = new Color(0.96f, 0.93f, 0.88f) } };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 8, 2, 2) };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.7f, 0.66f, 0.6f) } };
        }
    }
}
