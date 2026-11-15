#nullable enable
using System;

namespace HowToHunt.Foundation
{
    /// <summary>
    /// Six-character party codes in Crockford base32 (no I, L, O, U), so codes are easy to read aloud.
    /// Parsing is forgiving: lower case, spaces and dashes are ignored, O reads as 0 and I/L read as 1.
    /// </summary>
    public static class RoomCode
    {
        public const int Length = 6;
        public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string Generate(Random random)
        {
            Span<char> chars = stackalloc char[Length];
            for (int i = 0; i < Length; i++) chars[i] = Alphabet[random.Next(Alphabet.Length)];
            return new string(chars);
        }

        /// <summary>Normalizes user input to a canonical code. Returns false when it cannot be a valid code.</summary>
        public static bool TryNormalize(string? input, out string code)
        {
            code = string.Empty;
            if (input == null) return false;
            Span<char> chars = stackalloc char[Length];
            int n = 0;
            foreach (char raw in input)
            {
                if (raw == ' ' || raw == '-' || raw == '_') continue;
                char c = char.ToUpperInvariant(raw);
                if (c == 'O') c = '0';
                else if (c == 'I' || c == 'L') c = '1';
                if (Alphabet.IndexOf(c) < 0) return false;
                if (n == Length) return false;
                chars[n++] = c;
            }
            if (n != Length) return false;
            code = new string(chars);
            return true;
        }
    }
}
