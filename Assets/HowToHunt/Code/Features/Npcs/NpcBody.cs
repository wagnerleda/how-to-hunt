using HowToHunt.Avatar;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Player;
using UnityEngine;

namespace HowToHunt.Npcs
{
    /// <summary>
    /// Poses a camp character's stick figure (the hunters' body, built under Figure by the editor) for what they do
    /// (NpcDefinition.Activity): behind their stall with their hands on the counter; sitting at the campfire warming
    /// their hands; chopping wood (an axe swung onto a block, a thunk each time); sitting whittling a stick. They all
    /// breathe, turn their head to you when you come near, and while you talk to them they stop what they're doing,
    /// turn toward you and gesture. Wears the mask from their definition (or one made from their name). No
    /// allocations per frame.
    /// </summary>
    [RequireComponent(typeof(Npc))]
    public sealed class NpcBody : MonoBehaviour
    {
        private const float ShoulderY = 0.42f, ShoulderX = 0.19f, HipY = -0.06f, HipX = 0.09f;
        private const float UpperArm = 0.3f, Forearm = 0.28f, Thigh = 0.43f, Shin = 0.43f, FootRadius = 0.06f, LimbThickness = 0.045f;
        private const float LookRange = 9f, LookLimit = 70f, BreathHz = 0.25f;
        private static readonly Vector3 Counter = new Vector3(0f, 0.12f, 0.42f); // hands rest here, in front at waist height
        /// <summary>Sitting characters: the seat (a log, a stump) is this high; the builder puts one under them.</summary>
        public const float SeatHeight = 0.45f;
        private const float ChopSeconds = 1.9f, ChopReach = 0.62f;

        private Npc _npc;
        private Transform _figure, _head, _tool;
        private readonly Transform[] _upper = new Transform[4], _lower = new Transform[4], _ends = new Transform[4];
        private static readonly string[] UpperNames = { "UpperArm.L", "UpperArm.R", "Thigh.L", "Thigh.R" };
        private static readonly string[] LowerNames = { "Forearm.L", "Forearm.R", "Shin.L", "Shin.R" };
        private static readonly string[] EndNames = { "Hand.L", "Hand.R", "Foot.L", "Foot.R" };
        private Vector3 _headRest;
        private float _headYaw, _seed, _bodyYaw, _lastChopPhase;
        private const float BodyTurnLimit = 40f; // while talking the whole body turns toward you, this far at most
        private bool _ready;

        private void Awake()
        {
            _npc = GetComponent<Npc>();
            _figure = transform.Find("Figure");
            _tool = transform.Find("Tool");
            if (_figure == null) return;
            _head = _figure.Find("Head");
            for (int i = 0; i < 4; i++)
            {
                _upper[i] = _figure.Find(UpperNames[i]);
                _lower[i] = _figure.Find(LowerNames[i]);
                _ends[i] = _figure.Find(EndNames[i]);
                if (_upper[i] == null || _lower[i] == null || _ends[i] == null) return;
            }
            if (_head == null) return;
            _headRest = _head.localPosition;
            _seed = Random.value * 10f;
            PaintMask();
            _ready = true;
        }

        private void PaintMask()
        {
            Transform mask = _head.Find("Mask");
            if (mask == null || _npc.Definition == null) return;
            var cells = new byte[MaskData.Cells];
            if (!MaskData.FromText(_npc.Definition.Mask, cells)) NpcMasks.Make(_npc.Definition.Key, cells);
            Texture2D tex = MaskData.NewTexture("NPC Mask");
            MaskData.Paint(tex, cells, new Color32[MaskData.Cells]);
            var block = new MaterialPropertyBlock();
            Renderer r = mask.GetComponent<Renderer>();
            r.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", tex);
            r.SetPropertyBlock(block);
        }

        private NpcActivity Activity => _npc.Definition != null ? _npc.Definition.Activity : NpcActivity.Stall;
        private bool Sitting => Activity == NpcActivity.Campfire || Activity == NpcActivity.Whittle;

