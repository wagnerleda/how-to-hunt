using HowToHunt.Bosses;
using HowToHunt.Core;

[assembly: FeatureModule(typeof(BossesModule))]

namespace HowToHunt.Bosses
{
    /// <summary>
    /// Bosses (from the Pine Forest on): one big aggressive animal per biome, guarding its den. The fight itself is
    /// prey behaviour (PreyBrain.StepAggressive, host-simulated) and the reward is economy (every hunter who hurt it
    /// shares it); this feature is what players see of it: the boss bar and the announcements. ADR 0014.
    /// </summary>
    public sealed class BossesModule : IFeatureModule
    {
        public string Id => "bosses";
        public int Order => 40;

        public void Register(ModuleRegistrar r) => r.Cvar(BossHud.BarRange);

        public void Initialize() => Game.Root.gameObject.AddComponent<BossHud>();

        public void Shutdown() { }
    }
}
