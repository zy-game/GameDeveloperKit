using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.PsdToUgui
{
    /// <summary>
    /// PSD转UGUI 全局设置 - 保存到 ProjectSettings
    /// </summary>
    [Serializable]
    public class PsdToUguiSettings
    {
        private const string SettingsPath = "ProjectSettings/PsdToUguiSettings.json";

        [Header("导出路径")]
        public string ExportRootPath = "Assets/UI";
        
        [Header("字体设置")]
        public bool UseTextMeshPro = true;
        public string FontLibraryPath = "Assets/Fonts";
        public string TMPFontLibraryPath = "Assets/Fonts/TMP";
        public string DefaultFontGuid;
        public string DefaultTMPFontGuid;

        [Header("Canvas设置")]
        public Vector2 ReferenceResolution = new(1920, 1080);
        public float MatchWidthOrHeight = 0.5f;

        [Header("纹理设置")]
        public int MaxTextureSize = 2048;
        public int TextureCompression = 1; // TextureImporterCompression.Compressed
        public bool GenerateMipMaps = false;
        public string CommonTexturePath = "Assets/UI/Common/Textures";

        [Header("图层命名")]
        public bool CleanLayerNames = true;
        public string LayerNamePrefix = "";
        public string LayerNameSuffix = "";

        [Header("其他")]
        public string LastImportPath = "";
        public bool IgnoreHiddenLayers = false;
        public bool MergeSameTextures = true;
        
        // 字体缓存
        [NonSerialized] private Dictionary<string, Font> _fontCache;
        [NonSerialized] private Dictionary<string, UnityEngine.Object> _tmpFontCache;

        private static PsdToUguiSettings _instance;

        public static PsdToUguiSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    Load();
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// 获取 PSD 的纹理导出路径
        /// </summary>
        public string GetTextureOutputPath(string psdFileName)
        {
            return $"{ExportRootPath}/{psdFileName}/Textures";
        }
        
        /// <summary>
        /// 获取 PSD 的 Prefab 导出路径
        /// </summary>
        public string GetPrefabOutputPath(string psdFileName)
        {
            return $"{ExportRootPath}/{psdFileName}/{psdFileName}.prefab";
        }

        public static void Load()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsPath);
                    _instance = JsonUtility.FromJson<PsdToUguiSettings>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PsdToUguiSettings] Failed to load settings: {ex.Message}");
                    _instance = new PsdToUguiSettings();
                }
            }
            else
            {
                _instance = new PsdToUguiSettings();
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PsdToUguiSettings] Failed to save settings: {ex.Message}");
            }
        }

        public Font GetDefaultFont()
        {
            if (string.IsNullOrEmpty(DefaultFontGuid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(DefaultFontGuid);
            return AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        public void SetDefaultFont(Font font)
        {
            DefaultFontGuid = font != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(font)) : "";
        }

        public UnityEngine.Object GetDefaultTMPFont()
        {
            if (string.IsNullOrEmpty(DefaultTMPFontGuid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(DefaultTMPFontGuid);
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        }

        public void SetDefaultTMPFont(UnityEngine.Object font)
        {
            DefaultTMPFontGuid = font != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(font)) : "";
        }
        
        /// <summary>
        /// 根据字体名称查找字体
        /// </summary>
        public Font FindFont(string fontName, out bool isDefault)
        {
            isDefault = false;
            if (string.IsNullOrEmpty(fontName))
            {
                isDefault = true;
                return GetDefaultFont();
            }
            
            // 初始化缓存
            if (_fontCache == null)
            {
                RefreshFontCache();
            }
            
            // 尝试精确匹配
            if (_fontCache.TryGetValue(fontName, out var font))
            {
                return font;
            }
            
            // 尝试模糊匹配（忽略大小写，移除空格和连字符）
            var normalizedName = NormalizeFontName(fontName);
            foreach (var kvp in _fontCache)
            {
                if (NormalizeFontName(kvp.Key) == normalizedName)
                {
                    return kvp.Value;
                }
            }
            
            // 尝试部分匹配
            foreach (var kvp in _fontCache)
            {
                if (NormalizeFontName(kvp.Key).Contains(normalizedName) || 
                    normalizedName.Contains(NormalizeFontName(kvp.Key)))
                {
                    return kvp.Value;
                }
            }
            
            // 返回默认字体
            isDefault = true;
            return GetDefaultFont();
        }
        
        /// <summary>
        /// 根据字体名称查找 TMP 字体
        /// </summary>
        public UnityEngine.Object FindTMPFont(string fontName, out bool isDefault)
        {
            isDefault = false;
            if (string.IsNullOrEmpty(fontName))
            {
                isDefault = true;
                return GetDefaultTMPFont();
            }
            
            // 初始化缓存
            if (_tmpFontCache == null)
            {
                RefreshTMPFontCache();
            }
            
            // 尝试精确匹配
            if (_tmpFontCache.TryGetValue(fontName, out var font))
            {
                return font;
            }
            
            // 尝试模糊匹配
            var normalizedName = NormalizeFontName(fontName);
            foreach (var kvp in _tmpFontCache)
            {
                if (NormalizeFontName(kvp.Key) == normalizedName)
                {
                    return kvp.Value;
                }
            }
            
            // 尝试部分匹配
            foreach (var kvp in _tmpFontCache)
            {
                if (NormalizeFontName(kvp.Key).Contains(normalizedName) || 
                    normalizedName.Contains(NormalizeFontName(kvp.Key)))
                {
                    return kvp.Value;
                }
            }
            
            // 返回默认字体
            isDefault = true;
            return GetDefaultTMPFont();
        }
        
        private string NormalizeFontName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.ToLowerInvariant()
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "");
        }
        
        /// <summary>
        /// 刷新字体缓存
        /// </summary>
        public void RefreshFontCache()
        {
            _fontCache = new Dictionary<string, Font>();
            
            if (string.IsNullOrEmpty(FontLibraryPath) || !Directory.Exists(FontLibraryPath))
            {
                return;
            }
            
            var guids = AssetDatabase.FindAssets("t:Font", new[] { FontLibraryPath });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font != null)
                {
                    // 使用文件名（不含扩展名）作为键
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    _fontCache[fileName] = font;
                    
                    // 也使用字体的实际名称作为键
                    if (!string.IsNullOrEmpty(font.name) && font.name != fileName)
                    {
                        _fontCache[font.name] = font;
                    }
                }
            }
            
            Debug.Log($"[PsdToUguiSettings] 已加载 {_fontCache.Count} 个字体");
        }
        
        /// <summary>
        /// 刷新 TMP 字体缓存
        /// </summary>
        public void RefreshTMPFontCache()
        {
            _tmpFontCache = new Dictionary<string, UnityEngine.Object>();
            
            if (string.IsNullOrEmpty(TMPFontLibraryPath) || !Directory.Exists(TMPFontLibraryPath))
            {
                return;
            }
            
            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { TMPFontLibraryPath });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (font != null)
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    _tmpFontCache[fileName] = font;
                    
                    if (!string.IsNullOrEmpty(font.name) && font.name != fileName)
                    {
                        _tmpFontCache[font.name] = font;
                    }
                }
            }
            
            Debug.Log($"[PsdToUguiSettings] 已加载 {_tmpFontCache.Count} 个 TMP 字体");
        }
        
        /// <summary>
        /// 获取字体库中的所有字体名称
        /// </summary>
        public List<string> GetAvailableFontNames()
        {
            if (_fontCache == null) RefreshFontCache();
            return _fontCache?.Keys.ToList() ?? new List<string>();
        }
        
        /// <summary>
        /// 获取 TMP 字体库中的所有字体名称
        /// </summary>
        public List<string> GetAvailableTMPFontNames()
        {
            if (_tmpFontCache == null) RefreshTMPFontCache();
            return _tmpFontCache?.Keys.ToList() ?? new List<string>();
        }
    }

    /// <summary>
    /// PSD 文档配置 - 每个 PSD 文件独立的配置
    /// </summary>
    [Serializable]
    public class PsdDocumentConfig
    {
        public string PsdFilePath;
        public string TextureOutputPath;
        public string PrefabOutputPath;
        public string ConfigSavePath;
        
        private const string ConfigFolder = "Library/PsdToUgui/Configs";

        public static PsdDocumentConfig Load(string psdFilePath)
        {
            var configPath = GetConfigPath(psdFilePath);
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    return JsonUtility.FromJson<PsdDocumentConfig>(json);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public void Save()
        {
            var configPath = GetConfigPath(PsdFilePath);
            var directory = Path.GetDirectoryName(configPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonUtility.ToJson(this, true);
            File.WriteAllText(configPath, json);
        }

        private static string GetConfigPath(string psdFilePath)
        {
            var hash = psdFilePath.GetHashCode().ToString("X8");
            var fileName = Path.GetFileNameWithoutExtension(psdFilePath);
            return Path.Combine(ConfigFolder, $"{fileName}_{hash}_config.json");
        }
    }

    /// <summary>
    /// 全局设置窗口
    /// </summary>
    public class PsdToUguiSettingsWindow : EditorWindow
    {
        private PsdToUguiSettings _settings;
        private Vector2 _scrollPosition;

        public static void ShowWindow()
        {
            var window = GetWindow<PsdToUguiSettingsWindow>("PSD转UGUI 全局设置");
            window.minSize = new Vector2(400, 500);
        }

        private void OnEnable()
        {
            _settings = PsdToUguiSettings.Instance;
        }

        private void OnGUI()
        {
            if (_settings == null) return;

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            EditorGUI.BeginChangeCheck();
            
            // 导出路径设置
            EditorGUILayout.LabelField("导出路径", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _settings.ExportRootPath = EditorGUILayout.TextField("导出根路径", _settings.ExportRootPath);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                var path = EditorUtility.OpenFolderPanel("选择导出根路径", _settings.ExportRootPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        _settings.ExportRootPath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请选择 Assets 目录下的路径", "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("导出时会自动在根路径下创建 PSD名称 文件夹，例如：\n" +
                $"{_settings.ExportRootPath}/PSD名称/Textures/\n" +
                $"{_settings.ExportRootPath}/PSD名称/PSD名称.prefab", MessageType.Info);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("字体设置", EditorStyles.boldLabel);
            _settings.UseTextMeshPro = EditorGUILayout.Toggle("使用 TextMeshPro", _settings.UseTextMeshPro);
            
            // 字体库路径
            EditorGUILayout.BeginHorizontal();
            _settings.FontLibraryPath = EditorGUILayout.TextField("字体库路径", _settings.FontLibraryPath);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                var path = EditorUtility.OpenFolderPanel("选择字体库路径", _settings.FontLibraryPath, "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    _settings.FontLibraryPath = "Assets" + path.Substring(Application.dataPath.Length);
                    _settings.RefreshFontCache();
                }
            }
            if (GUILayout.Button("刷新", GUILayout.Width(50)))
            {
                _settings.RefreshFontCache();
            }
            EditorGUILayout.EndHorizontal();
            
            // TMP 字体库路径
            if (_settings.UseTextMeshPro)
            {
                EditorGUILayout.BeginHorizontal();
                _settings.TMPFontLibraryPath = EditorGUILayout.TextField("TMP字体库路径", _settings.TMPFontLibraryPath);
                if (GUILayout.Button("选择", GUILayout.Width(50)))
                {
                    var path = EditorUtility.OpenFolderPanel("选择TMP字体库路径", _settings.TMPFontLibraryPath, "");
                    if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                    {
                        _settings.TMPFontLibraryPath = "Assets" + path.Substring(Application.dataPath.Length);
                        _settings.RefreshTMPFontCache();
                    }
                }
                if (GUILayout.Button("刷新", GUILayout.Width(50)))
                {
                    _settings.RefreshTMPFontCache();
                }
                EditorGUILayout.EndHorizontal();
            }
            
            // 默认字体
            var font = _settings.GetDefaultFont();
            var newFont = EditorGUILayout.ObjectField("默认字体", font, typeof(Font), false) as Font;
            if (newFont != font) _settings.SetDefaultFont(newFont);
            
            if (_settings.UseTextMeshPro)
            {
                // 使用 TMP_FontAsset 类型
                var tmpFontType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
                if (tmpFontType != null)
                {
                    var tmpFont = _settings.GetDefaultTMPFont();
                    var newTmpFont = EditorGUILayout.ObjectField("默认 TMP 字体", tmpFont, tmpFontType, false);
                    if (newTmpFont != tmpFont) _settings.SetDefaultTMPFont(newTmpFont);
                }
                else
                {
                    EditorGUILayout.HelpBox("TextMeshPro 未安装", MessageType.Warning);
                }
            }
            
            EditorGUILayout.HelpBox("导入 PSD 时会根据文本图层的字体名称在字体库中查找匹配的字体。\n" +
                "如果找不到匹配的字体，将使用默认字体。", MessageType.Info);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Canvas 设置", EditorStyles.boldLabel);
            _settings.ReferenceResolution = EditorGUILayout.Vector2Field("参考分辨率", _settings.ReferenceResolution);
            _settings.MatchWidthOrHeight = EditorGUILayout.Slider("宽高匹配", _settings.MatchWidthOrHeight, 0, 1);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("纹理设置", EditorStyles.boldLabel);
            _settings.MaxTextureSize = EditorGUILayout.IntPopup("最大尺寸", _settings.MaxTextureSize, 
                new[] { "256", "512", "1024", "2048", "4096" }, 
                new[] { 256, 512, 1024, 2048, 4096 });
            _settings.TextureCompression = EditorGUILayout.IntPopup("压缩格式", _settings.TextureCompression,
                new[] { "无压缩", "压缩", "高质量压缩" },
                new[] { 0, 1, 2 });
            _settings.GenerateMipMaps = EditorGUILayout.Toggle("生成 MipMaps", _settings.GenerateMipMaps);
            
            EditorGUILayout.BeginHorizontal();
            _settings.CommonTexturePath = EditorGUILayout.TextField("通用纹理路径", _settings.CommonTexturePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("选择通用纹理文件夹", _settings.CommonTexturePath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        _settings.CommonTexturePath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请选择 Assets 目录下的文件夹", "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("图层命名", EditorStyles.boldLabel);
            _settings.CleanLayerNames = EditorGUILayout.Toggle("清理特殊字符", _settings.CleanLayerNames);
            _settings.LayerNamePrefix = EditorGUILayout.TextField("名称前缀", _settings.LayerNamePrefix);
            _settings.LayerNameSuffix = EditorGUILayout.TextField("名称后缀", _settings.LayerNameSuffix);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("其他", EditorStyles.boldLabel);
            _settings.IgnoreHiddenLayers = EditorGUILayout.Toggle("忽略隐藏图层", _settings.IgnoreHiddenLayers);
            _settings.MergeSameTextures = EditorGUILayout.Toggle("合并相同纹理", _settings.MergeSameTextures);
            
            if (EditorGUI.EndChangeCheck())
            {
                _settings.Save();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("重置为默认", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("确认", "确定要重置所有设置吗？", "确定", "取消"))
                {
                    ResetToDefault();
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void ResetToDefault()
        {
            _settings.UseTextMeshPro = true;
            _settings.DefaultFontGuid = "";
            _settings.DefaultTMPFontGuid = "";
            _settings.ReferenceResolution = new Vector2(1920, 1080);
            _settings.MatchWidthOrHeight = 0.5f;
            _settings.MaxTextureSize = 2048;
            _settings.TextureCompression = 1;
            _settings.GenerateMipMaps = false;
            _settings.CleanLayerNames = true;
            _settings.LayerNamePrefix = "";
            _settings.LayerNameSuffix = "";
            _settings.IgnoreHiddenLayers = false;
            _settings.MergeSameTextures = true;
            _settings.Save();
        }
    }

    /// <summary>
    /// PSD 导入配置窗口 - 选择保存路径
    /// </summary>
    public class PsdImportConfigWindow : EditorWindow
    {
        private string _psdFilePath;
        private string _textureOutputPath;
        private string _prefabOutputPath;
        private Action<PsdDocumentConfig> _onConfirm;
        private bool _confirmed;

        public static void Show(string psdFilePath, Action<PsdDocumentConfig> onConfirm)
        {
            var window = GetWindow<PsdImportConfigWindow>(true, "PSD 导入配置", true);
            window._psdFilePath = psdFilePath;
            window._onConfirm = onConfirm;
            
            // 尝试加载之前的配置
            var existingConfig = PsdDocumentConfig.Load(psdFilePath);
            if (existingConfig != null)
            {
                window._textureOutputPath = existingConfig.TextureOutputPath;
                window._prefabOutputPath = existingConfig.PrefabOutputPath;
            }
            else
            {
                // 默认路径
                var psdName = Path.GetFileNameWithoutExtension(psdFilePath);
                window._textureOutputPath = $"Assets/UI/Textures/{psdName}";
                window._prefabOutputPath = "Assets/UI/Prefabs";
            }
            
            window.minSize = new Vector2(450, 200);
            window.maxSize = new Vector2(450, 200);
            window.ShowModalUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("PSD 文件", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(Path.GetFileName(_psdFilePath), EditorStyles.miniLabel);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("输出路径配置", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            _textureOutputPath = EditorGUILayout.TextField("纹理输出路径", _textureOutputPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("选择纹理输出目录", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        _textureOutputPath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请选择 Assets 目录下的文件夹", "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            _prefabOutputPath = EditorGUILayout.TextField("Prefab 输出路径", _prefabOutputPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("选择 Prefab 输出目录", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        _prefabOutputPath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请选择 Assets 目录下的文件夹", "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(20);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("取消", GUILayout.Width(80)))
            {
                Close();
            }
            
            if (GUILayout.Button("确定", GUILayout.Width(80)))
            {
                _confirmed = true;
                
                var config = new PsdDocumentConfig
                {
                    PsdFilePath = _psdFilePath,
                    TextureOutputPath = _textureOutputPath,
                    PrefabOutputPath = _prefabOutputPath
                };
                config.Save();
                
                _onConfirm?.Invoke(config);
                Close();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void OnDestroy()
        {
            if (!_confirmed)
            {
                _onConfirm?.Invoke(null);
            }
        }
    }
}
