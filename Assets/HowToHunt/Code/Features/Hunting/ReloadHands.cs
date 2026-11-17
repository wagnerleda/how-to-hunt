using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// The off hand during a reload (first person). It works the gun's own moving parts as GunRig moves them: grabs the
    /// magazine and rides it down, waits below for the new one, brings it up and slaps it home, then (empty gun only)
    /// takes the slide, charging handle or bolt and rides it back; works the cylinder of a revolver, holds a launcher's
    /// opened head, visits a shotgun's open breech, or rams a cannon's muzzle. Pistols get a support hand that only
    /// shows during the reload. Keyframes are spots on moving parts, re-measured every frame. No allocations per frame.
    /// </summary>
    public sealed class ReloadHands
    {
        private enum Spot : byte { Rest, Mag, MagBelow, MagSlap, Rack, Cylinder, Hinge, Breech, Muzzle, MuzzleRam }

        private struct Key
        {
            public float T;
            public Spot Spot;
            public Key(float t, Spot spot) { T = t; Spot = spot; }
        }

        private const float BelowDrop = 0.09f, SlapLift = 0.018f, RamDepth = 0.14f, Ahead = 0.06f;

        private readonly Transform _root, _hand;
        private readonly Vector3 _restLocal;      // in the hand's parent
        private readonly bool _support;           // a pistol's extra hand: only seen during the reload
        private readonly Transform _mag, _rack, _cylinder, _hinge, _breech;
        private readonly Vector3 _magGrab, _rackGrab, _cylinderGrab, _hingeGrab, _breechGrab; // in each part's own space
        private readonly Vector3 _muzzle;         // gun space
        private Key[] _rackKeys, _plainKeys;

        public bool Valid => _hand != null;

        public ReloadHands(Transform root, ViewmodelFit fit, System.Func<string, Transform> find)
        {
            _root = root;
            if (fit == null) return;
            _muzzle = fit.Muzzle;
            Transform front = find("FrontHand"), grip = find("Hand");
            if (front != null)
            {
                _hand = front;
            }
            else if (grip != null)
            {
                // One-handed gun: a second hand appears for the reload only.
                GameObject copy = Object.Instantiate(grip.gameObject, root, false);
                copy.name = "SupportHand";
                copy.transform.localPosition = grip.localPosition + new Vector3(-0.06f, -0.14f, -0.04f);
                copy.SetActive(false);
                _hand = copy.transform;
                _support = true;
            }
            if (_hand == null) return;
            _restLocal = _hand.localPosition;

            foreach (RigPart p in fit.Parts)
            {
                Transform t = find(p.Name);
                if (t == null) continue;
                switch (p.Kind)
                {
                    case RigPartKind.Magazine when _mag == null: _mag = t; _magGrab = Grab(t, new Vector3(-0.8f, -0.8f, 0.5f)); break; // palm under the front-left of the magazine
                    case RigPartKind.Slide: _rack = t; _rackGrab = Grab(t, new Vector3(-0.9f, 0.3f, -0.6f)); break;
                    case RigPartKind.ChargingHandle: _rack = t; _rackGrab = Grab(t, new Vector3(-0.6f, 0f, 0f)); break;
                    case RigPartKind.Bolt when _rack == null: _rack = t; _rackGrab = Grab(t, Vector3.zero); break;
                    case RigPartKind.Cylinder: _cylinder = t; _cylinderGrab = Grab(t, new Vector3(-0.9f, 0f, 0f)); break;
                    case RigPartKind.Hinge: _hinge = t; _hingeGrab = Grab(t, new Vector3(0f, -0.6f, 0.3f)); break;
                    case RigPartKind.Ejector: _breech = t; _breechGrab = Grab(t, new Vector3(0f, 0.5f, -0.5f)); break;
                }
            }
            BuildKeys();
        }

        /// <summary>A point on a part (as a fraction of its half size from its middle), kept in the part's own space.</summary>
        private static Vector3 Grab(Transform part, Vector3 across)
        {
            Renderer r = part.GetComponentInChildren<Renderer>(true);
            if (r == null) return Vector3.zero;
            Bounds b = r.bounds;
            Vector3 world = b.center + Vector3.Scale(b.extents, across);
            return part.InverseTransformPoint(world);
        }

        private void BuildKeys()
        {
            if (_mag != null)
            {
                // With the magazine (it drops 22-36 %, comes back 48-60 %), then the slap.
                _plainKeys = new[]
                {
                    new Key(0f, Spot.Rest), new Key(0.16f, Spot.Mag), new Key(0.36f, Spot.Mag), new Key(0.42f, Spot.MagBelow),
                    new Key(0.47f, Spot.MagBelow), new Key(0.6f, Spot.Mag), new Key(0.63f, Spot.MagSlap), new Key(0.67f, Spot.Mag),
                    new Key(0.85f, Spot.Rest), new Key(1f, Spot.Rest),
                };
                _rackKeys = _rack == null ? _plainKeys : new[]
                {
                    new Key(0f, Spot.Rest), new Key(0.16f, Spot.Mag), new Key(0.36f, Spot.Mag), new Key(0.42f, Spot.MagBelow),
                    new Key(0.47f, Spot.MagBelow), new Key(0.6f, Spot.Mag), new Key(0.63f, Spot.MagSlap), new Key(0.67f, Spot.Mag),
                    new Key(0.74f, Spot.Rack), new Key(0.9f, Spot.Rack), new Key(1f, Spot.Rest),
                };
            }
            else if (_cylinder != null)
            {
                _plainKeys = new[] { new Key(0f, Spot.Rest), new Key(0.14f, Spot.Cylinder), new Key(0.88f, Spot.Cylinder), new Key(1f, Spot.Rest) };
            }
            else if (_hinge != null)
            {
                _plainKeys = new[] { new Key(0f, Spot.Rest), new Key(0.12f, Spot.Hinge), new Key(0.88f, Spot.Hinge), new Key(1f, Spot.Rest) };
            }
            else if (_breech != null)
            {
                // The front hand rides the opened barrels; it reaches into the breech while the shells are swapped.
                _plainKeys = new[] { new Key(0f, Spot.Rest), new Key(0.24f, Spot.Rest), new Key(0.32f, Spot.Breech), new Key(0.52f, Spot.Breech), new Key(0.62f, Spot.Rest), new Key(1f, Spot.Rest) };
            }
            else
            {
                // Nothing to work: load it from the muzzle and ram it home (the cannon, the Gunlance).
                _plainKeys = new[]
                {
                    new Key(0f, Spot.Rest), new Key(0.2f, Spot.Muzzle), new Key(0.3f, Spot.MuzzleRam), new Key(0.38f, Spot.Muzzle),
                    new Key(0.48f, Spot.MuzzleRam), new Key(0.56f, Spot.Muzzle), new Key(0.8f, Spot.Rest), new Key(1f, Spot.Rest),
                };
            }
            _rackKeys ??= _plainKeys;
        }

        public void Update(float reload, bool rack)
        {
            if (_hand == null) return;
            bool reloading = reload >= 0f;
            if (_support && _hand.gameObject.activeSelf != reloading) _hand.gameObject.SetActive(reloading);
            if (!reloading)
            {
                _hand.localPosition = _restLocal;
                return;
            }
            Key[] keys = rack ? _rackKeys : _plainKeys;
            int k = 0;
            while (k < keys.Length - 2 && reload >= keys[k + 1].T) k++;
            Key a = keys[k], b = keys[k + 1];
            float t = Mathf.Clamp01(Mathf.InverseLerp(a.T, b.T, reload));
            t = t * t * (3f - 2f * t);
            Vector3 pos = Vector3.Lerp(Where(a.Spot), Where(b.Spot), t); // gun space
            Transform parent = _hand.parent;
            _hand.localPosition = parent == _root ? pos : parent.InverseTransformPoint(_root.TransformPoint(pos));
        }

        /// <summary>A spot in gun space, measured now (the parts move).</summary>
        private Vector3 Where(Spot s)
        {
            switch (s)
            {
                case Spot.Mag: return On(_mag, _magGrab);
                case Spot.MagBelow: return On(_mag, _magGrab) + new Vector3(0f, -BelowDrop, -0.02f);
                case Spot.MagSlap: return On(_mag, _magGrab) + new Vector3(0f, SlapLift, 0f);
                case Spot.Rack: return On(_rack, _rackGrab);
                case Spot.Cylinder: return On(_cylinder, _cylinderGrab);
                case Spot.Hinge: return On(_hinge, _hingeGrab);
                case Spot.Breech: return On(_breech, _breechGrab);
                case Spot.Muzzle: return _muzzle + new Vector3(-0.02f, 0.02f, Ahead);
                case Spot.MuzzleRam: return _muzzle + new Vector3(-0.02f, 0.02f, Ahead - RamDepth);
                default: return Rest();
            }
        }

        private Vector3 Rest()
        {
            Transform parent = _hand.parent;
            return parent == _root ? _restLocal : _root.InverseTransformPoint(parent.TransformPoint(_restLocal));
        }

        private Vector3 On(Transform part, Vector3 local) => part == null ? Rest() : _root.InverseTransformPoint(part.TransformPoint(local));
    }
}
