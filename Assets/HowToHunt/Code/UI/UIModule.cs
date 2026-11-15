using HowToHunt.Core;
using HowToHunt.UI;

[assembly: FeatureModule(typeof(UIModule))]

namespace HowToHunt.UI
{
    public sealed class UIModule : IFeatureModule
    {
        public string Id => "ui";
        public int Order => 900;

        public void Register(ModuleRegistrar r) { }

        public void Initialize() => Game.Root.gameObject.AddComponent<SessionMenu>();

        public void Shutdown() { }
    }
}
