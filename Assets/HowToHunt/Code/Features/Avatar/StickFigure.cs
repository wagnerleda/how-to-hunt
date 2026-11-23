using HowToHunt.Net;
using HowToHunt.Player;
using UnityEngine;

namespace HowToHunt.Avatar
{
    /// <summary>
    /// Drives the players' stick figure (built by the editor under Visual/Figure): thin limbs between joints, ball hands
    /// and feet, two-bone IK for arms and legs. Feet walk a cycle from the body's speed and plant on the ground (crouching
    /// bends the knees), hands reach for the held tool (or swing), the head looks where the player looks, and the mask
    /// shows the player's own painting. No allocations per frame.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    [DefaultExecutionOrder(100)] // after ThirdPersonTool has placed the held tool this frame
    public sealed class StickFigure : MonoBehaviour
    {
        // Body layout, from the hull centre (+Z forward). The hull is 1.83 m tall.
        private const float ShoulderY = 0.42f, ShoulderX = 0.19f, HipY = -0.06f, HipX = 0.09f;
        private const float UpperArm = 0.3f, Forearm = 0.28f, Thigh = 0.43f, Shin = 0.43f, FootRadius = 0.06f;
        private const float LimbThickness = 0.045f;
        private const float StepLength = 0.75f;       // metres per step at a run
        private const float StrideReach = 0.3f, StepLift = 0.12f, ArmSwing = 0.22f;
        private const float HeadPitchShare = 0.7f, HeadPitchLimit = 55f;
        private const float SupportForward = 0.03f, SupportDown = 0.05f, SupportSide = 0.03f; // left hand under a one-handed grip

        private PlayerMotor _motor;
        private Transform _visual, _figure, _torso, _neck, _head, _leaf, _heldPivot;
        private readonly Transform[] _upper = new Transform[4], _lower = new Transform[4], _ends = new Transform[4]; // L arm, R arm, L leg, R leg
        private Vector3 _torsoRest, _neckRest, _headRest, _leafRest;
        private Vector3 _lastPos, _velocity;
        private float _phase, _crouch, _lie;
        private PlayerHealth _health;
        // Downed: the body lies on its back along the ground behind the feet. It used to tip 82 degrees about the
        // feet, so the back of the head and torso (0.15 m thick) sank into the ground, and slopes swallowed the head.
        private const float LieFlatDegrees = 90f, LieMinDegrees = 50f, LieMaxDegrees = 115f;
        private const float LieLift = 0.17f;     // the back's thickness: the body rests on the ground, not in it
        private const float LieReach = 1.55f;    // feet to the back of the head, along the ground
        private static readonly System.Numerics.Vector3 GroundProbe = new System.Numerics.Vector3(0.1f, 0.05f, 0.1f);
        private bool _ready;

        private Renderer _maskRenderer;
        private Texture2D _maskTexture;
        private MaterialPropertyBlock _maskBlock;
        private readonly byte[] _cells = new byte[MaskData.Cells];
        private readonly Color32[] _pixels = new Color32[MaskData.Cells];
        private int _maskVersion = -1;
        private string _maskText;

        private static readonly string[] UpperNames = { "UpperArm.L", "UpperArm.R", "Thigh.L", "Thigh.R" };
        private static readonly string[] LowerNames = { "Forearm.L", "Forearm.R", "Shin.L", "Shin.R" };
        private static readonly string[] EndNames = { "Hand.L", "Hand.R", "Foot.L", "Foot.R" };
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _visual = transform.Find("Visual");
            _figure = _visual != null ? _visual.Find("Figure") : null;
            if (_figure == null) return;
            _torso = _figure.Find("Torso");
            _neck = _figure.Find("Neck");
            _head = _figure.Find("Head");
            _leaf = _figure.Find("Leaf");
            for (int i = 0; i < 4; i++)
            {
                _upper[i] = _figure.Find(UpperNames[i]);
                _lower[i] = _figure.Find(LowerNames[i]);
                _ends[i] = _figure.Find(EndNames[i]);
                if (_upper[i] == null || _lower[i] == null || _ends[i] == null) return;
            }
            if (_torso == null || _neck == null || _head == null) return;
            _torsoRest = _torso.localPosition;
            _neckRest = _neck.localPosition;
            _headRest = _head.localPosition;
            if (_leaf != null) _leafRest = _leaf.localPosition;
            Transform mask = _head.Find("Mask");
            if (mask != null) _maskRenderer = mask.GetComponent<Renderer>();
            _lastPos = _visual.position;
            _ready = true;
        }

