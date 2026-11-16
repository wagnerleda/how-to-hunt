using System;
using HowToHunt.Core;
using Steamworks;
using UnityEngine;

namespace HowToHunt.Platform
{
    /// <summary>Steam implementation of <see cref="IPlatformServices"/> (Steamworks.NET).</summary>
    public sealed class SteamPlatform : IPlatformServices
    {
        private readonly SteamLobbyService _lobbies;
        private bool _initialized;

        private SteamPlatform()
        {
            _initialized = true;
            _lobbies = new SteamLobbyService();
            LocalUserId = SteamUser.GetSteamID().m_SteamID;
            LocalUserName = SteamFriends.GetPersonaName();
        }

        /// <summary>Tries to start Steam. Returns null and a human-readable reason when it can't.</summary>
        public static SteamPlatform TryCreate(PlatformSettings settings, out string reason)
        {
            reason = string.Empty;
            try
            {
                if (!Packsize.Test())
                {
                    reason = "Steamworks.NET packsize mismatch (wrong platform build).";
                    return null;
                }
                if (!DllCheck.Test())
                {
                    reason = "steam_api64.dll is missing or the wrong version.";
                    return null;
                }
#if !UNITY_EDITOR
                if (settings.RestartThroughSteam && SteamAPI.RestartAppIfNecessary(new AppId_t(settings.SteamAppId)))
                {
                    Application.Quit();
                    reason = "Relaunching through Steam.";
                    return null;
                }
#endif
                ESteamAPIInitResult result = SteamAPI.InitEx(out string error);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    reason = result == ESteamAPIInitResult.k_ESteamAPIInitResult_NoSteamClient
                        ? "Steam is not running. Start Steam to host or join online games; solo play still works."
                        : $"Steam failed to start ({result}: {error}).";
                    return null;
                }
                return new SteamPlatform();
            }
            catch (DllNotFoundException)
            {
                reason = "steam_api64.dll not found.";
                return null;
            }
            catch (Exception e)
            {
                reason = $"Steam failed to start: {e.Message}";
                return null;
            }
        }

        public string Name => "Steam";
        public bool IsOnline => _initialized;
        public string OfflineReason => _initialized ? string.Empty : "Steam shut down.";
        public ulong LocalUserId { get; }
        public string LocalUserName { get; }
        public ILobbyService Lobbies => _initialized ? _lobbies : null;
        public ICloudStorage Cloud => _initialized ? _cloud : null;
        private readonly SteamCloud _cloud = new SteamCloud();

        public void SetRichPresence(string key, string value)
        {
            if (_initialized) SteamFriends.SetRichPresence(key, value);
        }

        public void UnlockAchievement(string apiName)
        {
            if (!_initialized) return;
            SteamUserStats.SetAchievement(apiName);
            SteamUserStats.StoreStats();
        }

        public void Tick()
        {
            if (_initialized) SteamAPI.RunCallbacks();
        }

        public void Shutdown()
        {
            if (!_initialized) return;
            _lobbies.Leave();
            _lobbies.Dispose();
            SteamAPI.Shutdown();
            _initialized = false;
        }
    }
}
