using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using Newtonsoft.Json;
using OfficeOpenXml;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Excel
{
    public static class Exporter
    {
        internal static readonly string[] RequiredSheets =
        {
            "VolumeDefine",
            "EpisodeDefine",
            "EpisodeExit",
            "RouteEdge",
            "EpisodeData",
            "RouteLayout",
            "RouteEdgePlacement",
            "IdentityManifest"
        };

        [MenuItem("GameDeveloperKit/剧情编辑/导出当前剧情为 Excel")]
        private static void ExportMenu()
        {
            var asset = Selection.activeObject as AuthoringAsset;
            if (asset == null)
            {
                EditorUtility.DisplayDialog("导出剧情", "请先在 Project 窗口中选择一个 AuthoringAsset。", "确定");
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(asset);
            var outputPath = EditorUtility.SaveFilePanel(
                "导出当前剧情为 Excel",
                Path.GetDirectoryName(sourcePath),
                string.IsNullOrWhiteSpace(asset.StoryId) ? "story_export" : asset.StoryId,
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
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("导出失败", exception.Message, "确定");
                Debug.LogException(exception);
            }
        }

        public static void Export(AuthoringAsset asset, string outputPath)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path cannot be empty.", nameof(outputPath));
            }

            var program = ProgramCompiler.Compile(asset, out var validation);
            if (program == null || validation.HasErrors)
            {
                throw new InvalidOperationException("Only a validated current Story route asset can be exported.");
            }

            using (var package = new ExcelPackage())
            {
                BuildVolumeDefine(package, asset);
                BuildEpisodeDefine(package, asset);
                BuildEpisodeExit(package, asset);
                BuildRouteEdge(package, asset);
                BuildEpisodeData(package, asset);
                BuildRouteLayout(package, asset);
                BuildRouteEdgePlacement(package, asset);
                BuildIdentityManifest(package, IdentityManifest.Create(program));
                package.SaveAs(new FileInfo(outputPath));
            }
        }

        private static void BuildVolumeDefine(ExcelPackage package, AuthoringAsset asset)
        {
            var sheet = AddSheet(package, "VolumeDefine", "StoryId", "Version", "VolumeId", "Title", "Description", "PreviewImage");
            for (var i = 0; i < asset.Volumes.Count; i++)
            {
                var volume = asset.Volumes[i];
                if (volume == null)
                {
                    continue;
                }

                Write(sheet, i + 2, asset.StoryId, asset.Version, volume.VolumeId, volume.Title, volume.Description, AssetPath(volume.PreviewImage));
            }
        }

        private static void BuildEpisodeDefine(ExcelPackage package, AuthoringAsset asset)
        {
            var sheet = AddSheet(package, "EpisodeDefine", "VolumeId", "EpisodeId", "Title", "Description", "PreviewImage", "EntryNodeId");
            var row = 2;
            foreach (var pair in Episodes(asset))
            {
                Write(sheet, row++, pair.volume.VolumeId, pair.episode.EpisodeId, pair.episode.Title, pair.episode.Description, AssetPath(pair.episode.PreviewImage), pair.episode.EntryNodeId);
            }
        }

        private static void BuildEpisodeExit(ExcelPackage package, AuthoringAsset asset)
        {
            var sheet = AddSheet(package, "EpisodeExit", "VolumeId", "EpisodeId", "ExitId", "DisplayName");
            var row = 2;
            foreach (var pair in Episodes(asset))
            {
                foreach (var node in pair.episode.Nodes.Where(x => x != null && (x.NodeKind == NodeKind.Choice || x.NodeKind == NodeKind.End)))
                {
                    Write(sheet, row++, pair.volume.VolumeId, pair.episode.EpisodeId, node.NodeId, node.Title);
                }
            }
        }

        private static void BuildRouteEdge(ExcelPackage package, AuthoringAsset asset)
        {
            var sheet = AddSheet(package, "RouteEdge", "VolumeId", "EdgeId", "SourceKind", "FromEpisodeId", "FromExitId", "ToEpisodeId");
            var row = 2;
            for (var volumeIndex = 0; volumeIndex < asset.Volumes.Count; volumeIndex++)
            {
                var volume = asset.Volumes[volumeIndex];
                for (var edgeIndex = 0; edgeIndex < (volume?.Route?.Edges.Count ?? 0); edgeIndex++)
                {
                    var edge = volume.Route.Edges[edgeIndex];
                    if (edge != null)
                    {
                        Write(sheet, row++, volume.VolumeId, edge.EdgeId, edge.SourceKind.ToString(), edge.FromEpisodeId, edge.FromExitId, edge.ToEpisodeId);
                    }
                }
            }
        }

        private static void BuildEpisodeData(ExcelPackage package, AuthoringAsset asset)
        {
            var sheet = AddSheet(
                package,
                "EpisodeData",
                "VolumeId", "EpisodeId", "RecordKind", "RecordId", "Title", "NodeKind",
                "FromNodeId", "FromPortId", "FromPortLabel", "TargetKind", "TargetNodeId",
                "ParametersJson", "ConditionsJson", "PositionX", "PositionY");
            var row = 2;
            foreach (var pair in Episodes(asset))
            {
                var placements = pair.episode.DetailLayout.Nodes
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.NodeId))
                    .ToDictionary(x => x.NodeId, StringComparer.Ordinal);
                for (var nodeIndex = 0; nodeIndex < pair.episode.Nodes.Count; nodeIndex++)
                {
                    var node = pair.episode.Nodes[nodeIndex];
                    if (node == null)
                    {
                        continue;
                    }

                    placements.TryGetValue(node.NodeId, out var placement);
                    Write(
                        sheet,
                        row++,
                        pair.volume.VolumeId,
                        pair.episode.EpisodeId,
                        "Node",
                        node.NodeId,
                        node.Title,
                        node.NodeKind.ToString(),
                        null, null, null, null, null,
                        ParametersJson(node.Parameters),
                        ConditionsJson(node.Conditions),
                        placement?.Position.x,
                        placement?.Position.y);
                }

                for (var edgeIndex = 0; edgeIndex < pair.episode.Edges.Count; edgeIndex++)
                {
                    var edge = pair.episode.Edges[edgeIndex];
                    if (edge != null)
                    {
                        Write(
                            sheet,
                            row++,
                            pair.volume.VolumeId,
                            pair.episode.EpisodeId,
                            "Edge",
                            edge.EdgeId,
                            null, null,
                            edge.FromNodeId,
                            edge.FromPortId,
                            edge.FromPortLabel,
                            edge.TargetKind.ToString(),
                            edge.TargetNodeId,
                            null,
                            ConditionsJson(edge.Conditions),
                            null, null);
                    }
                }
            }
        }

        private static void BuildRouteLayout(ExcelPackage package, AuthoringAsset asset)
        {
            var sheet = AddSheet(
                package,
                "RouteLayout",
                "VolumeId", "LayoutId", "Orientation",
                "BackgroundImage", "EditorGuideImage", "RootX", "RootY", "EpisodePlacementsJson");
            var row = 2;
            for (var volumeIndex = 0; volumeIndex < asset.Volumes.Count; volumeIndex++)
            {
                var volume = asset.Volumes[volumeIndex];
                for (var layoutIndex = 0; layoutIndex < (volume?.Layouts.Count ?? 0); layoutIndex++)
                {
                    var layout = volume.Layouts[layoutIndex];
                    if (layout == null)
                    {
                        continue;
                    }

                    var placements = layout.Episodes.Select(x => new PlacementData
                    {
                        Id = x?.EpisodeId,
                        X = x?.Position?.Position.x ?? 0f,
                        Y = x?.Position?.Position.y ?? 0f
                    }).ToArray();
                    Write(
                        sheet,
                        row++,
                        volume.VolumeId,
                        layout.LayoutId,
                        layout.Orientation.ToString(),
                        AssetPath(layout.BackgroundImage),
                        AssetPath(layout.EditorGuideImage),
                        layout.RootPlacement?.Position.x,
                        layout.RootPlacement?.Position.y,
                        JsonConvert.SerializeObject(placements));
                }
            }
        }

        private static void BuildRouteEdgePlacement(ExcelPackage package, AuthoringAsset asset)
        {
            var sheet = AddSheet(package, "RouteEdgePlacement", "VolumeId", "LayoutId", "EdgeId", "StyleKey", "ControlPointsJson");
            var row = 2;
            for (var volumeIndex = 0; volumeIndex < asset.Volumes.Count; volumeIndex++)
            {
                var volume = asset.Volumes[volumeIndex];
                for (var layoutIndex = 0; layoutIndex < (volume?.Layouts.Count ?? 0); layoutIndex++)
                {
                    var layout = volume.Layouts[layoutIndex];
                    for (var edgeIndex = 0; edgeIndex < (layout?.Edges.Count ?? 0); edgeIndex++)
                    {
                        var edge = layout.Edges[edgeIndex];
                        if (edge == null)
                        {
                            continue;
                        }

                        var points = edge.ControlPoints.Select(x => new PlacementData
                        {
                            X = x?.Position.x ?? 0f,
                            Y = x?.Position.y ?? 0f
                        }).ToArray();
                        Write(sheet, row++, volume.VolumeId, layout.LayoutId, edge.EdgeId, edge.StyleKey, JsonConvert.SerializeObject(points));
                    }
                }
            }
        }

        private static void BuildIdentityManifest(ExcelPackage package, IdentityManifest manifest)
        {
            var sheet = AddSheet(package, "IdentityManifest", "StoryId", "Version", "EntityKind", "EpisodeId", "IdentityId");
            var row = 2;
            for (var i = 0; i < manifest.EpisodeIds.Count; i++)
            {
                Write(sheet, row++, manifest.StoryId, manifest.Version, "Episode", null, manifest.EpisodeIds[i]);
            }

            for (var i = 0; i < manifest.EdgeIds.Count; i++)
            {
                Write(sheet, row++, manifest.StoryId, manifest.Version, "Edge", null, manifest.EdgeIds[i]);
            }

            for (var i = 0; i < manifest.Exits.Count; i++)
            {
                Write(sheet, row++, manifest.StoryId, manifest.Version, "Exit", manifest.Exits[i].EpisodeId, manifest.Exits[i].ExitId);
            }
        }

        private static IEnumerable<(AuthoringVolume volume, AuthoringEpisode episode)> Episodes(AuthoringAsset asset)
        {
            for (var volumeIndex = 0; volumeIndex < asset.Volumes.Count; volumeIndex++)
            {
                var volume = asset.Volumes[volumeIndex];
                for (var episodeIndex = 0; episodeIndex < (volume?.Episodes.Count ?? 0); episodeIndex++)
                {
                    if (volume.Episodes[episodeIndex] != null)
                    {
                        yield return (volume, volume.Episodes[episodeIndex]);
                    }
                }
            }
        }

        private static string ParametersJson(IReadOnlyList<AuthoringParameter> parameters)
        {
            return JsonConvert.SerializeObject((parameters ?? Array.Empty<AuthoringParameter>())
                .Where(x => x != null)
                .Select(x => new ParameterData { Key = x.Key, Value = x.Value }));
        }

        private static string ConditionsJson(IReadOnlyList<AuthoringCondition> conditions)
        {
            return JsonConvert.SerializeObject((conditions ?? Array.Empty<AuthoringCondition>())
                .Where(x => x != null)
                .Select(x => new ConditionData
                {
                    ConditionId = x.ConditionId,
                    Parameters = x.Parameters.Where(y => y != null).Select(y => new ParameterData { Key = y.Key, Value = y.Value }).ToArray()
                }));
        }

        private static ExcelWorksheet AddSheet(ExcelPackage package, string name, params string[] headers)
        {
            var sheet = package.Workbook.Worksheets.Add(name);
            Write(sheet, 1, headers.Cast<object>().ToArray());
            return sheet;
        }

        private static void Write(ExcelWorksheet sheet, int row, params object[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                sheet.Cells[row, i + 1].Value = values[i];
            }
        }

        private static string AssetPath(UnityEngine.Object asset)
        {
            return asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset) ?? string.Empty;
        }

        [Serializable]
        private sealed class ParameterData
        {
            public string Key;
            public string Value;
        }

        [Serializable]
        private sealed class ConditionData
        {
            public string ConditionId;
            public ParameterData[] Parameters;
        }

        [Serializable]
        private sealed class PlacementData
        {
            public string Id;
            public float X;
            public float Y;
        }
    }
}
