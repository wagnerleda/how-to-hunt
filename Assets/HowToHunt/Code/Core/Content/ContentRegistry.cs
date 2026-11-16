using System.Collections.Generic;
using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// All content definitions under Resources/Content, by type and by stable id. Loaded once at boot; the same ids
    /// are used in saves and network messages.
    /// </summary>
    public static class ContentRegistry
    {
        private static readonly Dictionary<uint, ContentDefinition> ById = new Dictionary<uint, ContentDefinition>();
        private static readonly List<ContentDefinition> AllDefinitions = new List<ContentDefinition>();
        private static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (ContentDefinition def in Resources.LoadAll<ContentDefinition>("Content"))
            {
                if (string.IsNullOrEmpty(def.Key)) continue;
                if (ById.TryGetValue(def.Id.Value, out ContentDefinition clash))
                {
                    Debug.LogError($"[Content] Duplicate id {def.Key} vs {clash.Key}");
                    continue;
                }
                ById[def.Id.Value] = def;
                AllDefinitions.Add(def);
            }
            AllDefinitions.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        }

        public static T Get<T>(ContentId id) where T : ContentDefinition
        {
            EnsureLoaded();
            return ById.TryGetValue(id.Value, out ContentDefinition d) ? d as T : null;
        }

        public static T Get<T>(string key) where T : ContentDefinition => Get<T>(ContentId.FromKey(key));

        public static List<T> All<T>() where T : ContentDefinition
        {
            EnsureLoaded();
            var list = new List<T>();
            foreach (ContentDefinition d in AllDefinitions)
                if (d is T t) list.Add(t);
            return list;
        }

        public static void Reset()
        {
            ById.Clear();
            AllDefinitions.Clear();
            _loaded = false;
        }
    }
}
