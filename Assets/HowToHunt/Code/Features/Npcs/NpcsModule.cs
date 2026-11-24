using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Npcs;

[assembly: FeatureModule(typeof(NpcsModule))]

namespace HowToHunt.Npcs
{
    /// <summary>
    /// Camp life (Phase 6): characters at the stalls you talk to (dialogue boxes) instead of empty counters; they run
    /// their stall and hand out the quests (the quest log itself lives with the economy profile).
    /// </summary>
    public sealed class NpcsModule : IFeatureModule
    {
        public string Id => "npcs";
        public int Order => 20;

        public void Register(ModuleRegistrar r)
        {
        }

        public void Initialize()
        {
            Game.Root.gameObject.AddComponent<ChoiceList>();
            Game.Root.gameObject.AddComponent<NpcPresence>();
            Game.Root.gameObject.AddComponent<DialogueBox>();
            Game.Root.gameObject.AddComponent<QuestHud>();
            EconomyHud.TalkAt = station =>
            {
                Npc npc = Npc.At(station);
                return npc != null && DialogueBox.Open(npc);
            };
            EconomyHud.TalkPrompt = station =>
            {
                Npc npc = Npc.At(station);
                if (npc == null) return null;
                string key = KeyBindings.DisplayName(GameAction.Interact);
                EconomyNet.QuestFrom(npc.Definition, EconomyNet.Local, out QuestState state);
                string note = state == QuestState.Ready ? "  (quest done!)" : state == QuestState.Available ? "  (has work for you)" : string.Empty;
                return $"[{key}] Talk to {npc.Definition.DisplayName}, the {npc.Definition.Role}{note}";
            };
        }

        public void Shutdown()
        {
            EconomyHud.TalkAt = null;
            EconomyHud.TalkPrompt = null;
        }
    }
}
