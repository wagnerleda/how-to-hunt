using UnityEngine;

namespace HowToHunt.Player
{
    /// <summary>Squashes the placeholder body when crouching so other players see it. Cheap: only acts on change.</summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        private PlayerMotor _motor;
        private Transform _body;
        private Transform _visor;
        private Vector3 _bodyScale;
        private Vector3 _visorPos;
        private float _shown = 1f;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            Transform visual = transform.Find("Visual");
            if (visual == null) return;
            _body = visual.Find("Body");
            _visor = visual.Find("Visor");
            if (_body != null) _bodyScale = _body.localScale;
            if (_visor != null) _visorPos = _visor.localPosition;
        }

        private TMPro.TextMeshPro _plate;
        private int _plateVersion = -1;

        /// <summary>Name tag over other players' heads, from the party roster, always facing the camera.</summary>
        private void UpdateNameplate()
        {
            if (_motor.IsOwner || !_motor.IsClientInitialized) return;
            if (_plate == null)
            {
                var go = new GameObject("Nameplate");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                _plate = go.AddComponent<TMPro.TextMeshPro>();
                _plate.fontSize = 2.6f;
                _plate.alignment = TMPro.TextAlignmentOptions.Center;
                _plate.color = new Color(1f, 0.97f, 0.9f);
                _plate.outlineWidth = 0.25f;
                _plate.outlineColor = new Color32(30, 26, 22, 255);
                _plate.rectTransform.sizeDelta = new Vector2(6f, 1f);
            }
            if (_plateVersion != Net.PartyRoster.Version)
            {
                _plateVersion = Net.PartyRoster.Version;
                _plate.text = Net.PartyRoster.TryGet(_motor.OwnerId, out Net.PartyMember m) ? m.Name : string.Empty;
            }
            Camera cam = Camera.main;
            if (cam != null) _plate.transform.rotation = Quaternion.LookRotation(_plate.transform.position - cam.transform.position);
        }

        private void LateUpdate()
        {
            UpdateNameplate();
            if (_body == null) return;
            var cfg = MovementCvars.Config;
            float target = _motor.State.Ducked ? cfg.DuckHalfHeight / cfg.HullHalfExtents.Y : 1f;
            float k = Mathf.MoveTowards(_shown, target, Time.deltaTime * 4f);
            if (Mathf.Approximately(k, _shown)) return;
            _shown = k;
            _body.localScale = new Vector3(_bodyScale.x, _bodyScale.y * k, _bodyScale.z);
            if (_visor != null) _visor.localPosition = new Vector3(_visorPos.x, _visorPos.y * k, _visorPos.z);
        }
    }
}
