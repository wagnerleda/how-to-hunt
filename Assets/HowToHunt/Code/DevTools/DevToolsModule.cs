using HowToHunt.Core;
using HowToHunt.DevTools;

[assembly: FeatureModule(typeof(DevToolsModule))]

namespace HowToHunt.DevTools
{
    public sealed class DevToolsModule : IFeatureModule
    {
        public string Id => "devtools";
        public int Order => 1000;

        public void Register(ModuleRegistrar r)
        {
            r.Cvar(DebugOverlay.ShowFps);
            r.Command("net_graph", "net_graph <0|1|2>: alias of cl_showfps.", args =>
                r.Console.Execute($"cl_showfps {(args.Count > 0 ? args[0] : "2")}"));
            PerfBench.Register(r.Console);
        }

        public void Initialize() => Game.Root.gameObject.AddComponent<DebugOverlay>();

        public void Shutdown() { }
    }
}
