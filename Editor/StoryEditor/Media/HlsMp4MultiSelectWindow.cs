using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class HlsMp4MultiSelectWindow : EditorWindow
    {
        private readonly List<string> m_AllFiles = new List<string>();
        private readonly List<string> m_FilteredFiles = new List<string>();
        private readonly HashSet<string> m_SelectedFiles =
            new HashSet<string>(PathComparer());
        private Action<IReadOnlyList<string>> m_Completed;
        private TextField m_DirectoryField;
        private TextField m_SearchField;
        private ListView m_List;
        private Label m_Status;
        private Button m_ConfirmButton;

        public static void Open(
            string initialDirectory,
            Action<IReadOnlyList<string>> completed)
        {
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            var window = CreateInstance<HlsMp4MultiSelectWindow>();
            window.titleContent = new GUIContent("选择多个 MP4");
            window.minSize = new Vector2(620f, 440f);
            window.m_Completed = completed;
            window.BuildUi();
            window.LoadDirectory(ResolveInitialDirectory(initialDirectory));
            window.ShowUtility();
            window.Focus();
        }

        private void OnDisable()
        {
            m_Completed = null;
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12f;
            rootVisualElement.style.paddingRight = 12f;
            rootVisualElement.style.paddingTop = 12f;
            rootVisualElement.style.paddingBottom = 12f;

            var directoryRow = CreateRow();
            directoryRow.Add(CreateFieldLabel("目录", "hls-mp4-directory-label"));
            m_DirectoryField = new TextField
            {
                name = "hls-mp4-directory",
                isReadOnly = true
            };
            m_DirectoryField.style.flexGrow = 1f;
            m_DirectoryField.style.flexShrink = 1f;
            m_DirectoryField.style.minWidth = 0f;
            directoryRow.Add(m_DirectoryField);
            var browseButton = new Button(BrowseDirectory)
            {
                name = "hls-mp4-browse",
                text = "选择文件夹"
            };
            browseButton.style.width = 88f;
            browseButton.style.flexShrink = 0f;
            browseButton.style.marginLeft = 6f;
            directoryRow.Add(browseButton);
            rootVisualElement.Add(directoryRow);

            var searchRow = CreateRow();
            searchRow.Add(CreateFieldLabel("搜索", "hls-mp4-search-label"));
            m_SearchField = new TextField { name = "hls-mp4-search" };
            m_SearchField.style.flexGrow = 1f;
            m_SearchField.style.flexShrink = 1f;
            m_SearchField.style.minWidth = 0f;
            m_SearchField.RegisterValueChangedCallback(_ => ApplyFilter());
            searchRow.Add(m_SearchField);
            rootVisualElement.Add(searchRow);

            var selectionRow = CreateRow();
            selectionRow.Add(new Button(SelectAll) { text = "全选" });
            selectionRow.Add(new Button(ClearSelection) { text = "清除选择" });
            rootVisualElement.Add(selectionRow);

            m_List = new ListView
            {
                name = "hls-mp4-multi-select-list",
                itemsSource = m_FilteredFiles,
                selectionType = SelectionType.None,
                fixedItemHeight = 24f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = () =>
                {
                    var toggle = new Toggle();
                    toggle.style.flexGrow = 1f;
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (toggle.userData is string path)
                        {
                            SetSelected(path, evt.newValue);
                        }
                    });
                    return toggle;
                },
                bindItem = (element, index) =>
                {
                    var path = m_FilteredFiles[index];
                    var toggle = (Toggle)element;
                    toggle.userData = path;
                    toggle.text = Path.GetFileName(path);
                    toggle.tooltip = path;
                    toggle.SetValueWithoutNotify(m_SelectedFiles.Contains(path));
                },
                unbindItem = (element, _) => element.userData = null
            };
            m_List.style.flexGrow = 1f;
            rootVisualElement.Add(m_List);

            m_Status = new Label("可拖入来自不同目录的多个 MP4。")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginTop = 6f }
            };
            rootVisualElement.Add(m_Status);

            var actions = CreateRow();
            actions.style.justifyContent = Justify.FlexEnd;
            actions.Add(new Button(Close) { text = "取消" });
            m_ConfirmButton = new Button(Confirm) { text = "添加所选文件" };
            m_ConfirmButton.SetEnabled(false);
            actions.Add(m_ConfirmButton);
            rootVisualElement.Add(actions);

            rootVisualElement.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            rootVisualElement.RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        private void BrowseDirectory()
        {
            var selected = EditorUtility.OpenFolderPanel(
                "选择包含 MP4 的目录",
                m_DirectoryField.value,
                string.Empty);
            if (string.IsNullOrWhiteSpace(selected) is false)
            {
                LoadDirectory(selected);
            }
        }

        private void LoadDirectory(string directory)
        {
            m_AllFiles.Clear();
            m_SelectedFiles.Clear();
            try
            {
                var fullPath = Path.GetFullPath(directory);
                m_DirectoryField.value = fullPath.Replace('\\', '/');
                m_AllFiles.AddRange(Directory
                    .EnumerateFiles(fullPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(IsMp4)
                    .Select(path => Path.GetFullPath(path).Replace('\\', '/'))
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase));
                ApplyFilter();
                m_Status.text = $"找到 {m_AllFiles.Count} 个 MP4。";
            }
            catch (Exception exception)
            {
                m_Status.text = "无法读取目录：" + exception.Message;
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            var query = (m_SearchField?.value ?? string.Empty).Trim();
            m_FilteredFiles.Clear();
            m_FilteredFiles.AddRange(m_AllFiles.Where(path =>
                query.Length == 0 ||
                Path.GetFileName(path).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
            m_List?.Rebuild();
            UpdateSelectionStatus();
        }

        private void SelectAll()
        {
            foreach (var path in m_FilteredFiles)
            {
                m_SelectedFiles.Add(path);
            }

            m_List.Rebuild();
            UpdateSelectionStatus();
        }

        private void ClearSelection()
        {
            m_SelectedFiles.Clear();
            m_List.Rebuild();
            UpdateSelectionStatus();
        }

        private void Confirm()
        {
            var selected = m_AllFiles
                .Where(path => m_SelectedFiles.Contains(path))
                .ToArray();
            if (selected.Length == 0)
            {
                return;
            }

            var completed = m_Completed;
            m_Completed = null;
            completed?.Invoke(selected);
            Close();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.paths.Any(IsMp4))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            }
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var paths = new List<string>();
            var rejectedCount = 0;
            foreach (var sourcePath in DragAndDrop.paths)
            {
                if (IsMp4(sourcePath) is false || System.IO.File.Exists(sourcePath) is false)
                {
                    rejectedCount++;
                    continue;
                }

                try
                {
                    paths.Add(Path.GetFullPath(sourcePath).Replace('\\', '/'));
                }
                catch (Exception)
                {
                    rejectedCount++;
                }
            }

            DragAndDrop.AcceptDrag();
            if (paths.Count == 0)
            {
                m_Status.text = "未加入文件：只支持存在的 MP4 文件。";
                evt.StopPropagation();
                return;
            }

            var comparer = PathComparer();
            foreach (var path in paths)
            {
                if (m_AllFiles.Contains(path, comparer) is false)
                {
                    m_AllFiles.Add(path);
                }
            }

            m_AllFiles.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
                Path.GetFileName(left),
                Path.GetFileName(right)));
            ApplyFilter();
            foreach (var path in paths)
            {
                m_SelectedFiles.Add(path);
            }

            m_List.Rebuild();
            m_Status.text = rejectedCount == 0
                ? $"已加入并选择 {paths.Count} 个拖入的 MP4。"
                : $"已加入并选择 {paths.Count} 个 MP4；另有 {rejectedCount} 个文件被拒绝，只支持存在的 MP4 文件。";
            UpdateControls();
            evt.StopPropagation();
        }

        private void SetSelected(string path, bool selected)
        {
            if (selected)
            {
                m_SelectedFiles.Add(path);
            }
            else
            {
                m_SelectedFiles.Remove(path);
            }

            UpdateSelectionStatus();
        }

        private void UpdateSelectionStatus()
        {
            if (m_ConfirmButton == null)
            {
                return;
            }

            var count = m_SelectedFiles.Count;
            m_ConfirmButton.SetEnabled(count > 0);
            var query = (m_SearchField?.value ?? string.Empty).Trim();
            if (query.Length > 0)
            {
                m_Status.text = $"搜索到 {m_FilteredFiles.Count} 个 MP4；已选择 {count} 个。";
            }
            else
            {
                m_Status.text = count > 0
                    ? $"已选择 {count} 个 MP4。"
                    : $"共 {m_FilteredFiles.Count} 个 MP4，可勾选多个文件。";
            }
        }

        private void UpdateControls()
        {
            m_ConfirmButton?.SetEnabled(m_SelectedFiles.Count > 0);
        }

        private static bool IsMp4(string path)
        {
            return string.IsNullOrWhiteSpace(path) is false &&
                   string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveInitialDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) is false && Directory.Exists(directory))
            {
                return directory;
            }

            var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            return string.IsNullOrWhiteSpace(videos) is false && Directory.Exists(videos)
                ? videos
                : Directory.GetCurrentDirectory();
        }

        private static StringComparer PathComparer()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        private static VisualElement CreateRow()
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 6f
                }
            };
        }

        private static Label CreateFieldLabel(string text, string name)
        {
            return new Label(text)
            {
                name = name,
                style =
                {
                    width = 48f,
                    flexShrink = 0f
                }
            };
        }
    }
}
