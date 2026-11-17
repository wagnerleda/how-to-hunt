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
            r.Cvar(NetSession.LocalPortCvar);
            r.Cvar(FakeLag).Changed += _ => ApplyFakeNetwork(NetSession.Manager);
            r.Cvar(FakeLoss).Changed += _ => ApplyFakeNetwork(NetSession.Manager);
            NetSession.ManagerCreated += ApplyFakeNetwork;
            r.Command("disconnect", "Leave the current session.", _ => NetSession.Disconnect("You left the session."));
            r.Command("net_stats", "Print bandwidth (this machine) and party size.", _ =>
                r.Console.Print($"[net_stats] players {NetSession.PlayerCount} up {NetMeter.OutBytesPerSecond / 1024f:0.0} KB/s down {NetMeter.InBytesPerSecond / 1024f:0.0} KB/s fps {1f / UnityEngine.Mathf.Max(0.0001f, UnityEngine.Time.smoothDeltaTime):0}"));
            r.Command("status", "Show session state and connected players.", _ => r.Console.Print(Describe()));
        }

        public static readonly Foundation.IntCvar FakeLag = new Foundation.IntCvar("net_fakelag", 0, "Testing: add this many ms of latency to this machine's traffic.", Foundation.CvarFlags.None, 0, 1000);
        public static readonly Foundation.FloatCvar FakeLoss = new Foundation.FloatCvar("net_fakeloss", 0f, "Testing: drop this percent of this machine's packets.", Foundation.CvarFlags.None, 0f, 50f);

        private static void ApplyFakeNetwork(NetworkManager m)
        {
            if (m == null) return;
            var sim = m.TransportManager.LatencySimulator;
            bool on = FakeLag.Value > 0 || FakeLoss.Value > 0f;
            sim.SetLatency(FakeLag.Value);
            sim.SetPacketLoss(FakeLoss.Value / 100.0);
            sim.SetEnabled(on);
            if (on) Game.Console.Print($"[net] simulating {FakeLag.Value} ms latency, {FakeLoss.Value:0.#}% loss");
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
            if (NetSession.State != SessionState.Offline) NetSession.DisconnectNow("Quit.");
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
