using System.Collections.Generic;
using HowToHunt.Core;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Editor
{
    /// <summary>Validates every content asset. Same check runs in CI through ContentValidationTests.</summary>
    public static class ContentValidator
    {
        public readonly struct Problem
        {
            public Problem(string assetPath, string message)
            {
                AssetPath = assetPath;
                Message = message;
            }

            public string AssetPath { get; }
            public string Message { get; }
            public override string ToString() => $"{AssetPath}: {Message}";
        }

        public static List<Problem> ValidateAll()
        {
            var problems = new List<Problem>();
            var keys = new Dictionary<string, string>();
            var ids = new Dictionary<uint, string>();
            var errors = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ContentDefinition)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not ContentDefinition def) continue;
                    errors.Clear();
                    def.Validate(errors);
                    foreach (string e in errors) problems.Add(new Problem(path, e));
                    if (string.IsNullOrEmpty(def.Key)) continue;
                    if (keys.TryGetValue(def.Key, out string other)) problems.Add(new Problem(path, $"Duplicate key '{def.Key}' (also {other})."));
                    else keys[def.Key] = path;
                    if (ids.TryGetValue(def.Id.Value, out string clash) && clash != path) problems.Add(new Problem(path, $"Id hash collision with {clash}. Pick another key."));
                    else ids[def.Id.Value] = path;
                }
            }
            return problems;
        }

        [MenuItem("How to Hunt/Validate Content", priority = 10)]
        public static void ValidateFromMenu()
        {
            List<Problem> problems = ValidateAll();
            foreach (Problem p in problems) Debug.LogError("[Content] " + p, AssetDatabase.LoadMainAssetAtPath(p.AssetPath));
            if (problems.Count == 0) Debug.Log("[Content] All content valid.");
        }
    }
}
