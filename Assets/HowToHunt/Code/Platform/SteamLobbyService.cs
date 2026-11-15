using System;
using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Foundation;
using Steamworks;

namespace HowToHunt.Platform
{
    /// <summary>
    /// Invite-based parties on Steam: the host creates a friends-only lobby and invites friends through the Steam
    /// overlay. Accepting an invite (or "Join game" on a friend) raises <see cref="JoinRequested"/>.
    /// App 480 is shared by every Steamworks developer, so every lobby is checked for our game key and protocol
    /// version before connecting.
    /// </summary>
    public sealed class SteamLobbyService : ILobbyService
    {
        private const string ConnectPrefix = "+connect_lobby ";

        private readonly CallResult<LobbyCreated_t> _created = new CallResult<LobbyCreated_t>();
        private readonly CallResult<LobbyEnter_t> _entered = new CallResult<LobbyEnter_t>();
        private readonly Callback<GameLobbyJoinRequested_t> _joinRequested;
        private readonly Callback<GameRichPresenceJoinRequested_t> _richPresenceJoin;
        private readonly Callback<LobbyDataUpdate_t> _lobbyDataUpdate;
        private readonly List<FriendParty> _friendParties = new List<FriendParty>();
        private readonly Dictionary<ulong, string> _pendingFriendLobbies = new Dictionary<ulong, string>();
        private CSteamID _lobby = CSteamID.Nil;
        private LobbyInfo _current;

        public SteamLobbyService()
        {
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _richPresenceJoin = Callback<GameRichPresenceJoinRequested_t>.Create(OnRichPresenceJoinRequested);
            _lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        }

        public bool InLobby => _lobby != CSteamID.Nil;
        public LobbyInfo Current => _current;
        public IReadOnlyList<FriendParty> FriendParties => _friendParties;

        public event Action<ulong> JoinRequested;

        public void CreateLobby(int maxMembers, Action<LobbyResult> done)
        {
            Leave();
            SteamAPICall_t call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers);
            _created.Set(call, (result, ioFailure) =>
            {
                if (ioFailure || result.m_eResult != EResult.k_EResultOK)
                {
                    done(Fail(LobbyError.PlatformError, $"Steam could not create a party ({result.m_eResult})."));
                    return;
                }
                _lobby = new CSteamID(result.m_ulSteamIDLobby);
                SteamMatchmaking.SetLobbyData(_lobby, GameIdentity.LobbyGameKey, GameIdentity.LobbyGameValue);
                SteamMatchmaking.SetLobbyData(_lobby, GameIdentity.LobbyProtocolKey, Game.ProtocolVersion);
                SteamMatchmaking.SetLobbyData(_lobby, GameIdentity.LobbyLockedKey, "0");
                SteamMatchmaking.SetLobbyData(_lobby, GameIdentity.LobbyHostNameKey, SteamFriends.GetPersonaName());
                SteamMatchmaking.SetLobbyJoinable(_lobby, true);
                SetConnectPresence();
                _current = Describe(_lobby);
                done(new LobbyResult(LobbyError.None, _current, "Party created."));
            });
        }

        public void JoinLobby(ulong lobbyId, Action<LobbyResult> done)
        {
            Leave();
            var target = new CSteamID(lobbyId);
            SteamAPICall_t join = SteamMatchmaking.JoinLobby(target);
            _entered.Set(join, (entered, failure) =>
            {
                var response = (EChatRoomEnterResponse)entered.m_EChatRoomEnterResponse;
                if (failure || response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                {
                    LobbyError error = response == EChatRoomEnterResponse.k_EChatRoomEnterResponseFull ? LobbyError.Full
                        : response == EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist ? LobbyError.NotFound
                        : LobbyError.PlatformError;
                    done(Fail(error, error == LobbyError.Full ? "This party is full."
                        : error == LobbyError.NotFound ? "That party no longer exists."
                        : $"Could not join the party ({response})."));
                    return;
                }

                var lobby = new CSteamID(entered.m_ulSteamIDLobby);
                LobbyInfo info = Describe(lobby);
                LobbyResult? problem = Validate(lobby, info);
                if (problem.HasValue)
                {
                    SteamMatchmaking.LeaveLobby(lobby);
                    done(problem.Value);
                    return;
                }
                _lobby = lobby;
                _current = info;
                SetConnectPresence();
                done(new LobbyResult(LobbyError.None, info, $"Joined {info.HostName}'s party."));
            });
        }

        public bool OpenInviteDialog()
        {
            if (!InLobby) return false;
            SteamFriends.ActivateGameOverlayInviteDialog(_lobby);
            // The call does nothing visible when the overlay is disabled or not hooked; tell the UI so it can explain.
            return SteamUtils.IsOverlayEnabled();
        }

        public void RefreshFriendParties()
        {
            _friendParties.Clear();
            _pendingFriendLobbies.Clear();
            AppId_t ourApp = SteamUtils.GetAppID();
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                CSteamID friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (!SteamFriends.GetFriendGamePlayed(friend, out FriendGameInfo_t game)) continue;
                if (game.m_gameID.AppID() != ourApp || !game.m_steamIDLobby.IsValid()) continue;
                ulong lobbyId = game.m_steamIDLobby.m_SteamID;
                if (lobbyId == _lobby.m_SteamID || _pendingFriendLobbies.ContainsKey(lobbyId)) continue;
                _pendingFriendLobbies[lobbyId] = SteamFriends.GetFriendPersonaName(friend);
                SteamMatchmaking.RequestLobbyData(game.m_steamIDLobby);
            }
        }

