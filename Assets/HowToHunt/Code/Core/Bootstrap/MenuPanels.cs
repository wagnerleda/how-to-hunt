using System;
using System.Collections.Generic;

namespace HowToHunt.Core
{
    /// <summary>
    /// Buttons features add to the main menu (shown before joining a party), each opening a panel of their own. While a
    /// panel is open the menu window steps aside (the panel draws itself and sets <see cref="Open"/>).
    /// </summary>
    public static class MenuPanels
    {
        public readonly struct Entry
        {
            public Entry(string label, Action open)
            {
                Label = label;
                Open = open;
            }

            public string Label { get; }
            public Action Open { get; }
        }

        private static readonly List<Entry> Entries = new List<Entry>();

        public static IReadOnlyList<Entry> Buttons => Entries;

        /// <summary>The panel currently open (null: none), so the menu hides its own window.</summary>
        public static object Open { get; set; }

        /// <summary>Adds a button, or replaces the one with that label (modules can initialise again in the Editor).</summary>
        public static void Add(string label, Action open)
        {
            Entries.RemoveAll(e => e.Label == label);
            Entries.Add(new Entry(label, open));
        }
    }
}