        private void LateUpdate()
        {
            if (!_ready) return;
            float t = Time.time + _seed, dt = Time.deltaTime;
            bool talking = DialogueBox.TalkingTo == _npc;
            float breath = Mathf.Sin(t * BreathHz * Mathf.PI * 2f) * 0.008f;
            NpcActivity activity = Activity;

            // Sitting characters sit: the hips come down onto the seat.
            float drop = Sitting ? SeatHeight - Movement.HalfHeight - HipY : 0f;

            // While talking, the whole body turns toward you (up to BodyTurnLimit); the head does the rest.
            PlayerMotor me = PlayerMotor.Local;
            float bodyTarget = 0f;
            if (talking && me != null)
            {
                Vector3 local = transform.InverseTransformPoint(me.transform.position);
                bodyTarget = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -BodyTurnLimit, BodyTurnLimit);
            }
            _bodyYaw = Mathf.Lerp(_bodyYaw, bodyTarget, 1f - Mathf.Exp(-5f * dt));
            float lean = activity == NpcActivity.Campfire ? 10f : activity == NpcActivity.Whittle ? 16f : 0f;
            if (talking) lean *= 0.3f;
            _figure.localPosition = new Vector3(0f, drop, 0f);
            _figure.localRotation = Quaternion.Euler(lean, _bodyYaw, 0f);

