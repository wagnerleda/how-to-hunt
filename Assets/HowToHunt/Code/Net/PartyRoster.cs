using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using HowToHunt.Core;
using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Net
{
    /// <summary>Client → host on connect: who I am and which build I run.</summary>
    public struct HelloBroadcast : IBroadcast
    {
        public string Protocol;
        public string Name;
        public ulong SteamId;
    }

    /// <summary>Host → client before disconnecting it: why.</summary>
    public struct RejectBroadcast : IBroadcast
    {
        public string Reason;
    }

    /// <summary>Client → host every few seconds: my round-trip time.</summary>
    public struct PingBroadcast : IBroadcast
    {
        public ushort Ping;
    }

    /// <summary>Host → everyone: the party.</summary>
    public struct RosterBroadcast : IBroadcast
    {
        public int[] Ids;
        public string[] Names;
        public ulong[] SteamIds;
        public ushort[] Pings;
        public int HostId;
        public bool Locked;
    }

    public readonly struct PartyMember
    {
        public PartyMember(int clientId, string name, ulong steamId, int ping, bool isHost)
        {
            ClientId = clientId;
            Name = name;
            SteamId = steamId;
            Ping = ping;
            IsHost = isHost;
        }

        public int ClientId { get; }
        public string Name { get; }
        public ulong SteamId { get; }
        public int Ping { get; }
        public bool IsHost { get; }
    }

    /// <summary>
    /// The party: who's in it, their ping, and the join rules (version, lock, 8-player cap). The host is the
    /// authority; clients receive the roster. Join-in-progress is allowed unless the host locks the party.
    /// </summary>
    public static class PartyRoster
    {
        private const float PingInterval = 2f;
        private const float RosterInterval = 2f;

        private static readonly List<PartyMember> MembersList = new List<PartyMember>(GameIdentity.MaxPartySize);
        private static readonly Dictionary<int, PartyMember> ServerMembers = new Dictionary<int, PartyMember>();
        private static NetworkManager _manager;
        private static float _nextPing;
        private static float _nextRoster;

        public static IReadOnlyList<PartyMember> Members => MembersList;
        public static bool Locked { get; private set; }
        /// <summary>Bumped whenever the roster changes (UI caches by version).</summary>
        public static int Version { get; private set; }
        /// <summary>Reason the host gave when it turned us away or removed us (shown in the menu).</summary>
        public static string LastRejectReason { get; private set; } = string.Empty;

        public static event Action Changed;

        public static bool TryGet(int clientId, out PartyMember member)
        {
            for (int i = 0; i < MembersList.Count; i++)
            {
                if (MembersList[i].ClientId != clientId) continue;
                member = MembersList[i];
                return true;
            }
            member = default;
            return false;
        }

        internal static void Attach(NetworkManager manager)
        {
            Detach();
            _manager = manager;
            LastRejectReason = string.Empty;
            Locked = false;
            manager.ServerManager.RegisterBroadcast<HelloBroadcast>(OnHello);
            manager.ServerManager.RegisterBroadcast<PingBroadcast>(OnPing);
            manager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            manager.ClientManager.RegisterBroadcast<RejectBroadcast>(OnReject);
            manager.ClientManager.RegisterBroadcast<RosterBroadcast>(OnRoster);
            manager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        internal static void Detach()
        {
            if (_manager != null)
            {
                if (_manager.ServerManager != null)
                {
                    _manager.ServerManager.UnregisterBroadcast<HelloBroadcast>(OnHello);
                    _manager.ServerManager.UnregisterBroadcast<PingBroadcast>(OnPing);
                    _manager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
                }
                if (_manager.ClientManager != null)
                {
                    _manager.ClientManager.UnregisterBroadcast<RejectBroadcast>(OnReject);
                    _manager.ClientManager.UnregisterBroadcast<RosterBroadcast>(OnRoster);
                    _manager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                }
            }
            _manager = null;
            ServerMembers.Clear();
            MembersList.Clear();
            Locked = false;
            Version++;
            Changed?.Invoke();
        }

        /// <summary>Per-frame: ping reports (client) and periodic roster refresh (host).</summary>
        internal static void Update()
        {
            if (_manager == null) return;
            float now = Time.unscaledTime;
            if (_manager.IsClientStarted && now >= _nextPing)
            {
                _nextPing = now + PingInterval;
                long rtt = _manager.IsServerStarted ? 0 : _manager.TimeManager.RoundTripTime;
                _manager.ClientManager.Broadcast(new PingBroadcast { Ping = (ushort)Mathf.Clamp(rtt, 0, ushort.MaxValue) });
            }
            if (_manager.IsServerStarted && now >= _nextRoster)
            {
                _nextRoster = now + RosterInterval;
                PushRoster();
            }
        }

        // --- host controls --------------------------------------------------------------------------------

        public static void SetLocked(bool locked)
        {
            if (_manager == null || !_manager.IsServerStarted) return;
            Locked = locked;
            Game.Platform?.Lobbies?.SetLocked(locked);
            Game.Console.Print(locked ? "[party] Locked: nobody new can join." : "[party] Unlocked: friends can join.");
            PushRoster();
        }

        public static void Kick(int clientId)
        {
            if (_manager == null || !_manager.IsServerStarted) return;
            if (!_manager.ServerManager.Clients.TryGetValue(clientId, out NetworkConnection conn) || conn.IsLocalClient) return;
            Reject(conn, "You were removed from the party by the host.");
            Game.Console.Print($"[party] Kicked {(ServerMembers.TryGetValue(clientId, out PartyMember m) ? m.Name : "#" + clientId)}.");
        }

        /// <summary>Host is ending the party: tell everyone so they don't try to reconnect.</summary>
        internal static void NotifyEnding()
        {
            if (_manager == null || !_manager.IsServerStarted) return;
            _manager.ServerManager.Broadcast(new RejectBroadcast { Reason = "The host ended the party." }, requireAuthenticated: false);
        }

        // --- server side ----------------------------------------------------------------------------------

        private static void OnHello(NetworkConnection conn, HelloBroadcast hello, Channel channel)
        {
            if (hello.Protocol != Game.ProtocolVersion)
            {
                Reject(conn, $"Update required: the host runs version {Game.ProtocolVersion}, you run {hello.Protocol}. Both players need the same build.");
                return;
            }
            if (!conn.IsLocalClient && Locked)
            {
                Reject(conn, "This party is locked by the host.");
                return;
            }
            if (!conn.IsLocalClient && ServerMembers.Count >= GameIdentity.MaxPartySize)
            {
                Reject(conn, $"This party is full ({GameIdentity.MaxPartySize} players).");
                return;
            }
            string name = string.IsNullOrWhiteSpace(hello.Name) ? $"Hunter {conn.ClientId}" : hello.Name.Trim();
            if (name.Length > 32) name = name.Substring(0, 32);
            ServerMembers[conn.ClientId] = new PartyMember(conn.ClientId, name, hello.SteamId, 0, conn.IsLocalClient);
            if (!conn.IsLocalClient) Game.Console.Print($"[party] {name} joined.");
            PushRoster();
        }

        private static void OnPing(NetworkConnection conn, PingBroadcast ping, Channel channel)
        {
            if (!ServerMembers.TryGetValue(conn.ClientId, out PartyMember m)) return;
            ServerMembers[conn.ClientId] = new PartyMember(m.ClientId, m.Name, m.SteamId, ping.Ping, m.IsHost);
        }

        private static void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;
            if (ServerMembers.TryGetValue(conn.ClientId, out PartyMember m))
            {
                ServerMembers.Remove(conn.ClientId);
                Game.Console.Print($"[party] {m.Name} left.");
                PushRoster();
            }
        }

        private static void Reject(NetworkConnection conn, string reason)
        {
            _manager.ServerManager.Broadcast(conn, new RejectBroadcast { Reason = reason }, requireAuthenticated: false);
            ServerMembers.Remove(conn.ClientId);
            conn.Disconnect(false); // not immediate: lets the reason reach the client first
        }

        private static void PushRoster()
        {
            if (_manager == null || !_manager.IsServerStarted) return;
            int n = ServerMembers.Count;
            var msg = new RosterBroadcast
            {
                Ids = new int[n], Names = new string[n], SteamIds = new ulong[n], Pings = new ushort[n], HostId = -1, Locked = Locked,
            };
            int i = 0;
            foreach (PartyMember m in ServerMembers.Values)
            {
                msg.Ids[i] = m.ClientId;
                msg.Names[i] = m.Name;
                msg.SteamIds[i] = m.SteamId;
                msg.Pings[i] = (ushort)m.Ping;
                if (m.IsHost) msg.HostId = m.ClientId;
                i++;
            }
            _manager.ServerManager.Broadcast(msg);
        }

        // --- client side ----------------------------------------------------------------------------------

        private static void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started || _manager == null) return;
            IPlatformServices p = Game.Platform;
            string name = CoreCvars.PlayerName.Value;
            if (string.IsNullOrWhiteSpace(name)) name = p != null ? p.LocalUserName : Environment.UserName;
            _manager.ClientManager.Broadcast(new HelloBroadcast
            {
                Protocol = Game.ProtocolVersion,
                Name = name,
                SteamId = p != null && p.IsOnline ? p.LocalUserId : 0,
            });
            _nextPing = Time.unscaledTime + 1f;
        }

        private static void OnReject(RejectBroadcast msg, Channel channel)
        {
            LastRejectReason = msg.Reason;
            Game.Console.Warn("[party] " + msg.Reason);
        }

        private static void OnRoster(RosterBroadcast msg, Channel channel)
        {
            MembersList.Clear();
            if (msg.Ids != null)
                for (int i = 0; i < msg.Ids.Length; i++)
                    MembersList.Add(new PartyMember(msg.Ids[i], msg.Names[i], msg.SteamIds[i], msg.Pings[i], msg.Ids[i] == msg.HostId));
            MembersList.Sort((a, b) => a.IsHost != b.IsHost ? (a.IsHost ? -1 : 1) : a.ClientId.CompareTo(b.ClientId));
            Locked = msg.Locked;
            Version++;
            Changed?.Invoke();
        }
    }
}
