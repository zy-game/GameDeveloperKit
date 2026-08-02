using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.DesignImporter;
using GameDeveloperKit.LubanConfigEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorConfiguration
{
    internal sealed class EditorConfigurationPanel : VisualElement
    {
        private const string EmptyChoice = "(未选择)";

        private readonly EditorGlobalConfig m_ProjectConfig;
        private readonly EditorUserConfig m_UserConfig;
        private readonly Action m_OnSaved;

        private Label m_ErrorLabel;

        public EditorConfigurationPanel(Action onSaved = null)
        {
            name = "global-config-panel";
            m_ProjectConfig = EditorGlobalConfig.LoadOrCreate();
            m_UserConfig = EditorUserConfig.LoadOrCreate();
            m_OnSaved = onSaved;

            style.flexGrow = 1;
            style.minWidth = 0;
            style.paddingLeft = 24;
            style.paddingRight = 24;
            style.paddingTop = 20;
            style.paddingBottom = 24;
            Build();
        }

        private void Build()
        {
            var content = new VisualElement { name = "global-settings-form" };
            content.style.width = Length.Percent(100);
            content.style.maxWidth = 920;
            content.style.alignSelf = Align.Center;
            Add(content);

            content.Add(CreatePageTitle("全局设置"));
            content.Add(CreateSectionHeader("UI Prefab Studio"));

            var sourceLabels = new List<string> { "蓝湖", "Figma", "JSON 清单" };
            var sourceIds = new List<string>
            {
                UiPrefabStudioProjectConfig.LanhuSource,
                UiPrefabStudioProjectConfig.FigmaSource,
                UiPrefabStudioProjectConfig.ManifestSource
            };
            var sourceIndex = Mathf.Max(0, sourceIds.IndexOf(m_ProjectConfig.UiPrefabStudio.Source));
            var sourceField = new DropdownField("默认来源", sourceLabels, sourceIndex)
            {
                name = "ui-prefab-source-field"
            };
            ConfigureField(sourceField);
            sourceField.RegisterValueChangedCallback(evt =>
            {
                var index = sourceLabels.IndexOf(evt.newValue);
                m_ProjectConfig.UiPrefabStudio.Source = sourceIds[Mathf.Max(0, index)];
                SaveConfigs();
            });
            content.Add(CreateFieldRow(sourceField));

            content.Add(CreateFieldRow(CreateTextField(
                "ui-prefab-lanhu-url-field",
                "蓝湖项目 URL",
                m_ProjectConfig.UiPrefabStudio.LanhuProjectUrl,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.LanhuProjectUrl = value;
                    SaveConfigs();
                })));

            content.Add(CreateActionRow(
                "浏览器同步桥",
                new Button(LanhuBridgeInstaller.OpenExtensionFolder)
                {
                    name = "ui-prefab-lanhu-bridge-button",
                    text = "打开扩展目录",
                    tooltip = "首次使用时在 Chrome/Edge 加载该目录，之后无需重复安装"
                }));

            content.Add(CreateFieldRow(CreateTextField(
                "ui-prefab-figma-file-field",
                "Figma 文件",
                m_ProjectConfig.UiPrefabStudio.FigmaFile,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.FigmaFile = value;
                    SaveConfigs();
                })));

            var figmaTokenField = CreateTextField(
                "ui-prefab-figma-token-field",
                "Figma Token",
                m_UserConfig.FigmaToken,
                value =>
                {
                    m_UserConfig.FigmaToken = value;
                    SaveConfigs();
                });
            figmaTokenField.isPasswordField = true;
            content.Add(CreateFieldRow(figmaTokenField));

            content.Add(CreateFieldRow(CreateIntegerField(
                "ui-prefab-target-width-field",
                "默认目标宽度",
                m_ProjectConfig.UiPrefabStudio.TargetWidth,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.TargetWidth = value;
                    SaveConfigs();
                })));
            content.Add(CreateFieldRow(CreateIntegerField(
                "ui-prefab-target-height-field",
                "默认目标高度",
                m_ProjectConfig.UiPrefabStudio.TargetHeight,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.TargetHeight = value;
                    SaveConfigs();
                })));

            var scaleLabels = new List<string> { "等比适配", "填充裁切", "拉伸" };
            var scaleIds = new List<string>
            {
                UiPrefabStudioProjectConfig.FitScaleMode,
                UiPrefabStudioProjectConfig.FillScaleMode,
                UiPrefabStudioProjectConfig.StretchScaleMode
            };
            var scaleIndex = Mathf.Max(0, scaleIds.IndexOf(m_ProjectConfig.UiPrefabStudio.ScaleMode));
            var scaleField = new DropdownField("默认缩放策略", scaleLabels, scaleIndex)
            {
                name = "ui-prefab-scale-mode-field"
            };
            ConfigureField(scaleField);
            scaleField.RegisterValueChangedCallback(evt =>
            {
                var index = scaleLabels.IndexOf(evt.newValue);
                m_ProjectConfig.UiPrefabStudio.ScaleMode = scaleIds[Mathf.Max(0, index)];
                SaveConfigs();
            });
            content.Add(CreateFieldRow(scaleField));

            var textureSizes = new List<string> { "1024", "2048", "4096", "8192" };
            var selectedTexture = m_ProjectConfig.UiPrefabStudio.MaxTextureSize.ToString();
            var textureField = new DropdownField(
                "默认纹理上限",
                textureSizes,
                Mathf.Max(0, textureSizes.IndexOf(selectedTexture)))
            {
                name = "ui-prefab-texture-size-field"
            };
            ConfigureField(textureField);
            textureField.RegisterValueChangedCallback(evt =>
            {
                if (int.TryParse(evt.newValue, out var value))
                {
                    m_ProjectConfig.UiPrefabStudio.MaxTextureSize = value;
                    SaveConfigs();
                }
            });
            content.Add(CreateFieldRow(textureField));

            content.Add(CreateFieldRow(CreateToggleField(
                "ui-prefab-include-canvas-field",
                "默认包含 Canvas",
                m_ProjectConfig.UiPrefabStudio.IncludeCanvas,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.IncludeCanvas = value;
                    SaveConfigs();
                })));
            content.Add(CreateFieldRow(CreateToggleField(
                "ui-prefab-extract-shared-field",
                "默认抽离公共资源",
                m_ProjectConfig.UiPrefabStudio.ExtractSharedAssets,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.ExtractSharedAssets = value;
                    SaveConfigs();
                })));

            content.Add(CreateFieldRow(CreateToggleField(
                "ui-prefab-generate-window-code-field",
                "生成 UIWindow 绑定代码",
                m_ProjectConfig.UiPrefabStudio.GenerateWindowCode,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.GenerateWindowCode = value;
                    SaveConfigs();
                })));

            var uiCodeOutputField = CreateTextField(
                "ui-prefab-code-root-field",
                "窗口代码输出目录",
                m_ProjectConfig.UiPrefabStudio.GeneratedCodeRoot,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.GeneratedCodeRoot = value;
                    SaveConfigs();
                });
            content.Add(CreatePathFieldRow(
                uiCodeOutputField,
                CreateBrowseButton(
                    "ui-prefab-code-root-browse-button",
                    "选择窗口代码输出目录",
                    () => SelectDirectory(uiCodeOutputField, "选择窗口代码输出目录", value =>
                    {
                        m_ProjectConfig.UiPrefabStudio.GeneratedCodeRoot = value;
                        SaveConfigs();
                    }))));

            content.Add(CreateFieldRow(CreateTextField(
                "ui-prefab-code-namespace-field",
                "窗口代码命名空间",
                m_ProjectConfig.UiPrefabStudio.CodeNamespace,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.CodeNamespace = value;
                    SaveConfigs();
                })));
            content.Add(CreateFieldRow(CreateIntegerField(
                "ui-prefab-layer-order-field",
                "窗口层级",
                m_ProjectConfig.UiPrefabStudio.LayerOrder,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.LayerOrder = value;
                    SaveConfigs();
                })));
            content.Add(CreateFieldRow(CreateToggleField(
                "ui-prefab-cache-enabled-field",
                "窗口启用缓存",
                m_ProjectConfig.UiPrefabStudio.CacheEnabled,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.CacheEnabled = value;
                    SaveConfigs();
                })));

            var uiOutputField = CreateTextField(
                "ui-prefab-output-root-field",
                "Prefab 输出目录",
                m_ProjectConfig.UiPrefabStudio.OutputRoot,
                value =>
                {
                    m_ProjectConfig.UiPrefabStudio.OutputRoot = value;
                    SaveConfigs();
                });
            content.Add(CreatePathFieldRow(
                uiOutputField,
                CreateBrowseButton(
                    "ui-prefab-output-root-browse-button",
                    "选择 Prefab 输出目录",
                    () => SelectDirectory(uiOutputField, "选择 Prefab 输出目录", value =>
                    {
                        m_ProjectConfig.UiPrefabStudio.OutputRoot = value;
                        SaveConfigs();
                    }))));

            content.Add(CreateSectionHeader("Luban"));

            var tableDirectoryField = CreateTextField(
                "table-directory-field",
                "配置表目录",
                m_ProjectConfig.Luban.TableDirectory,
                value =>
                {
                    m_ProjectConfig.Luban.TableDirectory = value;
                    SaveConfigs();
                });
            content.Add(CreatePathFieldRow(
                tableDirectoryField,
                CreateBrowseButton(
                    "table-directory-browse-button",
                    "选择配置表目录",
                    () => SelectDirectory(tableDirectoryField, "选择配置表目录", value =>
                    {
                        m_ProjectConfig.Luban.TableDirectory = value;
                        SaveConfigs();
                    }))));

            var generatedCodeField = CreateTextField(
                "generated-code-directory-field",
                "生成代码目录",
                m_ProjectConfig.Luban.GeneratedCodeDirectory,
                value =>
                {
                    m_ProjectConfig.Luban.GeneratedCodeDirectory = value;
                    SaveConfigs();
                });
            content.Add(CreatePathFieldRow(
                generatedCodeField,
                CreateBrowseButton(
                    "generated-code-directory-browse-button",
                    "选择生成代码目录",
                    () => SelectDirectory(generatedCodeField, "选择生成代码目录", value =>
                    {
                        m_ProjectConfig.Luban.GeneratedCodeDirectory = value;
                        SaveConfigs();
                    }))));

            var generatedDataField = CreateTextField(
                "generated-data-directory-field",
                "导出数据目录",
                m_ProjectConfig.Luban.GeneratedDataDirectory,
                value =>
                {
                    m_ProjectConfig.Luban.GeneratedDataDirectory = value;
                    SaveConfigs();
                });
            content.Add(CreatePathFieldRow(
                generatedDataField,
                CreateBrowseButton(
                    "generated-data-directory-browse-button",
                    "选择导出数据目录",
                    () => SelectDirectory(generatedDataField, "选择导出数据目录", value =>
                    {
                        m_ProjectConfig.Luban.GeneratedDataDirectory = value;
                        SaveConfigs();
                    }))));

            content.Add(CreateFieldRow(CreateTextField(
                "code-namespace-field",
                "代码命名空间",
                m_ProjectConfig.Luban.CodeNamespace,
                value =>
                {
                    m_ProjectConfig.Luban.CodeNamespace = value;
                    SaveConfigs();
                })));

            var lubanDllField = CreateTextField(
                "luban-dll-path-field",
                "Luban.dll 路径",
                m_UserConfig.LubanDllPath,
                value =>
                {
                    m_UserConfig.LubanDllPath = value;
                    SaveConfigs();
                });
            content.Add(CreatePathFieldRow(
                lubanDllField,
                CreateBrowseButton(
                    "luban-dll-browse-button",
                    "选择 Luban.dll",
                    () => SelectFile(lubanDllField, "选择 Luban.dll", "dll", value =>
                    {
                        m_UserConfig.LubanDllPath = value;
                        SaveConfigs();
                    }))));

            content.Add(CreateSectionHeader("媒体工具"));
            var ffmpegField = CreateTextField(
                "ffmpeg-path-field",
                "ffmpeg 路径",
                m_UserConfig.FfmpegPath,
                value =>
                {
                    m_UserConfig.FfmpegPath = value;
                    SaveConfigs();
                });
            content.Add(CreatePathFieldRow(
                ffmpegField,
                CreateBrowseButton(
                    "ffmpeg-path-browse-button",
                    "选择 ffmpeg",
                    () => SelectFile(ffmpegField, "选择 ffmpeg", ExecutableExtension(), value =>
                    {
                        m_UserConfig.FfmpegPath = value;
                        SaveConfigs();
                    }))));

            var ffprobeField = CreateTextField(
                "ffprobe-path-field",
                "ffprobe 路径",
                m_UserConfig.FfprobePath,
                value =>
                {
                    m_UserConfig.FfprobePath = value;
                    SaveConfigs();
                });
            content.Add(CreatePathFieldRow(
                ffprobeField,
                CreateBrowseButton(
                    "ffprobe-path-browse-button",
                    "选择 ffprobe",
                    () => SelectFile(ffprobeField, "选择 ffprobe", ExecutableExtension(), value =>
                    {
                        m_UserConfig.FfprobePath = value;
                        SaveConfigs();
                    }))));

            m_ErrorLabel = new Label { name = "global-config-validation" };
            m_ErrorLabel.style.whiteSpace = WhiteSpace.Normal;
            m_ErrorLabel.style.color = new Color(0.95f, 0.35f, 0.3f);
            m_ErrorLabel.style.marginLeft = 150;
            m_ErrorLabel.style.marginTop = 8;
            content.Add(m_ErrorLabel);
            RefreshValidationMessage(null);
        }

        private void SaveConfigs()
        {
            if (m_ProjectConfig.TryValidate(out var error) is false)
            {
                RefreshValidationMessage(error);
                return;
            }

            try
            {
                m_ProjectConfig.Save();
                m_UserConfig.Save();
                RefreshValidationMessage(null);
                m_OnSaved?.Invoke();
            }
            catch (Exception exception)
            {
                RefreshValidationMessage($"保存 Editor 配置失败：{exception.Message}");
                Debug.LogException(exception);
            }
        }

        private void RefreshValidationMessage(string error)
        {
            if (m_ErrorLabel == null)
            {
                return;
            }

            m_ErrorLabel.text = error ?? string.Empty;
            m_ErrorLabel.style.display = string.IsNullOrWhiteSpace(error) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static Label CreatePageTitle(string text)
        {
            var title = new Label(text);
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 18;
            return title;
        }

        private static Label CreateSectionHeader(string text)
        {
            var header = new Label(text);
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 6;
            header.style.marginBottom = 12;
            header.style.paddingBottom = 6;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = EditorGUIUtility.isProSkin
                ? new Color(0.3f, 0.31f, 0.33f)
                : new Color(0.72f, 0.74f, 0.77f);
            return header;
        }

        private static VisualElement CreateFieldRow<TValue>(BaseField<TValue> field)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0;
            row.style.marginBottom = 8;
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.marginBottom = 0;
            row.Add(field);
            return row;
        }

        private static VisualElement CreatePathFieldRow(TextField field, Button browseButton)
        {
            var row = CreateFieldRow(field);
            field.style.marginRight = 8;
            row.Add(browseButton);
            return row;
        }

        private static VisualElement CreateActionRow(string label, Button button)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0;
            row.style.marginBottom = 8;
            var caption = new Label(label);
            caption.style.width = 150;
            caption.style.minWidth = 150;
            caption.style.maxWidth = 150;
            row.Add(caption);
            button.style.height = 24;
            row.Add(button);
            return row;
        }

        private static Button CreateBrowseButton(string name, string tooltip, Action clicked)
        {
            var button = new Button(clicked) { name = name, tooltip = tooltip };
            button.style.width = 28;
            button.style.minWidth = 28;
            button.style.height = 22;
            var icon = new Image
            {
                image = EditorGUIUtility.IconContent("Folder Icon").image,
                scaleMode = ScaleMode.ScaleToFit
            };
            icon.style.width = 16;
            icon.style.height = 16;
            icon.pickingMode = PickingMode.Ignore;
            button.Add(icon);
            return button;
        }

        private static void SelectDirectory(TextField field, string title, Action<string> selected)
        {
            var path = EditorUtility.OpenFolderPanel(title, GetInitialDirectory(field.value, false), string.Empty);
            ApplySelectedPath(field, path, selected);
        }

        private static string ExecutableExtension()
        {
            return Application.platform == RuntimePlatform.WindowsEditor ? "exe" : string.Empty;
        }

        private static void SelectFile(
            TextField field,
            string title,
            string extension,
            Action<string> selected)
        {
            var path = EditorUtility.OpenFilePanel(title, GetInitialDirectory(field.value, true), extension);
            ApplySelectedPath(field, path, selected);
        }

        private static void ApplySelectedPath(TextField field, string path, Action<string> selected)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var storedPath = LubanCommandRunner.ToProjectRelativePath(path).Replace('\\', '/');
            field.SetValueWithoutNotify(storedPath);
            selected(storedPath);
        }

        private static string GetInitialDirectory(string configuredPath, bool file)
        {
            var absolutePath = LubanCommandRunner.GetAbsoluteProjectPath(configuredPath);
            var directory = file ? System.IO.Path.GetDirectoryName(absolutePath) : absolutePath;
            return System.IO.Directory.Exists(directory)
                ? directory
                : LubanCommandRunner.GetProjectRoot();
        }

        private static TextField CreateTextField(
            string name,
            string label,
            string value,
            Action<string> changed)
        {
            var field = new TextField(label)
            {
                name = name,
                value = value ?? string.Empty,
                isDelayed = true
            };
            ConfigureField(field);
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return field;
        }

        private static IntegerField CreateIntegerField(
            string name,
            string label,
            int value,
            Action<int> changed)
        {
            var field = new IntegerField(label)
            {
                name = name,
                value = value,
                isDelayed = true
            };
            ConfigureField(field);
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return field;
        }

        private static Toggle CreateToggleField(
            string name,
            string label,
            bool value,
            Action<bool> changed)
        {
            var field = new Toggle(label)
            {
                name = name,
                value = value
            };
            ConfigureField(field);
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return field;
        }

        private static DropdownField CreateChoiceField(
            string name,
            string label,
            string current,
            IEnumerable<string> values,
            Action<string> changed)
        {
            var choices = values
                .Where(value => string.IsNullOrWhiteSpace(value) is false)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            current = current?.Trim() ?? string.Empty;
            if (current.Length > 0 && choices.Contains(current) is false)
            {
                choices.Add(current);
            }

            choices.Insert(0, EmptyChoice);
            var selected = current.Length == 0 ? EmptyChoice : current;
            var field = new DropdownField(label, choices, choices.IndexOf(selected)) { name = name };
            ConfigureField(field);
            field.RegisterValueChangedCallback(evt => changed(evt.newValue == EmptyChoice ? string.Empty : evt.newValue));
            return field;
        }

        private static void ConfigureField<TValue>(BaseField<TValue> field)
        {
            field.style.flexGrow = 1;
            field.style.minWidth = 0;
            field.style.marginBottom = 5;
            field.labelElement.style.width = 150;
            field.labelElement.style.minWidth = 150;
            field.labelElement.style.maxWidth = 150;
        }
    }
}
