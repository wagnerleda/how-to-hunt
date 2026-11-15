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
    }

    /// <summary>
    /// Owns the network session: host, invite, join a friend (Steam lobby), local connect, disconnect.
    /// Gameplay never talks to Steam or Fish-Net transports directly; it reads <see cref="NetSession"/> and spawned objects.
    /// A fresh NetworkManager is created per session (from Resources/Net/NetworkManager) with the right transport.
    /// </summary>
    public static class NetSession
    {
        public const ushort LocalPort = 7770;
        private const float ConnectTimeoutSeconds = 20f;

        private static NetworkManager _prefab;
        private static float _connectDeadline;
        private const int MaxReconnects = 3;
        private const float ReconnectDelay = 2f;
        private static int _reconnectAttempt;
        private static float _endingAt;
        private static string _endingReason;
        private static float _reconnectAt;
        private static ulong _lastLobby;
        private static string _lastAddress;
        private static ushort _lastPort;

        public static NetworkManager Manager { get; private set; }
        public static SessionState State { get; private set; } = SessionState.Offline;
        public static SessionTransport Transport { get; private set; }
        /// <summary>Display name of the party host when connected to someone else's party.</summary>
        public static string HostName { get; private set; } = string.Empty;
        public static string LastMessage { get; private set; } = string.Empty;

        public static bool IsServer => Manager != null && Manager.IsServerStarted;
        public static bool IsClient => Manager != null && Manager.IsClientStarted;

        /// <summary>Connected players (host only; 0 otherwise).</summary>
        public static int PlayerCount => Manager != null && Manager.IsServerStarted ? Manager.ServerManager.Clients.Count : 0;

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
                HostName = string.Empty;
                SetState(SessionState.Hosting, $"Hosting locally on port {LocalPort}" +
                                               (Game.Platform != null && !Game.Platform.IsOnline ? $" ({Game.Platform.OfflineReason})" : "."));
                return;
            }

            Game.Platform.Lobbies.CreateLobby(GameIdentity.MaxPartySize, result =>
            {
                if (!result.Ok)
                {
                    Disconnect(result.Message);
                    return;
                }
                Game.Platform.SetRichPresence("status", "Hunting");
                SetState(SessionState.Hosting, "Party ready. Press Esc > Invite friends (or Shift+Tab > friend > Invite to game).");
            });
        }

        /// <summary>Opens the Steam invite dialog for the current party.</summary>
        public static bool OpenInviteDialog()
        {
            ILobbyService lobbies = Game.Platform?.Lobbies;
            return lobbies != null && lobbies.OpenInviteDialog();
        }

        /// <summary>Join a friend's party (Steam lobby id from an invite, "Join game", or the friends' parties list).</summary>
        public static void JoinLobby(ulong lobbyId) => JoinLobbyCore(lobbyId, reconnecting: false);

        private static void JoinLobbyCore(ulong lobbyId, bool reconnecting)
        {
            if (Game.Platform == null || !Game.Platform.IsOnline || Game.Platform.Lobbies == null)
            {
                Fail(Game.Platform?.OfflineReason ?? "Steam is not available.");
                return;
            }
            if (!reconnecting && State != SessionState.Offline) Disconnect("Joining another party.");
            if (!reconnecting) _reconnectAttempt = 0;
            _lastLobby = lobbyId;
            _lastAddress = null;

            Transport = SessionTransport.Steam;
            SetState(SessionState.Connecting, reconnecting ? $"Reconnecting ({_reconnectAttempt}/{MaxReconnects})..." : "Joining party...");
            Game.Platform.Lobbies.JoinLobby(lobbyId, result =>
            {
                if (State != SessionState.Connecting) return; // cancelled meanwhile
                if (!result.Ok)
                {
                    Fail(result.Message);
                    return;
                }
                if (!CreateManager(SessionTransport.Steam)) return;
                CvarReplicator.MarkRemoteClient();
                HostName = result.Info.HostName;
                Manager.TransportManager.Transport.SetClientAddress(result.Info.OwnerId.ToString());
                _connectDeadline = Time.unscaledTime + (_reconnectAttempt > 0 ? 6f : ConnectTimeoutSeconds);
                SetState(SessionState.Connecting, $"Connecting to {result.Info.HostName}...");
                if (!Manager.ClientManager.StartConnection())
                    Fail("Could not start the connection.");
            });
        }

        /// <summary>Connect to a local/LAN host by address (loopback testing, second editor instance, bots).</summary>
        public static void ConnectLocal(string address, ushort port = LocalPort) => ConnectLocalCore(address, port, reconnecting: false);

        private static void ConnectLocalCore(string address, ushort port, bool reconnecting)
        {
            if (!reconnecting && State != SessionState.Offline) Disconnect("Connecting elsewhere.");
            if (!reconnecting) _reconnectAttempt = 0;
            _lastLobby = 0;
            _lastAddress = address;
            _lastPort = port;
            Transport = SessionTransport.Local;
            if (!CreateManager(SessionTransport.Local)) return;
            CvarReplicator.MarkRemoteClient();
            HostName = string.Empty;
            Manager.TransportManager.Transport.SetClientAddress(address);
            Manager.TransportManager.Transport.SetPort(port);
            _connectDeadline = Time.unscaledTime + (_reconnectAttempt > 0 ? 6f : ConnectTimeoutSeconds);
            SetState(SessionState.Connecting, $"Connecting to {address}:{port}...");
            if (!Manager.ClientManager.StartConnection())
                Fail("Could not start the connection.");
        }

        /// <summary>Leave the session on purpose (cancels any reconnect).</summary>
        public static void Disconnect(string reason = "Disconnected.")
        {
            _reconnectAt = 0f;
            _reconnectAttempt = 0;
            if (Manager != null && Manager.IsServerStarted && PlayerCount > 1 && _endingAt <= 0f)
            {
                // Tell the party first, then shut down a moment later so the message is delivered.
                PartyRoster.NotifyEnding();
                _endingAt = Time.unscaledTime + 0.25f;
                _endingReason = reason;
                return;
            }
            _endingAt = 0f;
            TearDown(reason);
        }

        /// <summary>Leave right now (application quit): no deferred goodbye.</summary>
        public static void DisconnectNow(string reason)
        {
            _reconnectAt = 0f;
            _reconnectAttempt = 0;
            _endingAt = 0f;
            if (Manager != null && Manager.IsServerStarted) PartyRoster.NotifyEnding();
            TearDown(reason);
        }

        private static void TearDown(string reason)
        {
            NetworkManager manager = Manager;
            Manager = null;
            if (manager != null)
            {
                Unhook(manager);
                CvarReplicator.Detach();
                PartyRoster.Detach();
                if (manager.IsClientStarted) manager.ClientManager.StopConnection();
                if (manager.IsServerStarted) manager.ServerManager.StopConnection(true);
                UnityEngine.Object.Destroy(manager.gameObject);
            }
            Game.Platform?.Lobbies?.Leave();
            Game.Platform?.SetRichPresence("status", string.Empty);
            HostName = string.Empty;
            SetState(SessionState.Offline, reason);
        }

        /// <summary>Called every frame by the Net module (connection timeout).</summary>
        internal static void Update()
        {
            PartyRoster.Update();
            if (_endingAt > 0f && Time.unscaledTime >= _endingAt)
            {
                _endingAt = 0f;
                TearDown(_endingReason);
                return;
            }
            if (State == SessionState.Connecting && Manager != null && Time.unscaledTime > _connectDeadline)
            {
                if (_reconnectAttempt > 0) ScheduleReconnect("Reconnect attempt timed out.");
                else Fail("Connection timed out. The host may be offline or behind a blocked network.");
            }
            if (_reconnectAt > 0f && Time.unscaledTime >= _reconnectAt)
            {
                _reconnectAt = 0f;
                if (_lastLobby != 0) JoinLobbyCore(_lastLobby, reconnecting: true);
                else if (!string.IsNullOrEmpty(_lastAddress)) ConnectLocalCore(_lastAddress, _lastPort, reconnecting: true);
            }
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
            CvarReplicator.Attach(manager);
            PartyRoster.Attach(manager);
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
                bool wasReconnect = _reconnectAttempt > 0;
                _reconnectAttempt = 0;
                SetState(SessionState.Connected, wasReconnect ? "Reconnected." : string.IsNullOrEmpty(HostName) ? "Connected." : $"Joined {HostName}'s party.");
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && !Manager.IsServerStarted &&
                     (State == SessionState.Connected || State == SessionState.Connecting))
            {
                if (!string.IsNullOrEmpty(PartyRoster.LastRejectReason))
                    Disconnect(PartyRoster.LastRejectReason); // kicked, locked, full or wrong version: don't retry
                else if (State == SessionState.Connected || _reconnectAttempt > 0)
                    ScheduleReconnect("Connection to the host was lost.");
                else
                    Disconnect("Could not connect to the host.");
            }
        }

        /// <summary>Lost the host: tear down and retry the same party a few times before giving up.</summary>
        private static void ScheduleReconnect(string why)
        {
            if (_reconnectAttempt >= MaxReconnects)
            {
                Disconnect($"{why} Could not reconnect after {MaxReconnects} tries.");
                return;
            }
            _reconnectAttempt++;
            TearDown(why);
            _reconnectAt = Time.unscaledTime + ReconnectDelay;
            SetState(SessionState.Connecting, $"{why} Reconnecting ({_reconnectAttempt}/{MaxReconnects})...");
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
            EventBus.Publish(new SessionStateChanged { State = state, Message = message });
        }

        internal static void ResetStatics()
        {
            Manager = null;
            State = SessionState.Offline;
            HostName = string.Empty;
            LastMessage = string.Empty;
            ManagerCreated = null;
        }
    }
}
