#nullable enable
using System.Collections.Generic;
using System.Text;

namespace HowToHunt.Sim.Economy
{
    public enum ModifierKind : byte
    {
        /// <summary>Added to the base before multipliers.</summary>
        Additive,
        /// <summary>Multiplies the total.</summary>
        Multiplicative,
    }

    public readonly struct Modifier
    {
        public Modifier(string source, ModifierKind kind, double value)
        {
            Source = source;
            Kind = kind;
            Value = value;
        }

        /// <summary>Why this modifier exists ("Captured alive", "Sturdy net", "Butterfly mastery 2").</summary>
        public string Source { get; }
        public ModifierKind Kind { get; }
        public double Value { get; }
    }

    /// <summary>
    /// (base + Σ additive) × Π multiplicative, with every term's source kept so the UI can explain any number.
    /// Reused (Clear) to avoid allocations in hot paths.
    /// </summary>
    public sealed class ModifierStack
    {
        private readonly List<Modifier> _modifiers = new List<Modifier>(8);

        public IReadOnlyList<Modifier> Modifiers => _modifiers;

        public ModifierStack Clear()
        {
            _modifiers.Clear();
            return this;
        }

        public ModifierStack Add(string source, double value)
        {
            _modifiers.Add(new Modifier(source, ModifierKind.Additive, value));
            return this;
        }

        public ModifierStack Multiply(string source, double factor)
        {
            _modifiers.Add(new Modifier(source, ModifierKind.Multiplicative, factor));
            return this;
        }

        public BigDouble Evaluate(BigDouble baseValue)
        {
            BigDouble sum = baseValue;
            for (int i = 0; i < _modifiers.Count; i++)
                if (_modifiers[i].Kind == ModifierKind.Additive) sum += _modifiers[i].Value;
            for (int i = 0; i < _modifiers.Count; i++)
                if (_modifiers[i].Kind == ModifierKind.Multiplicative) sum *= _modifiers[i].Value;
            return sum;
        }

        /// <summary>"12 base, +2 Lucky charm, ×1.5 Captured alive, ×1.1 Mastery 1 = 23.1"</summary>
        public string Explain(BigDouble baseValue, string baseLabel = "base")
        {
            var sb = new StringBuilder();
            sb.Append(baseValue.ToShortString()).Append(' ').Append(baseLabel);
            foreach (Modifier m in _modifiers)
            {
                sb.Append(m.Kind == ModifierKind.Additive ? (m.Value >= 0 ? ", +" : ", ") : ", ×");
                sb.Append(m.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append(' ').Append(m.Source);
            }
            sb.Append(" = ").Append(Evaluate(baseValue).ToShortString());
            return sb.ToString();
        }
    }
}
