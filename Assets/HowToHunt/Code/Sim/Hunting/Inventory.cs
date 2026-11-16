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
    /// The hotbar: a row of slots holding anything: tools, weapons, prey stacks. Everything can be dropped and picked
    /// up again; only prey can be sold. Pure and allocation-free except when resizing or selling.
    /// </summary>
    public sealed class Inventory
    {
        public const int MaxSlots = 12;

        private readonly ItemStack[] _slots = new ItemStack[MaxSlots];
        private int _capacity = 3;

        public int Capacity => _capacity;
        public ItemStack this[int slot] => slot >= 0 && slot < MaxSlots ? _slots[slot] : default;

        /// <summary>Changes the slot count. Items keep their slots; those beyond a smaller capacity are returned.</summary>
        public List<ItemStack> SetCapacity(int capacity)
        {
            var overflow = new List<ItemStack>();
            _capacity = Math.Clamp(capacity, 1, MaxSlots);
            for (int i = _capacity; i < MaxSlots; i++)
            {
                if (!_slots[i].IsEmpty) overflow.Add(_slots[i]);
                _slots[i] = default;
            }
            return overflow;
        }

        /// <summary>
        /// Adds an item, filling matching stacks first (up to <paramref name="maxStack"/>; tools never stack), then
        /// empty slots from the left.
        /// </summary>
        /// <returns>How many could not be added (0 = all fit).</returns>
        public int Add(in ItemStack item, int maxStack)
        {
            if (item.IsEmpty) return 0;
            int left = item.Count;
            maxStack = item.Kind == ItemKind.Tool ? 1 : Math.Max(1, maxStack);
            for (int i = 0; i < _capacity && left > 0 && maxStack > 1; i++)
            {
                ref ItemStack s = ref _slots[i];
                if (s.IsEmpty || !s.SameItem(item) || s.Count >= maxStack) continue;
                int add = Math.Min(left, maxStack - s.Count);
                s.Count = (ushort)(s.Count + add);
                left -= add;
            }
            for (int i = 0; i < _capacity && left > 0; i++)
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
            maxStack = item.Kind == ItemKind.Tool ? 1 : Math.Max(1, maxStack);
            for (int i = 0; i < _capacity; i++)
            {
                ItemStack s = _slots[i];
                if (s.IsEmpty) return true;
                if (maxStack > 1 && s.SameItem(item) && s.Count < maxStack) return true;
            }
            return false;
        }

        /// <summary>Takes one item from a slot (dropping, throwing).</summary>
        public bool TakeOne(int slot, out ItemStack taken)
        {
            taken = default;
            if (slot < 0 || slot >= _capacity) return false;
            ref ItemStack s = ref _slots[slot];
            if (s.IsEmpty) return false;
            taken = s;
            taken.Count = 1;
            s.Count--;
            if (s.Count == 0) s = default;
            return true;
        }

        /// <summary>Removes every prey stack (selling) and returns them. Tools stay.</summary>
        public List<ItemStack> TakeAllPrey()
        {
            var list = new List<ItemStack>();
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i].Kind != ItemKind.Prey) continue;
                if (_slots[i].Count > 0) list.Add(_slots[i]);
                _slots[i] = default;
            }
            return list;
        }

        public int PreyCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxSlots; i++) if (_slots[i].Kind == ItemKind.Prey) n += _slots[i].Count;
                return n;
            }
        }

        public bool HasTool(uint tool)
        {
            for (int i = 0; i < _capacity; i++) if (_slots[i].Kind == ItemKind.Tool && _slots[i].Content == tool && _slots[i].Count > 0) return true;
            return false;
        }

        /// <summary>Overwrites a slot (network restore).</summary>
        public void SetSlot(int slot, in ItemStack item)
        {
            if (slot >= 0 && slot < MaxSlots) _slots[slot] = item;
        }

        public void CopyFrom(Inventory other)
        {
            Array.Copy(other._slots, _slots, MaxSlots);
            _capacity = other._capacity;
        }

        public void Clear() => Array.Clear(_slots, 0, MaxSlots);
    }
}
