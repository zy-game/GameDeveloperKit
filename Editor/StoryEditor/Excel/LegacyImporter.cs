using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.StoryEditor.Migration;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Excel
{
    internal static class LegacyImporter
    {
        private const string ChapterDefineSheet = "ChapterDefine";
        private const string ChapterDataSheet = "ChapterData";

        [MenuItem("GameDeveloperKit/Story/Migrate Legacy Story Excel")]
        private static void ImportMenu()
        {
            var target = Selection.activeObject as AuthoringAsset;
            if (target == null)
            {
                EditorUtility.DisplayDialog("Legacy Story Excel", "Select an AuthoringAsset first.", "OK");
                return;
            }

            var path = EditorUtility.OpenFilePanel("Migrate Legacy Story Excel", string.Empty, "xlsx");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var result = Import(path, target, false);
            if (result.Status == MigrationApplyStatus.WarningConfirmationRequired &&
                EditorUtility.DisplayDialog("Confirm Story Migration", "The migration contains warnings. Apply it?", "Apply", "Cancel"))
            {
                result = Import(path, target, true);
            }

            ShowResult(result);
        }

        internal static MigrationResult Import(string inputPath, AuthoringAsset target, bool confirmWarnings)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var validation = new ValidationReport();
            var sheets = Importer.ReadWorkbook(inputPath, validation);
            var report = new MigrationReport();
            if (sheets == null || validation.HasErrors)
            {
                AddValidation(validation, report);
                return new MigrationResult(MigrationApplyStatus.Blocked, report);
            }

            if (!sheets.TryGetValue(ChapterDefineSheet, out var define) ||
                !sheets.TryGetValue(ChapterDataSheet, out var data))
            {
                report.AddConflict(
                    "missing_legacy_excel_sheet",
                    "excel",
                    "Legacy migration requires ChapterDefine and ChapterData sheets.");
                return new MigrationResult(MigrationApplyStatus.Blocked, report);
            }

            var legacy = ScriptableObject.CreateInstance<AuthoringAsset>();
            legacy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                legacy.StoryId = target.StoryId;
                legacy.Version = target.Version;
                legacy.Volumes.Clear();
                var volume = new AuthoringVolume { VolumeId = "legacy_volume", Title = "Legacy Volume" };
                legacy.Volumes.Add(volume);
                ParseDefinitions(define, volume, report);
                ParseData(data, volume, report);
                if (!report.CanApply || volume.Episodes.Count == 0)
                {
                    if (volume.Episodes.Count == 0)
                    {
                        report.AddConflict("empty_legacy_excel", ChapterDefineSheet, "Legacy Excel contains no chapters.");
                    }

                    return new MigrationResult(MigrationApplyStatus.Blocked, report);
                }

                legacy.LegacyEntryEpisodeId = volume.Episodes[0].EpisodeId;
                var result = MigrationService.Apply(legacy, confirmWarnings);
                if (!result.Succeeded)
                {
                    return result;
                }

                AuthoringUndo.Mutate(target, "Migrate Legacy Story Excel", () => EditorUtility.CopySerialized(legacy, target));
                if (EditorUtility.IsPersistent(target))
                {
                    AssetDatabase.SaveAssetIfDirty(target);
                }

                return new MigrationResult(MigrationApplyStatus.Applied, result.Report);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacy);
            }
        }

        private static void ParseDefinitions(
            Importer.SheetData sheet,
            AuthoringVolume volume,
            MigrationReport report)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var episodeId = sheet.Cell(row, "ChapterId");
                var entryNodeId = sheet.Cell(row, "EntryNodeId");
                if (string.IsNullOrWhiteSpace(episodeId) || string.IsNullOrWhiteSpace(entryNodeId) || !ids.Add(episodeId))
                {
                    report.AddConflict(
                        "invalid_legacy_chapter",
                        sheet.Location(row),
                        $"ChapterId/EntryNodeId is missing or duplicated. chapter:{episodeId}");
                    continue;
                }

                volume.Episodes.Add(new AuthoringEpisode
                {
                    EpisodeId = episodeId,
                    Title = sheet.Cell(row, "Title") ?? episodeId,
                    Description = sheet.Cell(row, "Description"),
                    EntryNodeId = entryNodeId,
                    PreviewImage = LoadTexture(sheet.Cell(row, "PreviewImage"))
                });
            }
        }

        private static void ParseData(
            Importer.SheetData sheet,
            AuthoringVolume volume,
            MigrationReport report)
        {
            var targets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (var row = 0; row < sheet.Rows.Count; row++)
            {
                var episodeId = sheet.Cell(row, "ChapterId");
                var episode = volume.Episodes.FirstOrDefault(x => x != null && x.EpisodeId == episodeId);
                var nodeId = sheet.Cell(row, "NodeId");
                var kindText = sheet.Cell(row, "NodeKind");
                if (episode == null || string.IsNullOrWhiteSpace(nodeId) || !TryLegacyKind(kindText, out var kind))
                {
                    report.AddConflict(
                        "invalid_legacy_node",
                        sheet.Location(row),
                        $"Legacy node ChapterId/NodeId/NodeKind is invalid. chapter:{episodeId} node:{nodeId} kind:{kindText}");
                    continue;
                }

                var node = new AuthoringNode
                {
                    NodeId = nodeId,
                    Title = sheet.Cell(row, "Title") ?? nodeId,
                    NodeKind = kind
                };
                ParseArgs(sheet.Cell(row, "Args"), node.Parameters, sheet.Location(row), report);
                episode.Nodes.Add(node);
                targets[episodeId + ":" + nodeId] = ParseTargets(sheet.Cell(row, "Targets"));
            }

            foreach (var episode in volume.Episodes)
            {
                for (var nodeIndex = 0; nodeIndex < episode.Nodes.Count; nodeIndex++)
                {
                    var node = episode.Nodes[nodeIndex];
                    if (!targets.TryGetValue(episode.EpisodeId + ":" + node.NodeId, out var nodeTargets))
                    {
                        continue;
                    }

                    for (var targetIndex = 0; targetIndex < nodeTargets.Count; targetIndex++)
                    {
                        var port = node.NodeKind == NodeKind.Choice
                            ? "selected"
                            : node.NodeKind == NodeKind.Parallel
                                ? $"branch_{targetIndex + 1}"
                                : "completed";
                        episode.Edges.Add(new AuthoringEdge
                        {
                            EdgeId = $"legacy_{node.NodeId}_{targetIndex}",
                            FromNodeId = node.NodeId,
                            FromPortId = port,
                            TargetKind = TransitionTargetKind.Node,
                            TargetNodeId = nodeTargets[targetIndex]
                        });
                    }
                }
            }
        }

        private static bool TryLegacyKind(string value, out NodeKind kind)
        {
            switch (value)
            {
                case "JumpChapter": kind = (NodeKind)LegacyNodeKinds.JumpEpisode; return true;
                case "MiniGame": kind = (NodeKind)LegacyNodeKinds.MiniGame; return true;
                case "Qte": kind = (NodeKind)LegacyNodeKinds.Qte; return true;
                case "Unlock": kind = (NodeKind)LegacyNodeKinds.Unlock; return true;
                case "SettleChapter": kind = (NodeKind)LegacyNodeKinds.SettleEpisode; return true;
                default: return Enum.TryParse(value, out kind);
            }
        }

        private static void ParseArgs(
            string text,
            ICollection<AuthoringParameter> target,
            string location,
            MigrationReport report)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var parts = text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var separator = parts[i].IndexOf('=');
                if (separator <= 0)
                {
                    report.AddConflict("invalid_legacy_argument", location, $"Legacy argument is invalid. value:{parts[i]}");
                    continue;
                }

                var value = parts[i].Substring(separator + 1);
                if (value.StartsWith("b64:", StringComparison.Ordinal))
                {
                    try
                    {
                        value = Encoding.UTF8.GetString(Convert.FromBase64String(value.Substring(4)));
                    }
                    catch (FormatException)
                    {
                        report.AddConflict("invalid_legacy_argument", location, "Legacy base64 argument is invalid.");
                        continue;
                    }
                }

                target.Add(new AuthoringParameter { Key = parts[i].Substring(0, separator), Value = value });
            }
        }

        private static List<string> ParseTargets(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text.Trim().TrimStart('[').TrimEnd(']')
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        private static Texture2D LoadTexture(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void AddValidation(ValidationReport validation, MigrationReport report)
        {
            for (var i = 0; i < validation.Issues.Count; i++)
            {
                report.AddConflict("legacy_excel_read_error", validation.Issues[i].Source, validation.Issues[i].Message);
            }
        }

        private static void ShowResult(MigrationResult result)
        {
            if (result.Succeeded)
            {
                EditorUtility.DisplayDialog("Legacy Story Excel", "Migration applied.", "OK");
                return;
            }

            var builder = new StringBuilder($"Migration blocked: {result.Status}\n\n");
            for (var i = 0; i < result.Report.Issues.Count; i++)
            {
                var issue = result.Report.Issues[i];
                builder.AppendLine($"[{issue.Code}] {issue.Location}: {issue.Message}");
            }

            EditorUtility.DisplayDialog("Legacy Story Excel", builder.ToString(), "OK");
        }
    }
}
