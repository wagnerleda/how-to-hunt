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
                model = Instantiate(tool.Viewmodel, _holder, false); // tool + gloved hand, posed in Blender
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
            if (tool.Archetype != ToolArchetype.Firearm) StartSweep();
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

            Vector3 rest = new Vector3(0.18f, -0.17f, 0.40f);
            Vector3 offset;
            Quaternion use;
            if (_tool.Archetype == ToolArchetype.Firearm)
            {
                float t = (Time.time - _useTime) / Mathf.Max(0.12f, _tool.Cooldown * 0.75f);
                Recoil(t >= 0f && t < 1f ? t : 1f, out offset, out use);
            }
            else
            {
                Sweep(dt, out offset, out use);
            }

            _holder.localPosition = rest + bob + offset + new Vector3(0f, -0.25f * (1f - raise), 0f);
            _holder.localRotation = _lag * use * Quaternion.Euler(25f * (1f - raise), 0f, 0f);
        }

        // --- melee sweeps -----------------------------------------------------------------------------------
        // A continuous "side" value: +1 = tool held to the right, -1 = to the left, 0 = centred rest. Each use sweeps
        // from the current side to the other one, so spamming alternates right->left, left->right with no snapping.
        private float _side;
        private float _sweepFrom, _sweepTo;
        private float _sweepStart = -10f, _sweepSeconds = 0.3f;

        private void StartSweep()
        {
            float from = Mathf.Abs(_side) < 0.3f ? 1f : Mathf.Sign(_side);
            _sweepFrom = _side;
            _sweepTo = -from;
            _sweepStart = Time.time;
            bool net = _tool != null && _tool.Archetype == ToolArchetype.Net;
            _sweepSeconds = Mathf.Clamp((_tool != null ? _tool.Cooldown : 0.5f) * (net ? 0.85f : 0.7f), 0.18f, 0.6f);
        }

        private void Sweep(float dt, out Vector3 offset, out Quaternion rotation)
        {
            bool net = _tool.Archetype == ToolArchetype.Net;
            float t = (Time.time - _sweepStart) / _sweepSeconds;
            float dip = 0f;
            if (t >= 0f && t < 1f)
            {
                // Knife: a short wind-up past the start side, then a fast cut. Net: one smooth scoop.
                float u;
                if (!net && t < 0.2f) u = -0.25f * Mathf.Sin(t / 0.2f * Mathf.PI * 0.5f);
                else u = Mathf.SmoothStep(net ? 0f : -0.25f, 1f, net ? t : (t - 0.2f) / 0.8f);
                _side = Mathf.LerpUnclamped(_sweepFrom, _sweepTo, u);
                dip = Mathf.Sin(Mathf.Clamp01(net ? t : (t - 0.2f) / 0.8f) * Mathf.PI);
            }
            else if (Time.time - _sweepStart > _sweepSeconds + 0.6f)
            {
                _side = Mathf.MoveTowards(_side, 0f, dt * 2.5f); // drift back to rest when idle
            }

            float s = _side;
            if (net)
            {
                // The hoop crosses the screen low and forward, tilted into the sweep.
                offset = new Vector3(s * 0.2f - 0.04f, 0.05f - 0.09f * dip, 0.12f * dip);
                rotation = Quaternion.Euler(-14f + 40f * dip, s * 38f, -s * 18f);
            }
            else
            {
                // Diagonal slashes: high on the right, low on the left; blade edge leads.
                // Held blade-up (so it reads at rest), then cut down through the diagonal.
                offset = new Vector3(s * 0.16f - 0.03f, s * 0.05f - 0.03f * dip + 0.02f, 0.1f * dip);
                rotation = Quaternion.Euler(-38f + 55f * dip - s * 10f, s * 32f - 8f, -s * 45f + 12f);
            }
        }

        private static void Recoil(float t, out Vector3 offset, out Quaternion rotation)
        {
            // Instant kick back and up, springy return.
            float k = Mathf.Exp(-t * 6f) * (1f - t);
            offset = new Vector3(0f, 0.015f * k, -0.07f * k);
            rotation = Quaternion.Euler(-14f * k, 0f, 0f);
        }
    }
}
