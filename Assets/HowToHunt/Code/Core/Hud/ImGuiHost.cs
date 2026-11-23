using System;
using UnityEngine;

namespace HowToHunt.Core
{
    /// <summary>
    /// Runs an IMGUI draw callback, only while enabled. Unity calls OnGUI on every enabled component that has one, twice
    /// a frame (layout and repaint), and each call allocates (~200 B) even when the method returns at once; four menus
    /// that were usually closed cost ~1.6 KB per frame and a garbage collection every couple of seconds. Menus keep
    /// their logic in Update and switch their host on only while they have something on screen.
    /// </summary>
    public sealed class ImGuiHost : MonoBehaviour
    {
        private Action _draw;

        /// <summary>Adds a host for <paramref name="draw"/> to the GameObject, switched off.</summary>
        public static ImGuiHost Attach(GameObject owner, Action draw)
        {
            var host = owner.AddComponent<ImGuiHost>();
            host._draw = draw;
            host.useGUILayout = true;
            host.enabled = false;
            return host;
        }

        /// <summary>Switches drawing on or off (allocation-free when nothing changes).</summary>
        public void Show(bool on)
        {
            if (enabled != on) enabled = on;
        }

        private void OnGUI() => _draw?.Invoke();
    }
}
