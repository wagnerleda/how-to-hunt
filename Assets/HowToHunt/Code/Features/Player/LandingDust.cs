using HowToHunt.Sim.Movement;
using UnityEngine;

namespace HowToHunt.Player
{
    /// <summary>
    /// Chunky low-poly dust when a player lands hard (art direction A: little cubes, no soft sprites).
    /// One particle system for everyone; Emit() reuses its pool, so no allocations.
    /// </summary>
    public sealed class LandingDust : MonoBehaviour
    {
        private ParticleSystem _ps;
        private ParticleSystem.EmitParams _emit;

        private void Awake()
        {
            var go = new GameObject("Landing Dust");
            go.transform.SetParent(transform, false);
            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 256;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startRotation3D = true;
            main.gravityModifier = 0.6f;
            main.startColor = new Color(0.86f, 0.78f, 0.6f);

            ParticleSystem.EmissionModule emission = _ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _ps.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = _ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);
            Material dust = Resources.Load<Material>("FX/M_Dust");
            if (dust != null) renderer.sharedMaterial = dust;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            PlayerMotor.MoveFeedback += OnMoveFeedback;
        }

        private void OnDestroy() => PlayerMotor.MoveFeedback -= OnMoveFeedback;

        private void OnMoveFeedback(PlayerMotor motor, MoveEvents events, float landSpeed)
        {
            if ((events & MoveEvents.Landed) == 0 || landSpeed < 5f) return;
            MoveState s = motor.State;
            Vector3 feet = motor.transform.position - new Vector3(0f, MovementCvars.Config.Hull(s.Ducked).Y - 0.05f, 0f);
            int count = Mathf.RoundToInt(Mathf.Lerp(6f, 22f, Mathf.Clamp01((landSpeed - 5f) / 12f)));
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0.35f + Random.value * 0.4f, Mathf.Sin(a));
                _emit.position = feet + new Vector3(dir.x, 0f, dir.z) * 0.3f;
                _emit.velocity = dir * Random.Range(1.2f, 2.8f);
                _emit.rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, 0f);
                _ps.Emit(_emit, 1);
            }
        }
    }
}
