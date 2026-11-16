using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Sim.Movement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToHunt.Player
{
    /// <summary>
    /// Local player HUD: speedometer (Source units or m/s), green/red flash when a jump gained/lost speed (the bhop
    /// feedback players expect), movement status, crosshair and short zone messages. Allocation-free.
    /// </summary>
    public sealed class MovementHud : MonoBehaviour
    {
        public static readonly IntCvar ShowSpeed = new IntCvar("cl_showspeed", 1, "Speedometer: 0 off, 1 Source units/s, 2 m/s.", CvarFlags.Archive, 0, 2);
        public static readonly BoolCvar Crosshair = new BoolCvar("cl_crosshair", true, "Show the crosshair.", CvarFlags.Archive);

        /// <summary>Set while holding a firearm: the gun itself aims (bullets leave along the barrel), so no crosshair.</summary>
        public static bool HideCrosshair { get; set; }

        private const float UnitsPerMeter = 1f / 0.0254f;
        private static readonly Color Neutral = new Color(1f, 0.97f, 0.9f);
        private static readonly Color Gain = new Color(0.45f, 1f, 0.5f);
        private static readonly Color Loss = new Color(1f, 0.45f, 0.4f);

        private TextMeshProUGUI _speed;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _message;
        private Image[] _crosshair;
        private int _shownSpeed = -1;
        private int _shownStatus = -1;
        private float _takeoffSpeed;
        private float _flashUntil;
        private Color _flashColor = Neutral;
        private float _messageUntil;

        public static MovementHud Instance { get; private set; }

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("MovementHud");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<MovementHud>();
            go.AddComponent<StaminaHud>();
        }

        private void Awake()
        {
            _speed = Hud.Text("Speed", new Vector2(0.5f, 0f), new Vector2(0, 150), 54, TextAlignmentOptions.Center, Neutral);
            _speed.fontStyle = FontStyles.Bold;
            _status = Hud.Text("MoveStatus", new Vector2(0.5f, 0f), new Vector2(0, 112), 22, TextAlignmentOptions.Center, new Color(0.85f, 0.9f, 0.95f, 0.85f));
            _message = Hud.Text("ZoneMessage", new Vector2(0.5f, 1f), new Vector2(0, -170), 34, TextAlignmentOptions.Center, Neutral);
            _message.text = string.Empty;

            var c = new Color(1f, 1f, 1f, 0.9f);
            _crosshair = new[]
            {
                Hud.Rect("Cross L", new Vector2(0.5f, 0.5f), new Vector2(-9, 0), new Vector2(8, 2), c),
                Hud.Rect("Cross R", new Vector2(0.5f, 0.5f), new Vector2(9, 0), new Vector2(8, 2), c),
                Hud.Rect("Cross U", new Vector2(0.5f, 0.5f), new Vector2(0, 9), new Vector2(2, 8), c),
                Hud.Rect("Cross D", new Vector2(0.5f, 0.5f), new Vector2(0, -9), new Vector2(2, 8), c),
            };
            foreach (Image i in _crosshair) i.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);

            PlayerMotor.MoveFeedback += OnMoveFeedback;
            PlayerMotor.ZoneFeedback += OnZoneFeedback;
        }

        private void OnDestroy()
        {
            PlayerMotor.MoveFeedback -= OnMoveFeedback;
            PlayerMotor.ZoneFeedback -= OnZoneFeedback;
            if (Instance == this) Instance = null;
        }

        private void OnMoveFeedback(PlayerMotor motor, MoveEvents events, float landSpeed)
        {
            if (motor != PlayerMotor.Local || (events & MoveEvents.Jumped) == 0) return;
            float speed = PlayerMove.HorizontalSpeed(motor.State.Velocity);
            if (_takeoffSpeed > 0.1f && Mathf.Abs(speed - _takeoffSpeed) > 0.05f)
            {
                _flashColor = speed > _takeoffSpeed ? Gain : Loss;
                _flashUntil = Time.unscaledTime + 0.35f;
            }
            _takeoffSpeed = speed;
        }

        private void OnZoneFeedback(PlayerMotor motor, PlayerMotor.ZoneEvent e)
        {
            if (motor != PlayerMotor.Local) return;
            switch (e)
            {
                case PlayerMotor.ZoneEvent.Checkpoint: _message.SetText("Checkpoint"); break;
                case PlayerMotor.ZoneEvent.Respawned: _message.SetText("Back to checkpoint"); break;
                case PlayerMotor.ZoneEvent.Teleported: _message.SetText(string.Empty); return;
            }
            _messageUntil = Time.unscaledTime + 1.6f;
        }

        private void LateUpdate()
        {
            PlayerMotor m = PlayerMotor.Local;
            bool visible = m != null && !Game.UiHasFocus;
            SetActive(_speed.gameObject, visible && ShowSpeed.Value != 0);
            SetActive(_status.gameObject, visible && ShowSpeed.Value != 0);
            foreach (Image i in _crosshair) SetActive(i.gameObject, visible && Crosshair.Value && !HideCrosshair);
            SetActive(_message.gameObject, visible && Time.unscaledTime < _messageUntil);
            if (!visible) return;

            MoveState s = m.State;
            float metersPerSecond = PlayerMove.HorizontalSpeed(s.Velocity);
            int shown = Mathf.RoundToInt(ShowSpeed.Value == 2 ? metersPerSecond * 10f : metersPerSecond * UnitsPerMeter);
            if (shown != _shownSpeed)
            {
                _shownSpeed = shown;
                if (ShowSpeed.Value == 2) _speed.SetText("{0:1} m/s", shown / 10f);
                else _speed.SetText("{0}", shown);
            }
            _speed.color = Time.unscaledTime < _flashUntil ? _flashColor : Neutral;

            int status = s.Mode == MoveMode.Noclip ? 4 : s.Surfing ? 3 : !s.OnGround ? 2 : s.Ducked ? 1 : 0;
            if (status != _shownStatus)
            {
                _shownStatus = status;
                _status.SetText(status == 4 ? "NOCLIP" : status == 3 ? "SURF" : status == 2 ? "AIR" : status == 1 ? "CROUCH" : string.Empty);
            }
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }
    }
}
