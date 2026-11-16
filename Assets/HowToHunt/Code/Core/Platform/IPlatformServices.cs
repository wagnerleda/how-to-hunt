using System;
using System.Collections.Generic;

namespace HowToHunt.Core
{
    /// <summary>
    /// Everything the game needs from the platform (Steam today). Gameplay talks to this, never to Steam directly.
    /// </summary>
    public interface IPlatformServices
    {
        /// <summary>"Steam" or "Offline".</summary>
        string Name { get; }

        /// <summary>True when online play (lobbies, P2P) is possible.</summary>
        bool IsOnline { get; }

        /// <summary>Why online play is unavailable, for the UI (empty when online).</summary>
        string OfflineReason { get; }

        /// <summary>Steam ID, or a stable local id when Steam is absent.</summary>
        ulong LocalUserId { get; }

        string LocalUserName { get; }

        /// <summary>Lobby service, or null when offline.</summary>
        ILobbyService Lobbies { get; }

        /// <summary>Cloud saves, or null when unavailable.</summary>
        ICloudStorage Cloud { get; }

        void SetRichPresence(string key, string value);

        /// <summary>Unlocks an achievement by API name (no-op offline).</summary>
        void UnlockAchievement(string apiName);

        /// <summary>Pump platform callbacks. Called every frame.</summary>
        void Tick();

        void Shutdown();
    }

    public enum LobbyError
    {
        None,
        NotFound,
        NotOurGame,
        VersionMismatch,
        Full,
        Locked,
        PlatformError,
    }

    public readonly struct LobbyInfo
    {
        public LobbyInfo(ulong lobbyId, ulong ownerId, string protocol, string hostName, int members, int maxMembers, bool locked)
        {
            LobbyId = lobbyId;
            OwnerId = ownerId;
            Protocol = protocol;
            HostName = hostName;
            Members = members;
            MaxMembers = maxMembers;
            Locked = locked;
        }

        public ulong LobbyId { get; }
        public ulong OwnerId { get; }
        public string Protocol { get; }
        public string HostName { get; }
        public int Members { get; }
        public int MaxMembers { get; }
        public bool Locked { get; }
    }

    public readonly struct LobbyResult
    {
        public LobbyResult(LobbyError error, LobbyInfo info, string message)
        {
            Error = error;
            Info = info;
            Message = message;
        }

        public LobbyError Error { get; }
        public LobbyInfo Info { get; }
        public string Message { get; }
        public bool Ok => Error == LobbyError.None;
    }

    /// <summary>A friend currently hosting or in a How to Hunt party.</summary>
    public readonly struct FriendParty
    {
        public FriendParty(ulong lobbyId, string friendName, int members, int maxMembers, bool sameVersion)
        {
            LobbyId = lobbyId;
            FriendName = friendName;
            Members = members;
            MaxMembers = maxMembers;
            SameVersion = sameVersion;
        }

        public ulong LobbyId { get; }
        public string FriendName { get; }
        public int Members { get; }
        public int MaxMembers { get; }
        public bool SameVersion { get; }
    }

    /// <summary>
    /// Invite-based parties (friends-only lobbies). Implementations check our game key and protocol version
    /// because the dev app id (480) is shared by every developer.
    /// </summary>
    public interface ILobbyService
    {
        bool InLobby { get; }
        LobbyInfo Current { get; }

        /// <summary>Raised when the local player accepts an invite or clicks "Join game" on a friend (lobby id).</summary>
        event Action<ulong> JoinRequested;

        void CreateLobby(int maxMembers, Action<LobbyResult> done);

        /// <summary>Joins a lobby and validates it (our game, same version, not locked, not full).</summary>
        void JoinLobby(ulong lobbyId, Action<LobbyResult> done);

        /// <summary>Opens the platform's invite dialog for the current lobby. Returns false if it can't.</summary>
        bool OpenInviteDialog();

        /// <summary>Friends currently in a How to Hunt party. Refresh is asynchronous; the list updates as answers arrive.</summary>
        IReadOnlyList<FriendParty> FriendParties { get; }

        void RefreshFriendParties();

        void SetLocked(bool locked);

        void Leave();
    }
}
