using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OfficeOpenXml;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor
{
    /// <summary>
    /// 将 StoryAuthoringAsset 导出为 .xlsx 文件。
    /// </summary>
    public static class StoryExcelExporter
    {
        private const string ChapterDefineSheet = "ChapterDefine";
        private const string ChapterDataSheet = "ChapterData";

        [MenuItem("GameDeveloperKit/剧情编辑/导出当前剧情为 Excel")]
        private static void ExportMenu()
        {
            var asset = Selection.activeObject as StoryAuthoringAsset;
            if (asset == null)
            {
                EditorUtility.DisplayDialog("导出剧情", "请先在 Project 窗口中选择一个 StoryAuthoringAsset。", "确定");
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(asset);
            var directory = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            var fileName = string.IsNullOrWhiteSpace(asset.StoryId) ? "story_export" : asset.StoryId;

            var outputPath = EditorUtility.SaveFilePanel(
                "导出当前剧情为 Excel",
                directory,
                fileName,
                "xlsx");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            try
            {
                Export(asset, outputPath);
                EditorUtility.DisplayDialog("导出剧情", $"成功导出到:\n{outputPath}", "确定");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导出失败", ex.Message, "确定");
                UnityEngine.Debug.LogException(ex);
            }
        }

        public static void Export(StoryAuthoringAsset asset, string outputPath)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path cannot be empty.", nameof(outputPath));
            }

            asset.EnsureDefaults();
            var chapters = asset.Chapters;

            using (var package = new ExcelPackage())
            {
                BuildChapterDefineSheet(package, chapters);
                BuildChapterDataSheet(package, asset.StoryId, chapters);
                package.SaveAs(new System.IO.FileInfo(outputPath));
            }
        }

        private static void BuildChapterDefineSheet(ExcelPackage package, IReadOnlyList<StoryAuthoringChapter> chapters)
        {
            var sheet = package.Workbook.Worksheets.Add(ChapterDefineSheet);
            WriteHeader(sheet, 1, "ChapterId", "Title", "Description", "EntryNodeId", "PreviewImage");

            for (var i = 0; i < chapters.Count; i++)
            {
                var chapter = chapters[i];
                if (chapter == null)
                {
                    continue;
                }

                var row = i + 2;
                sheet.Cells[row, 1].Value = chapter.ChapterId;
                sheet.Cells[row, 2].Value = chapter.Title;
                sheet.Cells[row, 3].Value = chapter.Description ?? string.Empty;
                sheet.Cells[row, 4].Value = chapter.EntryNodeId;
                sheet.Cells[row, 5].Value = GetPreviewImagePath(chapter);
            }
        }

        private static void BuildChapterDataSheet(
            ExcelPackage package,
            string storyId,
            IReadOnlyList<StoryAuthoringChapter> chapters)
        {
            var sheet = package.Workbook.Worksheets.Add(ChapterDataSheet);
            WriteHeader(sheet, 1, "ChapterId", "NodeId", "Title", "NodeKind", "Args", "Targets");

            var row = 2;
            for (var i = 0; i < chapters.Count; i++)
            {
                var chapter = chapters[i];
                if (chapter == null)
                {
                    continue;
                }

                var outgoingEdges = BuildOutgoingEdgeLookup(storyId, chapter);

                for (var j = 0; j < chapter.Nodes.Count; j++)
                {
                    var node = chapter.Nodes[j];
                    if (node == null)
                    {
                        continue;
                    }

                    sheet.Cells[row, 1].Value = chapter.ChapterId;
                    sheet.Cells[row, 2].Value = node.NodeId;
                    sheet.Cells[row, 3].Value = node.Title;
                    sheet.Cells[row, 4].Value = node.NodeKind.ToString();
                    sheet.Cells[row, 5].Value = BuildArgsCell(node);
                    sheet.Cells[row, 6].Value = BuildTargetsCell(node, outgoingEdges);

                    row++;
                }
            }
        }

        private static string BuildArgsCell(StoryAuthoringNode node)
        {
            var parameters = node.Parameters;
            if (parameters == null || parameters.Count == 0)
            {
                return string.Empty;
            }

            var sorted = new List<StoryAuthoringParameter>(parameters);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            var builder = new StringBuilder();
            for (var i = 0; i < sorted.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(sorted[i].Key))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(';');
                }

                builder.Append(sorted[i].Key);
                builder.Append('=');
                builder.Append(sorted[i].Value ?? string.Empty);
            }

            return builder.ToString();
        }

        private static string BuildTargetsCell(
            StoryAuthoringNode node,
            IReadOnlyDictionary<string, List<StoryAuthoringEdge>> outgoingEdges)
        {
            if (!outgoingEdges.TryGetValue(node.NodeId, out var edges) || edges.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append('[');
            var first = true;
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null || string.IsNullOrWhiteSpace(edge.TargetNodeId))
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(", ");
                }

                builder.Append(edge.TargetNodeId);
                first = false;
            }

            builder.Append(']');
            return first ? string.Empty : builder.ToString();
        }

        private static string GetPreviewImagePath(StoryAuthoringChapter chapter)
        {
            if (chapter.PreviewImage == null)
            {
                return string.Empty;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetAssetPath(chapter.PreviewImage) ?? string.Empty;
#else
            return string.Empty;
#endif
        }

        private static IReadOnlyDictionary<string, List<StoryAuthoringEdge>> BuildOutgoingEdgeLookup(
            string storyId,
            StoryAuthoringChapter chapter)
        {
            var lookup = new Dictionary<string, List<StoryAuthoringEdge>>(StringComparer.Ordinal);
            for (var i = 0; i < chapter.Edges.Count; i++)
            {
                var edge = chapter.Edges[i];
                if (edge == null || string.IsNullOrWhiteSpace(edge.FromNodeId))
                {
                    continue;
                }

                if (!lookup.TryGetValue(edge.FromNodeId, out var edges))
                {
                    edges = new List<StoryAuthoringEdge>();
                    lookup.Add(edge.FromNodeId, edges);
                }

                edges.Add(edge);
            }

            return lookup;
        }

        private static void WriteHeader(ExcelWorksheet sheet, int row, params string[] headers)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Cells[row, i + 1].Value = headers[i];
            }
        }
    }
}
