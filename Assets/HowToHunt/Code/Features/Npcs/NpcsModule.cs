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
            EconomyHud.TalkPrompt = station => Prompt(Npc.At(station));
            EconomyHud.NearbyWanderer = point => Npc.NearWanderer(point);
            EconomyHud.WandererPrompt = c => Prompt(c as Npc);
            EconomyHud.TalkToWanderer = c => c is Npc npc && DialogueBox.Open(npc);
        }

        private static string Prompt(Npc npc)
        {
            if (npc == null) return null;
            string key = KeyBindings.DisplayName(GameAction.Interact);
            EconomyNet.QuestFrom(npc.Definition, EconomyNet.Local, out QuestState state);
            string note = state == QuestState.Ready ? "  (quest done!)" : state == QuestState.Available ? "  (has work for you)" : string.Empty;
            return $"[{key}] Talk to {npc.Definition.DisplayName}, the {npc.Definition.Role}{note}";
        }

        public void Shutdown()
        {
            EconomyHud.NearbyWanderer = null;
            EconomyHud.WandererPrompt = null;
            EconomyHud.TalkToWanderer = null;
            EconomyHud.TalkAt = null;
            EconomyHud.TalkPrompt = null;
        }
    }
}
