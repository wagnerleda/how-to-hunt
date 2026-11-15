#nullable enable
using System;
using System.Globalization;

namespace HowToHunt.Foundation
{
    [Flags]
    public enum CvarFlags
    {
        None = 0,
        /// <summary>Saved to the player's config file.</summary>
        Archive = 1 << 0,
        /// <summary>Only changeable when sv_cheats is 1.</summary>
        Cheat = 1 << 1,
        /// <summary>Owned by the host; the host's value is pushed to every client (movement, rules).</summary>
        Replicated = 1 << 2,
        /// <summary>Cannot be changed from the console.</summary>
        ReadOnly = 1 << 3,
    }

    /// <summary>A console variable: a named, typed, live-tunable value with a default.</summary>
    public abstract class Cvar
    {
        protected Cvar(string name, string description, CvarFlags flags)
        {
            Name = name;
            Description = description;
            Flags = flags;
        }

        public string Name { get; }
        public string Description { get; }
        public CvarFlags Flags { get; }

        /// <summary>Raised after the value changes.</summary>
        public event Action<Cvar>? Changed;

        public abstract string ValueString { get; }
        public abstract string DefaultString { get; }
        public bool IsDefault => ValueString == DefaultString;

        /// <summary>Parses and sets the value. Returns false when the text is not a valid value.</summary>
        public abstract bool TrySetFromString(string text);

        public abstract void Reset();

        protected void RaiseChanged() => Changed?.Invoke(this);

        protected static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    }

    public sealed class FloatCvar : Cvar
    {
        private float _value;

        public FloatCvar(string name, float defaultValue, string description, CvarFlags flags = CvarFlags.None,
            float min = float.NegativeInfinity, float max = float.PositiveInfinity)
            : base(name, description, flags)
        {
            Default = defaultValue;
            Min = min;
            Max = max;
            _value = Clamp(defaultValue);
        }

        public float Default { get; }
        public float Min { get; }
        public float Max { get; }

        public float Value
        {
            get => _value;
            set
            {
                float v = Clamp(value);
                if (v.Equals(_value)) return;
                _value = v;
                RaiseChanged();
            }
        }

        public override string ValueString => _value.ToString("R", Inv);
        public override string DefaultString => Clamp(Default).ToString("R", Inv);

        public override bool TrySetFromString(string text)
        {
            if (!float.TryParse(text, NumberStyles.Float, Inv, out float v) || float.IsNaN(v)) return false;
            Value = v;
            return true;
        }

        public override void Reset() => Value = Default;

        private float Clamp(float v) => v < Min ? Min : v > Max ? Max : v;

        public static implicit operator float(FloatCvar c) => c._value;
    }

    public sealed class IntCvar : Cvar
    {
        private int _value;

        public IntCvar(string name, int defaultValue, string description, CvarFlags flags = CvarFlags.None,
            int min = int.MinValue, int max = int.MaxValue)
            : base(name, description, flags)
        {
            Default = defaultValue;
            Min = min;
            Max = max;
            _value = Clamp(defaultValue);
        }

        public int Default { get; }
        public int Min { get; }
        public int Max { get; }

        public int Value
        {
            get => _value;
            set
            {
                int v = Clamp(value);
                if (v == _value) return;
                _value = v;
                RaiseChanged();
            }
        }

        public override string ValueString => _value.ToString(Inv);
        public override string DefaultString => Clamp(Default).ToString(Inv);

        public override bool TrySetFromString(string text)
        {
            if (!int.TryParse(text, NumberStyles.Integer, Inv, out int v))
            {
                // Accept "1.0" style input for convenience.
                if (!float.TryParse(text, NumberStyles.Float, Inv, out float f)) return false;
                v = (int)Math.Round(f);
            }
            Value = v;
            return true;
        }

        public override void Reset() => Value = Default;

        private int Clamp(int v) => v < Min ? Min : v > Max ? Max : v;

        public static implicit operator int(IntCvar c) => c._value;
    }

    public sealed class BoolCvar : Cvar
    {
        private bool _value;

        public BoolCvar(string name, bool defaultValue, string description, CvarFlags flags = CvarFlags.None)
            : base(name, description, flags)
        {
            Default = defaultValue;
            _value = defaultValue;
        }

        public bool Default { get; }

        public bool Value
        {
            get => _value;
            set
            {
                if (value == _value) return;
                _value = value;
                RaiseChanged();
            }
        }

        public override string ValueString => _value ? "1" : "0";
        public override string DefaultString => Default ? "1" : "0";

        public override bool TrySetFromString(string text)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "1": case "true": case "on": case "yes": Value = true; return true;
                case "0": case "false": case "off": case "no": Value = false; return true;
                default: return false;
            }
        }

        public override void Reset() => Value = Default;

        public static implicit operator bool(BoolCvar c) => c._value;
    }

    public sealed class StringCvar : Cvar
    {
        private string _value;

        public StringCvar(string name, string defaultValue, string description, CvarFlags flags = CvarFlags.None)
            : base(name, description, flags)
        {
            Default = defaultValue;
            _value = defaultValue;
        }

        public string Default { get; }

        public string Value
        {
            get => _value;
            set
            {
                if (value == _value) return;
                _value = value;
                RaiseChanged();
            }
        }

        public override string ValueString => _value;
        public override string DefaultString => Default;

        public override bool TrySetFromString(string text)
        {
            Value = text;
            return true;
        }

        public override void Reset() => Value = Default;
    }
}
