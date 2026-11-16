using HowToHunt.Core;
using HowToHunt.Platform;
using UnityEngine;

[assembly: FeatureModule(typeof(PlatformModule))]

namespace HowToHunt.Platform
{
    /// <summary>Starts Steam if possible, otherwise falls back to offline (solo still works).</summary>
    public sealed class PlatformModule : IFeatureModule
    {
        public const string SteamInEditorPref = "HowToHunt.SteamInEditor";

        public string Id => "platform";
        public int Order => -1000;

        public void Register(ModuleRegistrar registrar)
        {
            PlatformSettings settings = PlatformSettings.Load();
            SteamPlatform steam = null;
            string reason;
#if UNITY_EDITOR
            // In the Editor, Steam is off unless asked for (How to Hunt > Use Steam in Play Mode): once the Editor has
            // registered as app 480, Steam closes it whenever any other app-480 instance (a test build) exits.
            if (!UnityEditor.EditorPrefs.GetBool(SteamInEditorPref, false))
                reason = "Steam is off in the Editor (How to Hunt > Use Steam in Play Mode).";
            else
#endif
            steam = SteamPlatform.TryCreate(settings, out reason);
            if (steam != null)
            {
                Game.Platform = steam;
            }
            else
            {
                Debug.LogWarning($"[Platform] {reason}");
                Game.Platform = new OfflinePlatform(reason);
            }

            registrar.Command("steam_status", "Show Steam status and lobby.", _ =>
            {
                IPlatformServices p = Game.Platform;
                registrar.Console.Print($"{p.Name}: online={p.IsOnline} user={p.LocalUserName} ({p.LocalUserId}) {p.OfflineReason}");
                if (p.Lobbies != null && p.Lobbies.InLobby)
                {
                    LobbyInfo l = p.Lobbies.Current;
                    registrar.Console.Print($"lobby {l.LobbyId} host {l.HostName} owner {l.OwnerId} members {l.Members}/{l.MaxMembers} locked={l.Locked} proto={l.Protocol}");
                }
            });
        }

        public void Initialize() { }
        public void Shutdown() { }
    }
}
