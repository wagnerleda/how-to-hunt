#nullable enable
namespace HowToHunt.Foundation
{
    /// <summary>Constants that identify this game on shared infrastructure (Steam app 480 is shared by every developer).</summary>
    public static class GameIdentity
    {
        /// <summary>Lobby metadata key/value that marks a lobby as ours.</summary>
        public const string LobbyGameKey = "hth_game";
        public const string LobbyGameValue = "how-to-hunt";
        public const string LobbyProtocolKey = "hth_proto";
        public const string LobbyCodeKey = "hth_code";
        public const string LobbyLockedKey = "hth_locked";
        public const string LobbyHostNameKey = "hth_host";

        /// <summary>Simulation tick rate in Hz. See docs/adr/0004-tick-rate-and-budgets.md.</summary>
        public const int TickRate = 64;
        public const int MaxPartySize = 8;
    }
}
