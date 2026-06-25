using System;
using System.Collections.Generic;
using System.Text;
using ExcelDataReader;
using GameDeveloperKit.Story;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor
{
    /// <summary>
    /// 从 .xlsx 文件导入 StoryAuthoringAsset 数据。
    /// </summary>
    public static class StoryExcelImporter
    {
        private const string ChapterDefineSheet = "ChapterDefine";
        private const string ChapterDataSheet = "ChapterData";

        [MenuItem("GameDeveloperKit/剧情编辑/从 Excel 导入剧情")]
        private static void ImportMenu()
        {
            var asset = Selection.activeObject as StoryAuthoringAsset;
            if (asset == null)
            {
                EditorUtility.DisplayDialog("导入剧情", "请先在 Project 窗口中选择一个 StoryAuthoringAsset。", "确定");
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(asset);
            var directory = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');

            var inputPath = EditorUtility.OpenFilePanel("从 Excel 导入剧情", directory, "xlsx");
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return;
            }

            try
            {
                var report = Import(inputPath, asset);
                if (report.HasErrors)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("导入失败，以下校验未通过：");
                    builder.AppendLine();
                    for (var i = 0; i < report.Issues.Count; i++)
                    {
                        builder.AppendLine($"  {report.Issues[i]}");
                    }

                    EditorUtility.DisplayDialog("导入失败", builder.ToString(), "确定");
                }
                else
                {
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("导入剧情", "导入成功。", "确定");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导入失败", ex.Message, "确定");
                UnityEngine.Debug.LogException(ex);
            }
        }

        public static StoryValidationReport Import(string inputPath, StoryAuthoringAsset target)
        {
            var report = new StoryValidationReport();

            if (target == null)
            {
                report.AddError("asset", "Target authoring asset is missing.");
                return report;
            }

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                report.AddError("path", "Input path cannot be empty.");
                return report;
            }

            if (!System.IO.File.Exists(inputPath))
            {
                report.AddError("path", $"File does not exist. path:{inputPath}");
                return report;
            }

            List<SheetData> sheets;
            try
            {
                using (var stream = System.IO.File.Open(inputPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        sheets = ReadAllSheets(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                report.AddError("file", $"Failed to read Excel file. {ex.Message}");
                return report;
            }

            var chapterDefineSheet = FindSheet(sheets, ChapterDefineSheet);
            var chapterDataSheet = FindSheet(sheets, ChapterDataSheet);

            if (chapterDefineSheet == null)
            {
                report.AddError(ChapterDefineSheet, $"Sheet '{ChapterDefineSheet}' is missing.");
                return report;
            }

            if (chapterDataSheet == null)
            {
                report.AddError(ChapterDataSheet, $"Sheet '{ChapterDataSheet}' is missing.");
                return report;
            }

            var chapters = ParseChapterDefineSheet(chapterDefineSheet, report);
            var nodesByChapter = ParseChapterDataSheet(chapterDataSheet, report);

            if (report.HasErrors)
            {
                return report;
            }

            var chapterEdges = ResolveTargets(nodesByChapter, report);

            if (report.HasErrors)
            {
                return report;
            }

            AtomicReplace(target, chapters, nodesByChapter, chapterEdges);
            return report;
        }

        private static SheetData FindSheet(List<SheetData> sheets, string name)
        {
            for (var i = 0; i < sheets.Count; i++)
            {
                if (string.Equals(sheets[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return sheets[i];
                }
            }

            return null;
        }

        private static List<SheetData> ReadAllSheets(IExcelDataReader reader)
        {
            var sheets = new List<SheetData>();
            do
            {
                var sheet = new SheetData { Name = reader.Name };
                var headers = new List<string>();
                var rows = new List<string[]>();

                var isFirstRow = true;
                while (reader.Read())
                {
                    if (isFirstRow)
                    {
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            headers.Add(reader.GetValue(i)?.ToString() ?? string.Empty);
                        }

                        isFirstRow = false;
                        continue;
                    }

                    var row = new string[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        row[i] = reader.GetValue(i)?.ToString() ?? string.Empty;
                    }

                    rows.Add(row);
                }

                sheet.Headers = headers;
                sheet.Rows = rows;
                sheets.Add(sheet);
            } while (reader.NextResult());

            return sheets;
        }

        private static List<ParsedChapter> ParseChapterDefineSheet(SheetData sheet, StoryValidationReport report)
        {
            var chapters = new List<ParsedChapter>();
            var chapterIds = new HashSet<string>(StringComparer.Ordinal);
            var colMap = BuildColumnMap(sheet.Headers);

            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var values = sheet.Rows[row];
                var chapterId = GetCellValue(values, colMap, "ChapterId");
                if (string.IsNullOrWhiteSpace(chapterId))
                {
                    report.AddError($"{ChapterDefineSheet}:row{row + 2}", "ChapterId is required.");
                    continue;
                }

                if (!chapterIds.Add(chapterId))
                {
                    report.AddError($"{ChapterDefineSheet}:row{row + 2}", $"Duplicate ChapterId '{chapterId}'.");
                    continue;
                }

                var entryNodeId = GetCellValue(values, colMap, "EntryNodeId");
                if (string.IsNullOrWhiteSpace(entryNodeId))
                {
                    report.AddError($"{ChapterDefineSheet}:row{row + 2}", "EntryNodeId is required.");
                    continue;
                }

                chapters.Add(new ParsedChapter
                {
                    ChapterId = chapterId,
                    Title = GetCellValue(values, colMap, "Title") ?? chapterId,
                    Description = GetCellValue(values, colMap, "Description"),
                    EntryNodeId = entryNodeId,
                    PreviewImagePath = GetCellValue(values, colMap, "PreviewImage")
                });
            }

            return chapters;
        }

        private static Dictionary<string, List<ParsedNode>> ParseChapterDataSheet(SheetData sheet, StoryValidationReport report)
        {
            var nodesByChapter = new Dictionary<string, List<ParsedNode>>(StringComparer.Ordinal);
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            var colMap = BuildColumnMap(sheet.Headers);

            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var values = sheet.Rows[row];
                var chapterId = GetCellValue(values, colMap, "ChapterId");
                var nodeId = GetCellValue(values, colMap, "NodeId");
                var nodeKindStr = GetCellValue(values, colMap, "NodeKind");
                var source = $"{ChapterDataSheet}:row{row + 2}";

                if (string.IsNullOrWhiteSpace(chapterId))
                {
                    report.AddError(source, "ChapterId is required.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    report.AddError(source, "NodeId is required.");
                    continue;
                }

                if (!nodeIds.Add(nodeId))
                {
                    report.AddError(source, $"Duplicate NodeId '{nodeId}'.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(nodeKindStr))
                {
                    report.AddError(source, "NodeKind is required.");
                    continue;
                }

                if (!Enum.TryParse<NodeKind>(nodeKindStr, out var nodeKind))
                {
                    report.AddError(source, $"Invalid NodeKind '{nodeKindStr}'.");
                    continue;
                }

                var args = GetCellValue(values, colMap, "Args");
                var parameters = ParseArgs(args, source, report);

                var targetsStr = GetCellValue(values, colMap, "Targets");
                var targets = ParseTargetsString(targetsStr);

                if (!nodesByChapter.TryGetValue(chapterId, out var nodes))
                {
                    nodes = new List<ParsedNode>();
                    nodesByChapter.Add(chapterId, nodes);
                }

                nodes.Add(new ParsedNode
                {
                    ChapterId = chapterId,
                    NodeId = nodeId,
                    Title = GetCellValue(values, colMap, "Title") ?? nodeId,
                    NodeKind = nodeKind,
                    Parameters = parameters,
                    TargetNodeIds = targets
                });
            }

            return nodesByChapter;
        }

        private static List<StoryAuthoringParameter> ParseArgs(string args, string source, StoryValidationReport report)
        {
            var parameters = new List<StoryAuthoringParameter>();
            if (string.IsNullOrWhiteSpace(args))
            {
                return parameters;
            }

            var pairs = args.Split(';');
            for (var i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i];
                if (string.IsNullOrWhiteSpace(pair))
                {
                    continue;
                }

                var eqIndex = pair.IndexOf('=');
                if (eqIndex <= 0)
                {
                    report.AddWarning(source, $"Invalid args format at segment '{pair}'. Expected key=value.");
                    continue;
                }

                var key = pair.Substring(0, eqIndex).Trim();
                var value = eqIndex + 1 < pair.Length ? pair.Substring(eqIndex + 1) : string.Empty;

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                parameters.Add(new StoryAuthoringParameter { Key = key, Value = value });
            }

            return parameters;
        }

        private static List<string> ParseTargetsString(string targetsStr)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(targetsStr))
            {
                return result;
            }

            var trimmed = targetsStr.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }

            var parts = trimmed.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var id = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    result.Add(id);
                }
            }

            return result;
        }

        private static Dictionary<string, List<StoryAuthoringEdge>> ResolveTargets(
            Dictionary<string, List<ParsedNode>> nodesByChapter,
            StoryValidationReport report)
        {
            var chapterEdges = new Dictionary<string, List<StoryAuthoringEdge>>(StringComparer.Ordinal);

            foreach (var chapterPair in nodesByChapter)
            {
                var chapterId = chapterPair.Key;
                var nodes = chapterPair.Value;
                var edges = new List<StoryAuthoringEdge>();
                chapterEdges[chapterId] = edges;

                var nodeIdSet = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < nodes.Count; i++)
                {
                    nodeIdSet.Add(nodes[i].NodeId);
                }

                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    for (var j = 0; j < node.TargetNodeIds.Count; j++)
                    {
                        var targetId = node.TargetNodeIds[j];
                        if (!nodeIdSet.Contains(targetId))
                        {
                            report.AddError(
                                $"{ChapterDataSheet}:node:{node.NodeId}",
                                $"Target node '{targetId}' does not exist in chapter '{chapterId}'.");
                            continue;
                        }

                        var portId = ResolvePortId(node, j, node.TargetNodeIds.Count);
                        edges.Add(new StoryAuthoringEdge
                        {
                            EdgeId = GenerateEdgeId(node.NodeId, targetId, j),
                            FromNodeId = node.NodeId,
                            FromPortId = portId,
                            TargetKind = TransitionTargetKind.Node,
                            TargetChapterId = chapterId,
                            TargetNodeId = targetId
                        });
                    }
                }
            }

            return chapterEdges;
        }

        private static string ResolvePortId(ParsedNode node, int index, int totalCount)
        {
            if (totalCount == 1)
            {
                return "completed";
            }

            switch (node.NodeKind)
            {
                case NodeKind.Parallel:
                    return $"branch_{index + 1}";
                case NodeKind.Choice:
                    return $"choice_{index + 1}";
                case NodeKind.Dialogue:
                case NodeKind.Narration:
                case NodeKind.Wait:
                    return "completed";
                default:
                    var schema = NodeSchemaRegistry.Get(node.NodeKind);
                    if (schema?.Ports != null)
                    {
                        var outputIndex = 0;
                        for (var p = 0; p < schema.Ports.Count; p++)
                        {
                            var port = schema.Ports[p];
                            if (port.Direction != PortDirection.Output)
                            {
                                continue;
                            }

                            if (outputIndex == index)
                            {
                                return port.PortId;
                            }

                            outputIndex++;
                        }
                    }

                    return "completed";
            }
        }

        private static string GenerateEdgeId(string fromNodeId, string toNodeId, int index)
        {
            return $"{fromNodeId}_to_{toNodeId}_{index}";
        }

        private static void AtomicReplace(
            StoryAuthoringAsset target,
            List<ParsedChapter> chapters,
            Dictionary<string, List<ParsedNode>> nodesByChapter,
            Dictionary<string, List<StoryAuthoringEdge>> chapterEdges)
        {
            target.Chapters.Clear();

            for (var i = 0; i < chapters.Count; i++)
            {
                var parsedChapter = chapters[i];
                var chapter = new StoryAuthoringChapter
                {
                    ChapterId = parsedChapter.ChapterId,
                    Title = parsedChapter.Title,
                    Description = parsedChapter.Description,
                    EntryNodeId = parsedChapter.EntryNodeId
                };

                if (!string.IsNullOrWhiteSpace(parsedChapter.PreviewImagePath))
                {
#if UNITY_EDITOR
                    chapter.PreviewImage = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(parsedChapter.PreviewImagePath);
#endif
                }

                if (nodesByChapter.TryGetValue(parsedChapter.ChapterId, out var nodes))
                {
                    for (var j = 0; j < nodes.Count; j++)
                    {
                        var parsedNode = nodes[j];
                        var authoringNode = new StoryAuthoringNode
                        {
                            NodeId = parsedNode.NodeId,
                            Title = parsedNode.Title,
                            NodeKind = parsedNode.NodeKind
                        };

                        if (parsedNode.Parameters != null)
                        {
                            authoringNode.Parameters.AddRange(parsedNode.Parameters);
                        }

                        chapter.Nodes.Add(authoringNode);
                    }
                }

                if (chapterEdges.TryGetValue(parsedChapter.ChapterId, out var edges))
                {
                    chapter.Edges.AddRange(edges);
                }

                target.Chapters.Add(chapter);
            }

            target.EnsureDefaults();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(target);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        private static Dictionary<string, int> BuildColumnMap(List<string> headers)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                map[headers[i]] = i;
            }

            return map;
        }

        private static string GetCellValue(string[] values, Dictionary<string, int> colMap, string columnName)
        {
            if (!colMap.TryGetValue(columnName, out var col))
            {
                return null;
            }

            if (col >= values.Length)
            {
                return null;
            }

            var str = values[col];
            return string.IsNullOrWhiteSpace(str) ? null : str;
        }

        private sealed class SheetData
        {
            public string Name;
            public List<string> Headers;
            public List<string[]> Rows;
        }

        private sealed class ParsedChapter
        {
            public string ChapterId;
            public string Title;
            public string Description;
            public string EntryNodeId;
            public string PreviewImagePath;
        }

        private sealed class ParsedNode
        {
            public string ChapterId;
            public string NodeId;
            public string Title;
            public NodeKind NodeKind;
            public List<StoryAuthoringParameter> Parameters;
            public List<string> TargetNodeIds;
        }
    }
}
