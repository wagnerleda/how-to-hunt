using HowToHunt.Core;
using UnityEngine;

namespace HowToHunt.Avatar
{
    /// <summary>
    /// The hunter on the main menu (MenuVista): the players' stick figure sitting on a rock at sunset, a rifle resting
    /// on the right thigh and leaning on the shoulder (its barrel up behind the head), the left hand on the knee, breathing, the head looking out over the plains and now and then
    /// glancing around, the body swaying a little. Wears your own mask (cl_mask). No allocations per frame.
    /// </summary>
    public sealed class RestingHunter : MonoBehaviour
    {
        private const float ShoulderY = 0.42f, ShoulderX = 0.19f, HipY = -0.06f, HipX = 0.09f;
        private const float UpperArm = 0.3f, Forearm = 0.28f, Thigh = 0.43f, Shin = 0.43f, LimbThickness = 0.045f;
        private const float BreathHz = 0.22f, Lean = 9f;

        [Tooltip("Where the feet rest, below the hips (the rock's height), metres.")]
        [SerializeField] private float _seatHeight = 0.48f;
        [Tooltip("The rifle (a child): stands between the knees, hands on it.")]
        [SerializeField] private Transform _rifle;
        [SerializeField] private float _rifleLength = 1.1f;

        private Transform _figure, _head;
        private readonly Transform[] _upper = new Transform[4], _lower = new Transform[4], _ends = new Transform[4];
        private static readonly string[] UpperNames = { "UpperArm.L", "UpperArm.R", "Thigh.L", "Thigh.R" };
        private static readonly string[] LowerNames = { "Forearm.L", "Forearm.R", "Shin.L", "Shin.R" };
        private static readonly string[] EndNames = { "Hand.L", "Hand.R", "Foot.L", "Foot.R" };
        private Vector3 _headRest;
        private float _lookYaw, _lookTarget, _nextGlance;
        private string _maskShown;
        private Texture2D _maskTexture;
        private bool _ready;

        public void Configure(Transform rifle, float rifleLength, float seatHeight)
        {
            _rifle = rifle;
            _rifleLength = rifleLength;
            _seatHeight = seatHeight;
        }

        private void Awake()
        {
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
            _ready = true;
        }

        private void OnEnable() => PaintMask();

        private void OnDestroy()
        {
            if (_maskTexture != null) Destroy(_maskTexture);
        }

        /// <summary>Your own mask on the hunter (repainted when it changes, e.g. after the mask editor).</summary>
        private void PaintMask()
        {
            if (_head == null) return;
            string text = AvatarModule.MaskSetting.Value;
            if (text == _maskShown) return;
            _maskShown = text;
            Transform mask = _head.Find("Mask");
            if (mask == null) return;
            var cells = new byte[MaskData.Cells];
            if (!MaskData.FromText(text, cells)) cells = MaskData.Default();
            if (_maskTexture == null) _maskTexture = MaskData.NewTexture("Menu Hunter Mask");
            MaskData.Paint(_maskTexture, cells, new Color32[MaskData.Cells]);
            var block = new MaterialPropertyBlock();
            Renderer r = mask.GetComponent<Renderer>();
            r.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", _maskTexture);
            r.SetPropertyBlock(block);
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            bool calm = UiFeel.Calm;
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            float breath = calm ? 0f : Mathf.Sin(t * BreathHz * Mathf.PI * 2f) * 0.012f;
            if (Time.frameCount % 60 == 0) PaintMask();

            // Leaning forward a little over the rifle, swaying slowly.
            float sway = calm ? 0f : Mathf.Sin(t * 0.31f) * 1.5f;
            _figure.localRotation = Quaternion.Euler(Lean + breath * 40f, 0f, sway);

            // The head: out over the plains, now and then a glance aside (and back).
            if (!calm && t >= _nextGlance)
            {
                _lookTarget = Mathf.Abs(_lookTarget) > 1f ? 0f : (Random.value < 0.5f ? -1f : 1f) * Random.Range(18f, 35f);
                _nextGlance = t + (_lookTarget == 0f ? Random.Range(5f, 9f) : Random.Range(1.8f, 3.2f));
            }
            _lookYaw = Mathf.Lerp(_lookYaw, calm ? 0f : _lookTarget, 1f - Mathf.Exp(-2.2f * dt));
            _head.localPosition = _headRest + new Vector3(0f, breath, 0f);
            _head.localRotation = Quaternion.Euler(-Lean * 0.8f + (calm ? 0f : Mathf.Sin(t * 0.5f) * 1.5f), _lookYaw, 0f);

            // Legs: sitting, knees up, feet on the ground in front.
            float feetY = HipY - _seatHeight + 0.06f;
            for (int leg = 0; leg < 2; leg++)
            {
                float side = leg == 0 ? -1f : 1f;
                Limb(2 + leg, new Vector3(side * HipX, HipY, 0f), new Vector3(side * 0.2f, feetY, 0.5f), Thigh, Shin, new Vector3(side * 0.2f, 1f, 1f));
            }

            // The rifle: butt on the right thigh, leaning back onto the right shoulder, barrel up behind the head.
            Vector3 butt = new Vector3(0.2f, HipY + 0.05f, 0.3f);
            Vector3 up = new Vector3(0.06f, 1f, -0.62f).normalized;
            if (_rifle != null)
                _rifle.SetPositionAndRotation(_figure.TransformPoint(butt), _figure.rotation * Quaternion.LookRotation(up, new Vector3(-1f, 0f, 0f)));
            Vector3 grip = butt + up * (_rifleLength * 0.3f);
            Limb(1, new Vector3(ShoulderX, ShoulderY + breath, 0f), grip + new Vector3(0.03f, 0f, 0.02f), UpperArm, Forearm, new Vector3(1f, -0.6f, -0.3f));
            // The left hand rests on the left knee.
            Vector3 knee = new Vector3(-0.2f, HipY + 0.04f, 0.38f);
            Limb(0, new Vector3(-ShoulderX, ShoulderY + breath, 0f), knee, UpperArm, Forearm, new Vector3(-1f, -0.3f, -0.4f));
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
    }
}
