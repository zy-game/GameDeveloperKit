using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Editor.PsdToUgui
{
    /// <summary>
    /// UGUI转换器
    /// </summary>
    public class UguiConverter
    {
        private readonly PsdToUguiSettings _settings;
        private readonly Dictionary<int, Sprite> _spriteCache = new();
        private readonly Dictionary<string, Sprite> _textureHashCache = new();
        private string _textureOutputPath;
        private PsdDocument _document;

        public UguiConverter(PsdToUguiSettings settings)
        {
            _settings = settings;
        }

        public GameObject Convert(PsdDocument document, string prefabPath, string textureOutputPath)
        {
            _document = document;
            _textureOutputPath = textureOutputPath;
            
            if (!Directory.Exists(_textureOutputPath))
            {
                Directory.CreateDirectory(_textureOutputPath);
            }

            // 创建根对象（不是 Canvas，而是普通 RectTransform）
            var root = CreateRoot(document);

            // 转换图层 - 传入 PSD 的尺寸作为父级尺寸
            ConvertLayers(document.Layers, root.transform, document.Width, document.Height);

            // 保存Prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            
            // 清理临时对象
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.Refresh();
            return prefab;
        }

        /// <summary>
        /// 转换 PSD 文档为 GameObject（不保存为 Prefab，用于 PsdImporter）
        /// </summary>
        public GameObject ConvertWithBinding(PsdDocument document, string textureOutputPath)
        {
            _document = document;
            _textureOutputPath = textureOutputPath;
            
            if (!Directory.Exists(_textureOutputPath))
            {
                Directory.CreateDirectory(_textureOutputPath);
            }

            var root = CreateRoot(document);
            ConvertLayers(document.Layers, root.transform, document.Width, document.Height);
            return root;
        }

        /// <summary>
        /// 创建单个图层的 GameObject（用于增量导入）
        /// </summary>
        public GameObject CreateLayerGameObject(PsdLayerInfo layer, Transform parent, 
            string textureOutputPath, float parentWidth, float parentHeight)
        {
            _textureOutputPath = textureOutputPath;
            return ConvertLayer(layer, parent, parentWidth, parentHeight);
        }

        /// <summary>
        /// 更新图层内容（用于增量导入）
        /// </summary>
        public void UpdateLayerContent(GameObject go, PsdLayerInfo layer, string textureOutputPath)
        {
            _textureOutputPath = textureOutputPath;

            switch (layer.LayerType)
            {
                case PsdLayerType.Normal:
                    if (layer.Texture != null)
                    {
                        var image = go.GetComponent<Image>();
                        if (image != null)
                        {
                            var sprite = ExportAndCreateSprite(layer);
                            if (sprite != null)
                            {
                                image.sprite = sprite;
                            }
                        }
                    }
                    break;
                    
                case PsdLayerType.Text:
                    UpdateTextContent(go, layer);
                    break;
            }
        }

        private void UpdateTextContent(GameObject go, PsdLayerInfo layer)
        {
            var textData = layer.TextData;
            if (textData == null) return;

            // 尝试更新 Unity Text
            var text = go.GetComponent<Text>();
            if (text != null)
            {
                text.text = textData.Text ?? layer.Name;
                return;
            }

            // 尝试更新 TextMeshPro
            var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (tmpType != null)
            {
                var tmp = go.GetComponent(tmpType);
                if (tmp != null)
                {
                    var textProperty = tmpType.GetProperty("text");
                    textProperty?.SetValue(tmp, textData.Text ?? layer.Name);
                }
            }
        }

        private GameObject CreateRoot(PsdDocument document)
        {
            var rootGo = new GameObject(document.FileName);
            var rootRect = rootGo.AddComponent<RectTransform>();
            
            // 设置根节点大小为 PSD 尺寸
            rootRect.sizeDelta = new Vector2(document.Width, document.Height);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);

            return rootGo;
        }

        private void ConvertLayers(List<PsdLayerInfo> layers, Transform parent, float parentWidth, float parentHeight)
        {
            // 从底部到顶部转换（保持正确的渲染顺序）
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                var layer = layers[i];
                
                if (_settings.IgnoreHiddenLayers && !layer.Visible)
                    continue;

                var go = ConvertLayer(layer, parent, parentWidth, parentHeight);
                
                if (go != null && layer.Children.Count > 0)
                {
                    // 确定子节点的父级
                    Transform childParent = go.transform;
                    float childParentWidth = parentWidth;
                    float childParentHeight = parentHeight;
                    
                    // 如果是 ScrollView，子节点应该放到 Content 下
                    if (layer.LayoutConfig != null && layer.LayoutConfig.LayoutType == LayoutType.ScrollView)
                    {
                        var content = go.transform.Find("Viewport/Content");
                        if (content != null)
                        {
                            childParent = content;
                        }
                    }
                    
                    // 子图层相对于当前图层定位
                    // 如果是分组，子图层相对于分组的边界定位
                    var groupWidth = layer.Bounds.width > 0 ? layer.Bounds.width : parentWidth;
                    var groupHeight = layer.Bounds.height > 0 ? layer.Bounds.height : parentHeight;
                    ConvertLayers(layer.Children, childParent, groupWidth, groupHeight);
                }
            }
        }

        private GameObject ConvertLayer(PsdLayerInfo layer, Transform parent, float parentWidth, float parentHeight)
        {
            var layerName = GetCleanLayerName(layer.Name);
            var go = new GameObject(layerName);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);

            // 设置位置和大小
            SetRectTransform(rect, layer, parentWidth, parentHeight);

            // 根据图层类型添加组件
            switch (layer.LayerType)
            {
                case PsdLayerType.Normal:
                    if (layer.Texture != null)
                    {
                        ConvertImageLayer(go, layer);
                    }
                    break;
                    
                case PsdLayerType.Text:
                    ConvertTextLayer(go, layer);
                    break;
                    
                case PsdLayerType.Group:
                    // 组只是容器，不需要额外组件
                    break;
            }
            
            // 应用布局配置
            if (layer.LayoutConfig != null && layer.LayoutConfig.LayoutType != LayoutType.None)
            {
                ApplyLayoutConfig(go, layer.LayoutConfig);
            }

            // 设置可见性
            go.SetActive(layer.Visible);

            return go;
        }
        
        private void ApplyLayoutConfig(GameObject go, LayoutConfig config)
        {
            switch (config.LayoutType)
            {
                case LayoutType.HorizontalLayout:
                    var hLayout = go.AddComponent<HorizontalLayoutGroup>();
                    hLayout.spacing = config.Spacing;
                    hLayout.padding = new RectOffset(config.PaddingLeft, config.PaddingRight, config.PaddingTop, config.PaddingBottom);
                    hLayout.childAlignment = TextAnchor.MiddleLeft;
                    hLayout.childControlWidth = false;
                    hLayout.childControlHeight = false;
                    hLayout.childForceExpandWidth = false;
                    hLayout.childForceExpandHeight = false;
                    ApplyContentSizeFitter(go, config);
                    break;
                    
                case LayoutType.VerticalLayout:
                    var vLayout = go.AddComponent<VerticalLayoutGroup>();
                    vLayout.spacing = config.Spacing;
                    vLayout.padding = new RectOffset(config.PaddingLeft, config.PaddingRight, config.PaddingTop, config.PaddingBottom);
                    vLayout.childAlignment = TextAnchor.UpperCenter;
                    vLayout.childControlWidth = false;
                    vLayout.childControlHeight = false;
                    vLayout.childForceExpandWidth = false;
                    vLayout.childForceExpandHeight = false;
                    ApplyContentSizeFitter(go, config);
                    break;
                    
                case LayoutType.GridLayout:
                    var gLayout = go.AddComponent<GridLayoutGroup>();
                    gLayout.spacing = new Vector2(config.Spacing, config.Spacing);
                    gLayout.padding = new RectOffset(config.PaddingLeft, config.PaddingRight, config.PaddingTop, config.PaddingBottom);
                    gLayout.cellSize = new Vector2(100, 100);
                    gLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
                    gLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
                    gLayout.childAlignment = TextAnchor.UpperLeft;
                    gLayout.constraint = GridLayoutGroup.Constraint.Flexible;
                    ApplyContentSizeFitter(go, config);
                    break;
                    
                case LayoutType.ScrollView:
                    CreateScrollView(go, config);
                    break;
            }
        }
        
        private void ApplyContentSizeFitter(GameObject go, LayoutConfig config)
        {
            if (config.HorizontalFit == FitMode.Unconstrained && config.VerticalFit == FitMode.Unconstrained)
                return;
                
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = (ContentSizeFitter.FitMode)(int)config.HorizontalFit;
            fitter.verticalFit = (ContentSizeFitter.FitMode)(int)config.VerticalFit;
        }
        
        private void CreateScrollView(GameObject go, LayoutConfig config)
        {
            var rect = go.GetComponent<RectTransform>();
            
            // 添加 ScrollRect
            var scrollRect = go.AddComponent<ScrollRect>();
            scrollRect.horizontal = config.ScrollHorizontal;
            scrollRect.vertical = config.ScrollVertical;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 1f;
            
            // 添加 Image 作为背景（如果没有的话）
            var bgImage = go.GetComponent<Image>();
            if (bgImage == null)
            {
                bgImage = go.AddComponent<Image>();
                bgImage.color = new Color(1, 1, 1, 0);
            }
            bgImage.raycastTarget = true;
            
            // 添加 Mask（如果没有的话）
            var mask = go.GetComponent<Mask>();
            if (mask == null)
            {
                mask = go.AddComponent<Mask>();
            }
            mask.showMaskGraphic = false;
            
            // 创建 Viewport
            var viewport = new GameObject("Viewport");
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.SetParent(rect, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportRect.pivot = new Vector2(0, 1);
            
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1, 1, 1, 0);
            viewportImage.raycastTarget = true;
            
            var viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            
            scrollRect.viewport = viewportRect;
            
            // 创建 Content
            var content = new GameObject("Content");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            
            scrollRect.content = contentRect;
            
            // Content 添加布局组件
            if (config.ScrollVertical && !config.ScrollHorizontal)
            {
                var vLayout = content.AddComponent<VerticalLayoutGroup>();
                vLayout.spacing = config.Spacing;
                vLayout.padding = new RectOffset(config.PaddingLeft, config.PaddingRight, config.PaddingTop, config.PaddingBottom);
                vLayout.childAlignment = TextAnchor.UpperCenter;
                vLayout.childControlWidth = true;
                vLayout.childControlHeight = false;
                vLayout.childForceExpandWidth = true;
                vLayout.childForceExpandHeight = false;
            }
            else if (config.ScrollHorizontal && !config.ScrollVertical)
            {
                var hLayout = content.AddComponent<HorizontalLayoutGroup>();
                hLayout.spacing = config.Spacing;
                hLayout.padding = new RectOffset(config.PaddingLeft, config.PaddingRight, config.PaddingTop, config.PaddingBottom);
                hLayout.childAlignment = TextAnchor.MiddleLeft;
                hLayout.childControlWidth = false;
                hLayout.childControlHeight = true;
                hLayout.childForceExpandWidth = false;
                hLayout.childForceExpandHeight = true;
            }
            
            // Content 添加 ContentSizeFitter
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = (ContentSizeFitter.FitMode)(int)config.HorizontalFit;
            fitter.verticalFit = (ContentSizeFitter.FitMode)(int)config.VerticalFit;
        }

        private void SetRectTransform(RectTransform rect, PsdLayerInfo layer, float parentWidth, float parentHeight)
        {
            // 用户创建的布局节点（ID >= 1000000）使用特殊处理
            bool isUserCreatedLayout = layer.Id >= PsdParser.UserCreatedIdStart && 
                                       layer.LayoutConfig != null && 
                                       layer.LayoutConfig.LayoutType != LayoutType.None;
            
            if (isUserCreatedLayout)
            {
                // 用户创建的布局节点默认填充父级
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                return;
            }
            
            // PSD坐标系：左上角为原点，Y轴向下
            // Unity UI坐标系：中心为原点，Y轴向上
            
            // 图层在 PSD 中的位置（左上角）
            var layerX = layer.Bounds.x;
            var layerY = layer.Bounds.y;
            var layerW = layer.Bounds.width;
            var layerH = layer.Bounds.height;
            
            // 获取锚点预设
            AnchorUtils.GetAnchorMinMax(layer.AnchorPreset, out var anchorMin, out var anchorMax);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            
            // 计算图层中心相对于父级中心的位置
            var centerX = layerX + layerW / 2 - parentWidth / 2;
            var centerY = -(layerY + layerH / 2 - parentHeight / 2);
            
            // 根据锚点类型计算位置
            if (anchorMin == anchorMax)
            {
                // 非拉伸模式：计算相对于锚点的偏移
                var anchorPosX = (anchorMin.x - 0.5f) * parentWidth;
                var anchorPosY = (anchorMin.y - 0.5f) * parentHeight;
                rect.anchoredPosition = new Vector2(centerX - anchorPosX, centerY - anchorPosY);
                rect.sizeDelta = new Vector2(layerW, layerH);
            }
            else
            {
                // 拉伸模式：计算边距
                var left = layerX - anchorMin.x * parentWidth;
                var right = (1 - anchorMax.x) * parentWidth - (parentWidth - layerX - layerW);
                var top = layerY - (1 - anchorMax.y) * parentHeight;
                var bottom = anchorMin.y * parentHeight - (parentHeight - layerY - layerH);
                
                rect.offsetMin = new Vector2(left, -bottom);
                rect.offsetMax = new Vector2(-right, -top);
            }
        }

        private void ConvertImageLayer(GameObject go, PsdLayerInfo layer)
        {
            var image = go.AddComponent<Image>();
            
            // 导出纹理并创建Sprite
            var sprite = ExportAndCreateSprite(layer);
            if (sprite != null)
            {
                image.sprite = sprite;
            }

            // 设置透明度
            var color = image.color;
            color.a = layer.Opacity;
            image.color = color;
            
            // 设置图片类型
            image.type = layer.ImageType switch
            {
                ImageType.Sliced => Image.Type.Sliced,
                ImageType.Tiled => Image.Type.Tiled,
                ImageType.Filled => Image.Type.Filled,
                _ => Image.Type.Simple
            };
            
            // 如果是9宫格，设置Sprite的border
            if (layer.ImageType == ImageType.Sliced && sprite != null)
            {
                ApplySpriteBorder(sprite, layer.SliceBorder);
            }

            // 设置raycast target
            image.raycastTarget = false;
        }
        
        private void ApplySpriteBorder(Sprite sprite, Vector4 border)
        {
            if (border == Vector4.zero) return;
            
            var texturePath = AssetDatabase.GetAssetPath(sprite.texture);
            if (string.IsNullOrEmpty(texturePath)) return;
            
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
        }

        private Sprite ExportAndCreateSprite(PsdLayerInfo layer)
        {
            if (layer.Texture == null) return null;

            // 检查缓存
            if (_spriteCache.TryGetValue(layer.Id, out var cachedSprite))
            {
                return cachedSprite;
            }

            var textureName = GetCleanLayerName(layer.Name);
            
            // 先检查通用纹理文件夹中是否存在相同名称的纹理
            var commonSprite = FindSpriteInCommonFolder(textureName);
            if (commonSprite != null)
            {
                _spriteCache[layer.Id] = commonSprite;
                return commonSprite;
            }
            
            // 检查是否存在相同内容的纹理（通过哈希比较）
            var textureHash = ComputeTextureHash(layer.Texture);
            var existingSprite = FindSpriteByHash(textureHash);
            if (existingSprite != null)
            {
                _spriteCache[layer.Id] = existingSprite;
                return existingSprite;
            }

            // 导出纹理
            var texturePath = $"{_textureOutputPath}/{textureName}.png";
            
            // 导出PNG
            var pngData = layer.Texture.EncodeToPNG();
            var fullPath = texturePath.StartsWith("Assets/") 
                ? Path.Combine(Application.dataPath, texturePath.Substring(7))
                : texturePath;
            
            var directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllBytes(fullPath, pngData);
            AssetDatabase.ImportAsset(texturePath);

            // 设置纹理导入设置
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = _settings.MaxTextureSize;
                importer.textureCompression = (TextureImporterCompression)_settings.TextureCompression;
                importer.mipmapEnabled = _settings.GenerateMipMaps;
                importer.SaveAndReimport();
            }

            // 加载Sprite
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            _spriteCache[layer.Id] = sprite;
            
            // 记录纹理哈希
            if (!string.IsNullOrEmpty(textureHash))
            {
                _textureHashCache[textureHash] = sprite;
            }
            
            return sprite;
        }
        
        /// <summary>
        /// 在通用纹理文件夹中查找同名的 Sprite
        /// </summary>
        private Sprite FindSpriteInCommonFolder(string textureName)
        {
            if (string.IsNullOrEmpty(_settings.CommonTexturePath))
                return null;
                
            // 检查多种可能的扩展名
            string[] extensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd" };
            foreach (var ext in extensions)
            {
                var path = $"{_settings.CommonTexturePath}/{textureName}{ext}";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    Debug.Log($"[UguiConverter] Found existing sprite in common folder: {path}");
                    return sprite;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 计算纹理的哈希值
        /// </summary>
        private string ComputeTextureHash(Texture2D texture)
        {
            if (texture == null) return null;
            
            try
            {
                var pixels = texture.GetPixels32();
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    var bytes = new byte[pixels.Length * 4];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        bytes[i * 4] = pixels[i].r;
                        bytes[i * 4 + 1] = pixels[i].g;
                        bytes[i * 4 + 2] = pixels[i].b;
                        bytes[i * 4 + 3] = pixels[i].a;
                    }
                    var hash = md5.ComputeHash(bytes);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 通过哈希值查找已存在的 Sprite
        /// </summary>
        private Sprite FindSpriteByHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return null;
                
            if (_textureHashCache.TryGetValue(hash, out var sprite))
            {
                return sprite;
            }
            
            return null;
        }

        private void ConvertTextLayer(GameObject go, PsdLayerInfo layer)
        {
            var textData = layer.TextData ?? new PsdTextData
            {
                Text = layer.Name,
                FontSize = 24,
                Color = Color.white,
                Alignment = PsdTextAlignment.Left
            };

            if (_settings.UseTextMeshPro)
            {
                ConvertToTextMeshPro(go, layer, textData);
            }
            else
            {
                ConvertToUnityText(go, layer, textData);
            }
        }

        private void ConvertToUnityText(GameObject go, PsdLayerInfo layer, PsdTextData textData)
        {
            var text = go.AddComponent<Text>();
            
            text.text = textData.Text ?? layer.Name;
            text.fontSize = Mathf.RoundToInt(textData.FontSize);
            text.color = new Color(textData.Color.r, textData.Color.g, textData.Color.b, layer.Opacity);
            
            text.alignment = textData.Alignment switch
            {
                PsdTextAlignment.Center => TextAnchor.MiddleCenter,
                PsdTextAlignment.Right => TextAnchor.MiddleRight,
                PsdTextAlignment.Justify => TextAnchor.MiddleCenter,
                _ => TextAnchor.MiddleLeft
            };

            // 根据字体名称查找字体
            var font = _settings.FindFont(textData.FontName, out var isDefault);
            if (font != null)
            {
                text.font = font;
            }
            
            if (isDefault && !string.IsNullOrEmpty(textData.FontName))
            {
                Debug.LogWarning($"[PsdToUgui] 未找到字体 \"{textData.FontName}\"，使用默认字体。图层: {layer.Name}");
            }

            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // 添加效果组件
            AddTextEffects(go, layer);
        }

        private void ConvertToTextMeshPro(GameObject go, PsdLayerInfo layer, PsdTextData textData)
        {
            var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (tmpType == null)
            {
                Debug.LogWarning("[PsdToUgui] TextMeshPro未安装，使用Unity Text替代");
                ConvertToUnityText(go, layer, textData);
                return;
            }

            var tmp = go.AddComponent(tmpType);
            
            var textProperty = tmpType.GetProperty("text");
            var fontSizeProperty = tmpType.GetProperty("fontSize");
            var colorProperty = tmpType.GetProperty("color");
            var alignmentProperty = tmpType.GetProperty("alignment");
            var raycastTargetProperty = tmpType.GetProperty("raycastTarget");

            textProperty?.SetValue(tmp, textData.Text ?? layer.Name);
            fontSizeProperty?.SetValue(tmp, textData.FontSize);
            colorProperty?.SetValue(tmp, new Color(textData.Color.r, textData.Color.g, textData.Color.b, layer.Opacity));
            raycastTargetProperty?.SetValue(tmp, false);

            var alignmentType = Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
            if (alignmentType != null && alignmentProperty != null)
            {
                var alignValue = textData.Alignment switch
                {
                    PsdTextAlignment.Center => Enum.Parse(alignmentType, "Center"),
                    PsdTextAlignment.Right => Enum.Parse(alignmentType, "Right"),
                    PsdTextAlignment.Justify => Enum.Parse(alignmentType, "Justified"),
                    _ => Enum.Parse(alignmentType, "Left")
                };
                alignmentProperty.SetValue(tmp, alignValue);
            }

            // 根据字体名称查找 TMP 字体
            var tmpFont = _settings.FindTMPFont(textData.FontName, out var isDefault);
            if (tmpFont != null)
            {
                var fontProperty = tmpType.GetProperty("font");
                fontProperty?.SetValue(tmp, tmpFont);
            }
            
            if (isDefault && !string.IsNullOrEmpty(textData.FontName))
            {
                Debug.LogWarning($"[PsdToUgui] 未找到 TMP 字体 \"{textData.FontName}\"，使用默认字体。图层: {layer.Name}");
            }

            AddTextEffects(go, layer);
        }

        private void AddTextEffects(GameObject go, PsdLayerInfo layer)
        {
            if (layer.EffectData == null) return;

            if (layer.EffectData.Stroke?.Enabled == true)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = layer.EffectData.Stroke.Color;
                outline.effectDistance = new Vector2(
                    layer.EffectData.Stroke.Size, 
                    layer.EffectData.Stroke.Size
                );
            }

            if (layer.EffectData.DropShadow?.Enabled == true)
            {
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = layer.EffectData.DropShadow.Color;
                
                var angle = layer.EffectData.DropShadow.Angle * Mathf.Deg2Rad;
                var distance = layer.EffectData.DropShadow.Distance;
                shadow.effectDistance = new Vector2(
                    Mathf.Cos(angle) * distance,
                    -Mathf.Sin(angle) * distance
                );
            }
        }

        private string GetCleanLayerName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "unnamed";
                
            var cleanName = name;
            
            // 将中文转换为拼音
            cleanName = ConvertChineseToPinyin(cleanName);
            
            if (_settings.CleanLayerNames)
            {
                var invalidChars = Path.GetInvalidFileNameChars();
                foreach (var c in invalidChars)
                {
                    cleanName = cleanName.Replace(c.ToString(), "");
                }

                cleanName = cleanName.Replace(" ", "_")
                                     .Replace("(", "")
                                     .Replace(")", "")
                                     .Replace("[", "")
                                     .Replace("]", "")
                                     .Replace("-", "_")
                                     .Replace(".", "_");
                
                // 移除连续的下划线
                while (cleanName.Contains("__"))
                {
                    cleanName = cleanName.Replace("__", "_");
                }
                
                // 移除首尾的下划线
                cleanName = cleanName.Trim('_');
            }

            if (string.IsNullOrEmpty(cleanName))
                cleanName = "layer";

            return _settings.LayerNamePrefix + cleanName + _settings.LayerNameSuffix;
        }
        
        /// <summary>
        /// 将中文字符转换为拼音
        /// </summary>
        private string ConvertChineseToPinyin(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            var result = new System.Text.StringBuilder();
            bool lastWasChinese = false;
            
            foreach (char c in text)
            {
                if (IsChinese(c))
                {
                    var pinyin = GetPinyin(c);
                    if (!string.IsNullOrEmpty(pinyin))
                    {
                        // 如果上一个字符不是中文，添加下划线分隔
                        if (result.Length > 0 && !lastWasChinese)
                        {
                            result.Append("_");
                        }
                        result.Append(pinyin);
                        lastWasChinese = true;
                    }
                }
                else
                {
                    // 如果上一个是中文，当前不是，添加下划线分隔
                    if (lastWasChinese && result.Length > 0 && c != '_' && c != ' ')
                    {
                        result.Append("_");
                    }
                    result.Append(c);
                    lastWasChinese = false;
                }
            }
            
            return result.ToString();
        }
        
        private bool IsChinese(char c)
        {
            return c >= 0x4E00 && c <= 0x9FFF;
        }
        
        /// <summary>
        /// 获取单个汉字的拼音（简化版，只包含常用字）
        /// </summary>
        private string GetPinyin(char c)
        {
            // 常用汉字拼音映射表
            var pinyinMap = new Dictionary<char, string>
            {
                // 常用UI相关词汇
                {'按', "an"}, {'钮', "niu"}, {'背', "bei"}, {'景', "jing"}, {'图', "tu"}, {'标', "biao"},
                {'题', "ti"}, {'文', "wen"}, {'本', "ben"}, {'字', "zi"}, {'体', "ti"}, {'框', "kuang"},
                {'输', "shu"}, {'入', "ru"}, {'确', "que"}, {'定', "ding"}, {'取', "qu"}, {'消', "xiao"},
                {'关', "guan"}, {'闭', "bi"}, {'返', "fan"}, {'回', "hui"}, {'主', "zhu"}, {'页', "ye"},
                {'面', "mian"}, {'菜', "cai"}, {'单', "dan"}, {'列', "lie"}, {'表', "biao"}, {'项', "xiang"},
                {'选', "xuan"}, {'择', "ze"}, {'设', "she"}, {'置', "zhi"}, {'头', "tou"}, {'像', "xiang"},
                {'名', "ming"}, {'称', "cheng"}, {'昵', "ni"}, {'等', "deng"}, {'级', "ji"}, {'经', "jing"},
                {'验', "yan"}, {'金', "jin"}, {'币', "bi"}, {'钻', "zuan"}, {'石', "shi"}, {'道', "dao"},
                {'具', "ju"}, {'装', "zhuang"}, {'备', "bei"}, {'技', "ji"}, {'能', "neng"}, {'属', "shu"},
                {'性', "xing"}, {'攻', "gong"}, {'击', "ji"}, {'防', "fang"}, {'御', "yu"}, {'血', "xue"},
                {'量', "liang"}, {'蓝', "lan"}, {'条', "tiao"}, {'进', "jin"}, {'度', "du"}, {'加', "jia"},
                {'载', "zai"}, {'登', "deng"}, {'录', "lu"}, {'注', "zhu"}, {'册', "ce"}, {'账', "zhang"},
                {'号', "hao"}, {'密', "mi"}, {'码', "ma"}, {'忘', "wang"}, {'记', "ji"}, {'找', "zhao"},
                {'商', "shang"}, {'店', "dian"}, {'购', "gou"}, {'买', "mai"}, {'出', "chu"}, {'售', "shou"},
                {'价', "jia"}, {'格', "ge"}, {'数', "shu"}, {'目', "mu"}, {'描', "miao"}, {'述', "shu"},
                {'详', "xiang"}, {'情', "qing"}, {'任', "ren"}, {'务', "wu"}, {'完', "wan"}, {'成', "cheng"},
                {'奖', "jiang"}, {'励', "li"}, {'领', "ling"}, {'活', "huo"}, {'动', "dong"}, {'公', "gong"},
                {'告', "gao"}, {'邮', "you"}, {'件', "jian"}, {'好', "hao"}, {'友', "you"}, {'聊', "liao"},
                {'天', "tian"}, {'发', "fa"}, {'送', "song"}, {'接', "jie"}, {'收', "shou"}, {'系', "xi"},
                {'统', "tong"}, {'提', "ti"}, {'示', "shi"}, {'警', "jing"}, {'错', "cuo"}, {'误', "wu"},
                {'成', "cheng"}, {'功', "gong"}, {'失', "shi"}, {'败', "bai"}, {'开', "kai"}, {'始', "shi"},
                {'结', "jie"}, {'束', "shu"}, {'暂', "zan"}, {'停', "ting"}, {'继', "ji"}, {'续', "xu"},
                {'重', "chong"}, {'新', "xin"}, {'试', "shi"}, {'退', "tui"}, {'游', "you"}, {'戏', "xi"},
                {'音', "yin"}, {'乐', "yue"}, {'效', "xiao"}, {'果', "guo"}, {'声', "sheng"}, {'画', "hua"},
                {'质', "zhi"}, {'高', "gao"}, {'中', "zhong"}, {'低', "di"}, {'语', "yu"}, {'言', "yan"},
                {'帮', "bang"}, {'助', "zhu"}, {'客', "ke"}, {'服', "fu"}, {'反', "fan"}, {'馈', "kui"},
                {'版', "ban"}, {'更', "geng"}, {'检', "jian"}, {'查', "cha"}, {'下', "xia"}, {'上', "shang"},
                {'左', "zuo"}, {'右', "you"}, {'前', "qian"}, {'后', "hou"}, {'内', "nei"}, {'外', "wai"},
                {'大', "da"}, {'小', "xiao"}, {'多', "duo"}, {'少', "shao"}, {'全', "quan"}, {'部', "bu"},
                {'分', "fen"}, {'组', "zu"}, {'合', "he"}, {'并', "bing"}, {'拆', "chai"}, {'解', "jie"},
                {'锁', "suo"}, {'已', "yi"}, {'未', "wei"}, {'可', "ke"}, {'不', "bu"}, {'是', "shi"},
                {'否', "fou"}, {'有', "you"}, {'无', "wu"}, {'空', "kong"}, {'满', "man"}, {'红', "hong"},
                {'绿', "lv"}, {'黄', "huang"}, {'白', "bai"}, {'黑', "hei"}, {'灰', "hui"}, {'紫', "zi"},
                {'橙', "cheng"}, {'粉', "fen"}, {'色', "se"}, {'透', "tou"}, {'明', "ming"}, {'亮', "liang"},
                {'暗', "an"}, {'深', "shen"}, {'浅', "qian"}, {'粗', "cu"}, {'细', "xi"}, {'宽', "kuan"},
                {'窄', "zhai"}, {'长', "chang"}, {'短', "duan"}, {'圆', "yuan"}, {'方', "fang"}, {'角', "jiao"},
                {'边', "bian"}, {'线', "xian"}, {'点', "dian"}, {'块', "kuai"}, {'层', "ceng"}, {'底', "di"},
                {'顶', "ding"}, {'侧', "ce"}, {'栏', "lan"}, {'导', "dao"}, {'航', "hang"}, {'搜', "sou"},
                {'索', "suo"}, {'筛', "shai"}, {'排', "pai"}, {'序', "xu"}, {'升', "sheng"}, {'降', "jiang"},
                {'刷', "shua"}, {'清', "qing"}, {'除', "chu"}, {'删', "shan"}, {'编', "bian"}, {'辑', "ji"},
                {'复', "fu"}, {'制', "zhi"}, {'粘', "zhan"}, {'贴', "tie"}, {'剪', "jian"}, {'切', "qie"},
                {'保', "bao"}, {'存', "cun"}, {'另', "ling"}, {'为', "wei"}, {'打', "da"}, {'印', "yin"},
                {'预', "yu"}, {'览', "lan"}, {'放', "fang"}, {'缩', "suo"}, {'旋', "xuan"}, {'转', "zhuan"},
                {'翻', "fan"}, {'滚', "gun"}, {'拖', "tuo"}, {'拽', "zhuai"}, {'移', "yi"}, {'位', "wei"},
                {'对', "dui"}, {'齐', "qi"}, {'居', "ju"}, {'间', "jian"}, {'距', "ju"}, {'填', "tian"},
                {'充', "chong"}, {'拉', "la"}, {'伸', "shen"}, {'固', "gu"}, {'锚', "mao"}, {'适', "shi"},
                {'应', "ying"}, {'自', "zi"}, {'手', "shou"}, {'脚', "jiao"}, {'身', "shen"}, {'心', "xin"},
                {'眼', "yan"}, {'耳', "er"}, {'口', "kou"}, {'鼻', "bi"}, {'嘴', "zui"}, {'脸', "lian"},
                {'人', "ren"}, {'物', "wu"}, {'怪', "guai"}, {'兽', "shou"}, {'龙', "long"}, {'凤', "feng"},
                {'虎', "hu"}, {'狼', "lang"}, {'鹰', "ying"}, {'马', "ma"}, {'牛', "niu"}, {'羊', "yang"},
                {'猪', "zhu"}, {'狗', "gou"}, {'猫', "mao"}, {'鸟', "niao"}, {'鱼', "yu"}, {'虫', "chong"},
                {'花', "hua"}, {'草', "cao"}, {'树', "shu"}, {'木', "mu"}, {'林', "lin"}, {'森', "sen"},
                {'山', "shan"}, {'水', "shui"}, {'火', "huo"}, {'土', "tu"}, {'风', "feng"}, {'雷', "lei"},
                {'电', "dian"}, {'冰', "bing"}, {'雪', "xue"}, {'雨', "yu"}, {'云', "yun"}, {'雾', "wu"},
                {'日', "ri"}, {'月', "yue"}, {'星', "xing"}, {'光', "guang"}, {'影', "ying"}, {'阴', "yin"},
                {'阳', "yang"}, {'春', "chun"}, {'夏', "xia"}, {'秋', "qiu"}, {'冬', "dong"}, {'年', "nian"},
                {'时', "shi"}, {'秒', "miao"}, {'刻', "ke"}, {'今', "jin"}, {'昨', "zuo"}, {'明', "ming"},
                {'早', "zao"}, {'晚', "wan"}, {'午', "wu"}, {'夜', "ye"}, {'周', "zhou"}, {'末', "mo"},
                {'一', "yi"}, {'二', "er"}, {'三', "san"}, {'四', "si"}, {'五', "wu"}, {'六', "liu"},
                {'七', "qi"}, {'八', "ba"}, {'九', "jiu"}, {'十', "shi"}, {'百', "bai"}, {'千', "qian"},
                {'万', "wan"}, {'亿', "yi"}, {'零', "ling"}, {'第', "di"}, {'次', "ci"}, {'个', "ge"},
                {'只', "zhi"}, {'把', "ba"}, {'张', "zhang"}, {'件', "jian"}, {'套', "tao"}, {'副', "fu"},
                {'双', "shuang"}, {'对', "dui"}, {'组', "zu"}, {'批', "pi"}, {'堆', "dui"}, {'群', "qun"},
                {'队', "dui"}, {'排', "pai"}, {'行', "hang"}, {'列', "lie"}, {'串', "chuan"}, {'束', "shu"},
                {'包', "bao"}, {'袋', "dai"}, {'盒', "he"}, {'箱', "xiang"}, {'瓶', "ping"}, {'罐', "guan"},
                {'碗', "wan"}, {'杯', "bei"}, {'盘', "pan"}, {'碟', "die"}, {'锅', "guo"}, {'壶', "hu"},
            };
            
            if (pinyinMap.TryGetValue(c, out var pinyin))
            {
                return pinyin;
            }
            
            // 如果没有找到映射，返回 Unicode 编码
            return $"u{((int)c):X4}";
        }
    }
}
