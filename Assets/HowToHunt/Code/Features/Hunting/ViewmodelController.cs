using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Player;
using HowToHunt.Sim.Hunting;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// The tool in your hands: raised when equipped, bobs with your speed, lags slightly behind the view, and plays
    /// a procedural swing (blade), scoop (net) or recoil (firearm) when used. Models come from ToolDefinition.Viewmodel
    /// and are instantiated once per tool, then shown and hidden.
    /// </summary>
    public sealed class ViewmodelController : MonoBehaviour
    {
        private const float RaiseSeconds = 0.22f;

        private readonly Dictionary<ToolDefinition, GameObject> _models = new Dictionary<ToolDefinition, GameObject>();
        private Transform _holder;
        private Camera _camera;
        private ToolDefinition _tool;
        private GameObject _shown;
        private float _raise;
        private float _useTime = -10f;
        private float _bobPhase;
        private float _bobAmount;
        private Quaternion _lag = Quaternion.identity;
        private Quaternion _lastCameraRotation = Quaternion.identity;

        private void Awake()
        {
            _holder = new GameObject("Viewmodel").transform;
            PlayerTools.LocalEquipped += OnEquipped;
            PlayerTools.LocalUse += OnUse;
        }

        private void OnDestroy()
        {
            PlayerTools.LocalEquipped -= OnEquipped;
            PlayerTools.LocalUse -= OnUse;
            if (_holder != null) Destroy(_holder.gameObject);
        }

        private void OnEquipped(ToolDefinition tool)
        {
            _tool = tool;
            if (_shown != null) _shown.SetActive(false);
            _shown = null;
            _raise = 0f;
            if (tool == null || tool.Viewmodel == null) return;
            if (!_models.TryGetValue(tool, out GameObject model) || model == null)
            {
                model = Instantiate(tool.Viewmodel, _holder, false);
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _models[tool] = model;
            }
            model.SetActive(true);
            _shown = model;
            Sfx.Play2D(tool.Archetype == ToolArchetype.Blade ? "Kenney/drawKnife" : "Kenney/cloth", 0.25f, Random.Range(0.95f, 1.05f));
        }

        private void OnUse(PlayerTools user, ToolDefinition tool, Vector3 origin, Vector3 dir)
        {
            _useTime = Time.time;
            if (tool.Archetype == ToolArchetype.Net) Sfx.Play2D("Kenney/cloth", 0.35f, Random.Range(1.05f, 1.2f));
            else if (tool.Archetype == ToolArchetype.Blade) Sfx.Play2D(ProceduralSwoosh, 0.25f, Random.Range(0.9f, 1.1f));
        }

        private static AudioClip _swoosh;
        private static AudioClip ProceduralSwoosh => _swoosh != null ? _swoosh : _swoosh = ProceduralSounds.Whoosh();

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != _camera)
            {
                _camera = cam;
                if (cam != null)
                {
                    _holder.SetParent(cam.transform, false);
                    _lastCameraRotation = cam.transform.rotation;
                }
            }
            bool visible = cam != null && _shown != null && PlayerMotor.Local != null && PlayerMotor.Local.State.Mode != HowToHunt.Sim.Movement.MoveMode.Noclip;
            if (_holder.gameObject.activeSelf != visible) _holder.gameObject.SetActive(visible);
            if (!visible || _tool == null) return;

            float dt = Time.deltaTime;
            _raise = Mathf.Min(1f, _raise + dt / RaiseSeconds);
            float raise = 1f - (1f - _raise) * (1f - _raise);

            // Bob with horizontal speed while grounded.
            var s = PlayerMotor.Local.State;
            float speed = s.OnGround ? new Vector2(s.Velocity.X, s.Velocity.Z).magnitude : 0f;
            _bobAmount = Mathf.Lerp(_bobAmount, Mathf.Clamp01(speed / 7f), dt * 8f);
            _bobPhase += dt * Mathf.Lerp(6f, 11f, Mathf.Clamp01(speed / 10f));
            Vector3 bob = new Vector3(Mathf.Sin(_bobPhase) * 0.012f, -Mathf.Abs(Mathf.Cos(_bobPhase)) * 0.014f, 0f) * _bobAmount;

            // Lag: the model trails view rotation a little, then catches up.
            Quaternion camRot = cam.transform.rotation;
            Quaternion delta = Quaternion.Inverse(camRot) * _lastCameraRotation;
            _lastCameraRotation = camRot;
            _lag = Quaternion.Slerp(_lag * delta, Quaternion.identity, dt * 12f);
            _lag = Quaternion.RotateTowards(Quaternion.identity, _lag, 6f);

            Vector3 rest = new Vector3(0.22f, -0.2f, 0.42f);
            Vector3 offset = Vector3.zero;
            Quaternion use = Quaternion.identity;
            float t = (Time.time - _useTime) / Mathf.Max(0.12f, _tool.Cooldown * 0.75f);
            if (t >= 0f && t < 1f) Animate(_tool.Archetype, t, ref offset, ref use);

            _holder.localPosition = rest + bob + offset + new Vector3(0f, -0.25f * (1f - raise), 0f);
            _holder.localRotation = _lag * use * Quaternion.Euler(25f * (1f - raise), 0f, 0f);
        }

        /// <summary>Procedural use animation, t = 0..1 over the tool's cooldown.</summary>
        private static void Animate(ToolArchetype archetype, float t, ref Vector3 offset, ref Quaternion rotation)
        {
            switch (archetype)
            {
                case ToolArchetype.Net:
                {
                    // Scoop: quick forward-down sweep, slow return.
                    float k = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                    k = Mathf.SmoothStep(0f, 1f, k);
                    offset = new Vector3(-0.12f * k, -0.05f * k, 0.22f * k);
                    rotation = Quaternion.Euler(55f * k, -20f * k, 10f * k);
                    break;
                }
                case ToolArchetype.Blade:
                {
                    // Slash: wind-up to the right, fast diagonal cut to the left, recover.
                    float k;
                    if (t < 0.15f) k = -t / 0.15f * 0.3f;
                    else if (t < 0.4f) k = Mathf.Lerp(-0.3f, 1f, (t - 0.15f) / 0.25f);
                    else k = 1f - (t - 0.4f) / 0.6f;
                    offset = new Vector3(-0.2f * k, -0.04f * Mathf.Abs(k), 0.12f * Mathf.Abs(k));
                    rotation = Quaternion.Euler(35f * k, -55f * k, -30f * k);
                    break;
                }
                default:
                {
                    // Recoil: instant kick back and up, springy return.
                    float k = Mathf.Exp(-t * 6f) * (1f - t);
                    offset = new Vector3(0f, 0.015f * k, -0.07f * k);
                    rotation = Quaternion.Euler(-14f * k, 0f, 0f);
                    break;
                }
            }
        }
    }
}
