using HowToHunt.Foundation;
using UnityEngine;

namespace HowToHunt.Economy
{
    /// <summary>
    /// A burst of paper bills over the trader when a sale is big for the area. Scaled to the station's ExpectedSale:
    /// nothing for small sales, a fistful for a normal full bag, a shower for a great one. Pooled particles, one system.
    /// </summary>
    public sealed class CashBurst : MonoBehaviour
    {
        public static readonly FloatCvar MinRatio = new FloatCvar("cl_cash_burst_min", 0.4f, "Sales below this fraction of the area's expected sale make no cash burst.", CvarFlags.Archive, 0f, 10f);
        public static readonly IntCvar BillsPerExpected = new IntCvar("cl_cash_burst_bills", 14, "Bills thrown for a sale equal to the area's expected sale.", CvarFlags.Archive, 0, 200);
        public static readonly IntCvar MaxBills = new IntCvar("cl_cash_burst_max", 60, "Most bills in one burst.", CvarFlags.Archive, 0, 300);

        private static readonly Vector3 BillSize = new Vector3(0.17f, 0.004f, 0.08f);
        private static readonly Color BillGreen = new Color(0.22f, 0.52f, 0.2f);
        private static readonly Color BillPale = new Color(0.42f, 0.68f, 0.34f);

        private static CashBurst _instance;
        private ParticleSystem _bills;
        private ParticleSystem.EmitParams _emit;

        private void Awake()
        {
            _instance = this;
            var go = new GameObject("Cash Bills");
            go.transform.SetParent(transform, false);
            _bills = go.AddComponent<ParticleSystem>();
            _bills.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _bills.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true;
            main.startSize3D = true;
            main.gravityModifier = 0.22f;
            ParticleSystem.EmissionModule emission = _bills.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _bills.shape;
            shape.enabled = false;
            // Paper: air drag slows the burst, then the bills tumble down slowly.
            ParticleSystem.LimitVelocityOverLifetimeModule drag = _bills.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.6f;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = true;
            ParticleSystem.RotationOverLifetimeModule spin = _bills.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            spin.y = new ParticleSystem.MinMaxCurve(-3f, 3f);
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            ParticleSystem.SizeOverLifetimeModule size = _bills.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Material m = Resources.Load<Material>("FX/M_Gore");
            if (m != null) r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Bills fly from the trader nearest <paramref name="near"/>, scaled to the sale against the area's expectation.</summary>
        public static void Play(Vector3 near, double coins)
        {
            if (_instance == null) return;
            CampStation trader = CampStation.Closest(near, CampStationKind.Sell);
            if (trader == null || trader.ExpectedSale <= 0) return;
            int bills = Bills(coins, trader.ExpectedSale);
            if (bills > 0) _instance.Burst(trader.transform.position + Vector3.up * 1.6f, bills);
        }

        /// <summary>How many bills a sale earns: none below the threshold, then proportional, capped.</summary>
        public static int Bills(double coins, double expected)
        {
            if (expected <= 0) return 0;
            double ratio = coins / expected;
            if (ratio < MinRatio.Value) return 0;
            return Mathf.Clamp(Mathf.RoundToInt((float)(ratio * BillsPerExpected.Value)), 1, MaxBills.Value);
        }

        private void Burst(Vector3 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 1.5f + 0.6f;
                _emit.position = at + Random.insideUnitSphere * 0.25f;
                _emit.velocity = dir.normalized * Random.Range(2.5f, 5.5f);
                _emit.startSize3D = BillSize * Random.Range(0.9f, 1.15f);
                _emit.startLifetime = Random.Range(2.2f, 3.4f);
                _emit.startColor = Color.Lerp(BillGreen, BillPale, Random.value * 0.6f);
                _emit.rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, Random.value * 360f);
                _bills.Emit(_emit, 1);
            }
        }
    }
}
