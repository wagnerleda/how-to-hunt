using HowToHunt.Avatar;
using HowToHunt.Player;
using UnityEngine;

namespace HowToHunt.Npcs
{
    /// <summary>
    /// Poses a camp character's stick figure (the hunters' body, built under Figure by the editor): standing behind
    /// their stall with their hands on the counter, breathing, the head turning to look at you when you come near, and
    /// gesturing while they talk to you. Wears the mask from their definition (or one made from their name). No
    /// allocations per frame.
    /// </summary>
    [RequireComponent(typeof(Npc))]
    public sealed class NpcBody : MonoBehaviour
    {
        private const float ShoulderY = 0.42f, ShoulderX = 0.19f, HipY = -0.06f, HipX = 0.09f;
        private const float UpperArm = 0.3f, Forearm = 0.28f, Thigh = 0.43f, Shin = 0.43f, FootRadius = 0.06f, LimbThickness = 0.045f;
        private const float LookRange = 9f, LookLimit = 70f, BreathHz = 0.25f;
        private static readonly Vector3 Counter = new Vector3(0f, 0.12f, 0.42f); // hands rest here, in front at waist height

        private Npc _npc;
        private Transform _figure, _head;
        private readonly Transform[] _upper = new Transform[4], _lower = new Transform[4], _ends = new Transform[4];
        private static readonly string[] UpperNames = { "UpperArm.L", "UpperArm.R", "Thigh.L", "Thigh.R" };
        private static readonly string[] LowerNames = { "Forearm.L", "Forearm.R", "Shin.L", "Shin.R" };
        private static readonly string[] EndNames = { "Hand.L", "Hand.R", "Foot.L", "Foot.R" };
        private Vector3 _headRest;
        private float _headYaw, _seed, _bodyYaw;
        private const float BodyTurnLimit = 40f; // while talking the whole body turns toward you, this far at most
        private bool _ready;

        private void Awake()
        {
            _npc = GetComponent<Npc>();
            _figure = transform.Find("Figure");
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

        private void LateUpdate()
        {
            if (!_ready) return;
            float t = Time.time + _seed, dt = Time.deltaTime;
            bool talking = DialogueBox.TalkingTo == _npc;
            float breath = Mathf.Sin(t * BreathHz * Mathf.PI * 2f) * 0.008f;

            // While talking, the whole body turns toward you (up to BodyTurnLimit); the head does the rest.
            PlayerMotor me = PlayerMotor.Local;
            float bodyTarget = 0f;
            if (talking && me != null)
            {
                Vector3 local = transform.InverseTransformPoint(me.transform.position);
                bodyTarget = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -BodyTurnLimit, BodyTurnLimit);
            }
            _bodyYaw = Mathf.Lerp(_bodyYaw, bodyTarget, 1f - Mathf.Exp(-5f * dt));
            _figure.localRotation = Quaternion.Euler(0f, _bodyYaw, 0f);

            // Look at the local player when close (the head turns, the body stays at the stall unless talking).
            float targetYaw = 0f, targetPitch = 0f;
            if (me != null)
            {
                Vector3 to = _figure.InverseTransformPoint(me.transform.position + Vector3.up * 0.7f) - _headRest;
                if (to.magnitude < LookRange)
                {
                    targetYaw = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -LookLimit, LookLimit);
                    targetPitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -25f, 25f);
                }
            }
            _headYaw = Mathf.Lerp(_headYaw, targetYaw, 1f - Mathf.Exp(-6f * dt));
            float nod = talking ? Mathf.Sin(t * 5f) * 4f : 0f;
            _head.localPosition = _headRest + new Vector3(0f, breath, 0f);
            _head.localRotation = Quaternion.Euler(targetPitch * 0.6f + nod, _headYaw, 0f);

            // Legs: standing, feet a little apart.
            float ground = -Movement.HalfHeight + FootRadius;
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? -1f : 1f;
                Limb(2 + leg, new Vector3(side * HipX, HipY + breath, 0f), new Vector3(side * (HipX + 0.04f), ground, 0.03f), Thigh, Shin, Vector3.forward);
            }
            // Arms: hands on the counter; while talking the right hand gestures.
            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? -1f : 1f;
                Vector3 shoulder = new Vector3(side * ShoulderX, ShoulderY + breath, 0f);
                Vector3 hand = Counter + new Vector3(side * 0.16f, 0f, 0f);
                if (talking && arm == 1) hand += new Vector3(0.05f, 0.22f + Mathf.Sin(t * 3.1f) * 0.06f, -0.08f + Mathf.Sin(t * 2.3f) * 0.05f);
                Limb(arm, shoulder, hand, UpperArm, Forearm, new Vector3(side * 0.5f, -0.3f, -1f));
            }
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
