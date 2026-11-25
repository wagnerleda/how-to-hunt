using HowToHunt.Core;
using HowToHunt.Net;
using UnityEngine;

namespace HowToHunt.Avatar
{
    /// <summary>
    /// Main menu > Customize your mask: paint your tribe mask cell by cell (left click paints, right click cuts away,
    /// middle click picks a colour, mirror paints both halves, fill floods an area), or import a picture onto it, with
    /// a turning preview of your head wearing it. Saved to cl_mask.
    /// </summary>
    public sealed class MaskEditor : MonoBehaviour
    {
        private const float Cell = 7.5f, PanelW = 860f, PanelH = 560f;
        private const int MaxImageBytes = 32 * 1024 * 1024;
        private const int PreviewW = 240, PreviewH = 300;
        private static readonly Vector3 Stage = new Vector3(0f, -950f, 0f);

        private static MaskEditor _instance;
        private bool _open;
        private ImGuiHost _gui;
        private readonly byte[] _cells = new byte[MaskData.Cells];
        private readonly byte[] _undo = new byte[MaskData.Cells];
        private readonly Color32[] _pixels = new Color32[MaskData.Cells];
        private byte _colour = 6;
        private int _brush = 1;
        private bool _mirror = true, _fill;
        private Texture2D _white, _maskTexture;
        private GUIStyle _title, _label, _small, _button;
        private Texture2D _checker;
        private string _importNote;

        private GameObject _stage;
        private Camera _camera;
        private RenderTexture _preview;
        private Transform _bust;

        public static void Show()
        {
            if (_instance == null) return;
            _instance.Open();
        }

        private void Awake()
        {
            _gui = ImGuiHost.Attach(gameObject, DrawGui);
            _instance = this;
            MenuPanels.Open = null; // a panel left open when the Editor stopped playing
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_stage != null) Destroy(_stage);
            if (_preview != null) _preview.Release();
        }

        private void Open()
        {
            if (!MaskData.FromText(AvatarModule.MaskSetting.Value, _cells)) System.Array.Copy(MaskData.Default(), _cells, MaskData.Cells);
            System.Array.Copy(_cells, _undo, MaskData.Cells);
            _open = true;
            _gui.Show(true);
            MenuPanels.Open = this;
            EnsurePreview();
            _stage.SetActive(true);
            Repaint();
        }

        private void Close(bool save)
        {
            if (save) AvatarModule.MaskSetting.Value = MaskData.ToText(_cells);
            _open = false;
            _gui.Show(false);
            if (ReferenceEquals(MenuPanels.Open, this)) MenuPanels.Open = null;
            if (_stage != null) _stage.SetActive(false);
        }

        private void Update()
        {
            if (!_open) return;
            // Joining (an invite accepted from Steam) closes the editor and keeps the painting.
            if (NetSession.State != SessionState.Offline) { Close(save: true); return; }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Close(save: false);
            if (_bust != null) _bust.localRotation = Quaternion.Euler(0f, 180f + 35f * Mathf.Sin(Time.unscaledTime * 0.8f), 0f);
        }

        // --- preview -------------------------------------------------------------------------------------------

        /// <summary>A head and shoulders far below the world, filmed into a texture while the editor is open.</summary>
        private void EnsurePreview()
        {
            if (_stage != null) return;
            _maskTexture = MaskData.NewTexture("Mask Preview");
            _stage = new GameObject("Mask Preview Stage");
            _stage.transform.position = Stage;
            Material skin = Resources.Load<Material>("Avatar/M_Skin"), mask = Resources.Load<Material>("Avatar/M_Mask");
            Mesh maskMesh = Resources.Load<Mesh>("Avatar/MaskPlate");
            _bust = new GameObject("Bust").transform;
            _bust.SetParent(_stage.transform, false);
            Prim(PrimitiveType.Sphere, _bust, new Vector3(0f, 0f, 0f), Vector3.one * AvatarShape.HeadSize, skin);
            Prim(PrimitiveType.Cylinder, _bust, new Vector3(0f, -0.2f, 0f), new Vector3(0.05f, 0.06f, 0.05f), skin);
            Prim(PrimitiveType.Capsule, _bust, new Vector3(0f, -0.48f, 0f), AvatarShape.TorsoScale, skin);
            var plate = new GameObject("Mask");
            plate.transform.SetParent(_bust, false);
            plate.transform.localPosition = AvatarShape.MaskOffset;
            plate.AddComponent<MeshFilter>().sharedMesh = maskMesh;
            var r = plate.AddComponent<MeshRenderer>();
            r.sharedMaterial = mask;
            var strap = new GameObject("Strap");
            strap.transform.SetParent(plate.transform, false);
            strap.AddComponent<MeshFilter>().sharedMesh = Resources.Load<Mesh>("Avatar/MaskStrap");
            strap.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("Avatar/M_Strap");
            var block = new MaterialPropertyBlock();
            block.SetTexture("_BaseMap", _maskTexture);
            r.SetPropertyBlock(block);

            _preview = new RenderTexture(PreviewW, PreviewH, 24) { name = "Mask Preview" };
            var camGo = new GameObject("Preview Camera");
            camGo.transform.SetParent(_stage.transform, false);
            camGo.transform.localPosition = new Vector3(0f, -0.14f, -1.9f);
            _camera = camGo.AddComponent<Camera>();
            _camera.targetTexture = _preview;
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 5f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.16f, 0.15f, 0.14f);
            var lightGo = new GameObject("Preview Light");
            lightGo.transform.SetParent(_stage.transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(25f, 20f, 0f);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 4f;
            l.intensity = 1.6f;
            lightGo.transform.localPosition = new Vector3(0.4f, 0.5f, -1f);
            _stage.SetActive(false);
        }

