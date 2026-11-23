using System.Collections.Generic;
using HowToHunt.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HowToHunt.Core
{
    /// <summary>
    /// The in-game developer console (toggle with the key left of 1, or F1). IMGUI: it only costs anything while open.
    /// Shows console output plus Unity warnings and errors.
    /// </summary>
    public sealed class DevConsoleView : MonoBehaviour
    {
        public const string EchoPrefix = "[con] ";
        private const int MaxLines = 400;
        private const int MaxHistory = 64;

        private struct Line
        {
            public ConsoleLevel Level;
            public string Text;
        }

        private static readonly Queue<Line> Lines = new Queue<Line>(MaxLines);
        private static int _version;

        private readonly List<string> _history = new List<string>(MaxHistory);
        private int _historyIndex = -1;
        private string _input = string.Empty;
        private bool _open;
        private ImGuiHost _gui;
        private bool _focusField;
        private Vector2 _scroll;
        private int _seenVersion = -1;
        private GUIStyle _lineStyle;
        private Texture2D _background;
        private CursorLockMode _prevLock;
        private bool _prevVisible;

        public static bool IsOpen { get; private set; }

        public static void Append(ConsoleLevel level, string text)
        {
            if (Lines.Count >= MaxLines) Lines.Dequeue();
            Lines.Enqueue(new Line { Level = level, Text = text });
            _version++;
        }

        public static void ClearLog()
        {
            Lines.Clear();
            _version++;
        }

        private void OnEnable() => Application.logMessageReceived += OnUnityLog;
        private void OnDisable() => Application.logMessageReceived -= OnUnityLog;

        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (condition.StartsWith(EchoPrefix, System.StringComparison.Ordinal)) return;
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    Append(ConsoleLevel.Error, condition);
                    break;
                case LogType.Warning:
                    Append(ConsoleLevel.Warning, condition);
                    break;
                default:
                    Append(ConsoleLevel.Info, condition);
                    break;
            }
        }

        private void Awake() => _gui = ImGuiHost.Attach(gameObject, DrawGui);

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.backquoteKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame) SetOpen(!_open);
            else if (_open && kb.escapeKey.wasPressedThisFrame) SetOpen(false);
        }

        private void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;
            IsOpen = open;
            _gui.Show(open);
            if (open)
            {
                Game.PushUiFocus(this);
                _prevLock = Cursor.lockState;
                _prevVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _focusField = true;
                _seenVersion = -1;
            }
            else
            {
                Game.PopUiFocus(this);
                if (!Game.UiHasFocus)
                {
                    Cursor.lockState = _prevLock;
                    Cursor.visible = _prevVisible;
                }
            }
        }

        private void DrawGui()
        {
            if (!_open) return;
            EnsureStyles();

            float height = Mathf.Round(Screen.height * 0.45f);
            var rect = new Rect(0, 0, Screen.width, height);
            GUI.DrawTexture(rect, _background);

            Event e = Event.current;
            if (e.type == EventType.KeyDown) HandleKey(e);

            GUILayout.BeginArea(new Rect(8, 6, Screen.width - 16, height - 12));
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (Line line in Lines)
            {
                _lineStyle.normal.textColor = line.Level == ConsoleLevel.Error ? new Color(1f, 0.45f, 0.4f)
                    : line.Level == ConsoleLevel.Warning ? new Color(1f, 0.85f, 0.4f) : new Color(0.88f, 0.92f, 0.95f);
                GUILayout.Label(line.Text, _lineStyle);
            }
            GUILayout.EndScrollView();
            if (_seenVersion != _version && e.type == EventType.Repaint)
            {
                _seenVersion = _version;
                _scroll.y = float.MaxValue;
            }

            GUI.SetNextControlName("hth_console_input");
            _input = GUILayout.TextField(_input ?? string.Empty);
            // The toggle key's character should never end up in the input.
            if (_input.IndexOf('`') >= 0 || _input.IndexOf('²') >= 0) _input = _input.Replace("`", "").Replace("²", "");
            GUILayout.EndArea();

            if (_focusField)
            {
                GUI.FocusControl("hth_console_input");
                _focusField = false;
            }
        }

        private void HandleKey(Event e)
        {
            switch (e.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Submit();
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    if (_history.Count > 0)
                    {
                        _historyIndex = _historyIndex < 0 ? _history.Count - 1 : Mathf.Max(0, _historyIndex - 1);
                        _input = _history[_historyIndex];
                    }
                    e.Use();
                    break;
                case KeyCode.DownArrow:
                    if (_historyIndex >= 0)
                    {
                        _historyIndex++;
                        if (_historyIndex >= _history.Count)
                        {
                            _historyIndex = -1;
                            _input = string.Empty;
                        }
                        else
                        {
                            _input = _history[_historyIndex];
                        }
                    }
                    e.Use();
                    break;
                case KeyCode.Tab:
                    Complete();
                    e.Use();
                    break;
            }
        }

        private void Submit()
        {
            string text = _input.Trim();
            _input = string.Empty;
            _historyIndex = -1;
            if (text.Length == 0) return;
            if (_history.Count == 0 || _history[_history.Count - 1] != text)
            {
                if (_history.Count >= MaxHistory) _history.RemoveAt(0);
                _history.Add(text);
            }
            Append(ConsoleLevel.Info, "] " + text);
            ConsoleSystem.Global.Execute(text);
        }

        private void Complete()
        {
            string prefix = _input.Trim();
            if (prefix.Length == 0 || prefix.Contains(" ")) return;
            List<string> matches = ConsoleSystem.Global.Complete(prefix);
            if (matches.Count == 1)
            {
                _input = matches[0] + " ";
            }
            else if (matches.Count > 1)
            {
                Append(ConsoleLevel.Info, string.Join("  ", matches));
                _input = CommonPrefix(matches);
            }
            // Move caret to end.
            var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            editor.text = _input;
            editor.MoveTextEnd();
        }

        private static string CommonPrefix(List<string> words)
        {
            string p = words[0];
            foreach (string w in words)
            {
                int n = 0;
                while (n < p.Length && n < w.Length && char.ToLowerInvariant(p[n]) == char.ToLowerInvariant(w[n])) n++;
                p = p.Substring(0, n);
            }
            return p;
        }

        private void EnsureStyles()
        {
            if (_lineStyle != null) return;
            _lineStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, fontSize = 13, margin = new RectOffset(0, 0, 0, 0), padding = new RectOffset(0, 0, 1, 1) };
            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.09f, 0.92f));
            _background.Apply();
        }
    }
}
