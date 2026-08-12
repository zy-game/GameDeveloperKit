using System.IO;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.UI
{
    public sealed partial class MainWindow
    {
        private ScrollView m_OverviewVolumeList;
        private Label m_OverviewStatus;
        private TextField m_StoryIdField;
        private TextField m_VersionField;
        private Label m_RuntimeOutputLabel;

        private VisualElement CreateOverviewPage()
        {
            var page = new VisualElement();
            page.AddToClassList("story-editor__workspace");

            var header = new VisualElement();
            header.AddToClassList("story-editor__pane-header");
            header.Add(new Label("剧情总览"));
            page.Add(header);

            m_StoryIdField = new TextField("Story ID") { isDelayed = true };
            m_StoryIdField.RegisterValueChangedCallback(evt => UpdateProjectMetadata(evt.newValue, null));
            page.Add(m_StoryIdField);
            m_VersionField = new TextField("Version") { isDelayed = true };
            m_VersionField.RegisterValueChangedCallback(evt => UpdateProjectMetadata(null, evt.newValue));
            page.Add(m_VersionField);
            m_RuntimeOutputLabel = new Label();
            page.Add(m_RuntimeOutputLabel);

            m_OverviewStatus = new Label();
            page.Add(m_OverviewStatus);
            m_OverviewVolumeList = new ScrollView(ScrollViewMode.Vertical);
            m_OverviewVolumeList.AddToClassList("story-editor__tree-scroll");
            m_OverviewVolumeList.AddToClassList("story-editor__overview-volume-grid");
            m_OverviewVolumeList.contentContainer.style.flexDirection = FlexDirection.Row;
            m_OverviewVolumeList.contentContainer.style.flexWrap = Wrap.Wrap;
            m_OverviewVolumeList.contentContainer.style.alignContent = Align.FlexStart;
            page.Add(m_OverviewVolumeList);
            return page;
        }

        private void RefreshPageVisibility()
        {
            var overview = m_EditorMode == EditorMode.Overview;
            if (m_OverviewPage != null)
            {
                m_OverviewPage.style.display = overview ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (m_WorkspacePage != null)
            {
                m_WorkspacePage.style.display = overview ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (m_OverviewActions != null)
            {
                m_OverviewActions.style.display = overview ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (m_VolumeActions != null)
            {
                m_VolumeActions.style.display = overview ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void RefreshOverview(string status = null)
        {
            if (m_OverviewVolumeList == null || m_Asset == null)
            {
                return;
            }

            m_OverviewStatus.text = status ?? $"{SafeText(m_Asset.StoryId, "story")}  {SafeText(m_Asset.Version, "version")}";
            m_StoryIdField?.SetValueWithoutNotify(m_Asset.StoryId);
            m_VersionField?.SetValueWithoutNotify(m_Asset.Version);
            if (m_RuntimeOutputLabel != null)
            {
                m_RuntimeOutputLabel.text = $"Runtime: {SafeText(m_Asset.RuntimeProgramAssetPath, "未发布")}";
            }
            m_OverviewVolumeList.Clear();
            for (var i = 0; i < m_Asset.VolumeAssets.Count; i++)
            {
                m_OverviewVolumeList.Add(CreateOverviewVolumeCard(m_Asset.VolumeAssets[i], i));
            }

            m_OverviewVolumeList.Add(CreateAddVolumeCard());
        }

        private VisualElement CreateOverviewVolumeCard(AuthoringVolumeAsset volumeAsset, int index)
        {
            var title = volumeAsset == null
                ? $"第{index + 1}卷（引用缺失）"
                : SafeText(volumeAsset.Volume.Title, volumeAsset.Volume.VolumeId);
            var volumeId = volumeAsset == null
                ? "引用缺失"
                : SafeText(volumeAsset.Volume.VolumeId, "未填写");
            var path = volumeAsset == null ? string.Empty : AssetDatabase.GetAssetPath(volumeAsset);
            var validation = "引用缺失";
            var hasErrors = volumeAsset == null;
            if (volumeAsset != null)
            {
                ProgramCompiler.CompileVolume(m_Asset, volumeAsset, out var report);
                hasErrors = report.HasErrors;
                validation = report.HasErrors ? $"错误：{report.Issues[0].Message}" : "校验通过";
            }

            var card = new VisualElement
            {
                name = $"story-editor-overview-volume-{index}",
                tooltip = $"{title}\nVolumeId: {volumeId}\nAsset: {SafeText(path, "未保存")}"
            };
            card.AddToClassList("story-editor__overview-volume-card");

            var openButton = new Button(() =>
            {
                if (volumeAsset != null)
                {
                    OpenVolume(volumeAsset);
                }
            })
            {
                tooltip = card.tooltip
            };
            openButton.AddToClassList("story-editor__overview-volume-open");

            var preview = new VisualElement();
            preview.AddToClassList("story-editor__overview-volume-preview");
            var firstEpisodePreview = volumeAsset != null && volumeAsset.Volume.Episodes.Count > 0
                ? volumeAsset.Volume.Episodes[0]?.PreviewImage
                : null;
            var icon = firstEpisodePreview != null
                ? firstEpisodePreview
                : volumeAsset == null
                    ? EditorGUIUtility.IconContent("console.erroricon").image
                    : AssetPreview.GetMiniThumbnail(volumeAsset);
            icon ??= EditorGUIUtility.IconContent("ScriptableObject Icon").image;
            var image = new Image
            {
                image = icon,
                scaleMode = ScaleMode.ScaleAndCrop,
                pickingMode = PickingMode.Ignore
            };
            image.AddToClassList("story-editor__overview-volume-icon");
            preview.Add(image);
            var indexLabel = new Label($"第 {index + 1} 卷");
            indexLabel.AddToClassList("story-editor__overview-volume-index");
            preview.Add(indexLabel);
            openButton.Add(preview);

            var details = new VisualElement();
            details.AddToClassList("story-editor__overview-volume-details");
            var titleLabel = new Label(title) { tooltip = title };
            titleLabel.AddToClassList("story-editor__overview-volume-title");
            details.Add(titleLabel);
            var metadata = volumeAsset == null
                ? volumeId
                : $"{volumeId} · {volumeAsset.Volume.Episodes.Count} 章";
            // 卷 ID 可点击复制：点击复制 VolumeId 到剪贴板（策划配置表时方便取 ID）。
            var metadataLabel = new Label(metadata) { tooltip = $"点击复制卷 ID\n{volumeId}" };
            metadataLabel.AddToClassList("story-editor__overview-volume-metadata");
            if (volumeAsset != null && !string.IsNullOrWhiteSpace(volumeId))
            {
                metadataLabel.RegisterCallback<ClickEvent>(_ => CopyVolumeIdToClipboard(volumeId, metadataLabel));
            }

            details.Add(metadataLabel);
            var pathLabel = new Label(SafeText(path, "未保存")) { tooltip = SafeText(path, "未保存") };
            pathLabel.AddToClassList("story-editor__overview-volume-path");
            details.Add(pathLabel);
            var validationLabel = new Label(validation) { tooltip = validation };
            validationLabel.AddToClassList(hasErrors
                ? "story-editor__overview-volume-validation--error"
                : "story-editor__overview-volume-validation--success");
            details.Add(validationLabel);
            openButton.Add(details);
            card.Add(openButton);

            var deleteButton = new Button(() => ConfirmRemoveVolumeReference(volumeAsset))
            {
                name = $"story-editor-overview-volume-delete-{index}",
                tooltip = "移除卷引用"
            };
            deleteButton.AddToClassList("story-editor__overview-volume-delete");
            deleteButton.SetEnabled(volumeAsset != null);
            var deleteIcon = new Image
            {
                image = EditorGUIUtility.IconContent("TreeEditor.Trash").image,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            deleteIcon.AddToClassList("story-editor__overview-volume-delete-icon");
            deleteButton.Add(deleteIcon);
            card.Add(deleteButton);
            return card;
        }

        /// <summary>
        /// 复制卷 ID 到剪贴板并给出短暂视觉反馈。
        /// </summary>
        private static void CopyVolumeIdToClipboard(string volumeId, Label target)
        {
            GUIUtility.systemCopyBuffer = volumeId ?? string.Empty;
            var originalText = target.text;
            target.text = "已复制卷 ID";
            EditorApplication.delayCall += () =>
            {
                if (target != null)
                {
                    target.text = originalText;
                }
            };
        }

        private Button CreateAddVolumeCard()
        {
            var card = new Button(CreateVolumeFromOverview);
            card.name = "story-editor-overview-add-volume";
            card.tooltip = "新增卷";
            card.AddToClassList("story-editor__overview-volume-add");
            var icon = new Label("+") { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("story-editor__overview-volume-add-icon");
            card.Add(icon);
            return card;
        }

        private void ReturnToOverview()
        {
            SelectDefaultRoute();
            ClearDetailSelection();
            m_SelectionKind = SelectionKind.Story;
            RefreshAll();
        }

        private void UpdateProjectMetadata(string storyId, string version)
        {
            AuthoringUndo.Mutate(m_Asset, "Update Story Project", () =>
            {
                if (storyId != null)
                {
                    m_Asset.StoryId = storyId.Trim();
                }

                if (version != null)
                {
                    m_Asset.Version = version.Trim();
                }
            });
            RefreshOverview();
        }

        private void ValidateProject()
        {
            ProgramCompiler.Compile(m_Asset, out var report);
            m_OverviewStatus.text = report.HasErrors
                ? report.Issues[0].Message
                : $"校验通过：{m_Asset.VolumeAssets.Count} 卷。";
        }

        private void OpenVolume(AuthoringVolumeAsset volumeAsset)
        {
            if (IsReferencedVolumeAsset(volumeAsset) is false)
            {
                RefreshOverview("无法打开：卷资产不属于当前剧情工程。");
                return;
            }

            m_SelectedVolumeAsset = volumeAsset;
            m_SelectedVolume = volumeAsset.Volume;
            m_SelectedEpisode = m_SelectedVolume.Episodes.Count == 0 ? null : m_SelectedVolume.Episodes[0];
            m_EditorMode = EditorMode.Route;
            m_SelectedRouteNodeId = RouteGraphAdapter.GetVirtualRootNodeId(m_SelectedVolume.VolumeId);
            ClearDetailSelection();
            RefreshAll();
        }

        private void CreateVolumeFromOverview()
        {
            var projectPath = AssetDatabase.GetAssetPath(m_Asset);
            var folder = $"{Path.GetDirectoryName(projectPath)?.Replace('\\', '/')}/{Path.GetFileNameWithoutExtension(projectPath)}.Volumes";
            if (AssetDatabase.IsValidFolder(folder) is false)
            {
                var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                if (AssetDatabase.IsValidFolder(parent) is false)
                {
                    RefreshOverview("无法创建卷：剧情工程路径无效。");
                    return;
                }

                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Volume{m_Asset.VolumeAssets.Count + 1:00}.asset");
            var volume = AuthoringVolumeAsset.CreateDefault(IdentityId.New(), $"第{m_Asset.VolumeAssets.Count + 1}卷");
            AssetDatabase.CreateAsset(volume, path);
            if (new AuthoringProjectMutation(m_Asset).TryAdd(volume, out var error) is false)
            {
                AssetDatabase.DeleteAsset(path);
                RefreshOverview(error);
                return;
            }

            AssetDatabase.SaveAssets();
            RefreshAll("已创建并引用新卷。");
        }

        private void RemoveVolumeReference(AuthoringVolumeAsset volume)
        {
            if (new AuthoringProjectMutation(m_Asset).TryRemove(volume, out var error) is false)
            {
                RefreshOverview(error);
                return;
            }

            RefreshAll("已解除卷引用，卷资产文件未删除。");
        }

        private void ConfirmRemoveVolumeReference(AuthoringVolumeAsset volume)
        {
            if (volume == null)
            {
                return;
            }

            var title = SafeText(volume.Volume.Title, volume.Volume.VolumeId);
            if (EditorUtility.DisplayDialog(
                    "移除卷引用",
                    $"确定从当前剧情工程中移除“{title}”吗？\n\n卷资产文件不会被删除。",
                    "移除",
                    "取消"))
            {
                RemoveVolumeReference(volume);
            }
        }
    }
}