        private static void Prim(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.transform.localScale = scale;
            if (m != null) g.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        private void Repaint()
        {
            if (_maskTexture != null) MaskData.Paint(_maskTexture, _cells, _pixels);
        }

        // --- painting --------------------------------------------------------------------------------------------

        private void PaintAt(int c, int r, byte colour)
        {
            for (int dr = 0; dr < _brush; dr++)
            for (int dc = 0; dc < _brush; dc++)
            {
                Set(c + dc, r + dr, colour);
                if (_mirror) Set(MaskData.Width - 1 - (c + dc), r + dr, colour);
            }
        }

        private void Set(int c, int r, byte colour)
        {
            if (c < 0 || c >= MaskData.Width || r < 0 || r >= MaskData.Height) return;
            _cells[r * MaskData.Width + c] = colour;
        }

        /// <summary>Flood fill from a cell (4-connected), with a fixed stack: no allocations beyond the first.</summary>
        private readonly int[] _stack = new int[MaskData.Cells * 4];

        private void Flood(int c, int r, byte colour)
        {
            byte from = _cells[r * MaskData.Width + c];
            if (from == colour) return;
            int top = 0;
            _stack[top++] = r * MaskData.Width + c;
            while (top > 0)
            {
                int i = _stack[--top];
                if (_cells[i] != from) continue;
                _cells[i] = colour;
                int x = i % MaskData.Width, y = i / MaskData.Width;
                if (x > 0) _stack[top++] = i - 1;
                if (x < MaskData.Width - 1) _stack[top++] = i + 1;
                if (y > 0) _stack[top++] = i - MaskData.Width;
                if (y < MaskData.Height - 1) _stack[top++] = i + MaskData.Width;
            }
        }

        // --- importing a picture ----------------------------------------------------------------------------------

        /// <summary>Pick a PNG or JPG and paste it over the mask (outline and eye holes stay cut). Undo brings it back.</summary>
        private void ImportImage()
        {
            string path = ImageFileDialog.Open("A picture for your mask");
            if (string.IsNullOrEmpty(path)) { _importNote = ImageFileDialog.Supported ? null : "Importing needs Windows."; return; }
            try
            {
                var info = new System.IO.FileInfo(path);
                if (!info.Exists || info.Length > MaxImageBytes) { _importNote = "That file is too big."; return; }
                var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                bool ok = image.LoadImage(System.IO.File.ReadAllBytes(path));
                if (ok)
                {
                    MaskData.PasteImage(image.GetPixels32(), image.width, image.height, _cells);
                    _importNote = "Pasted " + System.IO.Path.GetFileName(path) + ".";
                    Repaint();
                }
                else _importNote = "Not a PNG or JPG picture.";
                Destroy(image);
            }
            catch (System.Exception e)
            {
                _importNote = "Could not read that file.";
                Debug.LogWarning("[Mask] Import failed: " + e.Message);
            }
        }

        // --- drawing ---------------------------------------------------------------------------------------------

        private void DrawGui()
        {
            if (!_open) return;
            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.height / 720f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), _white);
            GUI.color = new Color(0.13f, 0.12f, 0.11f, 0.97f);
            var panel = new Rect(Mathf.Round((w - PanelW) * 0.5f), Mathf.Round((h - PanelH) * 0.5f), PanelW, PanelH);
            GUI.DrawTexture(panel, _white);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + 24f, panel.y + 14f, 600f, 34f), "Your tribe mask", _title);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 48f, 800f, 22f),
                "Everyone hunts in the same body: your mask is how your friends tell you apart.", _small);

            var canvas = new Rect(panel.x + 24f, panel.y + 84f, MaskData.Width * Cell, MaskData.Height * Cell);
            DrawCanvas(canvas);
            float tx = canvas.xMax + 24f, ty = canvas.y;
            DrawPalette(new Rect(tx, ty, 180f, 180f));
            DrawTools(new Rect(tx, ty + 196f, 180f, 240f));

            var view = new Rect(panel.xMax - PreviewW - 24f, canvas.y, PreviewW, PreviewH);
            if (_preview != null) GUI.DrawTexture(view, _preview, ScaleMode.ScaleToFit, false);
            GUI.Label(new Rect(view.x, view.yMax + 6f, PreviewW, 40f), "Left click paints, right click cuts away, middle click picks a colour. Cut cells are see-through.", _small);
            if (GUI.Button(new Rect(view.x, view.yMax + 48f, PreviewW, 28f), "Import image...", _button)) ImportImage();
            if (!string.IsNullOrEmpty(_importNote)) GUI.Label(new Rect(view.x, view.yMax + 80f, PreviewW, 20f), _importNote, _small);

            if (GUI.Button(new Rect(panel.xMax - 24f - 150f, panel.yMax - 58f, 150f, 40f), "Save", _button)) Close(save: true);
            if (GUI.Button(new Rect(panel.xMax - 24f - 150f - 12f - 120f, panel.yMax - 58f, 120f, 40f), "Cancel", _button)) Close(save: false);
            GUI.matrix = previous;
        }

        private void DrawCanvas(Rect canvas)
        {
            // Cut cells show a checkerboard (see-through) under the mask's own texture.
            GUI.DrawTextureWithTexCoords(canvas, _checker, new Rect(0f, 0f, MaskData.Width * 0.5f, MaskData.Height * 0.5f));
            if (_maskTexture != null) GUI.DrawTexture(canvas, _maskTexture, ScaleMode.StretchToFill, true);
            // The middle line helps keep both halves even.
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(canvas.center.x - 0.5f, canvas.y, 1f, canvas.height), _white);
            GUI.color = Color.white;

            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && canvas.Contains(e.mousePosition))
            {
                int c = Mathf.Clamp((int)((e.mousePosition.x - canvas.x) / Cell), 0, MaskData.Width - 1);
                int r = Mathf.Clamp((int)((e.mousePosition.y - canvas.y) / Cell), 0, MaskData.Height - 1);
                if (e.button == 2)
                {
                    byte picked = _cells[r * MaskData.Width + c];
                    if (picked != MaskData.Cut) _colour = picked; // middle click: pick a colour off the mask
                    e.Use();
                    return;
                }
                byte colour = e.button == 1 ? MaskData.Cut : _colour;
                if (_fill && e.type == EventType.MouseDown)
                {
                    Flood(c, r, colour);
                    if (_mirror) Flood(MaskData.Width - 1 - c, r, colour);
                }
                else if (!_fill)
                {
                    PaintAt(c, r, colour);
                }
                Repaint();
                e.Use();
            }
        }

        private void DrawPalette(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y - 2f, area.width, 20f), "Colour", _label);
            const float s = 38f, gap = 6f;
            for (int i = 1; i < MaskData.Palette.Length; i++)
            {
                int k = i - 1;
                var sw = new Rect(area.x + (k % 4) * (s + gap), area.y + 22f + (k / 4) * (s + gap), s, s);
                if (i == _colour)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(sw.x - 3f, sw.y - 3f, sw.width + 6f, sw.height + 6f), _white);
                }
                GUI.color = MaskData.Palette[i];
                GUI.DrawTexture(sw, _white);
                GUI.color = Color.white;
                if (GUI.Button(sw, GUIContent.none, GUIStyle.none)) _colour = (byte)i;
            }
        }

        private void DrawTools(Rect area)
        {
            float y = area.y;
            GUI.Label(new Rect(area.x, y, area.width, 20f), "Brush", _label);
            y += 22f;
            if (GUI.Toggle(new Rect(area.x, y, 56f, 28f), _brush == 1, "Fine", _button)) _brush = 1;
            if (GUI.Toggle(new Rect(area.x + 60f, y, 56f, 28f), _brush == 2, "Mid", _button)) _brush = 2;
            if (GUI.Toggle(new Rect(area.x + 120f, y, 56f, 28f), _brush == 4, "Big", _button)) _brush = 4;
            y += 36f;
            _mirror = GUI.Toggle(new Rect(area.x, y, 84f, 28f), _mirror, "Mirror", _button);
            _fill = GUI.Toggle(new Rect(area.x + 92f, y, 84f, 28f), _fill, "Fill", _button);
            y += 44f;
            if (GUI.Button(new Rect(area.x, y, 176f, 28f), "Blank mask", _button))
            {
                System.Array.Copy(MaskData.Blank(1), _cells, MaskData.Cells);
                Repaint();
            }
            y += 34f;
            if (GUI.Button(new Rect(area.x, y, 176f, 28f), "Default mask", _button))
            {
                System.Array.Copy(MaskData.Default(), _cells, MaskData.Cells);
                Repaint();
            }
            y += 34f;
            if (GUI.Button(new Rect(area.x, y, 176f, 28f), "Undo changes", _button))
            {
                System.Array.Copy(_undo, _cells, MaskData.Cells);
                Repaint();
            }
        }

        private void EnsureStyles()
        {
            if (_white != null) return;
            _white = Texture2D.whiteTexture;
            _checker = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Mask Checker" };
            Color32 a = new Color32(61, 59, 56, 255), b = new Color32(77, 74, 71, 255);
            _checker.SetPixels32(new[] { a, b, b, a });
            _checker.Apply(false);
            _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.95f, 0.85f) } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = new Color(0.95f, 0.92f, 0.85f) } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.78f, 0.75f, 0.7f) } };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }
    }
}
