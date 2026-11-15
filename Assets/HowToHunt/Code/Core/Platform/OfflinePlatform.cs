using System;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>Used when Steam isn't running: solo play works, online play is disabled with a clear reason.</summary>
    public sealed class OfflinePlatform : IPlatformServices
    {
        public OfflinePlatform(string reason)
        {
            OfflineReason = reason;
            string id = PlayerPrefs.GetString("hth_local_id", string.Empty);
            if (!ulong.TryParse(id, out ulong parsed) || parsed == 0)
            {
                byte[] bytes = Guid.NewGuid().ToByteArray();
                parsed = BitConverter.ToUInt64(bytes, 0) | 1UL;
                PlayerPrefs.SetString("hth_local_id", parsed.ToString());
                PlayerPrefs.Save();
            }
            LocalUserId = parsed;
            LocalUserName = Environment.UserName;
        }

        public string Name => "Offline";
        public bool IsOnline => false;
        public string OfflineReason { get; }
        public ulong LocalUserId { get; }
        public string LocalUserName { get; }
        public ILobbyService Lobbies => null;
        public void SetRichPresence(string key, string value) { }
        public void UnlockAchievement(string apiName) { }
        public void Tick() { }
        public void Shutdown() { }
    }
}
