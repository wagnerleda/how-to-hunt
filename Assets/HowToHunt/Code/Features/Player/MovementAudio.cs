using HowToHunt.Core;
using HowToHunt.Sim.Movement;
using UnityEngine;

namespace HowToHunt.Player
{
    /// <summary>
    /// Movement sounds for every player: footsteps by distance travelled, jump, landing thud scaled by impact,
    /// zone cues, and a wind loop for the local player that rises with speed (surf/bhop feel). Pooled sources,
    /// no allocations after startup.
    /// </summary>
    public sealed class MovementAudio : MonoBehaviour
    {
        public static readonly Foundation.FloatCvar Volume = new Foundation.FloatCvar("snd_movement", 0.6f, "Volume of footsteps, jumps, landings and wind (they sit in the background).", Foundation.CvarFlags.Archive, 0f, 1f);

        private const int PoolSize = 16;
        private const float Stride = 1.9f;

        private AudioClip[] _steps;
        private AudioClip _jump, _land, _chime, _whoosh;
        private AudioSource[] _pool;
        private int _next;
        private AudioSource _wind;
        private int _stepIndex;

        private void Awake()
        {
            _steps = new[] { ProceduralSounds.Footstep(1), ProceduralSounds.Footstep(2), ProceduralSounds.Footstep(3), ProceduralSounds.Footstep(4) };
            _jump = ProceduralSounds.Jump();
            _land = ProceduralSounds.Land();
            _chime = ProceduralSounds.Chime();
            _whoosh = ProceduralSounds.Whoosh();

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"Sfx {i}");
                go.transform.SetParent(transform, false);
                AudioSource src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 2f;
                src.maxDistance = 60f;
                src.dopplerLevel = 0f;
                _pool[i] = src;
            }

            _wind = gameObject.AddComponent<AudioSource>();
            _wind.clip = ProceduralSounds.Wind();
            _wind.loop = true;
            _wind.spatialBlend = 0f;
            _wind.volume = 0f;
            _wind.Play();

            PlayerMotor.MoveFeedback += OnMoveFeedback;
            PlayerMotor.ZoneFeedback += OnZoneFeedback;
        }

        private void OnDestroy()
        {
            PlayerMotor.MoveFeedback -= OnMoveFeedback;
            PlayerMotor.ZoneFeedback -= OnZoneFeedback;
        }

        private void OnMoveFeedback(PlayerMotor motor, MoveEvents events, float landSpeed)
        {
            if ((events & MoveEvents.Jumped) != 0) Play(motor, _jump, 0.05f, Random.Range(0.95f, 1.05f));
            if ((events & MoveEvents.Landed) != 0 && landSpeed > 2.5f)
            {
                float k = Mathf.Clamp01((landSpeed - 2.5f) / 12f);
                Play(motor, _land, 0.06f + 0.22f * k, Mathf.Lerp(1.05f, 0.9f, k));
                motor.FootstepDistance = 0f;
            }
        }

        private void OnZoneFeedback(PlayerMotor motor, PlayerMotor.ZoneEvent e)
        {
            if (motor != PlayerMotor.Local) return;
            switch (e)
            {
                case PlayerMotor.ZoneEvent.Checkpoint: Play(motor, _chime, 0.12f, 1f); break;
                case PlayerMotor.ZoneEvent.Respawned: Play(motor, _whoosh, 0.25f, 1f); break;
                case PlayerMotor.ZoneEvent.Teleported: Play(motor, _whoosh, 0.2f, 1.3f); break;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            var all = PlayerMotor.All;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerMotor m = all[i];
                MoveState s = m.State;
                if (!s.OnGround || s.Mode != MoveMode.Walk) continue;
                float speed = PlayerMove.HorizontalSpeed(s.Velocity);
                if (speed < 1.5f) continue;
                m.FootstepDistance += speed * dt;
                if (m.FootstepDistance < Stride) continue;
                m.FootstepDistance = 0f;
                float volume = Mathf.Clamp01(speed / MovementCvars.Config.MaxSpeed) * (s.Ducked ? 0.03f : 0.09f);
                Play(m, _steps[_stepIndex++ & 3], volume, Random.Range(0.92f, 1.08f));
            }

            PlayerMotor local = PlayerMotor.Local;
            float target = 0f, pitch = 0.8f;
            if (local != null && local.State.Mode == MoveMode.Walk)
            {
                Vector3 v = local.State.Velocity.ToUnity();
                float speed = v.magnitude;
                target = Mathf.Clamp01((speed - 8f) / 22f) * 0.22f * Volume.Value;
                pitch = 0.75f + Mathf.Clamp01(speed / 40f) * 0.6f;
            }
            _wind.volume = Mathf.MoveTowards(_wind.volume, target, dt * 0.8f);
            _wind.pitch = Mathf.Lerp(_wind.pitch, pitch, 1f - Mathf.Exp(-3f * dt));
        }

        private void Play(PlayerMotor motor, AudioClip clip, float volume, float pitch)
        {
            AudioSource src = _pool[_next];
            _next = (_next + 1) % PoolSize;
            bool local = motor == PlayerMotor.Local;
            src.spatialBlend = local ? 0f : 1f;
            src.transform.position = motor.transform.position;
            src.pitch = pitch;
            src.volume = volume * Volume.Value;
            src.clip = clip;
            src.Play();
        }
    }
}
