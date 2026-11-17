using UnityEngine;

namespace HowToHunt.Hunting
{
    /// <summary>
    /// How to Fish's weapon recoil, without its rigidbodies. A shot throws a "recoil rig" (position and rotation) and
    /// springs pull it back (the gun's own SpringPos/SpringRot and dampers, stiffer while aiming). A softer "sway rig"
    /// follows the recoil rig, and that's what moves the gun: the kick reads as a heavy shove that settles, not a snap.
    /// Steps at 50 Hz like the physics it copies (implicit springs: stable however stiff) and interpolates between steps.
    /// No allocations.
    /// </summary>
    public sealed class GunRecoilRig
    {
        public const float Step = 0.02f;
        private const int MaxSteps = 10;
        /// <summary>The rigs' rigidbody damping in How to Fish (linear and angular).</summary>
        private const float BodyDamping = 10f;
        // The sway rig's joint drives in How to Fish's player prefab.
        private static readonly Vector3 SwaySpring = new Vector3(150f, 500f, 150f);
        private const float SwayDamper = 5f, SwayRotSpring = 1000f, SwayRotDamper = 0f;

        private Vector3 _rp, _rv, _rr, _rw;   // recoil rig: position, velocity, rotation (degrees), angular velocity
        private Vector3 _sp, _sv, _sr, _sw;   // sway rig
        private Vector3 _prevPos, _prevRot;
        private float _acc;

        /// <summary>Gun offset (metres, gun space) and rotation (degrees: pitch, yaw, roll), interpolated.</summary>
        public Vector3 Position => Vector3.Lerp(_prevPos, _sp, _acc / Step);
        public Vector3 Euler => Vector3.Lerp(_prevRot, _sr, _acc / Step);

        public void Reset()
        {
            _rp = _rv = _rr = _rw = Vector3.zero;
            _sp = _sv = _sr = _sw = Vector3.zero;
            _prevPos = _prevRot = Vector3.zero;
            _acc = 0f;
        }

        /// <param name="dir">The shot's random direction, -1..1 (shared with the view kick).</param>
        public void Kick(in GunHandling h, float dir, bool aiming, float scale)
        {
            float kp = (aiming ? h.AdsKickPos : 1f) * scale, kr = (aiming ? h.AdsKickRot : 1f) * scale;
            // As How to Fish: yaw and roll by the random direction, muzzle up; thrown sideways, up and back.
            _rr += new Vector3(-h.KickRot.y, dir * h.KickRot.x, -dir * h.KickRot.x) * kr;
            _rp += new Vector3(dir * h.KickPos.x, h.KickPos.y, -h.KickPos.z) * kp;
        }

        public void Update(float dt, in GunHandling h, bool aiming)
        {
            float stiff = aiming ? Mathf.Max(0.01f, h.AdsSpring) : 1f;
            _acc += dt;
            int steps = 0;
            while (_acc >= Step && steps < MaxSteps)
            {
                _acc -= Step;
                steps++;
                _prevPos = _sp;
                _prevRot = _sr;
                for (int a = 0; a < 3; a++)
                {
                    Spring(ref _rp, ref _rv, a, 0f, h.SpringPos * stiff, h.DamperPos);
                    Spring(ref _rr, ref _rw, a, 0f, h.SpringRot * stiff, h.DamperRot);
                    Spring(ref _sp, ref _sv, a, _rp[a], SwaySpring[a], SwayDamper);
                    Spring(ref _sr, ref _sw, a, _rr[a], SwayRotSpring, SwayRotDamper);
                }
            }
            if (steps == MaxSteps) _acc = 0f; // a long hitch: don't try to catch up
        }

        /// <summary>One implicit (backward Euler) step of a unit mass on a damped spring, plus body damping.</summary>
        private static void Spring(ref Vector3 x, ref Vector3 v, int axis, float target, float k, float c)
        {
            float xi = x[axis], vi = v[axis];
            vi = (vi - Step * k * (xi - target)) / (1f + Step * c + Step * Step * k);
            vi /= 1f + Step * BodyDamping;
            xi += vi * Step;
            x[axis] = xi;
            v[axis] = vi;
        }
    }
}
