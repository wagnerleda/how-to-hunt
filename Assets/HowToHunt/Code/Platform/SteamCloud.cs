using HowToHunt.Core;
using Steamworks;

namespace HowToHunt.Platform
{
    /// <summary>Steam Remote Storage. With the shared dev app (480) quotas are tiny but enough for a profile.</summary>
    public sealed class SteamCloud : ICloudStorage
    {
        public bool Available => SteamRemoteStorage.IsCloudEnabledForAccount() && SteamRemoteStorage.IsCloudEnabledForApp();

        public bool Write(string fileName, byte[] data) => SteamRemoteStorage.FileWrite(fileName, data, data.Length);

        public byte[] Read(string fileName)
        {
            if (!SteamRemoteStorage.FileExists(fileName)) return null;
            int size = SteamRemoteStorage.GetFileSize(fileName);
            if (size <= 0) return null;
            var buffer = new byte[size];
            int read = SteamRemoteStorage.FileRead(fileName, buffer, size);
            return read == size ? buffer : null;
        }
    }
}
