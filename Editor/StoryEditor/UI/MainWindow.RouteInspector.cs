using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Media;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.UI
{
    public sealed partial class MainWindow
    {
        private VisualElement m_RouteInspectorContent;

        private VisualElement CreateRouteInspectorPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("story-editor__pane");
            pane.AddToClassList("story-editor__route-inspector");

            var header = new Label("属性");
            header.AddToClassList("story-editor__route-inspector-title");
            pane.Add(header);

            var scroll = new ScrollView();
            scroll.AddToClassList("story-editor__route-inspector-scroll");
            m_RouteInspectorContent = new VisualElement();
            scroll.Add(m_RouteInspectorContent);
            pane.Add(scroll);
            return pane;
        }

        private void RefreshRouteInspector()
        {
            if (m_RouteInspectorContent == null)
            {
                return;
            }

            m_RouteInspectorContent.Clear();
            var episode = SelectedRouteEpisode();
            if (episode != null && string.IsNullOrWhiteSpace(m_SelectedRouteEdgeId))
            {
                BuildEpisodeInspector(episode);
                return;
            }

            BuildRouteLayoutInspector();
            if (BuildRouteEdgeInspector())
            {
                return;
            }

            if (episode == null)
            {
                BuildVolumeInspector();
                return;
            }

            BuildEpisodeInspector(episode);
        }

        private void BuildVolumeInspector()
        {
            AddInspectorValue("类型", "卷");
            AddInspectorValue("ID", m_SelectedVolume?.VolumeId, copyable: true);
            if (m_SelectedVolume == null)
            {
                return;
            }

            var title = CreateTextField("标题", m_SelectedVolume.Title, false);
            title.RegisterValueChangedCallback(evt => UpdateVolumeMetadata(
                evt.newValue,
                m_SelectedVolume.Description,
                m_SelectedVolume.PreviewImage,
                m_SelectedVolume.HomeVideoReference));
            m_RouteInspectorContent.Add(title);

            var description = CreateTextField("介绍", m_SelectedVolume.Description, true);
            description.RegisterValueChangedCallback(evt => UpdateVolumeMetadata(
                m_SelectedVolume.Title,
                evt.newValue,
                m_SelectedVolume.PreviewImage,
                m_SelectedVolume.HomeVideoReference));
            m_RouteInspectorContent.Add(description);

            m_RouteInspectorContent.Add(CreateTexturePreviewField(
                "预览图",
                "preview-image",
                m_SelectedVolume.PreviewImage,
                value => UpdateVolumeMetadata(
                    m_SelectedVolume.Title,
                    m_SelectedVolume.Description,
                    value,
                    m_SelectedVolume.HomeVideoReference)));

            BuildHomeVideoField();
        }

        private void BuildEpisodeInspector(AuthoringEpisode episode)
        {
            AddInspectorValue("类型", "剧情段");
            AddInspectorValue("ID", episode.EpisodeId);

            var title = CreateTextField("标题", episode.Title, false);
            title.RegisterValueChangedCallback(evt => UpdateEpisodeMetadata(
                episode,
                evt.newValue,
                episode.Description,
                episode.PreviewImage));
            m_RouteInspectorContent.Add(title);

            var description = CreateTextField("介绍", episode.Description, true);
            description.RegisterValueChangedCallback(evt => UpdateEpisodeMetadata(
                episode,
                episode.Title,
                evt.newValue,
                episode.PreviewImage));
            m_RouteInspectorContent.Add(description);

            m_RouteInspectorContent.Add(CreateTexturePreviewField(
                "预览图",
                "preview-image",
                episode.PreviewImage,
                value => UpdateEpisodeMetadata(
                    episode,
                    episode.Title,
                    episode.Description,
                    value)));
        }

        private void BuildHomeVideoField()
        {
            var currentValue = m_SelectedVolume.HomeVideoReference;
            var field = new VisualElement();
            field.AddToClassList("story-editor__media-field");
            var label = new Label("主页视频");
            label.AddToClassList("story-editor__route-inspector-label");
            field.Add(label);

            var card = CreateMediaCard(
                "home-video",
                EditorGUIUtility.IconContent("VideoClip Icon").image,
                string.IsNullOrWhiteSpace(currentValue)
                    ? "未设置"
                    : SafeText(m_SelectedVolume.Title, "主页视频"),
                HomeVideoSummary(currentValue),
                true,
                out var preview,
                out var caption);
            var picker = new Button(() => VideoPickerWindow.Open(
                m_SelectedVolume.HomeVideoReference,
                value => UpdateVolumeMetadata(
                    m_SelectedVolume.Title,
                    m_SelectedVolume.Description,
                    m_SelectedVolume.PreviewImage,
                    value),
                VideoFormat.Hls))
            {
                name = "story-editor-media-picker-home-video",
                tooltip = "从 HLS 流媒体库选择主页视频"
            };
            picker.AddToClassList("story-editor__media-picker");
            card.Add(picker);

            var clear = CreateMediaClearButton("清除主页视频配置", () => UpdateVolumeMetadata(
                m_SelectedVolume.Title,
                m_SelectedVolume.Description,
                m_SelectedVolume.PreviewImage,
                string.Empty));
            clear.style.display = string.IsNullOrWhiteSpace(currentValue)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            card.Add(clear);
            field.Add(card);
            m_RouteInspectorContent.Add(field);

            if (string.IsNullOrWhiteSpace(currentValue) is false)
            {
                BindHomeVideoThumbnail(card, preview, caption, currentValue);
            }
        }

        private static string HomeVideoSummary(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "未设置（使用默认主页视频）";
            }

            return VideoReferenceCodec.TryDeserialize(value, out var reference, out _)
                ? reference.Primary.Value
                : "配置无效，点击重新选择";
        }

        private void UpdateVolumeMetadata(
            string title,
            string description,
            Texture2D previewImage,
            string homeVideoReference)
        {
            if (m_SelectedVolume == null)
            {
                return;
            }

            var result = new RouteMutation(m_Asset).UpdateVolume(
                m_SelectedVolume.VolumeId,
                new VolumeMetadata(title, description, previewImage, homeVideoReference));
            RefreshMetadataResult(result);
        }

        private void UpdateEpisodeMetadata(
            AuthoringEpisode episode,
            string title,
            string description,
            Texture2D previewImage)
        {
            var volume = FindVolume(episode);
            if (volume == null)
            {
                RefreshAll("剧情段已不存在。");
                return;
            }

            var result = new RouteMutation(m_Asset).UpdateEpisode(
                volume.VolumeId,
                episode.EpisodeId,
                new EpisodeMetadata(title, description, previewImage));
            RefreshMetadataResult(result);
        }

        private void RefreshMetadataResult(RouteMutationResult result)
        {
            if (result.Succeeded)
            {
                RefreshAll(result.Message);
                return;
            }

            RefreshReport(result.Message);
        }

        private void AddInspectorValue(string label, string value, bool copyable = false)
        {
            var labelElement = new Label(label);
            labelElement.AddToClassList("story-editor__route-inspector-label");
            m_RouteInspectorContent.Add(labelElement);

            var valueElement = new Label(string.IsNullOrWhiteSpace(value) ? "未设置" : value);
            valueElement.AddToClassList("story-editor__route-inspector-value");
            if (copyable && !string.IsNullOrWhiteSpace(value))
            {
                // 可点击复制（如卷 ID，策划配置表时取用）。
                valueElement.tooltip = "点击复制";
                valueElement.AddToClassList("story-editor__route-inspector-value--copyable");
                valueElement.RegisterCallback<ClickEvent>(_ =>
                {
                    GUIUtility.systemCopyBuffer = value;
                    var originalText = valueElement.text;
                    valueElement.text = "已复制";
                    EditorApplication.delayCall += () =>
                    {
                        if (valueElement != null)
                        {
                            valueElement.text = originalText;
                        }
                    };
                });
            }

            m_RouteInspectorContent.Add(valueElement);
        }

        private static TextField CreateTextField(string label, string value, bool multiline)
        {
            var field = new TextField(label)
            {
                isDelayed = true,
                multiline = multiline
            };
            field.SetValueWithoutNotify(value ?? string.Empty);
            field.AddToClassList("story-editor__route-inspector-field");
            return field;
        }

        private static VisualElement CreateTexturePreviewField(
            string label,
            string fieldName,
            Texture2D image,
            Action<Texture2D> changed)
        {
            var field = new VisualElement();
            field.AddToClassList("story-editor__media-field");
            var fieldLabel = new Label(label);
            fieldLabel.AddToClassList("story-editor__route-inspector-label");
            field.Add(fieldLabel);

            var assetPath = image == null ? string.Empty : AssetDatabase.GetAssetPath(image);
            var card = CreateMediaCard(
                fieldName,
                image ?? EditorGUIUtility.IconContent("Image Icon").image,
                image == null ? "点击选择图片" : image.name,
                string.IsNullOrWhiteSpace(assetPath) ? label : assetPath,
                image == null,
                out _,
                out _);
            var objectField = new ObjectField
            {
                name = $"story-editor-media-object-{fieldName}",
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
                tooltip = $"点击选择或拖入{label}"
            };
            objectField.SetValueWithoutNotify(image);
            objectField.AddToClassList("story-editor__media-object-field");
            objectField.RegisterValueChangedCallback(evt => changed?.Invoke(evt.newValue as Texture2D));
            card.Add(objectField);

            var clear = CreateMediaClearButton($"清除{label}", () => changed?.Invoke(null));
            clear.style.display = image == null ? DisplayStyle.None : DisplayStyle.Flex;
            card.Add(clear);
            field.Add(card);
            return field;
        }

        private static VisualElement CreateMediaCard(
            string fieldName,
            Texture image,
            string captionText,
            string tooltip,
            bool placeholder,
            out Image preview,
            out Label caption)
        {
            var card = new VisualElement
            {
                name = $"story-editor-media-card-{fieldName}",
                tooltip = tooltip ?? string.Empty
            };
            card.AddToClassList("story-editor__media-card");

            preview = new Image
            {
                name = $"story-editor-media-preview-{fieldName}",
                image = image,
                scaleMode = placeholder ? ScaleMode.ScaleToFit : ScaleMode.ScaleAndCrop,
                pickingMode = PickingMode.Ignore
            };
            preview.AddToClassList("story-editor__media-card-image");
            preview.EnableInClassList("story-editor__media-card-image--placeholder", placeholder);
            card.Add(preview);

            caption = new Label(captionText ?? string.Empty)
            {
                name = $"story-editor-media-caption-{fieldName}",
                pickingMode = PickingMode.Ignore,
                tooltip = tooltip ?? string.Empty
            };
            caption.AddToClassList("story-editor__media-card-caption");
            card.Add(caption);
            return card;
        }

        private static Button CreateMediaClearButton(string tooltip, Action clicked)
        {
            var button = new Button(clicked)
            {
                tooltip = tooltip
            };
            button.AddToClassList("story-editor__media-card-clear");
            var icon = new Image
            {
                image = EditorGUIUtility.IconContent("TreeEditor.Trash").image,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("story-editor__media-card-clear-icon");
            button.Add(icon);
            return button;
        }

        private static void BindHomeVideoThumbnail(
            VisualElement card,
            Image preview,
            Label caption,
            string serializedReference)
        {
            CancellationTokenSource cancellation = null;
            Texture2D loadedTexture = null;
            void StartLoading()
            {
                if (cancellation != null)
                {
                    return;
                }

                cancellation = new CancellationTokenSource();
                LoadHomeVideoThumbnailAsync(
                        card,
                        preview,
                        caption,
                        serializedReference,
                        cancellation.Token,
                        texture => loadedTexture = texture)
                    .Forget(exception =>
                    {
                        if (exception is not OperationCanceledException)
                        {
                            Debug.LogWarning($"主页视频缩略图加载失败：{exception.Message}");
                        }
                    });
            }

            card.RegisterCallback<AttachToPanelEvent>(_ => StartLoading());
            card.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                cancellation?.Cancel();
                cancellation?.Dispose();
                cancellation = null;
                if (loadedTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(loadedTexture);
                    loadedTexture = null;
                }
            });

            if (card.panel != null)
            {
                StartLoading();
            }
        }

        private static async UniTask LoadHomeVideoThumbnailAsync(
            VisualElement card,
            Image preview,
            Label caption,
            string serializedReference,
            CancellationToken cancellationToken,
            Action<Texture2D> retainTexture)
        {
            var result = await VideoPickerWindow.LoadReferenceThumbnailAsync(
                serializedReference,
                cancellationToken);
            if (result?.Texture == null)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested || card.panel == null)
            {
                UnityEngine.Object.DestroyImmediate(result.Texture);
                return;
            }

            retainTexture(result.Texture);
            preview.image = result.Texture;
            preview.scaleMode = ScaleMode.ScaleAndCrop;
            preview.RemoveFromClassList("story-editor__media-card-image--placeholder");
            card.tooltip = $"{result.DisplayName}\n{HomeVideoSummary(serializedReference)}";
            caption.tooltip = card.tooltip;
        }
    }
}
