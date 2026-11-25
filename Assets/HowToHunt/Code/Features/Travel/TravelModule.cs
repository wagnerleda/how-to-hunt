using FishNet.Managing;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Foundation;
using HowToHunt.Net;
using HowToHunt.Travel;
using UnityEngine;

[assembly: FeatureModule(typeof(TravelModule))]

namespace HowToHunt.Travel
{
    /// <summary>
    /// Travel (owner, 2026-11-25): a sacred totem by each camp takes you to any part of the world you have been to,
    /// with a transition. Regions are content (RegionDefinition); what you've discovered is saved with your profile.
    /// </summary>
    public sealed class TravelModule : IFeatureModule
    {
        public string Id => "travel";
        public int Order => 30;

        public void Register(ModuleRegistrar r)
        {
            r.Cvar(TravelNet.Reach);
            r.Cvar(TravelNet.DiscoverInterval);
            r.Cvar(TravelTransition.Speed);
            r.Command("totem_discover_all", "totem_discover_all: every hunter has been everywhere (host, sv_cheats 1).", _ =>
            {
                if (NetSession.Manager == null || !NetSession.IsServer) { r.Console.Warn("Only the host can do that."); return; }
                foreach (int id in NetSession.Manager.ServerManager.Clients.Keys)
                    foreach (RegionDefinition region in ContentRegistry.All<RegionDefinition>()) EconomyNet.HostDiscover(id, region);
            }, CommandFlags.Cheat);
        }

        public void Initialize()
        {
            GameObject root = Game.Root.gameObject;
            root.AddComponent<TotemMenu>();
            root.AddComponent<TravelTransition>();
            root.AddComponent<TravelTicker>();
            NetSession.ManagerCreated += OnManagerCreated;
            NetSession.SessionEnded += TravelNet.Detach;
            EconomyHud.NearbyTotem = point => Totem.Near(point);
            EconomyHud.TotemPrompt = c => c is Totem t ? Prompt(t) : null;
            EconomyHud.UseTotem = c => c is Totem t && TotemMenu.Open(t);
        }

        public void Shutdown()
        {
            NetSession.ManagerCreated -= OnManagerCreated;
            NetSession.SessionEnded -= TravelNet.Detach;
            EconomyHud.NearbyTotem = null;
            EconomyHud.TotemPrompt = null;
            EconomyHud.UseTotem = null;
            TravelNet.Detach();
        }

        private static void OnManagerCreated(NetworkManager m) => TravelNet.Attach(m);

        private static string Prompt(Totem t) => $"[{KeyBindings.DisplayName(GameAction.Interact)}] Consult the totem of {(t.Region != null ? t.Region.DisplayName : "this land")}";
    }
}