        public void SetLocked(bool locked)
        {
            if (!InLobby) return;
            SteamMatchmaking.SetLobbyData(_lobby, GameIdentity.LobbyLockedKey, locked ? "1" : "0");
            SteamMatchmaking.SetLobbyJoinable(_lobby, !locked);
            _current = Describe(_lobby);
        }

        public void Leave()
        {
            if (!InLobby) return;
            SteamMatchmaking.LeaveLobby(_lobby);
            _lobby = CSteamID.Nil;
            _current = default;
            SteamFriends.SetRichPresence("connect", null);
        }

        /// <summary>Unregisters Steam callbacks (platform shutdown).</summary>
        public void Dispose()
        {
            _joinRequested.Dispose();
            _richPresenceJoin.Dispose();
            _lobbyDataUpdate.Dispose();
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t e)
        {
            if (e.m_steamIDLobby.IsValid()) JoinRequested?.Invoke(e.m_steamIDLobby.m_SteamID);
        }

        private void OnRichPresenceJoinRequested(GameRichPresenceJoinRequested_t e)
        {
            string connect = e.m_rgchConnect ?? string.Empty;
            if (connect.StartsWith(ConnectPrefix, StringComparison.Ordinal) &&
                ulong.TryParse(connect.Substring(ConnectPrefix.Length).Trim(), out ulong lobbyId))
                JoinRequested?.Invoke(lobbyId);
        }

        private void OnLobbyDataUpdate(LobbyDataUpdate_t e)
        {
            if (e.m_ulSteamIDLobby != e.m_ulSteamIDMember) return; // member data, not lobby data
            if (!_pendingFriendLobbies.TryGetValue(e.m_ulSteamIDLobby, out string friendName)) return;
            _pendingFriendLobbies.Remove(e.m_ulSteamIDLobby);
            if (e.m_bSuccess == 0) return;
            var lobby = new CSteamID(e.m_ulSteamIDLobby);
            if (SteamMatchmaking.GetLobbyData(lobby, GameIdentity.LobbyGameKey) != GameIdentity.LobbyGameValue) return;
            LobbyInfo info = Describe(lobby);
            if (info.Locked) return;
            _friendParties.Add(new FriendParty(lobby.m_SteamID, friendName, info.Members, info.MaxMembers, info.Protocol == Game.ProtocolVersion));
        }

        private static LobbyResult? Validate(CSteamID lobby, LobbyInfo info)
        {
            if (SteamMatchmaking.GetLobbyData(lobby, GameIdentity.LobbyGameKey) != GameIdentity.LobbyGameValue)
                return new LobbyResult(LobbyError.NotOurGame, info, "That invite is not for How to Hunt.");
            if (info.Protocol != Game.ProtocolVersion)
                return new LobbyResult(LobbyError.VersionMismatch, info,
                    $"Update required: {info.HostName} runs version {info.Protocol}, you run {Game.ProtocolVersion}. Both players need the same build.");
            if (info.Locked)
                return new LobbyResult(LobbyError.Locked, info, "This party is locked by the host.");
            return null;
        }

        private void SetConnectPresence()
        {
            // Lets friends use "Join game" from the Steam friends list while our game is running.
            SteamFriends.SetRichPresence("connect", ConnectPrefix + _lobby.m_SteamID);
        }

        private static LobbyInfo Describe(CSteamID lobby)
        {
            return new LobbyInfo(
                lobby.m_SteamID,
                SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID,
                SteamMatchmaking.GetLobbyData(lobby, GameIdentity.LobbyProtocolKey),
                SteamMatchmaking.GetLobbyData(lobby, GameIdentity.LobbyHostNameKey),
                SteamMatchmaking.GetNumLobbyMembers(lobby),
                SteamMatchmaking.GetLobbyMemberLimit(lobby),
                SteamMatchmaking.GetLobbyData(lobby, GameIdentity.LobbyLockedKey) == "1");
        }

        private static LobbyResult Fail(LobbyError error, string message) => new LobbyResult(error, default, message);
    }
}
