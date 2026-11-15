using System;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using HowToHunt.Core;
using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Net
{
    public enum SessionState
    {
        Offline,
        Starting,
        /// <summary>Hosting a party (listen server + local client).</summary>
        Hosting,
        Connecting,
        /// <summary>Connected to someone else's party.</summary>
        Connected,
    }

    public enum SessionTransport
    {
        /// <summary>Steam P2P through Valve's relay (real online play).</summary>
        Steam,
        /// <summary>UDP on this machine or LAN (solo, editor multi-instance, bots, CI).</summary>
        Local,
    }

    /// <summary>Published whenever the session state changes. Message is user-facing.</summary>
    public struct SessionStateChanged
    {
        public SessionState State;
        public string Message;
        public string RoomCode;
    }

    /// <summary>
    /// Owns the network session: host, join by room code, local connect, disconnect.
    /// Gameplay never talks to Steam or Fish-Net transports directly; it reads <see cref="NetSession"/> and spawned objects.
    /// A fresh NetworkManager is created per session (from Resources/Net/NetworkManager) with the right transport.
    /// </summary>
    public static class NetSession
    {
        public const ushort LocalPort = 7770;
        private const float ConnectTimeoutSeconds = 20f;

        private static NetworkManager _prefab;
        private static float _connectDeadline;

        public static NetworkManager Manager { get; private set; }
        public static SessionState State { get; private set; } = SessionState.Offline;
        public static SessionTransport Transport { get; private set; }
        public static string RoomCode { get; private set; } = string.Empty;
        public static string LastMessage { get; private set; } = string.Empty;

        public static bool IsServer => Manager != null && Manager.IsServerStarted;
        public static bool IsClient => Manager != null && Manager.IsClientStarted;

        /// <summary>Raised on the main thread after a NetworkManager is created, before it starts (features hook events here).</summary>
        public static event Action<NetworkManager> ManagerCreated;

        /// <summary>Host a party. Online through Steam when available, otherwise a local session (solo still works).</summary>
        public static void Host(bool forceLocal = false)
        {
            if (State != SessionState.Offline) Disconnect("Starting a new session.");
            bool online = !forceLocal && Game.Platform != null && Game.Platform.IsOnline && Game.Platform.Lobbies != null;
            Transport = online ? SessionTransport.Steam : SessionTransport.Local;
            SetState(SessionState.Starting, online ? "Starting party..." : "Starting local session...");

            if (!CreateManager(Transport)) return;
            Transport t = Manager.TransportManager.Transport;
            t.SetMaximumClients(GameIdentity.MaxPartySize - 1);
            if (Transport == SessionTransport.Local)
            {
                t.SetServerBindAddress(string.Empty, IPAddressType.IPv4);
                t.SetPort(LocalPort);
            }

            if (!Manager.ServerManager.StartConnection())
            {
                Fail("Could not start the server. Is another copy already hosting on this PC?");
                return;
            }
            Manager.ClientManager.StartConnection();

            if (!online)
            {
                RoomCode = string.Empty;
                SetState(SessionState.Hosting, $"Hosting locally on port {LocalPort}" +
                                               (Game.Platform != null && !Game.Platform.IsOnline ? $" ({Game.Platform.OfflineReason})" : "."));
                return;
            }

            string code = Foundation.RoomCode.Generate(new System.Random(Guid.NewGuid().GetHashCode()));
            Game.Platform.Lobbies.CreateLobby(code, GameIdentity.MaxPartySize, result =>
            {
                if (!result.Ok)
                {
                    Disconnect(result.Message);
                    return;
                }
                RoomCode = code;
                Game.Platform.SetRichPresence("status", "Hunting");
                SetState(SessionState.Hosting, $"Party code: {code}");
            });
        }

        /// <summary>Join a party by room code over Steam.</summary>
        public static void JoinByCode(string input)
        {
            if (!Foundation.RoomCode.TryNormalize(input, out string code))
            {
                Fail($"'{input}' is not a valid party code (6 letters/digits).");
                return;
            }
            if (Game.Platform == null || !Game.Platform.IsOnline || Game.Platform.Lobbies == null)
            {
                Fail(Game.Platform?.OfflineReason ?? "Steam is not available.");
                return;
            }
            if (State != SessionState.Offline) Disconnect("Joining another party.");

            Transport = SessionTransport.Steam;
            SetState(SessionState.Connecting, $"Looking for party {code}...");
            Game.Platform.Lobbies.JoinByCode(code, result =>
            {
                if (State != SessionState.Connecting) return; // cancelled meanwhile
                if (!result.Ok)
                {
                    Fail(result.Message);
                    return;
                }
                if (!CreateManager(SessionTransport.Steam)) return;
                RoomCode = code;
                Manager.TransportManager.Transport.SetClientAddress(result.Info.OwnerId.ToString());
                _connectDeadline = Time.unscaledTime + ConnectTimeoutSeconds;
                SetState(SessionState.Connecting, $"Connecting to {result.Info.HostName}...");
                if (!Manager.ClientManager.StartConnection())
                    Fail("Could not start the connection.");
            });
        }

        /// <summary>Connect to a local/LAN host by address (loopback testing, second editor instance, bots).</summary>
        public static void ConnectLocal(string address, ushort port = LocalPort)
        {
            if (State != SessionState.Offline) Disconnect("Connecting elsewhere.");
            Transport = SessionTransport.Local;
            if (!CreateManager(SessionTransport.Local)) return;
            RoomCode = string.Empty;
            Manager.TransportManager.Transport.SetClientAddress(address);
            Manager.TransportManager.Transport.SetPort(port);
            _connectDeadline = Time.unscaledTime + ConnectTimeoutSeconds;
            SetState(SessionState.Connecting, $"Connecting to {address}:{port}...");
            if (!Manager.ClientManager.StartConnection())
                Fail("Could not start the connection.");
        }

        public static void Disconnect(string reason = "Disconnected.")
        {
            NetworkManager manager = Manager;
            Manager = null;
            if (manager != null)
            {
                Unhook(manager);
                if (manager.IsClientStarted) manager.ClientManager.StopConnection();
                if (manager.IsServerStarted) manager.ServerManager.StopConnection(true);
                UnityEngine.Object.Destroy(manager.gameObject);
            }
            Game.Platform?.Lobbies?.Leave();
            Game.Platform?.SetRichPresence("status", string.Empty);
            RoomCode = string.Empty;
            SetState(SessionState.Offline, reason);
        }

        /// <summary>Called every frame by the Net module (connection timeout).</summary>
        internal static void Update()
        {
            if (State == SessionState.Connecting && Manager != null && Time.unscaledTime > _connectDeadline)
                Fail("Connection timed out. The host may be offline or behind a blocked network.");
        }

        private static bool CreateManager(SessionTransport transport)
        {
            if (_prefab == null) _prefab = Resources.Load<NetworkManager>("Net/NetworkManager");
            if (_prefab == null)
            {
                Fail("Missing Resources/Net/NetworkManager prefab. Run How to Hunt > Rebuild Generated Assets.");
                return false;
            }

            // Instantiate under an inactive holder so we can pick the transport before Fish-Net initializes in Awake.
            var holder = new GameObject("NetworkManagerHolder");
            holder.SetActive(false);
            NetworkManager manager = UnityEngine.Object.Instantiate(_prefab, holder.transform);
            manager.name = $"NetworkManager ({transport})";

            Transport chosen = transport == SessionTransport.Steam
                ? (Transport)manager.GetComponent<FishySteamworks.FishySteamworks>()
                : manager.GetComponent<Tugboat>();
            if (chosen == null)
            {
                UnityEngine.Object.Destroy(holder);
                Fail($"NetworkManager prefab has no {transport} transport.");
                return false;
            }
            manager.GetComponent<FishNet.Managing.Transporting.TransportManager>().Transport = chosen;
            // Unused transports stay disabled: they are never initialized and must not tick.
            foreach (Transport t in manager.GetComponents<Transport>()) t.enabled = t == chosen;

            // Unparenting activates it: Fish-Net's Awake runs now, with the chosen transport.
            manager.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(holder);

            Manager = manager;
            Hook(manager);
            ManagerCreated?.Invoke(manager);
            return true;
        }

        private static void Hook(NetworkManager m)
        {
            m.ClientManager.OnClientConnectionState += OnClientConnectionState;
            m.ServerManager.OnServerConnectionState += OnServerConnectionState;
        }

        private static void Unhook(NetworkManager m)
        {
            m.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            m.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        private static void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (Manager == null) return;
            if (args.ConnectionState == LocalConnectionState.Started && State == SessionState.Connecting)
            {
                SetState(SessionState.Connected, string.IsNullOrEmpty(RoomCode) ? "Connected." : $"Joined party {RoomCode}.");
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && !Manager.IsServerStarted &&
                     (State == SessionState.Connected || State == SessionState.Connecting))
            {
                Disconnect(State == SessionState.Connected ? "The host ended the session or the connection was lost." : "Could not connect to the host.");
            }
        }

        private static void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (Manager == null) return;
            if (args.ConnectionState == LocalConnectionState.Stopped && State == SessionState.Hosting)
                Disconnect("Server stopped.");
        }

        private static void Fail(string message)
        {
            if (State != SessionState.Offline || Manager != null) Disconnect(message);
            else SetState(SessionState.Offline, message);
            Game.Console.Warn(message);
        }

        private static void SetState(SessionState state, string message)
        {
            State = state;
            LastMessage = message;
            Game.Console.Print($"[net] {message}");
            EventBus.Publish(new SessionStateChanged { State = state, Message = message, RoomCode = RoomCode });
        }

        internal static void ResetStatics()
        {
            Manager = null;
            State = SessionState.Offline;
            RoomCode = string.Empty;
            LastMessage = string.Empty;
            ManagerCreated = null;
        }
    }
}
