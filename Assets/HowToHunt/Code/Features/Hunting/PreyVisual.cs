using HowToHunt.Core;
using HowToHunt.Net;
using HowToHunt.Sim.Combat;
using HowToHunt.Sim.Hunting;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Draws one prey at this machine's view tick (the same tick its shots and swings are checked at), with a
    /// procedural hop for ground prey and the Flap clip for flyers. Pooled: hidden while the prey is gone.
    /// </summary>
    public sealed class PreyVisual : MonoBehaviour
    {
        private Transform _model;
        private float _hopPhase;

        public static PreyVisual Create(PreyInstance prey)
        {
            var go = new GameObject($"Prey {prey.Id} ({prey.Def.Key})");
            var v = go.AddComponent<PreyVisual>();
            if (prey.Def.Visual != null)
            {
                GameObject model = Instantiate(prey.Def.Visual, go.transform, false);
                model.transform.localScale = Vector3.one * prey.Def.VisualScale;
                v._model = model.transform;
                Animator anim = model.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.cullingMode = AnimatorCullingMode.CullCompletely;
                    anim.speed = Random.Range(0.85f, 1.2f);
                }
            }
            go.SetActive(false);
            return v;
        }

        /// <summary>Called every frame by the hunting module for all prey (one loop, no per-object Update).</summary>
        public static void RenderAll()
        {
            if (NetSession.Manager == null) return;
            double tick = HitClock.ViewTick();
            bool host = NetSession.IsServer;
            var all = PreyWorld.All;
            for (int i = 0; i < all.Count; i++)
            {
                PreyInstance p = all[i];
                PreyVisual v = p.Visual;
                if (v == null) continue;
                EntityPose pose = default;
                bool show = p.Alive && p.TryPoseAt(tick, host, out pose);
                if (v.gameObject.activeSelf != show) v.gameObject.SetActive(show);
                if (!show) continue;
                v.Place(p, pose);
            }
        }

        private void Place(PreyInstance p, EntityPose pose)
        {
            Vector3 position = pose.Position.ToUnity();
            Quaternion rotation = Quaternion.Euler(0f, pose.Yaw, 0f);
            if (!p.IsFlyer && _model != null)
            {
                float speed = p.RenderSpeed;
                bool stunned = p.RenderAi == PreyAiState.Stunned;
                if (speed > 0.3f && !stunned)
                {
                    _hopPhase += Time.deltaTime * Mathf.Lerp(5f, 11f, Mathf.Clamp01(speed / 6f));
                    float hop = Mathf.Abs(Mathf.Sin(_hopPhase));
                    _model.localPosition = new Vector3(0f, hop * Mathf.Lerp(0.08f, 0.22f, Mathf.Clamp01(speed / 6f)), 0f);
                    _model.localRotation = Quaternion.Euler(-Mathf.Cos(_hopPhase) * 12f, 0f, 0f);
                }
                else
                {
                    _model.localPosition = Vector3.Lerp(_model.localPosition, Vector3.zero, Time.deltaTime * 10f);
                    _model.localRotation = stunned ? Quaternion.Euler(0f, 0f, 70f) : Quaternion.Slerp(_model.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                }
            }
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
