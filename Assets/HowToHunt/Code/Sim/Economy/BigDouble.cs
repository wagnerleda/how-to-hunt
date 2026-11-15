#nullable enable
using System;
using System.Globalization;

namespace HowToHunt.Sim.Economy
{
    /// <summary>
    /// Big-number-safe value for incremental economies: mantissa in [1, 10) times 10^exponent (break_infinity style).
    /// Covers far beyond double's 1e308 range. Immutable, allocation-free arithmetic.
    /// </summary>
    public readonly struct BigDouble : IComparable<BigDouble>, IEquatable<BigDouble>
    {
        public readonly double Mantissa;
        public readonly long Exponent;

        public static readonly BigDouble Zero = new BigDouble(0, 0);
        public static readonly BigDouble One = new BigDouble(1, 0);

        private BigDouble(double mantissa, long exponent)
        {
            Mantissa = mantissa;
            Exponent = exponent;
        }

        public static BigDouble Create(double mantissa, long exponent) => Normalize(mantissa, exponent);

        public static implicit operator BigDouble(double value) => Normalize(value, 0);

        public static implicit operator BigDouble(long value) => Normalize(value, 0);

        public static implicit operator BigDouble(int value) => Normalize(value, 0);

        public bool IsZero => Mantissa == 0;
        public int Sign => Math.Sign(Mantissa);

        public double ToDouble()
        {
            if (Mantissa == 0) return 0;
            if (Exponent > 308) return Mantissa > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            if (Exponent < -324) return 0;
            return Mantissa * Math.Pow(10, Exponent);
        }

        private static BigDouble Normalize(double mantissa, long exponent)
        {
            if (mantissa == 0 || double.IsNaN(mantissa)) return Zero;
            if (double.IsInfinity(mantissa)) throw new OverflowException("BigDouble from infinity.");
            int shift = (int)Math.Floor(Math.Log10(Math.Abs(mantissa)));
            mantissa /= Math.Pow(10, shift);
            exponent += shift;
            // Floating error can leave 9.999999 or 10.0000001.
            if (Math.Abs(mantissa) >= 10) { mantissa /= 10; exponent++; }
            else if (Math.Abs(mantissa) < 1) { mantissa *= 10; exponent--; }
            return new BigDouble(mantissa, exponent);
        }

        public static BigDouble operator +(BigDouble a, BigDouble b)
        {
            if (a.IsZero) return b;
            if (b.IsZero) return a;
            if (a.Exponent < b.Exponent) (a, b) = (b, a);
            long diff = a.Exponent - b.Exponent;
            if (diff > 17) return a; // b is below a's precision
            return Normalize(a.Mantissa + b.Mantissa / Math.Pow(10, diff), a.Exponent);
        }

        public static BigDouble operator -(BigDouble a) => new BigDouble(-a.Mantissa, a.Exponent);

        public static BigDouble operator -(BigDouble a, BigDouble b) => a + (-b);

        public static BigDouble operator *(BigDouble a, BigDouble b) => a.IsZero || b.IsZero ? Zero : Normalize(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);

        public static BigDouble operator /(BigDouble a, BigDouble b)
        {
            if (b.IsZero) throw new DivideByZeroException();
            return a.IsZero ? Zero : Normalize(a.Mantissa / b.Mantissa, a.Exponent - b.Exponent);
        }

        public int CompareTo(BigDouble other)
        {
            if (Sign != other.Sign) return Sign.CompareTo(other.Sign);
            if (IsZero) return 0;
            int byExponent = Exponent.CompareTo(other.Exponent);
            if (byExponent != 0) return Sign > 0 ? byExponent : -byExponent;
            return Mantissa.CompareTo(other.Mantissa);
        }

        public bool Equals(BigDouble other) => CompareTo(other) == 0 && Math.Abs(Mantissa - other.Mantissa) < 1e-12;
        public override bool Equals(object? obj) => obj is BigDouble b && Equals(b);
        public override int GetHashCode() => HashCode.Combine(Math.Round(Mantissa, 10), Exponent);

        public static bool operator <(BigDouble a, BigDouble b) => a.CompareTo(b) < 0;
        public static bool operator >(BigDouble a, BigDouble b) => a.CompareTo(b) > 0;
        public static bool operator <=(BigDouble a, BigDouble b) => a.CompareTo(b) <= 0;
        public static bool operator >=(BigDouble a, BigDouble b) => a.CompareTo(b) >= 0;

        /// <summary>Floor with a tiny epsilon so 20 × 0.3 = 5.999… still gives 6.</summary>
        public BigDouble Floor() => Exponent >= 15 ? this : (BigDouble)Math.Floor(ToDouble() + 1e-9);

        private static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

        /// <summary>Player-facing: 999, 12.3K, 4.56M ... then scientific (1.23e36).</summary>
        public string ToShortString()
        {
            if (IsZero) return "0";
            if (Exponent < 3) return ToDouble().ToString("0.#", CultureInfo.InvariantCulture);
            long group = Exponent / 3;
            if (group < Suffixes.Length)
            {
                double scaled = Mantissa * Math.Pow(10, Exponent - group * 3);
                return scaled.ToString(scaled < 10 ? "0.##" : scaled < 100 ? "0.#" : "0", CultureInfo.InvariantCulture) + Suffixes[group];
            }
            return Mantissa.ToString("0.##", CultureInfo.InvariantCulture) + "e" + Exponent.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Lossless text for saves: "m;e".</summary>
        public string Serialize() => Mantissa.ToString("R", CultureInfo.InvariantCulture) + ";" + Exponent.ToString(CultureInfo.InvariantCulture);

        public static BigDouble Deserialize(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Zero;
            int i = text!.IndexOf(';');
            if (i < 0) return double.Parse(text, CultureInfo.InvariantCulture);
            return Normalize(double.Parse(text.Substring(0, i), CultureInfo.InvariantCulture), long.Parse(text.Substring(i + 1), CultureInfo.InvariantCulture));
        }

        public override string ToString() => ToShortString();
    }
}
