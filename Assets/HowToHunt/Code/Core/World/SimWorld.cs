using HowToHunt.Sim.Collision;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HowToHunt.Core
{
    /// <summary>
    /// The collision world the movement simulation runs against, rebuilt from <see cref="SimBrush"/> components
    /// whenever a scene loads. Host and clients build the same world from the same scene.
    /// </summary>
    public static class SimWorld
    {
        private static readonly BrushWorld World = new BrushWorld();
        private static bool _hooked;

        public static ICollisionWorld Current => World;
        public static int BrushCount => World.BrushCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (!_hooked)
            {
                SceneManager.sceneLoaded += (_, __) => Rebuild();
                SceneManager.sceneUnloaded += _ => Rebuild();
                _hooked = true;
            }
            Rebuild();
        }

        public static void Rebuild()
        {
            World.Clear();
            SimBrush[] brushes = Object.FindObjectsByType<SimBrush>(FindObjectsInactive.Exclude);
            foreach (SimBrush b in brushes)
            {
                Brush brush = b.BuildBrush();
                if (brush != null) World.Add(brush);
            }
        }
    }
}
