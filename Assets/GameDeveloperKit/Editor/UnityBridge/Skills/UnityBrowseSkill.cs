using System;
using System.Collections.Generic;
using System.Linq;
using SysIO = System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityBrowseSkill : IUnityBridgeSkill
    {
        public string Name => "unity-browse";

        public string Description =>
            "Browse the Unity project directory structure: list files and folders under a given project path, with metadata.";

        public string Trigger =>
            "Use when the user wants to explore the project structure, list files in a directory, see what assets exist, or navigate the project folder tree.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/browse?path=Assets/&depth=1&skipMeta=true", "Lists files and folders under the given project-relative path."),
            new UnityBridgeSkillEndpoint("GET", "/browse/tree?path=Assets/Scripts&depth=2", "Returns a tree structure of the directory.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js browse --path Assets/`",
            "`node scripts/unity-bridge.js browse --path Assets/Scripts --depth 2`",
            "`node scripts/unity-bridge.js browse-tree --path Assets/Editor`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Paths are relative to the project root. Use 'Assets/' to start browsing.",
            "Files ending in .meta are typically skipped unless skipMeta=false.",
            "The tree format helps understand project structure at a glance."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return path == "browse" || path == "browse/tree";
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return path switch
            {
                "browse" => Browse(request),
                "browse/tree" => BrowseTree(request),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{path}")
            };
        }

        private static UnityBridgeSkillResponse Browse(UnityBridgeSkillRequest request)
        {
            var query = ParseQuery(request.QueryString);
            var browsePath = query.GetValueOrDefault("path", "Assets");
            var depthStr = query.GetValueOrDefault("depth", "1");
            var skipMeta = !query.GetValueOrDefault("skipMeta", "true").Equals("false", StringComparison.OrdinalIgnoreCase);

            if (!int.TryParse(depthStr, out var depth) || depth < 1) depth = 1;
            depth = Math.Min(depth, 5);

            var fullPath = SysIO.Path.Combine(Application.dataPath, "..", browsePath);
            fullPath = SysIO.Path.GetFullPath(fullPath);

            if (!SysIO.Directory.Exists(fullPath))
                return UnityBridgeSkillResponse.Error(404, $"Directory not found: {browsePath}");

            var results = new List<string>();
            CollectEntries(fullPath, browsePath, depth, skipMeta, results);

            return UnityBridgeSkillResponse.Success($"{{\"path\":\"{Esc(browsePath)}\",\"entries\":[{string.Join(",", results)}],\"count\":{results.Count}}}");
        }

        private static UnityBridgeSkillResponse BrowseTree(UnityBridgeSkillRequest request)
        {
            var query = ParseQuery(request.QueryString);
            var browsePath = query.GetValueOrDefault("path", "Assets");
            var depthStr = query.GetValueOrDefault("depth", "2");
            var skipMeta = !query.GetValueOrDefault("skipMeta", "true").Equals("false", StringComparison.OrdinalIgnoreCase);

            if (!int.TryParse(depthStr, out var depth) || depth < 1) depth = 2;
            depth = Math.Min(depth, 4);

            var fullPath = SysIO.Path.Combine(Application.dataPath, "..", browsePath);
            fullPath = SysIO.Path.GetFullPath(fullPath);

            if (!SysIO.Directory.Exists(fullPath))
                return UnityBridgeSkillResponse.Error(404, $"Directory not found: {browsePath}");

            var tree = BuildTree(fullPath, browsePath, depth, skipMeta, "");
            var parts = tree.Select(t => $"\"{Esc(t)}\"").ToArray();
            return UnityBridgeSkillResponse.Success($"{{\"path\":\"{Esc(browsePath)}\",\"tree\":[{string.Join(",", parts)}]}}");
        }

        private static void CollectEntries(string fullPath, string relativePath, int depth, bool skipMeta, List<string> results)
        {
            try
            {
                foreach (var dir in SysIO.Directory.GetDirectories(fullPath))
                {
                    var name = SysIO.Path.GetFileName(dir);
                    if (skipMeta && (name.StartsWith(".") || name == "Library" || name == "Temp" || name == "obj" || name == "Logs" || name == "UserSettings" || name == "Build"))
                        continue;

                    var relDir = string.IsNullOrEmpty(relativePath) ? name : relativePath + "/" + name;
                    results.Add($"{{\"name\":\"{Esc(name)}\",\"path\":\"{Esc(relDir)}\",\"type\":\"folder\"}}");

                    if (depth > 1)
                        CollectEntries(dir, relDir, depth - 1, skipMeta, results);
                }

                foreach (var file in SysIO.Directory.GetFiles(fullPath))
                {
                    var name = SysIO.Path.GetFileName(file);
                    if (skipMeta && (name.EndsWith(".meta") || name.EndsWith(".csproj") || name.EndsWith(".sln")))
                        continue;

                    var relFile = string.IsNullOrEmpty(relativePath) ? name : relativePath + "/" + name;
                    var ext = SysIO.Path.GetExtension(name).ToLowerInvariant();
                    var assetType = GetAssetKind(ext);
                    var size = new SysIO.FileInfo(file).Length;

                    results.Add($"{{\"name\":\"{Esc(name)}\",\"path\":\"{Esc(relFile)}\",\"type\":\"{Esc(assetType)}\",\"extension\":\"{Esc(ext)}\",\"sizeBytes\":{size}}}");
                }
            }
            catch { /* skip inaccessible directories */ }
        }

        private static List<string> BuildTree(string fullPath, string relativePath, int depth, bool skipMeta, string indent)
        {
            var lines = new List<string>();
            var dirName = SysIO.Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(dirName)) dirName = relativePath;
            lines.Add($"{indent}{dirName}/");

            if (depth <= 0) return lines;

            try
            {
                foreach (var dir in SysIO.Directory.GetDirectories(fullPath).OrderBy(d => d))
                {
                    var name = SysIO.Path.GetFileName(dir);
                    if (skipMeta && (name.StartsWith(".") || name == "Library" || name == "Temp" || name == "obj" || name == "Logs" || name == "UserSettings" || name == "Build"))
                        continue;
                    lines.AddRange(BuildTree(dir, "", depth - 1, skipMeta, indent + "  "));
                }

                foreach (var file in SysIO.Directory.GetFiles(fullPath).OrderBy(f => f).Take(50))
                {
                    var name = SysIO.Path.GetFileName(file);
                    if (skipMeta && (name.EndsWith(".meta") || name.EndsWith(".csproj") || name.EndsWith(".sln")))
                        continue;
                    lines.Add($"{indent}  {name}");
                }
            }
            catch { }

            return lines;
        }

        private static string GetAssetKind(string ext)
        {
            return ext switch
            {
                ".cs" => "script",
                ".prefab" => "prefab",
                ".unity" => "scene",
                ".mat" => "material",
                ".png" or ".jpg" or ".jpeg" or ".tga" or ".psd" or ".bmp" => "texture",
                ".fbx" or ".obj" or ".blend" => "model",
                ".anim" => "animation",
                ".controller" => "animator",
                ".shader" or ".shadergraph" => "shader",
                ".asset" => "asset",
                ".wav" or ".mp3" or ".ogg" => "audio",
                ".ttf" or ".otf" => "font",
                ".uss" or ".uxml" => "ui",
                "" => "folder",
                _ => "file"
            };
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query)) return result;
            query = query.TrimStart('?');
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                    result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
            return result;
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
