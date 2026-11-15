#nullable enable
using System;

namespace HowToHunt.Foundation
{
    /// <summary>
    /// Stable 32-bit id derived from a content key such as "prey.butterfly".
    /// The same key always yields the same id on every machine, so ids are safe to send over the network and to save.
    /// </summary>
    public readonly struct ContentId : IEquatable<ContentId>
    {
        public readonly uint Value;

        public ContentId(uint value) => Value = value;

        public static ContentId FromKey(string key) => new ContentId(StableHash.Fnv1a32(key));

        public bool IsValid => Value != 0;

        public bool Equals(ContentId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is ContentId other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => $"#{Value:x8}";

        public static bool operator ==(ContentId a, ContentId b) => a.Value == b.Value;
        public static bool operator !=(ContentId a, ContentId b) => a.Value != b.Value;
    }

    /// <summary>Strongly-typed content id: a PreyId can't be passed where a ToolId is expected.</summary>
    public readonly struct ContentId<TKind> : IEquatable<ContentId<TKind>>
    {
        public readonly ContentId Id;

        public ContentId(ContentId id) => Id = id;

        public static ContentId<TKind> FromKey(string key) => new ContentId<TKind>(ContentId.FromKey(key));

        public bool IsValid => Id.IsValid;
        public bool Equals(ContentId<TKind> other) => Id == other.Id;
        public override bool Equals(object? obj) => obj is ContentId<TKind> other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => $"{typeof(TKind).Name}{Id}";

        public static bool operator ==(ContentId<TKind> a, ContentId<TKind> b) => a.Id == b.Id;
        public static bool operator !=(ContentId<TKind> a, ContentId<TKind> b) => a.Id != b.Id;
    }

    public static class StableHash
    {
        /// <summary>FNV-1a over UTF-16 code units, lower-cased. 0 is reserved for "invalid".</summary>
        public static uint Fnv1a32(string text)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (char raw in text)
            {
                char c = char.ToLowerInvariant(raw);
                hash ^= (byte)c;
                hash *= prime;
                hash ^= (byte)(c >> 8);
                hash *= prime;
            }
            return hash == 0 ? 1u : hash;
        }
    }
}
