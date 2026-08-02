using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using GameDeveloperKit.UI;
using GameDeveloperKit.UIEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameDeveloperKit.DesignImporter
{
    internal static class DesignPrefabBuilder
    {
        private static readonly Dictionary<string, TMP_FontAsset> s_FontAssets =
            new Dictionary<string, TMP_FontAsset>(StringComparer.OrdinalIgnoreCase);
        private static bool s_FontAssetsLoaded;

        public static string Build(
            DesignPage page,
            string prefabPath,
            DesignImportOptions options,
            IReadOnlyDictionary<string, string> nodeAssetPaths,
            string previewFallbackAssetPath = null)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            var constructionScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = EditorUtility.CreateGameObjectWithHideFlags(
                    DesignPathUtility.SanitizeFileName(page.Name, "Screen"),
                    HideFlags.HideAndDontSave,
                    typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, constructionScene);

                ConfigureRoot(root, options);
                var viewport = DesignLayoutUtility.CalculateViewport(
                    new Vector2(page.Width, page.Height),
                    options.TargetResolution,
                    options.ScaleMode);
                var content = CreateCenteredRect(root.transform, "Design", page.Width, page.Height);
                content.localScale = new Vector3(viewport.Scale.x, viewport.Scale.y, 1f);
                content.anchoredPosition = new Vector2(
                    -options.TargetResolution.x * 0.5f + viewport.Offset.x + page.Width * viewport.Scale.x * 0.5f,
                    options.TargetResolution.y * 0.5f - viewport.Offset.y - page.Height * viewport.Scale.y * 0.5f);
                BuildPreviewFallback(content, page.Width, page.Height, previewFallbackAssetPath);
                var bindings = new List<GeneratedBinding>();
                BuildNode(page.Root, content, nodeAssetPaths, true, bindings);
                ConfigureDocument(root.GetComponent<UIDocument>(), root.GetComponent<RectTransform>(), options, bindings);

                EnsureAssetFolder(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));
                ClearHideFlags(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                {
                    throw new IOException("无法保存 Prefab：" + prefabPath);
                }

                if (options.GenerateWindowCode)
                {
                    var document = prefab.GetComponent<UIDocument>();
                    if (document == null)
                    {
                        throw new InvalidOperationException("生成的 Prefab 缺少 UIDocument。");
                    }

                    UIDocumentGenerator.Generate(
                        document,
                        Path.GetFileNameWithoutExtension(prefabPath),
                        options.GeneratedCodeRoot,
                        prefabPath,
                        UILayer.FromOrder(options.LayerOrder));
                }

                return prefabPath;
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                if (constructionScene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(constructionScene);
                }
            }
        }

        private static void ConfigureRoot(GameObject root, DesignImportOptions options)
        {
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = options.TargetResolution;
            rect.localScale = Vector3.one;
            root.AddComponent<UIDocument>();

            if (!options.IncludeCanvas)
            {
                return;
            }

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = options.TargetResolution;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
        }

        private static void BuildNode(
            DesignNode node,
            RectTransform parent,
            IReadOnlyDictionary<string, string> nodeAssetPaths,
            bool isPageRoot,
            ICollection<GeneratedBinding> bindings)
        {
            if (node == null || !node.Visible || node.Width <= 0f || node.Height <= 0f)
            {
                return;
            }

            var x = isPageRoot ? 0f : node.X;
            var y = isPageRoot ? 0f : node.Y;
            var width = isPageRoot ? parent.rect.width : node.Width;
            var height = isPageRoot ? parent.rect.height : node.Height;
            var rect = CreateNodeRect(
                parent,
                DesignPathUtility.SanitizeFileName(node.Name, node.Kind.ToString()),
                node,
                x,
                y,
                width,
                height);

            Graphic graphic;
            switch (node.Kind)
            {
                case DesignNodeKind.Image:
                    graphic = BuildImage(node, rect, nodeAssetPaths);
                    break;
                case DesignNodeKind.Text:
                    graphic = BuildText(node, rect);
                    break;
                default:
                    graphic = BuildContainer(node, rect);
                    break;
            }

            var childParent = rect;
            RectTransform viewport = null;
            RectTransform scrollContent = null;
            if (node.Component == DesignComponentKind.ScrollRect)
            {
                viewport = CreateStretchRect(rect, "Viewport");
                viewport.gameObject.AddComponent<RectMask2D>();
                scrollContent = CreateCenteredRect(viewport, "Content", node.Width, node.Height);
                childParent = scrollContent;
            }

            foreach (var child in node.Children)
            {
                BuildNode(child, childParent, nodeAssetPaths, false, bindings);
            }

            var component = BuildInteractiveComponent(node, rect, graphic, viewport, scrollContent);
            if (!string.IsNullOrWhiteSpace(node.BindingName))
            {
                Component bindingComponent = component ?? (Component)graphic ?? rect;
                bindings.Add(new GeneratedBinding(node.BindingName.Trim(), rect.gameObject, bindingComponent));
            }
        }

        private static Image BuildImage(
            DesignNode node,
            RectTransform rect,
            IReadOnlyDictionary<string, string> nodeAssetPaths)
        {
            if (!nodeAssetPaths.TryGetValue(node.Id, out var assetPath))
            {
                throw new InvalidOperationException("节点缺少已导入切图：" + node.Name);
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("无法加载 Sprite：" + assetPath);
            }

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = node.NineSlice || node.Border.HasValue ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.color = WithOpacity(Color.white, node.Opacity);
            return image;
        }

        private static void BuildPreviewFallback(
            RectTransform parent,
            float width,
            float height,
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("无法加载设计稿背景兜底图：" + assetPath);
            }

            var rect = CreateCenteredRect(parent, "Background [Preview Fallback]", width, height);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        private static TextMeshProUGUI BuildText(DesignNode node, RectTransform rect)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = node.Text;
            text.fontSize = node.FontSize;
            text.enableAutoSizing = false;
            text.richText = false;
            text.fontStyle = ResolveFontStyle(node);
            text.color = WithOpacity(ParseColor(node.Color, Color.white), node.Opacity);
            text.alignment = ResolveTextAlignment(node.TextAlignment);
            text.characterSpacing = node.Tracking / 1000f * node.FontSize;
            text.lineSpacing = node.LineHeight > 0f ? node.LineHeight - node.FontSize : 0f;
            text.margin = new Vector4(0f, -node.FontSize * 0.2f, 0f, 0f);
            text.enableWordWrapping = node.WordWrap && !IsSingleLineText(node);
            text.overflowMode = ResolveOverflow(node.Overflow);
            text.raycastTarget = false;
            var font = ResolveFontAsset(node);
            if (font != null)
            {
                text.font = font;
            }

            return text;
        }

        private static bool IsSingleLineText(DesignNode node)
        {
            if (string.IsNullOrEmpty(node.Text) ||
                node.Text.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                return false;
            }

            var sourceLineHeight = node.LineHeight > 0f ? node.LineHeight : node.FontSize;
            return node.Height < Mathf.Max(1f, sourceLineHeight) * 1.75f;
        }

        private static Image BuildContainer(DesignNode node, RectTransform rect)
        {
            Image image = null;
            if (!string.IsNullOrWhiteSpace(node.BackgroundColor))
            {
                image = rect.gameObject.AddComponent<Image>();
                image.color = WithOpacity(ParseColor(node.BackgroundColor, Color.clear), node.Opacity);
                image.raycastTarget = false;
            }

            if (node.ClipsContent)
            {
                rect.gameObject.AddComponent<RectMask2D>();
            }

            return image;
        }

        private static Component BuildInteractiveComponent(
            DesignNode node,
            RectTransform rect,
            Graphic graphic,
            RectTransform viewport,
            RectTransform scrollContent)
        {
            switch (node.Component)
            {
                case DesignComponentKind.Button:
                {
                    var target = EnsureRaycastGraphic(rect, graphic);
                    var button = rect.gameObject.AddComponent<Button>();
                    button.targetGraphic = target;
                    button.interactable = node.Interactable;
                    return button;
                }
                case DesignComponentKind.Toggle:
                {
                    var target = EnsureRaycastGraphic(rect, graphic);
                    var toggle = rect.gameObject.AddComponent<Toggle>();
                    toggle.targetGraphic = target;
                    toggle.isOn = node.ToggleValue;
                    toggle.interactable = node.Interactable;
                    var indicator = FindChildGraphic(rect, target);
                    if (indicator != null && !ReferenceEquals(indicator, target))
                    {
                        toggle.graphic = indicator;
                    }

                    return toggle;
                }
                case DesignComponentKind.Slider:
                {
                    var target = EnsureRaycastGraphic(rect, graphic);
                    var slider = rect.gameObject.AddComponent<Slider>();
                    slider.targetGraphic = target;
                    slider.minValue = node.SliderMinValue;
                    slider.maxValue = Mathf.Max(node.SliderMinValue, node.SliderMaxValue);
                    slider.value = Mathf.Clamp(node.SliderValue, slider.minValue, slider.maxValue);
                    slider.wholeNumbers = node.SliderWholeNumbers;
                    slider.interactable = node.Interactable;
                    return slider;
                }
                case DesignComponentKind.InputField:
                {
                    var target = EnsureRaycastGraphic(rect, graphic);
                    var input = rect.gameObject.AddComponent<TMP_InputField>();
                    input.targetGraphic = target;
                    input.textComponent = rect.GetComponent<TextMeshProUGUI>() ??
                                          rect.GetComponentInChildren<TextMeshProUGUI>(true);
                    input.interactable = node.Interactable;
                    return input;
                }
                case DesignComponentKind.ScrollRect:
                {
                    var target = EnsureRaycastGraphic(rect, graphic);
                    var scrollRect = rect.gameObject.AddComponent<ScrollRect>();
                    scrollRect.viewport = viewport;
                    scrollRect.content = scrollContent;
                    scrollRect.horizontal = node.ScrollHorizontal;
                    scrollRect.vertical = node.ScrollVertical;
                    scrollRect.inertia = true;
                    scrollRect.enabled = node.Interactable;
                    target.raycastTarget = true;
                    return scrollRect;
                }
                default:
                    return null;
            }
        }

        private static Graphic EnsureRaycastGraphic(RectTransform rect, Graphic graphic)
        {
            var target = graphic ?? rect.GetComponent<Graphic>();
            if (target == null)
            {
                var image = rect.gameObject.AddComponent<Image>();
                image.color = Color.clear;
                target = image;
            }

            target.raycastTarget = true;
            return target;
        }

        private static Graphic FindChildGraphic(RectTransform rect, Graphic ignored)
        {
            var graphics = rect.GetComponentsInChildren<Graphic>(true);
            foreach (var graphic in graphics)
            {
                if (!ReferenceEquals(graphic, ignored))
                {
                    return graphic;
                }
            }

            return null;
        }

        private static RectTransform CreateCenteredRect(
            Transform parent,
            string name,
            float width,
            float height)
        {
            var gameObject = EditorUtility.CreateGameObjectWithHideFlags(
                name,
                HideFlags.HideAndDontSave,
                typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static RectTransform CreateStretchRect(RectTransform parent, string name)
        {
            var rect = CreateCenteredRect(parent, name, parent.rect.width, parent.rect.height);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform CreateNodeRect(
            RectTransform parent,
            string name,
            DesignNode node,
            float x,
            float y,
            float width,
            float height)
        {
            var rect = CreateCenteredRect(parent, name, width, height);
            var parentWidth = Mathf.Max(1f, parent.rect.width);
            var parentHeight = Mathf.Max(1f, parent.rect.height);
            var pivot = node.Pivot?.Value ?? new Vector2(0.5f, 0.5f);
            var defaultAnchor = new Vector2(
                (x + width * pivot.x) / parentWidth,
                1f - (y + height * (1f - pivot.y)) / parentHeight);
            var anchorMin = node.AnchorMin?.Value ?? defaultAnchor;
            var anchorMax = node.AnchorMax?.Value ?? anchorMin;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(
                Mathf.Max(1f, width) - (anchorMax.x - anchorMin.x) * parentWidth,
                Mathf.Max(1f, height) - (anchorMax.y - anchorMin.y) * parentHeight);

            var pivotPosition = new Vector2(
                x + width * pivot.x,
                parentHeight - y - height * (1f - pivot.y));
            var anchorReference = new Vector2(
                Mathf.Lerp(anchorMin.x, anchorMax.x, pivot.x) * parentWidth,
                Mathf.Lerp(anchorMin.y, anchorMax.y, pivot.y) * parentHeight);
            rect.anchoredPosition = pivotPosition - anchorReference;
            return rect;
        }

        private static void ClearHideFlags(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null)
                {
                    component.hideFlags = HideFlags.None;
                }
            }

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = HideFlags.None;
            }
        }

        private static void ConfigureDocument(
            UIDocument document,
            RectTransform fullScreenRoot,
            DesignImportOptions options,
            IReadOnlyList<GeneratedBinding> bindings)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in bindings)
            {
                if (UIDocumentBindingRules.IsBindingNameValid(binding.Name) is false)
                {
                    throw new InvalidOperationException($"绑定 Key '{binding.Name}' 不是有效的 C# 标识符。");
                }

                if (!names.Add(binding.Name))
                {
                    throw new InvalidOperationException($"页面中存在重复的绑定 Key：{binding.Name}。");
                }
            }

            var serializedDocument = new SerializedObject(document);
            serializedDocument.FindProperty("fullScreenRoot").objectReferenceValue = fullScreenRoot;
            serializedDocument.FindProperty("layerOrder").intValue = options.LayerOrder;
            serializedDocument.FindProperty("m_CacheEnabled").boolValue = options.CacheEnabled;
            serializedDocument.FindProperty("m_CodeNamespace").stringValue = options.CodeNamespace ?? string.Empty;
            var mappings = serializedDocument.FindProperty("mappings");
            mappings.arraySize = bindings.Count;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                var mapping = mappings.GetArrayElementAtIndex(index);
                mapping.FindPropertyRelative("Name").stringValue = binding.Name;
                mapping.FindPropertyRelative("Target").objectReferenceValue = binding.Target;
                var components = mapping.FindPropertyRelative("Components");
                components.arraySize = 1;
                components.GetArrayElementAtIndex(0).objectReferenceValue = binding.Component;
            }

            serializedDocument.FindProperty("localizedTexts").arraySize = 0;
            serializedDocument.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TextAlignmentOptions ResolveTextAlignment(string value)
        {
            return (value ?? string.Empty).ToLowerInvariant() switch
            {
                "center" => TextAlignmentOptions.Top,
                "right" => TextAlignmentOptions.TopRight,
                "justified" => TextAlignmentOptions.TopJustified,
                _ => TextAlignmentOptions.TopLeft
            };
        }

        private static FontStyles ResolveFontStyle(DesignNode node)
        {
            var style = FontStyles.Normal;
            if (node.Bold || node.FontStyleName.IndexOf("bold", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                style |= FontStyles.Bold;
            }

            if (node.Italic || node.FontStyleName.IndexOf("italic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                style |= FontStyles.Italic;
            }

            return style;
        }

        private static TextOverflowModes ResolveOverflow(string value)
        {
            return (value ?? string.Empty).ToLowerInvariant() switch
            {
                "ellipsis" => TextOverflowModes.Ellipsis,
                "truncate" => TextOverflowModes.Truncate,
                "masking" => TextOverflowModes.Masking,
                _ => TextOverflowModes.Overflow
            };
        }

        private static TMP_FontAsset ResolveFontAsset(DesignNode node)
        {
            EnsureFontAssetIndex();
            foreach (var name in new[] { node.FontPostScriptName, node.FontName })
            {
                if (!string.IsNullOrWhiteSpace(name) && s_FontAssets.TryGetValue(name.Trim(), out var font))
                {
                    return font;
                }
            }

            foreach (var alias in ResolveCompatibleFontAliases(node))
            {
                if (s_FontAssets.TryGetValue(alias, out var font))
                {
                    return font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static IEnumerable<string> ResolveCompatibleFontAliases(DesignNode node)
        {
            var sourceName = $"{node.FontPostScriptName} {node.FontName}".ToLowerInvariant();
            if (sourceName.Contains("zhongsong") ||
                sourceName.Contains("simsun") ||
                sourceName.Contains("songti") ||
                sourceName.Contains("serif"))
            {
                yield return "SOURCEHANSERIFCN-MEDIUM SDF";
                yield return "SOURCEHANSERIFCN-MEDIUM";
                yield return "SIMSUN SDF";
            }
            else if (sourceName.Contains("simhei") ||
                     sourceName.Contains("heiti") ||
                     sourceName.Contains("sans"))
            {
                yield return "Regular SDF";
                yield return "Regular";
            }
        }

        private static void EnsureFontAssetIndex()
        {
            if (s_FontAssetsLoaded)
            {
                return;
            }

            s_FontAssetsLoaded = true;
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null)
                {
                    continue;
                }

                AddFontAlias(font.name, font);
                AddFontAlias(Path.GetFileNameWithoutExtension(path), font);
                if (font.sourceFontFile != null)
                {
                    AddFontAlias(font.sourceFontFile.name, font);
                }
            }
        }

        private static void AddFontAlias(string name, TMP_FontAsset font)
        {
            if (!string.IsNullOrWhiteSpace(name) && !s_FontAssets.ContainsKey(name.Trim()))
            {
                s_FontAssets.Add(name.Trim(), font);
            }
        }

        private static Color ParseColor(string html, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : fallback;
        }

        private static Color WithOpacity(Color color, float opacity)
        {
            color.a *= Mathf.Clamp01(opacity);
            return color;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var segments = path.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private readonly struct GeneratedBinding
        {
            public GeneratedBinding(string name, GameObject target, Component component)
            {
                Name = name;
                Target = target;
                Component = component;
            }

            public string Name { get; }

            public GameObject Target { get; }

            public Component Component { get; }
        }
    }
}
