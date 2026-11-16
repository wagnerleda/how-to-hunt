using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Net;
using HowToHunt.Sim.Combat;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Draws one prey at this machine's view tick (the same tick its shots and swings are checked at), with a
    /// procedural hop for ground prey and the Flap clip for flyers. A wound flashes the model red and shakes it for
    /// a moment (no falling over: it bolts right away). Pooled: hidden while the prey is gone.
    /// </summary>
    public sealed class PreyVisual : MonoBehaviour
    {
        private const float HitSeconds = 0.35f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color HitColor = new Color(1f, 0.12f, 0.08f);

        private Transform _model;
        private float _hopPhase;
        private float _hitAt = -10f;
        private bool _tinted;
        private Renderer[] _renderers;
        private Color[][] _baseColors;
        private MaterialPropertyBlock _block;

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
                v._renderers = model.GetComponentsInChildren<Renderer>();
                v._baseColors = new Color[v._renderers.Length][];
                for (int r = 0; r < v._renderers.Length; r++)
                {
                    Material[] mats = v._renderers[r].sharedMaterials;
                    v._baseColors[r] = new Color[mats.Length];
                    for (int m = 0; m < mats.Length; m++)
                        v._baseColors[r][m] = mats[m] != null && mats[m].HasProperty(BaseColorId) ? mats[m].GetColor(BaseColorId) : Color.white;
                }
                v._block = new MaterialPropertyBlock();
            }
            go.SetActive(false);
            return v;
        }

        /// <summary>A hit landed on this kind of prey near <paramref name="point"/>: flash the closest one.</summary>
        public static void FlashNear(PreyDefinition def, Vector3 point)
        {
            PreyVisual best = null;
            float bestSq = 4f; // within 2 m of the hit
            IReadOnlyList<PreyInstance> all = PreyWorld.All;
            for (int i = 0; i < all.Count; i++)
            {
                PreyInstance p = all[i];
                if (p.Def != def || p.Visual == null || !p.Visual.gameObject.activeSelf) continue;
                float sq = (p.Visual.transform.position - point).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p.Visual; }
            }
            if (best != null) best._hitAt = Time.time;
        }

        /// <summary>Called every frame by the hunting module for all prey (one loop, no per-object Update).</summary>
        public static void RenderAll()
        {
            if (NetSession.Manager == null) return;
            double tick = PreyClock.RenderTick(); // smooth, two snapshot gaps behind on clients
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
            float hit = Mathf.Clamp01(1f - (Time.time - _hitAt) / HitSeconds);
            if (_model != null)
            {
                Vector3 offset = Vector3.zero;
                Quaternion tilt = Quaternion.identity;
                if (!p.IsFlyer)
                {
                    float speed = p.RenderSpeed;
                    if (speed > 0.3f)
                    {
                        _hopPhase += Time.deltaTime * Mathf.Lerp(5f, 13f, Mathf.Clamp01(speed / 8f));
                        float hop = Mathf.Abs(Mathf.Sin(_hopPhase));
                        offset.y = hop * Mathf.Lerp(0.08f, 0.24f, Mathf.Clamp01(speed / 8f));
                        tilt = Quaternion.Euler(-Mathf.Cos(_hopPhase) * 12f, 0f, 0f);
                    }
                }
                if (hit > 0f)
                {
                    // Shake: a few centimetres of jitter that fades out.
                    float t = Time.time * 60f;
                    offset += new Vector3(Mathf.Sin(t * 1.3f), Mathf.Sin(t * 1.7f) * 0.5f, Mathf.Sin(t * 1.1f)) * (0.035f * hit);
                    tilt *= Quaternion.Euler(0f, 0f, Mathf.Sin(t) * 8f * hit);
                }
                _model.localPosition = Vector3.Lerp(_model.localPosition, offset, hit > 0f ? 1f : Time.deltaTime * 14f);
                _model.localRotation = Quaternion.Slerp(_model.localRotation, tilt, hit > 0f ? 1f : Time.deltaTime * 14f);
                Tint(hit);
            }
            transform.SetPositionAndRotation(position, rotation);
        }

        private void Tint(float hit)
        {
            if (_renderers == null || (hit <= 0f && !_tinted)) return;
            _tinted = hit > 0f;
            for (int r = 0; r < _renderers.Length; r++)
            {
                Color[] colors = _baseColors[r];
                for (int m = 0; m < colors.Length; m++)
                {
                    if (hit <= 0f)
                    {
                        _renderers[r].SetPropertyBlock(null, m);
                        continue;
                    }
                    _block.SetColor(BaseColorId, Color.Lerp(colors[m], HitColor, hit * 0.85f));
                    _renderers[r].SetPropertyBlock(_block, m);
                }
            }
        }
    }
}
