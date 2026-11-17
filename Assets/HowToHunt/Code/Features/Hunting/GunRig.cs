using System.Collections.Generic;
using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Procedural animation of a gun's own parts (from its <see cref="ViewmodelFit"/>): the magazine drops out and goes
    /// back in during a reload, a pistol slide snaps back per shot, a bolt cycles after each shot, a revolver cylinder
    /// turns, a launcher head swings open, the Gunlance's front leaves open while it charges. Built once per model,
    /// no allocations per frame. All motion is expressed in the gun's own space (barrel +Z, up +Y).
    /// </summary>
    public sealed class GunRig
    {
        private struct Part
        {
            public Transform T;
            public RigPartKind Kind;
            public Vector3 Pivot;          // gun space
            public Vector3 RestPos;
            public Quaternion RestRot;
        }

        private readonly Transform _root;
        private readonly List<Part> _parts = new List<Part>();

        private float _shotAt = -10f;
        private int _shots;

        public GunRig(GameObject model)
        {
            _root = model.transform;
            ViewmodelFit fit = model.GetComponent<ViewmodelFit>();
            if (fit == null) return;
            foreach (RigPart p in fit.Parts)
            {
                Transform t = Find(_root, p.Name);
                if (t == null) continue;
                _parts.Add(new Part { T = t, Kind = p.Kind, Pivot = p.Pivot, RestPos = t.localPosition, RestRot = t.localRotation });

            }
        }

        public bool Empty => _parts.Count == 0;

        public void Shot()
        {
            _shotAt = Time.time;
            _shots++;
        }

        /// <param name="reload">Reload progress 0..1, or below 0 when not reloading.</param>
        /// <param name="cycleSeconds">Time between shots (a bolt cycles within it).</param>
        /// <param name="charge">Charge 0..1 (the Gunlance opens as it charges).</param>
        /// <param name="rack">The reload ends with a rack (slide, charging handle, bolt): only when the gun was empty.</param>
        public void Update(float reload, float cycleSeconds, float charge, bool rack = true)
        {
            float since = Time.time - _shotAt;
            for (int i = 0; i < _parts.Count; i++)
            {
                Part p = _parts[i];
                if (p.T == null) continue;
                Vector3 move = Vector3.zero;
                float angle = 0f;
                Vector3 axis = Vector3.forward;
                bool visible = true;
                switch (p.Kind)
                {
                    case RigPartKind.Magazine:
                        if (reload >= 0f)
                        {
                            // Out (0.22-0.36, a flick), gone, back in (0.48-0.6, slapped home): How to Fish's timing.
                            float outT = Smooth(Mathf.InverseLerp(0.22f, 0.36f, reload)), inT = Smooth(Mathf.InverseLerp(0.48f, 0.6f, reload));
                            float away = outT * (1f - inT);
                            move = new Vector3(0f, -0.22f, -0.03f) * away;
                            visible = away < 0.95f;
                        }
                        break;
                    case RigPartKind.Slide:
                    {
                        float k = since < 0f ? 0f : since < 0.03f ? since / 0.03f : Mathf.Clamp01(1f - (since - 0.03f) / 0.09f);
                        float racked = reload >= 0f && rack ? Bump(reload, 0.76f, 0.88f) : 0f;
                        move = new Vector3(0f, 0f, -0.028f * Mathf.Max(k, racked));
                        break;
                    }
                    case RigPartKind.ChargingHandle:
                        move = new Vector3(0f, 0f, -0.07f * (reload >= 0f && rack ? Bump(reload, 0.76f, 0.88f) : 0f));
                        break;
                    case RigPartKind.Bolt:
                    {
                        // After each shot (or at the end of a reload): lift, pull back, push forward, lock down.
                        float c = cycleSeconds > 0f ? Mathf.Clamp01((since - 0.08f) / (cycleSeconds * 0.85f)) : 1f;
                        if (reload >= 0f) c = rack ? Mathf.InverseLerp(0.72f, 0.92f, reload) : 0f;
                        if (c > 0f && c < 1f)
                        {
                            float lift = Bump(c, 0f, 1f, 0.2f);
                            float back = Bump(c, 0.2f, 0.8f, 0.3f);
                            angle = 60f * lift;
                            move = new Vector3(0f, 0f, -0.08f * back);
                        }
                        break;
                    }
                    case RigPartKind.Cylinder:
                    {
                        // One chamber per shot; a full spin as the reload ends.
                        float turn = Mathf.Clamp01(since / 0.1f);
                        angle = 60f * (_shots - 1 + turn);
                        if (reload >= 0f) angle += 720f * Smooth(Mathf.InverseLerp(0.6f, 0.95f, reload));
                        break;
                    }
                    case RigPartKind.Hinge:
                        axis = Vector3.right;
                        angle = reload >= 0f ? 38f * Bump(reload, 0.1f, 0.9f, 0.25f) : 0f;
                        break;
                    case RigPartKind.LeafPort:
                        axis = Vector3.up;
                        angle = -40f * Smooth(charge);
                        break;
                    case RigPartKind.LeafStarboard:
                        axis = Vector3.up;
                        angle = 40f * Smooth(charge);
                        break;
                    case RigPartKind.BreakOpen:
                    case RigPartKind.Ejector:
                    case RigPartKind.Latch:
                    case RigPartKind.Shell:
                        BreakAction(p, reload);
                        continue;
                }
                if (p.T.gameObject.activeSelf != visible) p.T.gameObject.SetActive(visible);
                Apply(p, move, axis, angle);
            }
        }

        // A break action, timed like How to Fish's shotgun reload: the latch slips (0-8 %), the barrels drop open
        // (6-20 %) and the ejector lifts the shells, the spent shells come out and fresh ones go in (30-55 %), then the
        // barrels snap shut (68-78 %) and the latch returns. Each part moves in its own frame, as the model's own open
        // pose shows (barrels 60 degrees about X, ejector 5 mm back, latch 15 degrees).
        private const float BreakOpenDegrees = 55f, EjectorTravel = 0.005f, LatchDegrees = 15f;

        private static void BreakAction(Part p, float reload)
        {
            float open = reload < 0f ? 0f : Smooth(Mathf.InverseLerp(0.06f, 0.2f, reload)) * (1f - Smooth(Mathf.InverseLerp(0.68f, 0.78f, reload)));
            switch (p.Kind)
            {
                case RigPartKind.BreakOpen:
                    p.T.localRotation = p.RestRot * Quaternion.Euler(BreakOpenDegrees * open, 0f, 0f);
                    break;
                case RigPartKind.Ejector:
                    p.T.localPosition = p.RestPos + new Vector3(0f, 0f, -EjectorTravel * open);
                    break;
                case RigPartKind.Latch:
                {
                    float unlock = reload < 0f ? 0f : Smooth(Mathf.InverseLerp(0f, 0.08f, reload)) * (1f - Smooth(Mathf.InverseLerp(0.76f, 0.84f, reload)));
                    p.T.localRotation = p.RestRot * Quaternion.Euler(0f, -LatchDegrees * unlock, 0f);
                    break;
                }
                case RigPartKind.Shell:
                {
                    bool swapping = reload >= 0.3f && reload < 0.55f;
                    if (p.T.gameObject.activeSelf == swapping) p.T.gameObject.SetActive(!swapping);
                    break;
                }
            }
        }

        /// <summary>Puts a part back at rest, then moves it by a gun-space offset and turns it about its pivot.</summary>
        private void Apply(Part p, Vector3 move, Vector3 axis, float angle)
        {
            Transform parent = p.T.parent;
            if (parent == null) return;
            Vector3 pos = p.RestPos;
            Quaternion rot = p.RestRot;
            if (angle != 0f)
            {
                Vector3 axisP = parent.InverseTransformDirection(_root.TransformDirection(axis)).normalized;
                Vector3 centerP = parent.InverseTransformPoint(_root.TransformPoint(p.Pivot));
                Quaternion q = Quaternion.AngleAxis(angle, axisP);
                pos = centerP + q * (pos - centerP);
                rot = q * rot;
            }
            if (move != Vector3.zero) pos += parent.InverseTransformVector(_root.TransformVector(move));
            p.T.localPosition = pos;
            p.T.localRotation = rot;
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform f = Find(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>0 outside [a, b], rising and falling smoothly inside it (edge = share of the span spent rising).</summary>
        private static float Bump(float t, float a, float b, float edge = 0.35f)
        {
            if (t <= a || t >= b) return 0f;
            float u = (t - a) / (b - a);
            return Smooth(Mathf.Clamp01(u / edge)) * Smooth(Mathf.Clamp01((1f - u) / edge));
        }
    }
}
