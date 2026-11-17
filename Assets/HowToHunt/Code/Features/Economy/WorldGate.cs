using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Economy
{
    /// <summary>
    /// Something in the world that changes when the party unlocks a world flag (a quest's reward): the fallen trees
    /// blocking the way east disappear (collision too), a sign appears. Its children are what it shows or hides; the
    /// collision world is rebuilt when they change, on every machine.
    /// </summary>
    public sealed class WorldGate : MonoBehaviour
    {
        [SerializeField] private string _flag;
        [Tooltip("On: the children are shown once the flag is set. Off: they're there until it is (a barricade).")]
        [SerializeField] private bool _showWhenSet;
        private bool _applied, _shown = true;

        public void Configure(string flag, bool showWhenSet)
        {
            _flag = flag;
            _showWhenSet = showWhenSet;
        }

        private void OnEnable()
        {
            EconomyNet.FlagsChanged += Apply;
            Apply();
        }

        private void OnDisable() => EconomyNet.FlagsChanged -= Apply;

        private void Apply()
        {
            bool set = EconomyNet.HasFlag(_flag);
            bool show = _showWhenSet ? set : !set;
            if (_applied && show == _shown) return;
            _applied = true;
            _shown = show;
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(show);
            SimWorld.Rebuild(); // blockers have collision brushes
        }
    }
}
