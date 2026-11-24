using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// One module's piece of the player profile. Each slice has its own version and migrates itself, so features
    /// evolve their save data independently.
    /// </summary>
    public interface ISaveSlice
    {
        /// <summary>Stable key in the profile file, e.g. "economy".</summary>
        string Key { get; }
        int Version { get; }
        /// <summary>Serialize the current state (JSON recommended).</summary>
        string Save();
        /// <summary>Restore from data written by <paramref name="savedVersion"/> (migrate older versions here).</summary>
        void Load(string data, int savedVersion);
        /// <summary>No saved data: start fresh.</summary>
        void Reset();
    }

    /// <summary>
    /// The local player's profile: personal, independent of any party, keyed by Steam ID (or the local fallback id).
    /// Versioned JSON with per-slice migrations, written locally (atomic replace) and to Steam Cloud when available.
    /// </summary>
    public static class ProfileStore
    {
        public const int FileVersion = 1;
        private const float SaveDelay = 2f;

        [Serializable]
        private sealed class SliceRecord
        {
            public string key;
            public int version;
            public string data;
        }

        [Serializable]
        private sealed class ProfileFile
        {
            public int version;
            public long savedUtcTicks;
            public string gameVersion;
            public List<SliceRecord> slices = new List<SliceRecord>();
        }

        private static readonly List<ISaveSlice> Slices = new List<ISaveSlice>();
        private static float _saveAt = -1f;
        private static ulong _userId;

        public static string Directory => Path.Combine(Application.persistentDataPath, "profiles");
        public static string CloudFileName => "profile.json";
        /// <summary>Editor Play mode keeps its own profile and never touches Steam Cloud, so testing can't alter a real save.</summary>
        public static string LocalPath => Path.Combine(Directory, Application.isEditor ? $"{_userId}-editor.json" : $"{_userId}.json");

        private static ICloudStorage Cloud => Application.isEditor ? null : Game.Platform?.Cloud;

        /// <summary>Raised after the profile has been loaded (or reset).</summary>
        public static event Action Loaded;

        public static void Register(ISaveSlice slice)
        {
            if (Slices.Exists(s => s.Key == slice.Key)) throw new InvalidOperationException($"Save slice '{slice.Key}' registered twice.");
            Slices.Add(slice);
        }

        public static void Load(ulong userId)
        {
            _userId = userId;
            ProfileFile local = Read(File.Exists(LocalPath) ? File.ReadAllText(LocalPath) : null);
            ProfileFile cloud = null;
            ICloudStorage c = Cloud;
            if (c != null && c.Available)
            {
                byte[] bytes = c.Read(CloudFileName);
                if (bytes != null) cloud = Read(Encoding.UTF8.GetString(bytes));
            }
            ProfileFile chosen = local;
            if (cloud != null && (local == null || cloud.savedUtcTicks > local.savedUtcTicks)) chosen = cloud;

            foreach (ISaveSlice slice in Slices)
            {
                SliceRecord rec = chosen?.slices.Find(r => r.key == slice.Key);
                try
                {
                    if (rec == null) slice.Reset();
                    else slice.Load(rec.data, rec.version);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Profile] Could not load slice '{slice.Key}': {e.Message}. Starting it fresh; the old file is kept as a backup.");
                    BackupCorrupt();
                    slice.Reset();
                }
            }
            Game.Console.Print($"[profile] Loaded {(chosen == null ? "new profile" : chosen == cloud ? "from Steam Cloud" : "from disk")} for {userId}.");
            Loaded?.Invoke();
        }

        /// <summary>
        /// Start over (the main menu's "Reset progress"): every slice back to a new hunter's, saved at once (locally and
        /// to Steam Cloud) so the old progress can't come back. The previous file stays as the .bak next to it.
        /// </summary>
        public static void ResetAll()
        {
            foreach (ISaveSlice slice in Slices) slice.Reset();
            SaveNow();
            Game.Console.Print("[profile] Progress reset.");
            Loaded?.Invoke();
        }

        /// <summary>Ask for a save soon (coalesces bursts of changes).</summary>
        public static void MarkDirty()
        {
            if (_saveAt < 0f) _saveAt = Time.unscaledTime + SaveDelay;
        }

        /// <summary>Called every frame by the bootstrap.</summary>
        internal static void Update()
        {
            if (_saveAt > 0f && Time.unscaledTime >= _saveAt) SaveNow();
        }

        public static void SaveNow()
        {
            _saveAt = -1f;
            if (_userId == 0 || Game.TestInstance) return;
            var file = new ProfileFile { version = FileVersion, savedUtcTicks = DateTime.UtcNow.Ticks, gameVersion = Game.Version };
            foreach (ISaveSlice s in Slices) file.slices.Add(new SliceRecord { key = s.Key, version = s.Version, data = s.Save() });
            string json = JsonUtility.ToJson(file, true);
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                string tmp = LocalPath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(LocalPath)) File.Replace(tmp, LocalPath, LocalPath + ".bak");
                else File.Move(tmp, LocalPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profile] Local save failed: {e.Message}");
            }
            ICloudStorage c = Cloud;
            if (c != null && c.Available && !c.Write(CloudFileName, Encoding.UTF8.GetBytes(json)))
                Debug.LogWarning("[Profile] Steam Cloud write failed (local copy is saved).");
        }

        private static ProfileFile Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                ProfileFile f = JsonUtility.FromJson<ProfileFile>(json);
                return f != null && f.version <= FileVersion ? f : null;
            }
            catch
            {
                BackupCorrupt();
                return null;
            }
        }

        private static void BackupCorrupt()
        {
            try
            {
                if (File.Exists(LocalPath)) File.Copy(LocalPath, LocalPath + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", true);
            }
            catch
            {
                // best effort
            }
        }

        internal static void ResetStatics()
        {
            Slices.Clear();
            _saveAt = -1f;
            _userId = 0;
            Loaded = null;
        }
    }

    /// <summary>Platform cloud saves (Steam Remote Storage).</summary>
    public interface ICloudStorage
    {
        bool Available { get; }
        bool Write(string fileName, byte[] data);
        byte[] Read(string fileName);
    }
}