        /// <summary>
        /// How far to tip back so the head rests on the ground behind the feet: 90 degrees on flat ground, less when
        /// the ground behind rises, more when it falls away. One short trace per frame, only while downed.
        /// </summary>
        private static float LieAngle(Vector3 feetWorld, Quaternion facing)
        {
            Vector3 head = feetWorld + facing * (Vector3.back * LieReach);
            var trace = Core.SimWorld.Current.TraceBox(new System.Numerics.Vector3(head.x, feetWorld.y + LieReach, head.z),
                new System.Numerics.Vector3(head.x, feetWorld.y - LieReach * 2f, head.z), GroundProbe);
            if (trace.StartSolid || trace.Fraction >= 1f) return LieFlatDegrees;
            float rise = trace.EndPosition.Y - GroundProbe.Y - feetWorld.y;
            float slope = Mathf.Atan2(rise, LieReach) * Mathf.Rad2Deg;
            return Mathf.Clamp(LieFlatDegrees - slope, LieMinDegrees, LieMaxDegrees);
        }

        private void OnDestroy()
        {
            if (_maskTexture != null) Destroy(_maskTexture);
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            UpdateMask();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Facing: the body turns with the view (the network root does not rotate).
            // Downed: the figure tips over backwards onto the ground (about the feet).
            if (_health == null) _health = GetComponent<PlayerHealth>();
            _lie = Mathf.MoveTowards(_lie, _health != null && _health.Downed ? 1f : 0f, dt * 2.5f);
            Quaternion facing = Quaternion.Euler(0f, _motor.ViewYaw, 0f);
            Vector3 feet = new Vector3(0f, -MovementCvars.Config.HullHalfExtents.Y, 0f);
            float lieDegrees = _lie > 0f ? LieAngle(_visual.position + feet, facing) : LieFlatDegrees;
            Quaternion tip = Quaternion.Euler(-lieDegrees * _lie, 0f, 0f);
            _figure.rotation = facing * tip;
            _figure.position = _visual.position + facing * (feet - tip * feet) + Vector3.up * (LieLift * _lie);

            // Speed from how the smoothed body moved.
            Vector3 pos = _visual.position, step = (pos - _lastPos) / dt;
            _lastPos = pos;
            step.y = 0f;
            if (step.sqrMagnitude > 400f) step = Vector3.zero; // a teleport
            _velocity = Vector3.Lerp(_velocity, step, 1f - Mathf.Exp(-12f * dt));
            Vector3 localVel = _figure.InverseTransformDirection(_velocity);
            float speed = localVel.magnitude;
            Vector3 moveDir = speed > 0.05f ? localVel / speed : Vector3.forward;
            bool grounded = _motor.State.OnGround || _motor.State.InWater;
            if (grounded) _phase = Mathf.Repeat(_phase + speed / StepLength * Mathf.PI * dt, Mathf.PI * 2f);

            // Crouch: the upper body drops, the feet stay on the ground, the knees bend.
            var cfg = MovementCvars.Config;
            _crouch = Mathf.MoveTowards(_crouch, _motor.State.Ducked ? 1f : 0f, dt * 5f);
            float half = Mathf.Lerp(cfg.HullHalfExtents.Y, cfg.DuckHalfHeight, _crouch);
            float drop = cfg.HullHalfExtents.Y - half;
            Vector3 down = new Vector3(0f, -drop, 0f);
            _torso.localPosition = _torsoRest + down;
            _neck.localPosition = _neckRest + down;
            float pitch = Mathf.Clamp(_motor.ViewPitch * HeadPitchShare, -HeadPitchLimit, HeadPitchLimit);
            _head.localPosition = _headRest + down;
            _head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            if (_leaf != null) _leaf.localPosition = _leafRest + down;

            float ground = -half + FootRadius;
            float gait = Mathf.Clamp01(speed / 4f);
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? -1f : 1f;
                float p = _phase + (leg == 0 ? 0f : Mathf.PI);
                Vector3 hip = new Vector3(side * HipX, HipY - drop, 0f);
                Vector3 foot;
                if (grounded)
                {
                    foot = new Vector3(side * (HipX + 0.03f), ground + StepLift * gait * Mathf.Max(0f, Mathf.Cos(p)), 0.02f)
                         + moveDir * (StrideReach * gait * Mathf.Sin(p));
                }
                else
                {
                    foot = new Vector3(side * (HipX + 0.04f), ground + 0.28f, -0.06f + (leg == 0 ? 0.08f : -0.04f));
                }
                Limb(2 + leg, hip, foot, Thigh, Shin, new Vector3(0f, 0f, 1f));
            }

