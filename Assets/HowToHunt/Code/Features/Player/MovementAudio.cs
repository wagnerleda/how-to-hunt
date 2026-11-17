using HowToHunt.Core;
using HowToHunt.Sim.Movement;
using UnityEngine;

namespace HowToHunt.Player
{
    /// <summary>
    /// Movement sounds for every player: footsteps by distance travelled (Kenney CC0 recordings: grass on the ground,
    /// wood on raised platforms), a cloth rustle on jump, a soft landing thud scaled by impact,
    /// zone cues, and a wind loop for the local player that rises with speed (surf/bhop feel). Pooled sources,
    /// no allocations after startup.
    /// </summary>
    public sealed class MovementAudio : MonoBehaviour
    {
        public static readonly Foundation.FloatCvar Volume = new Foundation.FloatCvar("snd_movement", 0.6f, "Volume of footsteps, jumps, landings and wind (they sit in the background).", Foundation.CvarFlags.Archive, 0f, 1f);

        private const int PoolSize = 16;
        private const float Stride = 1.9f;

        private AudioClip[] _grass, _wood, _cloth, _thud;
        private AudioClip _splash;
        private AudioClip _chime, _whoosh;
        private AudioSource[] _pool;
        private int _next;
        private AudioSource _wind;
        private int _stepIndex;

        private void Awake()
        {
            _grass = Load("footstep_grass_00", 5);
            _wood = Load("footstep_wood_00", 3);
            _cloth = new[] { Resources.Load<AudioClip>("Audio/Kenney/cloth1"), Resources.Load<AudioClip>("Audio/Kenney/cloth2"), Resources.Load<AudioClip>("Audio/Kenney/cloth3"), Resources.Load<AudioClip>("Audio/Kenney/cloth4") };
            _thud = Load("impactSoft_medium_00", 2);
            _splash = ProceduralSounds.Splash();
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
            if ((events & MoveEvents.Jumped) != 0) Play(motor, _cloth[Random.Range(0, _cloth.Length)], 0.07f, Random.Range(0.95f, 1.08f));
            if ((events & MoveEvents.Landed) != 0 && landSpeed > 2.5f)
            {
                float k = Mathf.Clamp01((landSpeed - 2.5f) / 12f);
                Play(motor, _thud[Random.Range(0, _thud.Length)], 0.05f + 0.2f * k, Mathf.Lerp(1.05f, 0.85f, k));
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
                float volume = Mathf.Clamp01(speed / MovementCvars.Config.MaxSpeed) * (s.Ducked ? 0.06f : 0.16f);
                if (s.InWater)
                {
                    // Wading: splashes, a bit louder than steps.
                    Play(m, _splash, Mathf.Min(1f, volume * 2.2f), Random.Range(0.85f, 1.15f));
                    continue;
                }
                // Feet on the terrain (or the flat lab floor) = grass; anything raised (platforms, crates, stalls) = wood.
                float feet = s.Position.Y - MovementCvars.Config.Hull(s.Ducked).Y;
                float ground = GroundHeight(s.Position.X, s.Position.Z);
                AudioClip[] set = feet - ground < 0.3f ? _grass : _wood;
                Play(m, set[_stepIndex++ % set.Length], volume, Random.Range(0.94f, 1.06f));
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

        private SimTerrain[] _terrains;

        /// <summary>Terrain height under a point (0 where there is no terrain: the lab floor). Several maps, several terrains.</summary>
        private float GroundHeight(float x, float z)
        {
            if (_terrains == null || _terrains.Length == 0 || _terrains[0] == null)
                _terrains = FindObjectsByType<SimTerrain>(FindObjectsInactive.Exclude);
            for (int i = 0; i < _terrains.Length; i++)
            {
                float h = _terrains[i] != null ? _terrains[i].HeightAt(x, z) : float.NaN;
                if (!float.IsNaN(h)) return h;
            }
            return 0f;
        }

        private static AudioClip[] Load(string prefix, int count)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++) clips[i] = Resources.Load<AudioClip>($"Audio/Kenney/{prefix}{i}");
            return clips;
        }

        private void Play(PlayerMotor motor, AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
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
