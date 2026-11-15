using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using HowToHunt.Foundation;

namespace HowToHunt.Net
{
    /// <summary>Host → client cvar values (Replicated cvars: movement physics, rules, sv_cheats).</summary>
    public struct CvarSyncBroadcast : IBroadcast
    {
        public string[] Names;
        public string[] Values;
    }

    /// <summary>
    /// Keeps every client's Replicated cvars equal to the host's, so client prediction runs the same physics as
    /// the host. Full snapshot when a client finishes loading, deltas on change. Clients can't change Replicated
    /// cvars while connected; they reset to defaults when leaving a party.
    /// </summary>
    internal static class CvarReplicator
    {
        private static readonly List<Cvar> Replicated = new List<Cvar>();
        private static NetworkManager _manager;

        public static void Attach(NetworkManager manager)
        {
            Detach();
            _manager = manager;
            Replicated.Clear();
            foreach (Cvar c in ConsoleSystem.Global.Cvars)
                if ((c.Flags & CvarFlags.Replicated) != 0) Replicated.Add(c);

            manager.ClientManager.RegisterBroadcast<CvarSyncBroadcast>(OnClientReceived);
            manager.SceneManager.OnClientLoadedStartScenes += OnClientLoaded;
            foreach (Cvar c in Replicated) c.Changed += OnCvarChanged;
        }

        public static void Detach()
        {
            if (_manager != null)
            {
                foreach (Cvar c in Replicated) c.Changed -= OnCvarChanged;
                if (_manager.ClientManager != null) _manager.ClientManager.UnregisterBroadcast<CvarSyncBroadcast>(OnClientReceived);
                if (_manager.SceneManager != null) _manager.SceneManager.OnClientLoadedStartScenes -= OnClientLoaded;
            }
            _manager = null;
            if (ConsoleSystem.Global.IsRemoteClient)
            {
                // Leaving someone else's party: our physics go back to our own defaults.
                ConsoleSystem.Global.IsRemoteClient = false;
                foreach (Cvar c in Replicated) c.Reset();
            }
        }

        /// <summary>Called when this machine becomes a remote client (not the host).</summary>
        public static void MarkRemoteClient() => ConsoleSystem.Global.IsRemoteClient = true;

        private static void OnClientLoaded(NetworkConnection conn, bool asServer)
        {
            if (!asServer || conn.IsLocalClient) return;
            var msg = new CvarSyncBroadcast { Names = new string[Replicated.Count], Values = new string[Replicated.Count] };
            for (int i = 0; i < Replicated.Count; i++)
            {
                msg.Names[i] = Replicated[i].Name;
                msg.Values[i] = Replicated[i].ValueString;
            }
            _manager.ServerManager.Broadcast(conn, msg);
        }

        private static void OnCvarChanged(Cvar cvar)
        {
            if (_manager == null || !_manager.IsServerStarted) return;
            _manager.ServerManager.Broadcast(new CvarSyncBroadcast { Names = new[] { cvar.Name }, Values = new[] { cvar.ValueString } });
            ConsoleSystem.Global.Print($"[net] host set {cvar.Name} = {cvar.ValueString}");
        }

        private static void OnClientReceived(CvarSyncBroadcast msg, Channel channel)
        {
            if (_manager == null || _manager.IsServerStarted || msg.Names == null) return;
            for (int i = 0; i < msg.Names.Length; i++)
                ConsoleSystem.Global.TrySet(msg.Names[i], msg.Values[i], fromHost: true);
        }
    }
}
