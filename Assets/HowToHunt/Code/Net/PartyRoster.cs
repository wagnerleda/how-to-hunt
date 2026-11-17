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
        /// <summary>Per-session secret only the host's own process knows (identifies the host's client).</summary>
        public ulong HostToken;
        /// <summary>The player's mask (opaque bytes the avatar feature reads; at most MaxMaskBytes).</summary>
        public byte[] Mask;
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

    /// <summary>Host → everyone, once per join: every member's mask, packed one after another.</summary>
    public struct MasksBroadcast : IBroadcast
    {
        public int[] Ids;
        public ushort[] Lengths;
        public byte[] Data;
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
        private static ulong _hostToken;

        public static IReadOnlyList<PartyMember> Members => MembersList;
        public static bool Locked { get; private set; }
        /// <summary>Bumped whenever the roster changes (UI caches by version).</summary>
        public static int Version { get; private set; }
        /// <summary>Reason the host gave when it turned us away or removed us (shown in the menu).</summary>
        public static string LastRejectReason { get; private set; } = string.Empty;

        public static event Action Changed;

        // --- masks: sent once on join (not with the 2 s roster) ------------------------------------------
        public const int MaxMaskBytes = 1024;
        private static readonly Dictionary<int, byte[]> ServerMasks = new Dictionary<int, byte[]>();
        private static readonly Dictionary<int, byte[]> ClientMasks = new Dictionary<int, byte[]>();

        /// <summary>Our own mask, sent in the hello (set by the avatar feature from its saved setting).</summary>
        public static byte[] LocalMask { get; set; }

        /// <summary>Bumped whenever masks arrive (avatars cache by version).</summary>
        public static int MasksVersion { get; private set; }

        public static bool TryGetMask(int clientId, out byte[] mask) => ClientMasks.TryGetValue(clientId, out mask);

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
            _hostToken = ((ulong)(uint)Guid.NewGuid().GetHashCode() << 32) | (uint)Guid.NewGuid().GetHashCode() | 1UL;
            LastRejectReason = string.Empty;
            Locked = false;
            manager.ServerManager.RegisterBroadcast<HelloBroadcast>(OnHello);
            manager.ServerManager.RegisterBroadcast<PingBroadcast>(OnPing);
            manager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            manager.ClientManager.RegisterBroadcast<RejectBroadcast>(OnReject);
            manager.ClientManager.RegisterBroadcast<RosterBroadcast>(OnRoster);
            manager.ClientManager.RegisterBroadcast<MasksBroadcast>(OnMasks);
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
                    _manager.ClientManager.UnregisterBroadcast<MasksBroadcast>(OnMasks);
                    _manager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                }
            }
            _manager = null;
            ServerMembers.Clear();
            ServerMasks.Clear();
            ClientMasks.Clear();
            MasksVersion++;
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
            if (!_manager.ServerManager.Clients.TryGetValue(clientId, out NetworkConnection conn) || IsHostConnection(conn)) return;
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

        /// <summary>The host's own client (its connection isn't always flagged local, depending on the transport).</summary>
        private static bool IsHostConnection(NetworkConnection conn) =>
            conn.IsLocalClient || (ServerMembers.TryGetValue(conn.ClientId, out PartyMember m) && m.IsHost);

        private static void OnHello(NetworkConnection conn, HelloBroadcast hello, Channel channel)
        {
            bool isHost = conn.IsLocalClient || (hello.HostToken != 0 && hello.HostToken == _hostToken);
            if (hello.Protocol != Game.ProtocolVersion)
            {
                Reject(conn, $"Update required: the host runs version {Game.ProtocolVersion}, you run {hello.Protocol}. Both players need the same build.");
                return;
            }
            if (!isHost && Locked)
            {
                Reject(conn, "This party is locked by the host.");
                return;
            }
            if (!isHost && ServerMembers.Count >= GameIdentity.MaxPartySize)
            {
                Reject(conn, $"This party is full ({GameIdentity.MaxPartySize} players).");
                return;
            }
            string name = string.IsNullOrWhiteSpace(hello.Name) ? $"Hunter {conn.ClientId}" : hello.Name.Trim();
            if (name.Length > 32) name = name.Substring(0, 32);
            ServerMembers[conn.ClientId] = new PartyMember(conn.ClientId, name, hello.SteamId, 0, isHost);
            if (!isHost) Game.Console.Print($"[party] {name} joined.");
            if (hello.Mask != null && hello.Mask.Length > 0 && hello.Mask.Length <= MaxMaskBytes) ServerMasks[conn.ClientId] = hello.Mask;
            else ServerMasks.Remove(conn.ClientId);
            PushMasks();
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
                ServerMasks.Remove(conn.ClientId);
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

        /// <summary>Everyone's masks to everyone (on a join: a few KB at most, never per tick).</summary>
        private static void PushMasks()
        {
            if (_manager == null || !_manager.IsServerStarted) return;
            int n = ServerMasks.Count, total = 0;
            foreach (byte[] m in ServerMasks.Values) total += m.Length;
            var msg = new MasksBroadcast { Ids = new int[n], Lengths = new ushort[n], Data = new byte[total] };
            int i = 0, at = 0;
            foreach (KeyValuePair<int, byte[]> kv in ServerMasks)
            {
                msg.Ids[i] = kv.Key;
                msg.Lengths[i] = (ushort)kv.Value.Length;
                Buffer.BlockCopy(kv.Value, 0, msg.Data, at, kv.Value.Length);
                at += kv.Value.Length;
                i++;
            }
            _manager.ServerManager.Broadcast(msg);
        }

        private static void OnMasks(MasksBroadcast msg, Channel channel)
        {
            if (msg.Ids == null || msg.Lengths == null || msg.Data == null || msg.Ids.Length != msg.Lengths.Length) return;
            ClientMasks.Clear();
            int at = 0;
            for (int i = 0; i < msg.Ids.Length; i++)
            {
                int len = msg.Lengths[i];
                if (len > MaxMaskBytes || at + len > msg.Data.Length) break;
                var mask = new byte[len];
                Buffer.BlockCopy(msg.Data, at, mask, 0, len);
                ClientMasks[msg.Ids[i]] = mask;
                at += len;
            }
            MasksVersion++;
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
                HostToken = _manager.IsServerStarted ? _hostToken : 0,
                Mask = LocalMask,
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