            Transform held = HeldModel();
            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? -1f : 1f;
                Vector3 shoulder = new Vector3(side * ShoulderX, ShoulderY - drop, 0f);
                Vector3 hand;
                if (held != null)
                {
                    // Right hand on the grip; left hand on the fore-end of a long gun, else cupping under the grip.
                    Transform front = arm == 0 ? FindDeep(held, "FrontGrip") : null;
                    Vector3 at = arm == 1 ? held.position
                        : front != null ? front.position
                        : held.position + held.forward * SupportForward - held.up * SupportDown - held.right * SupportSide;
                    hand = _figure.InverseTransformPoint(at);
                }
                else
                {
                    float p = _phase + (arm == 0 ? 0f : Mathf.PI);
                    hand = shoulder + new Vector3(side * 0.07f, -0.5f, 0.02f) - moveDir * (ArmSwing * gait * Mathf.Sin(p));
                    if (!grounded) hand += new Vector3(side * 0.15f, 0.2f, 0f);
                }
                Limb(arm, shoulder, hand, UpperArm, Forearm, new Vector3(side * 0.5f, -0.3f, -1f));
            }
        }

        /// <summary>Two-bone IK in figure space: places the upper and lower segments and the ball at the end.</summary>
        private void Limb(int i, Vector3 root, Vector3 target, float a, float b, Vector3 hint)
        {
            Vector3 to = target - root;
            float d = Mathf.Clamp(to.magnitude, 0.01f, a + b - 0.001f);
            Vector3 dir = to.sqrMagnitude > 1e-8f ? to.normalized : Vector3.down;
            float x = (a * a - b * b + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 bend = hint - Vector3.Dot(hint, dir) * dir;
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.forward;
            Vector3 joint = root + dir * x + bend * h, end = root + dir * d;
            Segment(_upper[i], root, joint);
            Segment(_lower[i], joint, end);
            _ends[i].position = _figure.TransformPoint(end);
        }

        /// <summary>A unit cylinder (2 m tall, 1 m wide) stretched between two figure-space points.</summary>
        private void Segment(Transform t, Vector3 from, Vector3 to)
        {
            Vector3 a = _figure.TransformPoint(from), b = _figure.TransformPoint(to), ab = b - a;
            t.position = (a + b) * 0.5f;
            t.rotation = ab.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, ab) : Quaternion.identity;
            t.localScale = new Vector3(LimbThickness, ab.magnitude * 0.5f, LimbThickness);
        }

        /// <summary>The model ThirdPersonTool shows in the hand (its origin is the grip), if any.</summary>
        private Transform HeldModel()
        {
            if (_heldPivot == null) _heldPivot = _visual.Find("Held Tool");
            if (_heldPivot == null) return null;
            for (int i = 0; i < _heldPivot.childCount; i++)
            {
                Transform c = _heldPivot.GetChild(i);
                if (c.gameObject.activeSelf) return c;
            }
            return null;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                if (c.name == name) return c;
                Transform deeper = FindDeep(c, name);
                if (deeper != null) return deeper;
            }
            return null;
        }

        /// <summary>Our own mask from the setting; other players' from what the host passed round.</summary>
        private void UpdateMask()
        {
            if (_maskRenderer == null) return;
            bool changed;
            if (_motor.IsOwner)
            {
                string text = AvatarModule.MaskSetting.Value;
                changed = !ReferenceEquals(text, _maskText);
                if (changed)
                {
                    _maskText = text;
                    if (!MaskData.FromText(text, _cells)) System.Array.Copy(MaskData.Default(), _cells, MaskData.Cells);
                }
            }
            else
            {
                changed = _maskVersion != PartyRoster.MasksVersion;
                if (changed)
                {
                    _maskVersion = PartyRoster.MasksVersion;
                    if (!PartyRoster.TryGetMask(_motor.OwnerId, out byte[] data) || !MaskData.Unpack(data, _cells))
                        System.Array.Copy(MaskData.Default(), _cells, MaskData.Cells);
                }
            }
            if (!changed) return;
            if (_maskTexture == null) _maskTexture = MaskData.NewTexture("Mask");
            MaskData.Paint(_maskTexture, _cells, _pixels);
            _maskBlock ??= new MaterialPropertyBlock();
            _maskRenderer.GetPropertyBlock(_maskBlock);
            _maskBlock.SetTexture(BaseMap, _maskTexture);
            _maskRenderer.SetPropertyBlock(_maskBlock);
        }
    }
}
