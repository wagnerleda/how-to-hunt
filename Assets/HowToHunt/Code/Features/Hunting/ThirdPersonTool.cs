using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Player;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// What other players see you hold: the tool model (hands hidden) in your right hand at chest height, pitched with
    /// where you look and kicked back a little when you fire. Models are made once per tool and reused.
    /// </summary>
    public sealed class ThirdPersonTool : MonoBehaviour
    {
        private static readonly Vector3 HandOffset = new Vector3(0.36f, 0.32f, 0.3f); // from the hull centre
        private const float ToolScale = 1.1f;  // a touch larger than first person so it reads at a distance
        private const float KickSeconds = 0.18f;

        private readonly Dictionary<ToolDefinition, GameObject> _models = new Dictionary<ToolDefinition, GameObject>();
        private PlayerMotor _motor;
        private Transform _body; // the smoothed visual (the root moves in tick steps)
        private Transform _pivot;
        private GameObject _shown;
        private float _kickAt = -10f;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            Transform visual = transform.Find("Visual");
            _body = visual != null ? visual : transform;
            _pivot = new GameObject("Held Tool").transform;
            _pivot.SetParent(_body, false);
            _pivot.localPosition = HandOffset;
        }

        public void Show(ToolDefinition tool)
        {
            if (_shown != null) _shown.SetActive(false);
            _shown = null;
            if (tool == null || tool.Viewmodel == null) return;
            if (!_models.TryGetValue(tool, out GameObject model) || model == null)
            {
                model = Instantiate(tool.Viewmodel, _pivot, false);
                IconRenderer.HideParts(model, "Hand");
                foreach (Animator a in model.GetComponentsInChildren<Animator>()) a.enabled = false;
                model.transform.localScale = Vector3.one * ToolScale;
                _models[tool] = model;
            }
            model.SetActive(true);
            _shown = model;
        }

        public void Kick() => _kickAt = Time.time;

        private void LateUpdate()
        {
            if (_shown == null || _motor == null) return;
            // Follow the look direction: yaw comes from the body, pitch from the view.
            float t = (Time.time - _kickAt) / KickSeconds;
            float kick = t >= 0f && t < 1f ? (1f - t) * (1f - t) : 0f;
            _pivot.rotation = Quaternion.Euler(_motor.ViewPitch - 18f * kick, _motor.ViewYaw, 0f);
            _pivot.position = _body.position + Quaternion.Euler(0f, _motor.ViewYaw, 0f) * HandOffset - _pivot.forward * (0.06f * kick);
        }
    }
}