            // Look at the local player when close; otherwise at the work (the fire, the stick, the block).
            float targetYaw = 0f, targetPitch = activity == NpcActivity.Stall ? 0f : 18f;
            if (me != null)
            {
                Vector3 to = _figure.InverseTransformPoint(me.transform.position + Vector3.up * 0.7f) - _headRest;
                if (to.magnitude < (activity == NpcActivity.Stall || talking ? LookRange : LookRange * 0.5f))
                {
                    targetYaw = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -LookLimit, LookLimit);
                    targetPitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -25f, 25f);
                }
            }
            _headYaw = Mathf.Lerp(_headYaw, targetYaw, 1f - Mathf.Exp(-6f * dt));
            float nod = talking ? Mathf.Sin(t * 5f) * 4f : 0f;
            _head.localPosition = _headRest + new Vector3(0f, breath, 0f);
            _head.localRotation = Quaternion.Euler(targetPitch * 0.6f + nod - lean * 0.6f, _headYaw, 0f);

            Legs(breath, drop);
            switch (activity)
            {
                case NpcActivity.Campfire: Campfire(t, breath, talking); break;
                case NpcActivity.ChopWood: Chop(t, breath, talking); break;
                case NpcActivity.Whittle: Whittle(t, breath, talking); break;
                default: Stall(t, breath, talking); break;
            }
        }

        private void Legs(float breath, float drop)
        {
            float ground = -Movement.HalfHeight + FootRadius - drop; // in figure space (the figure moved down by drop)
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? -1f : 1f;
                if (Sitting) // knees up, feet on the ground in front
                    Limb(2 + leg, new Vector3(side * HipX, HipY, 0f), new Vector3(side * 0.16f, ground, 0.42f), Thigh, Shin, new Vector3(side * 0.2f, 1f, 1f));
                else
                    Limb(2 + leg, new Vector3(side * HipX, HipY + breath, 0f), new Vector3(side * (HipX + 0.04f), ground, 0.03f), Thigh, Shin, Vector3.forward);
            }
        }

        private void Stall(float t, float breath, bool talking)
        {
            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? -1f : 1f;
                Vector3 hand = Counter + new Vector3(side * 0.16f, 0f, 0f);
                if (talking && arm == 1) hand += new Vector3(0.05f, 0.22f + Mathf.Sin(t * 3.1f) * 0.06f, -0.08f + Mathf.Sin(t * 2.3f) * 0.05f);
                Arm(arm, breath, hand);
            }
        }

        /// <summary>Hands held out to the fire, rubbed now and then.</summary>
        private void Campfire(float t, float breath, bool talking)
        {
            float rub = Mathf.Max(0f, Mathf.Sin(t * 0.45f)) * Mathf.Sin(t * 9f) * 0.03f;
            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? -1f : 1f;
                Vector3 hand = new Vector3(side * (0.09f + rub * side), 0.12f + rub, 0.5f);
                if (talking && arm == 1) hand = new Vector3(0.2f, 0.3f + Mathf.Sin(t * 3.1f) * 0.06f, 0.3f);
                Arm(arm, breath, hand);
            }
        }

        /// <summary>The axe goes up over the shoulder and comes down on the block in front; a thunk as it lands.</summary>
        private void Chop(float t, float breath, bool talking)
        {
            float phase = talking ? 0.95f : Mathf.Repeat(t / ChopSeconds, 1f);
            // 0..0.55 lift slowly, 0.55..0.7 swing down fast, 0.7..1 rest on the block.
            float up = phase < 0.55f ? Ease(phase / 0.55f) : phase < 0.7f ? 1f - Ease((phase - 0.55f) / 0.15f) : 0f;
            if (!talking && _lastChopPhase < 0.7f && phase >= 0.7f && (PlayerMotor.Local == null || (PlayerMotor.Local.transform.position - transform.position).sqrMagnitude < 900f))
                Sfx.PlayAt("Kenney/impactWood_light", transform.position + transform.forward * ChopReach, 0.7f, Random.Range(0.6f, 0.75f));
            _lastChopPhase = phase;
            // Both hands near the end of the handle; the head of the axe arcs from behind the head to the block.
            float angle = Mathf.Lerp(-12f, 170f, up); // 0 = pointing forward-down at the block, up = back over the head
            Vector3 grip = new Vector3(0f, Mathf.Lerp(0.02f, 0.62f, up), Mathf.Lerp(0.3f, 0.05f, up));
            Quaternion swing = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 handle = swing * Vector3.forward;
            if (_tool != null)
            {
                _tool.SetPositionAndRotation(_figure.TransformPoint(grip), _figure.rotation * Quaternion.LookRotation(handle, swing * Vector3.up));
            }
            Arm(1, breath, grip + handle * 0.06f);
            Arm(0, breath, grip - handle * 0.06f);
        }

        /// <summary>A stick in the left hand, the knife in the right shaving it in short strokes.</summary>
        private void Whittle(float t, float breath, bool talking)
        {
            Vector3 stick = new Vector3(-0.05f, 0.05f, 0.38f);
            float stroke = talking ? 0f : Mathf.Repeat(t * 1.6f, 1f);
            float s = stroke < 0.35f ? stroke / 0.35f : 1f - (stroke - 0.35f) / 0.65f;
            Vector3 knife = stick + new Vector3(0.1f - s * 0.08f, 0.04f + s * 0.05f, 0.02f);
            if (talking) knife = new Vector3(0.2f, 0.3f + Mathf.Sin(t * 3.1f) * 0.06f, 0.3f);
            if (_tool != null) _tool.SetPositionAndRotation(_figure.TransformPoint(stick), _figure.rotation * Quaternion.Euler(-35f, 20f, 0f));
            Arm(0, breath, stick);
            Arm(1, breath, knife);
        }

        private static float Ease(float x) => x * x * (3f - 2f * x);

        private void Arm(int arm, float breath, Vector3 hand)
        {
            float side = arm == 0 ? -1f : 1f;
            Limb(arm, new Vector3(side * ShoulderX, ShoulderY + breath, 0f), hand, UpperArm, Forearm, new Vector3(side * 0.5f, -0.3f, -1f));
        }

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

        private void Segment(Transform t, Vector3 from, Vector3 to)
        {
            Vector3 a = _figure.TransformPoint(from), b = _figure.TransformPoint(to), ab = b - a;
            t.position = (a + b) * 0.5f;
            t.rotation = ab.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, ab) : Quaternion.identity;
            t.localScale = new Vector3(LimbThickness, ab.magnitude * 0.5f, LimbThickness);
        }

        private static class Movement
        {
            public static float HalfHeight => MovementCvars.Config.HullHalfExtents.Y;
        }
    }
}
