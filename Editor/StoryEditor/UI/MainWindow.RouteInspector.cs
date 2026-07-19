using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.Story.Model;
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
            BuildRouteLayoutInspector();
            if (BuildRouteEdgeInspector())
            {
                return;
            }

            var episode = SelectedRouteEpisode();
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
            AddInspectorValue("ID", m_SelectedVolume?.VolumeId);
            if (m_SelectedVolume == null)
            {
                return;
            }

            var title = CreateTextField("标题", m_SelectedVolume.Title, false);
            title.RegisterValueChangedCallback(evt => UpdateVolumeMetadata(
                evt.newValue,
                m_SelectedVolume.Description,
                m_SelectedVolume.PreviewImage));
            m_RouteInspectorContent.Add(title);

            var description = CreateTextField("介绍", m_SelectedVolume.Description, true);
            description.RegisterValueChangedCallback(evt => UpdateVolumeMetadata(
                m_SelectedVolume.Title,
                evt.newValue,
                m_SelectedVolume.PreviewImage));
            m_RouteInspectorContent.Add(description);

            var preview = CreatePreviewField(m_SelectedVolume.PreviewImage);
            preview.RegisterValueChangedCallback(evt => UpdateVolumeMetadata(
                m_SelectedVolume.Title,
                m_SelectedVolume.Description,
                evt.newValue as Texture2D));
            m_RouteInspectorContent.Add(preview);
        }

        private void BuildEpisodeInspector(AuthoringChapter episode)
        {
            AddInspectorValue("类型", "剧情段");
            AddInspectorValue("ID", episode.ChapterId);

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

            var preview = CreatePreviewField(episode.PreviewImage);
            preview.RegisterValueChangedCallback(evt => UpdateEpisodeMetadata(
                episode,
                episode.Title,
                episode.Description,
                evt.newValue as Texture2D));
            m_RouteInspectorContent.Add(preview);
        }

        private void UpdateVolumeMetadata(string title, string description, Texture2D previewImage)
        {
            if (m_SelectedVolume == null)
            {
                return;
            }

            var result = new RouteMutation(m_Asset).UpdateVolume(
                m_SelectedVolume.VolumeId,
                new VolumeMetadata(title, description, previewImage));
            RefreshMetadataResult(result);
        }

        private void UpdateEpisodeMetadata(
            AuthoringChapter episode,
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
                episode.ChapterId,
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

        private void AddInspectorValue(string label, string value)
        {
            var labelElement = new Label(label);
            labelElement.AddToClassList("story-editor__route-inspector-label");
            m_RouteInspectorContent.Add(labelElement);

            var valueElement = new Label(string.IsNullOrWhiteSpace(value) ? "未设置" : value);
            valueElement.AddToClassList("story-editor__route-inspector-value");
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

        private static ObjectField CreatePreviewField(Texture2D previewImage)
        {
            var field = new ObjectField("预览图")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false
            };
            field.SetValueWithoutNotify(previewImage);
            field.AddToClassList("story-editor__route-inspector-field");
            return field;
        }

    }
}
