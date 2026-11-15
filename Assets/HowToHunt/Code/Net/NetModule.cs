using System.Text;
using FishNet.Connection;
using FishNet.Managing;
using HowToHunt.Core;
using HowToHunt.Net;
using UnityEngine;

[assembly: FeatureModule(typeof(NetModule))]

namespace HowToHunt.Net
{
    /// <summary>Network session commands: host, join, connect, disconnect, status.</summary>
    public sealed class NetModule : IFeatureModule
    {
        public string Id => "net";
        public int Order => -900;

        public void Register(ModuleRegistrar r)
        {
            NetSession.ResetStatics();
            r.Command("host", "host [local]: start a party. Online (Steam, invite friends) when Steam runs, otherwise local.",
                args => NetSession.Host(args.Count > 0 && args[0] == "local"));
            r.Command("invite", "Open the Steam invite dialog for your party.", _ =>
            {
                if (!NetSession.OpenInviteDialog()) r.Console.Warn("No online party to invite to. Host one first.");
            });
            r.Command("connect_lobby", "connect_lobby <steam lobby id>: join a friend's party (used by Steam invites).", args =>
            {
                if (args.Count > 0 && ulong.TryParse(args[0], out ulong id)) NetSession.JoinLobby(id);
                else r.Console.Warn("Usage: connect_lobby <id>");
            });
            r.Command("connect", "connect <ip>[:port]: join a local/LAN host (testing).", args =>
            {
                string target = args.Count > 0 ? args[0] : "127.0.0.1";
                ushort port = NetSession.LocalPort;
                int colon = target.LastIndexOf(':');
                if (colon > 0 && ushort.TryParse(target.Substring(colon + 1), out ushort p))
                {
                    port = p;
                    target = target.Substring(0, colon);
                }
                NetSession.ConnectLocal(target, port);
            });
            r.Command("disconnect", "Leave the current session.", _ => NetSession.Disconnect("You left the session."));
            r.Command("status", "Show session state and connected players.", _ => r.Console.Print(Describe()));
        }

        public void Initialize()
        {
            Game.Root.gameObject.AddComponent<NetPump>();
            // Accepting a Steam invite or "Join game" on a friend joins that party.
            if (Game.Platform?.Lobbies != null) Game.Platform.Lobbies.JoinRequested += OnJoinRequested;
        }

        private static void OnJoinRequested(ulong lobbyId) => NetSession.JoinLobby(lobbyId);

        public void Shutdown()
        {
            if (Game.Platform?.Lobbies != null) Game.Platform.Lobbies.JoinRequested -= OnJoinRequested;
            if (NetSession.State != SessionState.Offline) NetSession.Disconnect("Quit.");
        }

        private static string Describe()
        {
            var sb = new StringBuilder();
            sb.Append($"state {NetSession.State} via {NetSession.Transport}");
            if (!string.IsNullOrEmpty(NetSession.HostName)) sb.Append($", host {NetSession.HostName}");
            NetworkManager m = NetSession.Manager;
            if (m != null)
            {
                sb.Append($", tick {m.TimeManager.Tick} @ {m.TimeManager.TickRate} Hz, rtt {m.TimeManager.RoundTripTime} ms");
                if (m.IsServerStarted)
                {
                    sb.Append($"\n{m.ServerManager.Clients.Count} connection(s):");
                    foreach (NetworkConnection c in m.ServerManager.Clients.Values)
                        sb.Append($"\n  #{c.ClientId} {(c.IsLocalClient ? "(host)" : c.GetAddress())}");
                }
            }
            return sb.ToString();
        }
    }
}
