using System;

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
        VersionMismatch,
        Full,
        Locked,
        PlatformError,
        Timeout,
    }

    public readonly struct LobbyInfo
    {
        public LobbyInfo(ulong lobbyId, ulong ownerId, string code, string protocol, string hostName, int members, int maxMembers, bool locked)
        {
            LobbyId = lobbyId;
            OwnerId = ownerId;
            Code = code;
            Protocol = protocol;
            HostName = hostName;
            Members = members;
            MaxMembers = maxMembers;
            Locked = locked;
        }

        public ulong LobbyId { get; }
        public ulong OwnerId { get; }
        public string Code { get; }
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

    /// <summary>Room-code lobbies. Implementations filter on the game key so shared app ids (480) don't collide.</summary>
    public interface ILobbyService
    {
        bool InLobby { get; }
        LobbyInfo Current { get; }

        void CreateLobby(string code, int maxMembers, Action<LobbyResult> done);

        /// <summary>Finds and joins the lobby with this code. Fails with VersionMismatch when builds differ.</summary>
        void JoinByCode(string code, Action<LobbyResult> done);

        void SetLocked(bool locked);

        void Leave();
    }
}
