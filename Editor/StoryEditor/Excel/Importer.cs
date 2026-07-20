using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Excel
{
    public static class Importer
    {
        [MenuItem("GameDeveloperKit/剧情编辑/从 Excel 导入剧情")]
        private static void ImportMenu()
        {
            var asset = Selection.activeObject as AuthoringAsset;
            if (asset == null)
            {
                EditorUtility.DisplayDialog("导入剧情", "请先在 Project 窗口中选择一个 AuthoringAsset。", "确定");
                return;
            }

            var inputPath = EditorUtility.OpenFilePanel(
                "从 Excel 导入剧情",
                Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset)),
                "xlsx");
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return;
            }

            var report = Import(inputPath, asset);
            ShowReport(report);
        }

        public static ValidationReport Import(string inputPath, AuthoringAsset target)
        {
            var report = new ValidationReport();
            if (target == null)
            {
                report.AddError("asset", "Target authoring asset is missing.");
                return report;
            }

            var sheets = ReadWorkbook(inputPath, report);
            if (sheets == null)
            {
                return report;
            }

            if (!ValidateSheetProtocol(sheets.Keys, report))
            {
                return report;
            }

            var candidate = ScriptableObject.CreateInstance<AuthoringAsset>();
            candidate.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                ParseVolumes(sheets["VolumeDefine"], candidate, report);
                ParseEpisodes(sheets["EpisodeDefine"], candidate, report);
                ParseEpisodeData(sheets["EpisodeData"], candidate, report);
                ParseExits(sheets["EpisodeExit"], candidate, report);
                ParseRouteEdges(sheets["RouteEdge"], candidate, report);
                ParseRouteLayouts(sheets["RouteLayout"], candidate, report);
                ParseRouteEdgePlacements(sheets["RouteEdgePlacement"], candidate, report);
                ParseIdentityManifest(sheets["IdentityManifest"], candidate, report);
                if (!report.HasErrors)
                {
                    ValidateCandidate(candidate, report);
                }

                if (!report.HasErrors)
                {
                    AuthoringUndo.Mutate(target, "Import Story Excel", () => EditorUtility.CopySerialized(candidate, target));
                    if (EditorUtility.IsPersistent(target))
                    {
                        AssetDatabase.SaveAssetIfDirty(target);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }

            return report;
        }

        internal static bool ValidateSheetProtocol(IEnumerable<string> sheetNames, ValidationReport report)
        {
            var names = new HashSet<string>(sheetNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (names.Contains("ChapterDefine") || names.Contains("ChapterData"))
            {
                report.AddError(
                    "legacy-sheets",
                    "ChapterDefine/ChapterData are legacy sheets. Use GameDeveloperKit/Story/Migrate Legacy Story Excel.");
                return false;
            }

            for (var i = 0; i < Exporter.RequiredSheets.Length; i++)
            {
                if (!names.Contains(Exporter.RequiredSheets[i]))
                {
                    report.AddError(Exporter.RequiredSheets[i], $"Sheet '{Exporter.RequiredSheets[i]}' is missing.");
                }
            }

            return !report.HasErrors;
        }

        internal static Dictionary<string, SheetData> ReadWorkbook(string inputPath, ValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                report.AddError("path", "Input path cannot be empty.");
                return null;
            }

            if (!System.IO.File.Exists(inputPath))
            {
                report.AddError("path", $"File does not exist. path:{inputPath}");
                return null;
            }

            try
            {
                var result = new Dictionary<string, SheetData>(StringComparer.OrdinalIgnoreCase);
                using (var stream = System.IO.File.Open(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    do
                    {
                        var headers = new List<string>();
                        var rows = new List<string[]>();
                        var first = true;
                        while (reader.Read())
                        {
                            if (first)
                            {
                                for (var i = 0; i < reader.FieldCount; i++)
                                {
                                    headers.Add(reader.GetValue(i)?.ToString() ?? string.Empty);
                                }

                                first = false;
                                continue;
                            }

                            var row = new string[reader.FieldCount];
                            for (var i = 0; i < reader.FieldCount; i++)
                            {
                                row[i] = reader.GetValue(i)?.ToString() ?? string.Empty;
                            }

                            rows.Add(row);
                        }

                        result[reader.Name] = new SheetData(reader.Name, headers, rows);
                    } while (reader.NextResult());
                }

                return result;
            }
            catch (Exception exception)
            {
                report.AddError("file", $"Failed to read Excel file. {exception.Message}");
                return null;
            }
        }

        private static void ParseVolumes(SheetData sheet, AuthoringAsset candidate, ValidationReport report)
        {
            candidate.Volumes.Clear();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var source = sheet.Location(row);
                var storyId = sheet.Cell(row, "StoryId");
                var version = sheet.Cell(row, "Version");
                var volumeId = sheet.Cell(row, "VolumeId");
                if (!Require(storyId, "StoryId", source, report) ||
                    !Require(version, "Version", source, report) ||
                    !Require(volumeId, "VolumeId", source, report))
                {
                    continue;
                }

                if (!ids.Add(volumeId))
                {
                    report.AddError(source, $"Duplicate VolumeId. volume:{volumeId}");
                    continue;
                }

                if (candidate.Volumes.Count == 0)
                {
                    candidate.StoryId = storyId;
                    candidate.Version = version;
                }
                else if (!string.Equals(candidate.StoryId, storyId, StringComparison.Ordinal) ||
                         !string.Equals(candidate.Version, version, StringComparison.Ordinal))
                {
                    report.AddError(source, "StoryId and Version must be identical on every VolumeDefine row.");
                    continue;
                }

                candidate.Volumes.Add(new AuthoringVolume
                {
                    VolumeId = volumeId,
                    Title = sheet.Cell(row, "Title") ?? volumeId,
                    Description = sheet.Cell(row, "Description"),
                    PreviewImage = LoadTexture(sheet.Cell(row, "PreviewImage"), source + "/PreviewImage", report),
                    Route = new AuthoringRoute()
                });
            }

            if (candidate.Volumes.Count == 0)
            {
                report.AddError(sheet.Name, "VolumeDefine must contain at least one row.");
            }
        }

        private static void ParseEpisodes(SheetData sheet, AuthoringAsset candidate, ValidationReport report)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var source = sheet.Location(row);
                var volume = FindVolume(candidate, sheet.Cell(row, "VolumeId"));
                var episodeId = sheet.Cell(row, "EpisodeId");
                var entryNodeId = sheet.Cell(row, "EntryNodeId");
                if (volume == null)
                {
                    report.AddError(source, $"Episode references an unknown Volume. volume:{sheet.Cell(row, "VolumeId")}");
                    continue;
                }

                if (!Require(episodeId, "EpisodeId", source, report) ||
                    !Require(entryNodeId, "EntryNodeId", source, report))
                {
                    continue;
                }

                if (!ids.Add(episodeId))
                {
                    report.AddError(source, $"Duplicate EpisodeId. episode:{episodeId}");
                    continue;
                }

                volume.Episodes.Add(new AuthoringEpisode
                {
                    EpisodeId = episodeId,
                    Title = sheet.Cell(row, "Title") ?? episodeId,
                    Description = sheet.Cell(row, "Description"),
                    PreviewImage = LoadTexture(sheet.Cell(row, "PreviewImage"), source + "/PreviewImage", report),
                    EntryNodeId = entryNodeId
                });
            }
        }

        private static void ParseEpisodeData(SheetData sheet, AuthoringAsset candidate, ValidationReport report)
        {
            var recordIds = new HashSet<string>(StringComparer.Ordinal);
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var source = sheet.Location(row);
                var episode = candidate.FindEpisode(sheet.Cell(row, "EpisodeId"));
                var recordKind = sheet.Cell(row, "RecordKind");
                var recordId = sheet.Cell(row, "RecordId");
                if (episode == null)
                {
                    report.AddError(source, $"EpisodeData references an unknown Episode. episode:{sheet.Cell(row, "EpisodeId")}");
                    continue;
                }

                if (!Require(recordId, "RecordId", source, report) || !recordIds.Add(recordKind + ":" + recordId))
                {
                    if (!string.IsNullOrWhiteSpace(recordId))
                    {
                        report.AddError(source, $"Duplicate EpisodeData record. kind:{recordKind} id:{recordId}");
                    }
                    continue;
                }

                if (string.Equals(recordKind, "Node", StringComparison.Ordinal))
                {
                    ParseNode(sheet, row, source, recordId, episode, report);
                }
                else if (string.Equals(recordKind, "Edge", StringComparison.Ordinal))
                {
                    ParseEdge(sheet, row, source, recordId, episode, report);
                }
                else
                {
                    report.AddError(source, $"RecordKind must be Node or Edge. value:{recordKind}");
                }
            }
        }

        private static void ParseNode(
            SheetData sheet,
            int row,
            string source,
            string nodeId,
            AuthoringEpisode episode,
            ValidationReport report)
        {
            var kindText = sheet.Cell(row, "NodeKind");
            if (!Enum.TryParse(kindText, out NodeKind kind) ||
                !Enum.IsDefined(typeof(NodeKind), kind) ||
                !NodeSchemaRegistry.IsDefaultAuthoringNode(kind))
            {
                report.AddError(source, $"NodeKind is not part of the current Story authoring surface. kind:{kindText}");
                return;
            }

            var node = new AuthoringNode
            {
                NodeId = nodeId,
                Title = sheet.Cell(row, "Title") ?? nodeId,
                NodeKind = kind
            };
            AddParameters(node.Parameters, sheet.Cell(row, "ParametersJson"), source, report);
            AddConditions(node.Conditions, sheet.Cell(row, "ConditionsJson"), source, report);
            episode.Nodes.Add(node);
            if (TryFloat(sheet.Cell(row, "PositionX"), out var x) && TryFloat(sheet.Cell(row, "PositionY"), out var y))
            {
                episode.DetailLayout.Nodes.Add(new EpisodeNodePlacement { NodeId = nodeId, Position = new Vector2(x, y) });
            }
        }

        private static void ParseEdge(
            SheetData sheet,
            int row,
            string source,
            string edgeId,
            AuthoringEpisode episode,
            ValidationReport report)
        {
            var kindText = sheet.Cell(row, "TargetKind");
            if (!Enum.TryParse(kindText, out TransitionTargetKind targetKind) ||
                !Enum.IsDefined(typeof(TransitionTargetKind), targetKind))
            {
                report.AddError(source, $"TargetKind is invalid. kind:{kindText}");
                return;
            }

            var edge = new AuthoringEdge
            {
                EdgeId = edgeId,
                FromNodeId = sheet.Cell(row, "FromNodeId"),
                FromPortId = sheet.Cell(row, "FromPortId"),
                FromPortLabel = sheet.Cell(row, "FromPortLabel"),
                TargetKind = targetKind,
                TargetNodeId = targetKind == TransitionTargetKind.Node ? sheet.Cell(row, "TargetNodeId") : null
            };
            AddConditions(edge.Conditions, sheet.Cell(row, "ConditionsJson"), source, report);
            episode.Edges.Add(edge);
        }

        private static void ParseExits(SheetData sheet, AuthoringAsset candidate, ValidationReport report)
        {
            var declared = new HashSet<string>(StringComparer.Ordinal);
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var source = sheet.Location(row);
                var episodeId = sheet.Cell(row, "EpisodeId");
                var exitId = sheet.Cell(row, "ExitId");
                var episode = candidate.FindEpisode(episodeId);
                if (episode == null || string.IsNullOrWhiteSpace(exitId) ||
                    !episode.Nodes.Any(x => x != null && x.NodeId == exitId && (x.NodeKind == NodeKind.Choice || x.NodeKind == NodeKind.End)))
                {
                    report.AddError(source, $"EpisodeExit must match one Choice or End node. episode:{episodeId} exit:{exitId}");
                    continue;
                }

                if (!declared.Add(episodeId + ":" + exitId))
                {
                    report.AddError(source, $"Duplicate EpisodeExit. episode:{episodeId} exit:{exitId}");
                }
            }

            foreach (var episode in candidate.Episodes)
            {
                foreach (var node in episode.Nodes.Where(x => x != null && (x.NodeKind == NodeKind.Choice || x.NodeKind == NodeKind.End)))
                {
                    if (!declared.Contains(episode.EpisodeId + ":" + node.NodeId))
                    {
                        report.AddError(sheet.Name, $"EpisodeExit is missing. episode:{episode.EpisodeId} exit:{node.NodeId}");
                    }
                }
            }
        }

        private static void ParseRouteEdges(SheetData sheet, AuthoringAsset candidate, ValidationReport report)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var source = sheet.Location(row);
                var volume = FindVolume(candidate, sheet.Cell(row, "VolumeId"));
                var edgeId = sheet.Cell(row, "EdgeId");
                if (volume == null || string.IsNullOrWhiteSpace(edgeId) || !ids.Add(edgeId))
                {
                    report.AddError(source, $"RouteEdge Volume/EdgeId is invalid or duplicated. edge:{edgeId}");
                    continue;
                }

                if (!Enum.TryParse(sheet.Cell(row, "SourceKind"), out RouteEdgeSourceKind sourceKind) ||
                    !Enum.IsDefined(typeof(RouteEdgeSourceKind), sourceKind))
                {
                    report.AddError(source, $"RouteEdge SourceKind is invalid. kind:{sheet.Cell(row, "SourceKind")}");
                    continue;
                }

                volume.Route.Edges.Add(new AuthoringRouteEdge
                {
                    EdgeId = edgeId,
                    SourceKind = sourceKind,
                    FromEpisodeId = sheet.Cell(row, "FromEpisodeId"),
                    FromExitId = sheet.Cell(row, "FromExitId"),
                    ToEpisodeId = sheet.Cell(row, "ToEpisodeId")
                });
            }
        }

        private static void ParseRouteLayouts(SheetData sheet, AuthoringAsset candidate, ValidationReport report)
        {
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var source = sheet.Location(row);
                var volume = FindVolume(candidate, sheet.Cell(row, "VolumeId"));
                if (volume == null || !Enum.TryParse(sheet.Cell(row, "Orientation"), out LayoutOrientation orientation))
                {
                    report.AddError(source, "RouteLayout Volume or Orientation is invalid.");
                    continue;
                }

                if (!TryFloat(sheet.Cell(row, "RootX"), out var rootX) ||
                    !TryFloat(sheet.Cell(row, "RootY"), out var rootY))
                {
                    report.AddError(source, "RouteLayout viewport-relative root position is required.");
                    continue;
                }

                var widthText = sheet.Cell(row, "ReferenceWidth");
                var heightText = sheet.Cell(row, "ReferenceHeight");
                var hasLegacySize = string.IsNullOrWhiteSpace(widthText) is false ||
                                    string.IsNullOrWhiteSpace(heightText) is false;
                if (hasLegacySize &&
                    (!int.TryParse(widthText, out var legacyWidth) ||
                     !int.TryParse(heightText, out var legacyHeight) ||
                     legacyWidth <= 0 || legacyHeight <= 0))
                {
                    report.AddError(source, "Legacy RouteLayout reference size is invalid.");
                    continue;
                }

                int.TryParse(widthText, out var width);
                int.TryParse(heightText, out var height);

                var layout = new AuthoringRouteLayout
                {
                    LayoutId = sheet.Cell(row, "LayoutId"),
                    Orientation = orientation,
                    LegacyReferenceWidth = width,
                    LegacyReferenceHeight = height,
                    UsesRelativeCoordinates = hasLegacySize is false,
                    BackgroundImage = LoadTexture(sheet.Cell(row, "BackgroundImage"), source + "/BackgroundImage", report),
                    EditorGuideImage = LoadTexture(sheet.Cell(row, "EditorGuideImage"), source + "/EditorGuideImage", report),
                    RootPlacement = new AuthoringPlacement { Position = new Vector2(rootX, rootY) }
                };
                foreach (var placement in Deserialize<PlacementData[]>(sheet.Cell(row, "EpisodePlacementsJson"), source, report) ?? Array.Empty<PlacementData>())
                {
                    layout.Episodes.Add(new AuthoringEpisodePlacement
                    {
                        EpisodeId = placement.Id,
                        Position = new AuthoringPlacement { Position = new Vector2(placement.X, placement.Y) }
                    });
                }

                volume.Layouts.Add(layout);
            }
        }

        private static void ParseRouteEdgePlacements(SheetData sheet, AuthoringAsset candidate, ValidationReport report)
        {
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var source = sheet.Location(row);
                var volume = FindVolume(candidate, sheet.Cell(row, "VolumeId"));
                var layout = volume?.Layouts.FirstOrDefault(x => x != null && x.LayoutId == sheet.Cell(row, "LayoutId"));
                if (layout == null)
                {
                    report.AddError(source, "RouteEdgePlacement references an unknown Layout.");
                    continue;
                }

                var edge = new AuthoringRouteEdgePlacement
                {
                    EdgeId = sheet.Cell(row, "EdgeId"),
                    StyleKey = sheet.Cell(row, "StyleKey")
                };
                foreach (var point in Deserialize<PlacementData[]>(sheet.Cell(row, "ControlPointsJson"), source, report) ?? Array.Empty<PlacementData>())
                {
                    edge.ControlPoints.Add(new AuthoringPlacement { Position = new Vector2(point.X, point.Y) });
                }

                layout.Edges.Add(edge);
            }
        }

        private static void ParseIdentityManifest(SheetData sheet, AuthoringAsset candidate, ValidationReport report)
        {
            var episodes = new List<string>();
            var edges = new List<string>();
            var exits = new List<ExitIdentity>();
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var kind = sheet.Cell(row, "EntityKind");
                var id = sheet.Cell(row, "IdentityId");
                if (kind == "Episode") episodes.Add(id);
                else if (kind == "Edge") edges.Add(id);
                else if (kind == "Exit") exits.Add(new ExitIdentity(sheet.Cell(row, "EpisodeId"), id));
                else report.AddError(sheet.Location(row), $"IdentityManifest EntityKind is invalid. kind:{kind}");
            }

            if (report.HasErrors)
            {
                return;
            }

            try
            {
                candidate.CommitPublishedIdentity(new IdentityManifest(candidate.StoryId, candidate.Version, episodes, edges, exits));
            }
            catch (Exception exception)
            {
                report.AddError(sheet.Name, $"IdentityManifest is invalid. {exception.Message}");
            }
        }

        private static void ValidateCandidate(AuthoringAsset candidate, ValidationReport report)
        {
            var program = ProgramCompiler.Compile(candidate, out var compiled);
            for (var i = 0; i < compiled.Issues.Count; i++)
            {
                report.Add(compiled.Issues[i].Severity, compiled.Issues[i].Source, compiled.Issues[i].Message);
            }

            if (program == null || report.HasErrors)
            {
                return;
            }

            try
            {
                new StoryModule().Register(program);
            }
            catch (Exception exception)
            {
                report.AddError($"story:{candidate.StoryId}", $"Imported Program cannot be registered. {exception.Message}");
            }
        }

        private static void AddParameters(List<AuthoringParameter> target, string json, string source, ValidationReport report)
        {
            foreach (var value in Deserialize<ParameterData[]>(json, source, report) ?? Array.Empty<ParameterData>())
            {
                target.Add(new AuthoringParameter { Key = value.Key, Value = value.Value });
            }
        }

        private static void AddConditions(List<AuthoringCondition> target, string json, string source, ValidationReport report)
        {
            foreach (var value in Deserialize<ConditionData[]>(json, source, report) ?? Array.Empty<ConditionData>())
            {
                var condition = new AuthoringCondition { ConditionId = value.ConditionId };
                foreach (var parameter in value.Parameters ?? Array.Empty<ParameterData>())
                {
                    condition.Parameters.Add(new AuthoringParameter { Key = parameter.Key, Value = parameter.Value });
                }

                target.Add(condition);
            }
        }

        private static T Deserialize<T>(string json, string source, ValidationReport report)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json) ? default : JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception exception)
            {
                report.AddError(source, $"JSON field is invalid. {exception.Message}");
                return default;
            }
        }

        private static AuthoringVolume FindVolume(AuthoringAsset asset, string volumeId)
        {
            return asset.Volumes.FirstOrDefault(x => x != null && string.Equals(x.VolumeId, volumeId, StringComparison.Ordinal));
        }

        private static Texture2D LoadTexture(string path, string source, ValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                report.AddError(source, $"Texture asset does not exist. path:{path}");
            }

            return texture;
        }

        private static bool Require(string value, string field, string source, ValidationReport report)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            report.AddError(source, $"{field} is required.");
            return false;
        }

        private static bool TryFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
                   float.TryParse(value, out result);
        }

        private static void ShowReport(ValidationReport report)
        {
            if (!report.HasErrors)
            {
                EditorUtility.DisplayDialog("导入剧情", "导入成功。", "确定");
                return;
            }

            var builder = new StringBuilder("导入失败，以下校验未通过：\n\n");
            for (var i = 0; i < report.Issues.Count; i++)
            {
                builder.AppendLine(report.Issues[i].ToString());
            }

            EditorUtility.DisplayDialog("导入失败", builder.ToString(), "确定");
        }

        internal sealed class SheetData
        {
            private readonly Dictionary<string, int> m_Columns;

            public SheetData(string name, List<string> headers, List<string[]> rows)
            {
                Name = name;
                Rows = rows;
                m_Columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headers.Count; i++)
                {
                    m_Columns[headers[i]] = i;
                }
            }

            public string Name { get; }

            public List<string[]> Rows { get; }

            public string Cell(int row, string column)
            {
                if (!m_Columns.TryGetValue(column, out var index) || row < 0 || row >= Rows.Count || index >= Rows[row].Length)
                {
                    return null;
                }

                return string.IsNullOrWhiteSpace(Rows[row][index]) ? null : Rows[row][index];
            }

            public string Location(int row)
            {
                return $"{Name}:row{row + 2}";
            }
        }

        [Serializable]
        private sealed class ParameterData
        {
            public string Key { get; set; }
            public string Value { get; set; }
        }

        [Serializable]
        private sealed class ConditionData
        {
            public string ConditionId { get; set; }
            public ParameterData[] Parameters { get; set; }
        }

        [Serializable]
        private sealed class PlacementData
        {
            public string Id { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
        }
    }
}
