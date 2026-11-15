using System;
using HowToHunt.Core;
using HowToHunt.Foundation;
using Steamworks;

namespace HowToHunt.Platform
{
    /// <summary>
    /// Room-code lobbies on Steam. App 480 is shared by every Steamworks developer, so every query filters on our
    /// game key; the protocol version is compared after the search so mismatched builds get "update required"
    /// instead of "not found".
    /// </summary>
    public sealed class SteamLobbyService : ILobbyService
    {
        private readonly CallResult<LobbyCreated_t> _created = new CallResult<LobbyCreated_t>();
        private readonly CallResult<LobbyMatchList_t> _matchList = new CallResult<LobbyMatchList_t>();
        private readonly CallResult<LobbyEnter_t> _entered = new CallResult<LobbyEnter_t>();
        private CSteamID _lobby = CSteamID.Nil;
        private LobbyInfo _current;

        public bool InLobby => _lobby != CSteamID.Nil;
        public LobbyInfo Current => _current;

        public void CreateLobby(string code, int maxMembers, Action<LobbyResult> done)
        {
            Leave();
            SteamAPICall_t call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxMembers);
            _created.Set(call, (result, ioFailure) =>
            {
                if (ioFailure || result.m_eResult != EResult.k_EResultOK)
                {
                    done(Fail(LobbyError.PlatformError, $"Steam could not create a lobby ({result.m_eResult})."));
                    return;
                }
                _lobby = new CSteamID(result.m_ulSteamIDLobby);
                SteamMatchmaking.SetLobbyData(_lobby, GameIdentity.LobbyGameKey, GameIdentity.LobbyGameValue);
                SteamMatchmaking.SetLobbyData(_lobby, GameIdentity.LobbyProtocolKey, Game.ProtocolVersion);
                SteamMatchmaking.SetLobbyData(_lobby, GameIdentity.LobbyCodeKey, code);
                SteamMatchmaking.SetLobbyData(_lobby, GameIdentity.LobbyLockedKey, "0");
                SteamMatchmaking.SetLobbyData(_lobby, GameIdentity.LobbyHostNameKey, SteamFriends.GetPersonaName());
                SteamMatchmaking.SetLobbyJoinable(_lobby, true);
                _current = Describe(_lobby);
                done(new LobbyResult(LobbyError.None, _current, $"Lobby {code} created."));
            });
        }

        public void JoinByCode(string code, Action<LobbyResult> done)
        {
            Leave();
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameIdentity.LobbyGameKey, GameIdentity.LobbyGameValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameIdentity.LobbyCodeKey, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(4);
            SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
            _matchList.Set(call, (list, ioFailure) =>
            {
                if (ioFailure)
                {
                    done(Fail(LobbyError.PlatformError, "Steam lobby search failed."));
                    return;
                }
                if (list.m_nLobbiesMatching == 0)
                {
                    done(Fail(LobbyError.NotFound, $"No party with code {code}. Check the code, and that the host is still online."));
                    return;
                }

                CSteamID candidate = SteamMatchmaking.GetLobbyByIndex(0);
                LobbyInfo info = Describe(candidate);
                if (info.Protocol != Game.ProtocolVersion)
                {
                    done(new LobbyResult(LobbyError.VersionMismatch, info,
                        $"Update required: the host runs version {info.Protocol}, you run {Game.ProtocolVersion}. Both players need the same version."));
                    return;
                }
                if (info.Locked)
                {
                    done(new LobbyResult(LobbyError.Locked, info, "This party is locked by the host."));
                    return;
                }
                if (info.MaxMembers > 0 && info.Members >= info.MaxMembers)
                {
                    done(new LobbyResult(LobbyError.Full, info, $"This party is full ({info.Members}/{info.MaxMembers})."));
                    return;
                }

                SteamAPICall_t join = SteamMatchmaking.JoinLobby(candidate);
                _entered.Set(join, (entered, joinFailure) =>
                {
                    var response = (EChatRoomEnterResponse)entered.m_EChatRoomEnterResponse;
                    if (joinFailure || response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                    {
                        done(new LobbyResult(response == EChatRoomEnterResponse.k_EChatRoomEnterResponseFull ? LobbyError.Full : LobbyError.PlatformError,
                            info, $"Could not join the party ({response})."));
                        return;
                    }
                    _lobby = new CSteamID(entered.m_ulSteamIDLobby);
                    _current = Describe(_lobby);
                    done(new LobbyResult(LobbyError.None, _current, $"Joined {_current.HostName}'s party."));
                });
            });
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
        }

        private static LobbyInfo Describe(CSteamID lobby)
        {
            return new LobbyInfo(
                lobby.m_SteamID,
                SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID,
                SteamMatchmaking.GetLobbyData(lobby, GameIdentity.LobbyCodeKey),
                SteamMatchmaking.GetLobbyData(lobby, GameIdentity.LobbyProtocolKey),
                SteamMatchmaking.GetLobbyData(lobby, GameIdentity.LobbyHostNameKey),
                SteamMatchmaking.GetNumLobbyMembers(lobby),
                SteamMatchmaking.GetLobbyMemberLimit(lobby),
                SteamMatchmaking.GetLobbyData(lobby, GameIdentity.LobbyLockedKey) == "1");
        }

        private static LobbyResult Fail(LobbyError error, string message) => new LobbyResult(error, default, message);
    }
}
