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

        private void LateUpdate()
        {
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
