using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 调试控制台 - IMGUI 版本
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class DebugConsole : MonoBehaviour
    {
        [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote;
        [SerializeField] private float _floatButtonWidth = 90f;
        [SerializeField] private float _floatButtonHeight = 50f;

        private bool _isExpanded;
        private bool _isDragging;
        private Vector2 _dragOffset;
        private Vector2 _dragStartPos;
        private Rect _floatButtonRect;
        private Vector2 _scrollPosition;
        private int _currentTabIndex;

        private ProfilerCollector _profiler;
        private readonly List<IDebugPanelIMGUI> _panels = new();
        private string[] _tabNames = new string[0];
        
        private GUIStyle _floatButtonStyle;
        private GUIStyle _fpsLabelStyle;
        private GUIStyle _windowStyle;
        private GUIStyle _tabButtonStyle;
        private GUIStyle _tabButtonActiveStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _closeBtnStyle;
        private bool _stylesInitialized;

        public static DebugConsole Instance { get; private set; }

        public void RegisterPanel(IDebugPanelIMGUI panel)
        {
            if (_panels.Any(p => p.Name == panel.Name)) return;
            _panels.Add(panel);
            _panels.Sort((a, b) => a.Order.CompareTo(b.Order));
            _tabNames = _panels.Select(p => p.Name).ToArray();
        }

        public void UnregisterPanel(string name)
        {
            var panel = _panels.FirstOrDefault(p => p.Name == name);
            if (panel == null) return;
            panel.OnDestroy();
            _panels.Remove(panel);
            _tabNames = _panels.Select(p => p.Name).ToArray();
            if (_currentTabIndex >= _panels.Count) _currentTabIndex = _panels.Count - 1;
        }

        public T GetPanel<T>() where T : class, IDebugPanelIMGUI
        {
            return _panels.OfType<T>().FirstOrDefault();
        }

        public void Initialize(LogCache logCache, CommandManager commandManager, ProfilerCollector profiler)
        {
            _profiler = profiler;
            RegisterPanel(new LogPanel(logCache));
            RegisterPanel(new CommandPanel(commandManager));
            RegisterPanel(new ProfilePanel(profiler));
            RegisterPanel(new DevicePanel());
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _floatButtonRect = new Rect(20, 20, _floatButtonWidth, _floatButtonHeight);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                _isExpanded = !_isExpanded;

            if (_isExpanded && _currentTabIndex >= 0 && _currentTabIndex < _panels.Count)
                _panels[_currentTabIndex].OnUpdate();
        }

        private void OnDestroy()
        {
            foreach (var panel in _panels)
                panel.OnDestroy();
            _panels.Clear();
        }

        private void OnGUI()
        {
            InitStyles();

            if (_isExpanded)
                DrawMainWindow();
            else
                DrawFloatButton();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _floatButtonStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0.12f, 0.12f, 0.12f, 0.95f)) },
                border = new RectOffset(2, 2, 2, 2),
                alignment = TextAnchor.MiddleCenter
            };

            _fpsLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            _windowStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.98f)) }
            };

            _tabButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = MakeTex(2, 2, new Color(0.24f, 0.24f, 0.24f, 0.6f)), textColor = new Color(0.67f, 0.67f, 0.67f) },
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };

            _tabButtonActiveStyle = new GUIStyle(_tabButtonStyle)
            {
                normal = { background = MakeTex(2, 2, new Color(0.13f, 0.59f, 0.95f, 1f)), textColor = Color.white }
            };

            _headerStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0.16f, 0.16f, 0.16f, 0.95f)) },
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(15, 10, 0, 0)
            };
            _headerStyle.normal.textColor = new Color(0.88f, 0.88f, 0.88f);

            _closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = MakeTex(2, 2, new Color(0.96f, 0.26f, 0.21f, 1f)), textColor = Color.white },
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void DrawFloatButton()
        {
            GUI.Box(_floatButtonRect, "", _floatButtonStyle);

            var titleRect = new Rect(_floatButtonRect.x, _floatButtonRect.y + 8, _floatButtonRect.width, 20);
            GUI.Label(titleRect, "Debug", new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            });

            var fpsRect = new Rect(_floatButtonRect.x, _floatButtonRect.y + 28, _floatButtonRect.width, 18);
            var fps = _profiler?.CurrentFPS ?? 0;
            GUI.Label(fpsRect, $"{fps:F0} FPS", _fpsLabelStyle);

            // Handle click - check for MouseUp without drag
            var e = Event.current;
            if (e.type == EventType.MouseDown && _floatButtonRect.Contains(e.mousePosition))
            {
                _isDragging = false;
                _dragOffset = e.mousePosition - new Vector2(_floatButtonRect.x, _floatButtonRect.y);
                _dragStartPos = e.mousePosition;
            }
            else if (e.type == EventType.MouseDrag && _floatButtonRect.Contains(_dragStartPos))
            {
                _isDragging = true;
                _floatButtonRect.x = e.mousePosition.x - _dragOffset.x;
                _floatButtonRect.y = e.mousePosition.y - _dragOffset.y;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _floatButtonRect.Contains(e.mousePosition))
            {
                if (!_isDragging)
                {
                    _isExpanded = true;
                }
                _isDragging = false;
                e.Use();
            }
        }

        private void DrawMainWindow()
        {
            var windowRect = new Rect(0, 0, Screen.width, Screen.height);
            GUI.Box(windowRect, "", _windowStyle);

            // Header
            var headerRect = new Rect(0, 0, Screen.width, 40);
            GUI.Box(headerRect, "Debug Console", _headerStyle);

            var closeBtnRect = new Rect(Screen.width - 38, 6, 28, 28);
            if (GUI.Button(closeBtnRect, "X", _closeBtnStyle))
                _isExpanded = false;

            // Tab bar
            var tabBarRect = new Rect(0, 40, Screen.width, 45);
            GUI.Box(tabBarRect, "", new GUIStyle { normal = { background = MakeTex(2, 2, new Color(0.14f, 0.14f, 0.14f, 0.9f)) } });

            if (_tabNames.Length > 0)
            {
                float tabWidth = (Screen.width - 20) / _tabNames.Length;
                for (int i = 0; i < _tabNames.Length; i++)
                {
                    var tabRect = new Rect(10 + i * tabWidth, 47, tabWidth - 6, 35);
                    var style = i == _currentTabIndex ? _tabButtonActiveStyle : _tabButtonStyle;
                    if (GUI.Button(tabRect, _tabNames[i], style))
                    {
                        if (_currentTabIndex != i)
                        {
                            if (_currentTabIndex >= 0 && _currentTabIndex < _panels.Count)
                                _panels[_currentTabIndex].OnDeactivate();
                            _currentTabIndex = i;
                            _panels[_currentTabIndex].OnActivate();
                        }
                    }
                }
            }

            // Content area
            var contentRect = new Rect(10, 95, Screen.width - 20, Screen.height - 105);
            GUILayout.BeginArea(contentRect);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            if (_currentTabIndex >= 0 && _currentTabIndex < _panels.Count)
                _panels[_currentTabIndex].OnGUI();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
