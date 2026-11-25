using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Hunting;
using HowToHunt.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Bosses
{
    /// <summary>
    /// The boss bar (top centre): the boss's title and health while it is alive and near you, for everyone in the
    /// party. Announces when a boss wakes in its den (respawns) and when it falls. Reads the prey every machine
    /// already has (health comes with the 16 Hz snapshots); no allocations per frame.
    /// </summary>
    public sealed class BossHud : MonoBehaviour
    {
        public static readonly FloatCvar BarRange = new FloatCvar("cl_boss_bar_range", 70f, "Show a boss's health bar when it is this close (m).", CvarFlags.Archive, 10f, 500f);

        private const float BarWidth = 520f, BarHeight = 14f;
        private static readonly Color Fill = new Color(0.86f, 0.25f, 0.18f, 0.95f), Back = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Title = new Color(1f, 0.92f, 0.78f, 0.97f);
        private static readonly Color EnragedFill = new Color(1f, 0.1f, 0.05f, 1f), EnragedTitle = new Color(1f, 0.55f, 0.4f, 1f);

        private Image _back, _fill;
        private TextMeshProUGUI _title;
        private PreyInstance _shown;
        private int _shownPct = -1;
        private bool _shownEnraged;
        private readonly Dictionary<ushort, bool> _wasAlive = new Dictionary<ushort, bool>();

        private void Awake()
        {
            _title = Hud.Text("Boss Title", new Vector2(0.5f, 1f), new Vector2(0f, -44f), 24f, TextAlignmentOptions.Center, Title);
            _title.rectTransform.sizeDelta = new Vector2(BarWidth + 200f, 34f); // sits just above the bar, not over it
            UiFonts.Use(_title).fontSize = 30f;
            _back = Hud.Rect("Boss Bar", new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(BarWidth, BarHeight), Back);
            _fill = Hud.Rect("Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(BarWidth, BarHeight), Fill);
            _fill.rectTransform.SetParent(_back.rectTransform, false);
            _fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            Show(null);
        }

        private void Update()
        {
            IReadOnlyList<PreyInstance> all = PreyWorld.All;
            PlayerMotor me = PlayerMotor.Local;
            PreyInstance nearest = null;
            float best = BarRange.Value * BarRange.Value;
            for (int i = 0; i < all.Count; i++)
            {
                PreyInstance p = all[i];
                if (!p.Def.IsBoss) continue;
                Announce(p);
                if (!p.Alive || me == null || p.Visual == null || !p.Visual.gameObject.activeSelf) continue;
                float d = (p.Visual.transform.position - me.transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = p; }
            }
            Show(nearest);
            if (nearest == null) return;
            int pct = Mathf.Clamp(Mathf.CeilToInt(nearest.Health / nearest.Def.MaxHealth * 100f), 0, 100);
            // Enraged (below its threshold): the bar turns bright red and says so.
            bool enraged = nearest.Def.EnrageBelow > 0f && nearest.Health > 0f && nearest.Health < nearest.Def.MaxHealth * nearest.Def.EnrageBelow;
            if (enraged != _shownEnraged)
            {
                _shownEnraged = enraged;
                _fill.color = enraged ? EnragedFill : Fill;
                _title.color = enraged ? EnragedTitle : Title;
                _title.SetText(enraged ? nearest.Def.BossTitle + " - ENRAGED" : nearest.Def.BossTitle);
                if (enraged) HuntFeedback.Message($"{nearest.Def.BossTitle} is enraged!", warning: true);
            }
            if (pct == _shownPct) return;
            _shownPct = pct;
            _fill.rectTransform.sizeDelta = new Vector2(BarWidth * pct / 100f, BarHeight);
        }

        /// <summary>A boss woke (respawned) or fell since we last looked: say so. Not at the start of a session.</summary>
        private void Announce(PreyInstance p)
        {
            bool alive = p.Alive;
            if (!_wasAlive.TryGetValue(p.Id, out bool was)) { _wasAlive[p.Id] = alive; return; }
            if (was == alive) return;
            _wasAlive[p.Id] = alive;
            if (PlayerMotor.Local == null) return;
            if (alive) HuntFeedback.Message($"{p.Def.BossTitle} smells the bait and wakes in its den!", warning: true);
            else if (p.Slept) HuntFeedback.Message($"{p.Def.BossTitle} went back to sleep in its den.");
            else HuntFeedback.Message($"{p.Def.BossTitle} has fallen!");
        }

        private void Show(PreyInstance boss)
        {
            bool on = boss != null;
            if (_back.gameObject.activeSelf != on) _back.gameObject.SetActive(on);
            if (_title.gameObject.activeSelf != on) _title.gameObject.SetActive(on);
            if (boss == _shown) return;
            _shown = boss;
            _shownPct = -1;
            _shownEnraged = false;
            _fill.color = Fill;
            _title.color = Title;
            if (on) _title.SetText(boss.Def.BossTitle);
        }

        private void OnDisable() => _wasAlive.Clear();
    }
}
