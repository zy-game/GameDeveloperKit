using System;
using System.IO;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Migration;
using GameDeveloperKit.StoryEditor.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.UI
{
    public sealed partial class MainWindow
    {
        private VisualElement m_OverviewVolumeList;
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

            var commands = new VisualElement();
            commands.AddToClassList("story-editor__toolbar-actions");
            commands.Add(CreateButton("新增卷", "创建并引用一个独立卷资产。", CreateVolumeFromOverview));
            commands.Add(CreateButton("引用已有卷", "将孤立卷资产加入当前剧情工程。", AddExistingVolumeFromOverview));
            commands.Add(CreateButton("拆分旧资产", "将内嵌卷迁移为独立卷资产。", MigrateEmbeddedVolumes));
            commands.Add(CreateButton("校验", "校验剧情工程全部卷。", ValidateProject));
            page.Add(commands);

            m_OverviewStatus = new Label();
            page.Add(m_OverviewStatus);
            m_OverviewVolumeList = new VisualElement();
            m_OverviewVolumeList.AddToClassList("story-editor__tree-scroll");
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
                var index = i;
                var volumeAsset = m_Asset.VolumeAssets[i];
                var row = new VisualElement();
                row.AddToClassList("story-editor__tree-row");
                row.AddToClassList("story-editor__overview-volume-row");
                var title = volumeAsset == null
                    ? $"第{i + 1}卷（引用缺失）"
                    : SafeText(volumeAsset.Volume.Title, volumeAsset.Volume.VolumeId);
                var summary = new VisualElement();
                summary.AddToClassList("story-editor__overview-volume-summary");
                summary.Add(new Label($"{i + 1}. {title}"));
                if (volumeAsset != null)
                {
                    var path = AssetDatabase.GetAssetPath(volumeAsset);
                    ProgramCompiler.CompileVolume(m_Asset, volumeAsset, out var report);
                    var validation = report.HasErrors ? $"错误：{report.Issues[0].Message}" : "校验通过";
                    summary.Add(new Label($"VolumeId: {SafeText(volumeAsset.Volume.VolumeId, "未填写")}"));
                    summary.Add(new Label($"Asset: {SafeText(path, "未保存")}"));
                    summary.Add(new Label(validation));
                }

                row.Add(summary);
                row.Add(CreateButton("打开", "打开此卷路线。", () => OpenVolume(volumeAsset)));
                row.Add(CreateButton("上移", "向前调整运行时顺序。", () => MoveVolume(volumeAsset, index - 1)));
                row.Add(CreateButton("下移", "向后调整运行时顺序。", () => MoveVolume(volumeAsset, index + 1)));
                row.Add(CreateButton("移除", "解除引用但不删除卷文件。", () => RemoveVolumeReference(volumeAsset)));
                m_OverviewVolumeList.Add(row);
            }
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

        private void AddExistingVolumeFromOverview()
        {
            var path = EditorUtility.OpenFilePanel("引用已有卷", Application.dataPath, "asset");
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase) is false)
            {
                return;
            }

            var assetPath = "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/');
            var volume = AssetDatabase.LoadAssetAtPath<AuthoringVolumeAsset>(assetPath);
            if (new AuthoringProjectMutation(m_Asset).TryAdd(volume, out var error) is false)
            {
                RefreshOverview(error);
                return;
            }

            RefreshAll("已引用卷资产。");
        }

        private void MoveVolume(AuthoringVolumeAsset volume, int index)
        {
            if (new AuthoringProjectMutation(m_Asset).TryMove(volume, index, out var error) is false)
            {
                RefreshOverview(error);
                return;
            }

            RefreshAll("已调整卷顺序。");
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

        private void MigrateEmbeddedVolumes()
        {
            var result = AssetSplitMigrationService.Apply(m_Asset);
            var status = result.HasErrors ? result.Errors[0] : result.IsNoOp ? "无需迁移。" : "卷资产拆分完成。";
            SelectDefaults();
            RefreshAll(status);
        }
    }
}
