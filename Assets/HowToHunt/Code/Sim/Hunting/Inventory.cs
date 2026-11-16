#nullable enable
using System;
using System.Collections.Generic;

namespace HowToHunt.Sim.Hunting
{
    public enum ItemKind : byte { Empty, Tool, Prey }

    /// <summary>One inventory slot: a tool, or a stack of identical prey (same species, caught/killed, quality).</summary>
    public struct ItemStack : IEquatable<ItemStack>
    {
        public ItemKind Kind;
        /// <summary>ContentId value of the tool or prey.</summary>
        public uint Content;
        /// <summary>Prey: bit 0 = captured alive, bits 1-2 = CarcassQuality.</summary>
        public byte Flags;
        public ushort Count;

        public bool IsEmpty => Kind == ItemKind.Empty || Count == 0;
        public bool Captured => (Flags & 1) != 0;
        public CarcassQuality Quality => (CarcassQuality)((Flags >> 1) & 3);

        public static byte PreyFlags(bool captured, CarcassQuality quality) => (byte)((captured ? 1 : 0) | ((byte)quality << 1));
        public static ItemStack Tool(uint id) => new ItemStack { Kind = ItemKind.Tool, Content = id, Count = 1 };
        public static ItemStack Prey(uint id, bool captured, CarcassQuality quality, ushort count = 1)
            => new ItemStack { Kind = ItemKind.Prey, Content = id, Flags = PreyFlags(captured, quality), Count = count };

        public bool SameItem(in ItemStack other) => Kind == other.Kind && Content == other.Content && Flags == other.Flags;
        public bool Equals(ItemStack other) => SameItem(other) && Count == other.Count;
        public override bool Equals(object? obj) => obj is ItemStack s && Equals(s);
        public override int GetHashCode() => HashCode.Combine((int)Kind, Content, Flags, Count);
    }

    /// <summary>
    /// The hotbar: a row of slots. Owned tools always fill the first slots (in tool order); prey stacks use the rest.
    /// Capacity counts every slot, tools included. Pure and allocation-free except when reorganizing tools.
    /// </summary>
    public sealed class Inventory
    {
        public const int MaxSlots = 12;

        private readonly ItemStack[] _slots = new ItemStack[MaxSlots];
        private int _capacity = 6;
        private int _toolCount;

        public int Capacity => _capacity;
        public int ToolCount => _toolCount;
        public ItemStack this[int slot] => slot >= 0 && slot < MaxSlots ? _slots[slot] : default;

        /// <summary>Sets the tools (in order) and the slot count; prey are kept, packed after the tools.</summary>
        /// <returns>Prey stacks that no longer fit (the caller drops them on the ground).</returns>
        public List<ItemStack> Configure(IReadOnlyList<uint> tools, int capacity)
        {
            var prey = new List<ItemStack>();
            for (int i = 0; i < MaxSlots; i++)
                if (_slots[i].Kind == ItemKind.Prey && _slots[i].Count > 0) prey.Add(_slots[i]);
            Array.Clear(_slots, 0, MaxSlots);
            _capacity = Math.Clamp(capacity, 1, MaxSlots);
            _toolCount = Math.Min(tools.Count, _capacity);
            for (int i = 0; i < _toolCount; i++) _slots[i] = ItemStack.Tool(tools[i]);
            var overflow = new List<ItemStack>();
            int next = _toolCount;
            foreach (ItemStack p in prey)
            {
                if (next < _capacity) _slots[next++] = p;
                else overflow.Add(p);
            }
            return overflow;
        }

        /// <summary>
        /// Adds prey, filling existing matching stacks first (up to <paramref name="maxStack"/>), then empty slots.
        /// </summary>
        /// <returns>How many could not be added (0 = all fit).</returns>
        public int AddPrey(in ItemStack item, int maxStack)
        {
            int left = item.Count;
            maxStack = Math.Max(1, maxStack);
            for (int i = _toolCount; i < _capacity && left > 0; i++)
            {
                ref ItemStack s = ref _slots[i];
                if (s.Kind != ItemKind.Prey || !s.SameItem(item) || s.Count >= maxStack) continue;
                int add = Math.Min(left, maxStack - s.Count);
                s.Count = (ushort)(s.Count + add);
                left -= add;
            }
            for (int i = _toolCount; i < _capacity && left > 0; i++)
            {
                ref ItemStack s = ref _slots[i];
                if (!s.IsEmpty) continue;
                int add = Math.Min(left, maxStack);
                s = item;
                s.Count = (ushort)add;
                left -= add;
            }
            return left;
        }

        public bool CanAdd(in ItemStack item, int maxStack)
        {
            for (int i = _toolCount; i < _capacity; i++)
            {
                ItemStack s = _slots[i];
                if (s.IsEmpty) return true;
                if (s.Kind == ItemKind.Prey && s.SameItem(item) && s.Count < Math.Max(1, maxStack)) return true;
            }
            return false;
        }

        /// <summary>Takes one prey from a slot (throwing, dropping).</summary>
        public bool TakeOne(int slot, out ItemStack taken)
        {
            taken = default;
            if (slot < _toolCount || slot >= _capacity) return false;
            ref ItemStack s = ref _slots[slot];
            if (s.Kind != ItemKind.Prey || s.Count == 0) return false;
            taken = s;
            taken.Count = 1;
            s.Count--;
            if (s.Count == 0) s = default;
            return true;
        }

        /// <summary>Removes every prey stack (selling) and returns them.</summary>
        public List<ItemStack> TakeAllPrey()
        {
            var list = new List<ItemStack>();
            for (int i = _toolCount; i < MaxSlots; i++)
            {
                if (_slots[i].Kind == ItemKind.Prey && _slots[i].Count > 0) list.Add(_slots[i]);
                _slots[i] = default;
            }
            return list;
        }

        public int PreyCount
        {
            get
            {
                int n = 0;
                for (int i = _toolCount; i < MaxSlots; i++) if (_slots[i].Kind == ItemKind.Prey) n += _slots[i].Count;
                return n;
            }
        }

        /// <summary>Overwrites a slot (network/save restore). Use Configure first to set tools and capacity.</summary>
        public void SetSlot(int slot, in ItemStack item)
        {
            if (slot >= 0 && slot < MaxSlots) _slots[slot] = item;
        }

        public void CopyFrom(Inventory other)
        {
            Array.Copy(other._slots, _slots, MaxSlots);
            _capacity = other._capacity;
            _toolCount = other._toolCount;
        }

        public void Clear()
        {
            Array.Clear(_slots, 0, MaxSlots);
            _toolCount = 0;
        }
    }
}
