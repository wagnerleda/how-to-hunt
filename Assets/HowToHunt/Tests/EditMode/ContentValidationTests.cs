using System.Collections.Generic;
using HowToHunt.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HowToHunt.Tests
{
    /// <summary>Broken content fails CI: every ContentDefinition asset must validate, keys must be unique.</summary>
    public class ContentValidationTests
    {
        [Test]
        public void AllContentAssets_AreValid()
        {
            var failures = new List<string>();
            var keys = new HashSet<string>();
            var errors = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ContentDefinition)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not ContentDefinition def) continue;
                    errors.Clear();
                    def.Validate(errors);
                    foreach (string e in errors) failures.Add($"{path}: {e}");
                    if (!string.IsNullOrEmpty(def.Key) && !keys.Add(def.Key)) failures.Add($"{path}: duplicate key '{def.Key}'");
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void GeneratedPrefabs_Exist()
        {
            Assert.IsNotNull(Resources.Load("Net/NetworkManager"), "Run How to Hunt > Rebuild Generated Assets");
            Assert.IsNotNull(Resources.Load("Player/PlayerAvatar"), "Run How to Hunt > Rebuild Generated Assets");
            Assert.IsNotNull(Resources.Load<PlatformSettings>("PlatformSettings"));
        }
    }
}
