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
            r.Command("host", "host [local]: start a party. Online (Steam room code) when Steam runs, otherwise local.",
                args => NetSession.Host(args.Count > 0 && args[0] == "local"));
            r.Command("join", "join <code>: join a party by its 6-character code (Steam).",
                args =>
                {
                    if (args.Count == 0) r.Console.Warn("Usage: join <code>");
                    else NetSession.JoinByCode(args.ArgString);
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
        }

        public void Shutdown()
        {
            if (NetSession.State != SessionState.Offline) NetSession.Disconnect("Quit.");
        }

        private static string Describe()
        {
            var sb = new StringBuilder();
            sb.Append($"state {NetSession.State} via {NetSession.Transport}");
            if (!string.IsNullOrEmpty(NetSession.RoomCode)) sb.Append($", code {NetSession.RoomCode}");
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
