using System.Collections.Generic;
using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Base for every piece of game content (prey, tool, biome, upgrade, ...). Content is data: designers add
    /// assets, not code. The key (e.g. "prey.butterfly") is permanent: it is hashed into the id used by saves
    /// and network messages.
    /// </summary>
    public abstract class ContentDefinition : ScriptableObject
    {
        [Tooltip("Permanent key, lower case, dotted: 'prey.butterfly'. Never rename after release.")]
        [SerializeField] private string _key = string.Empty;

        [SerializeField] private string _displayName = string.Empty;

        [TextArea(2, 4)]
        [SerializeField] private string _description = string.Empty;

        public string Key => _key;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Description => _description;
        public ContentId Id => ContentId.FromKey(_key);

        /// <summary>Prefix every key of this content type must start with, e.g. "prey.".</summary>
        public abstract string KeyPrefix { get; }

        /// <summary>Adds human-readable problems to <paramref name="errors"/>. Called by the editor validator and CI.</summary>
        public virtual void Validate(List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(_key)) errors.Add("Key is empty.");
            else
            {
                if (!_key.StartsWith(KeyPrefix, System.StringComparison.Ordinal)) errors.Add($"Key '{_key}' must start with '{KeyPrefix}'.");
                if (_key != _key.ToLowerInvariant() || _key.Contains(" ")) errors.Add($"Key '{_key}' must be lower case without spaces.");
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor helper for content factories and tests.</summary>
        public void EditorSetKey(string key) => _key = key;
#endif
    }
}
