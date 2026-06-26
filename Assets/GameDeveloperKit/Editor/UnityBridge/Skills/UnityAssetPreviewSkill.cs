using System;
using System.Collections.Generic;
using System.IO;
using SysIO = System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityAssetPreviewSkill : IUnityBridgeSkill
    {
        public string Name => "unity-asset-preview";

        public string Description =>
            "Generate preview thumbnail images for project assets. Save to file or return as base64.";

        public string Trigger =>
            "Use when the user wants to preview an asset, see a thumbnail of a texture/material/prefab, or get a visual of any project asset.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("POST", "/preview", "Generates a preview for an asset at the given path.", "{\"path\":\"Assets/MyTexture.png\",\"output\":\"Assets/preview.png\",\"width\":256,\"height\":256}"),
            new UnityBridgeSkillEndpoint("GET", "/preview/info?path=Assets/...", "Returns info about an asset including its type and whether a preview is available.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js preview --path Assets/MyPrefab.prefab --output Assets/preview.png`",
            "`node scripts/unity-bridge.js preview --path Assets/MyTexture.jpg --width 128`",
            "`node scripts/unity-bridge.js preview-info --path Assets/MyPrefab.prefab`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Unity generates previews for many asset types but not all. Scripts and some data assets may not have previews.",
            "The output is always a PNG file.",
            "Generated previews respect the asset's import settings and may differ from runtime appearance."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return (request.Method == "POST" && path == "preview")
                || (request.Method == "GET" && path == "preview/info");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return path switch
            {
                "preview" => GeneratePreview(request),
                "preview/info" => GetAssetInfo(request),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{path}")
            };
        }

        private static UnityBridgeSkillResponse GeneratePreview(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var assetPath = body?.GetValueOrDefault("path", "");
            var outputPath = body?.GetValueOrDefault("output", "");

            if (string.IsNullOrWhiteSpace(assetPath))
                return UnityBridgeSkillResponse.Error(400, "Missing 'path' field");

            var width = ParseInt(body, "width", 256);
            var height = ParseInt(body, "height", 256);

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
                return UnityBridgeSkillResponse.Error(404, $"Asset not found: {assetPath}");

            // Try to get cached preview first
            var previewTex = AssetPreview.GetAssetPreview(asset);
            if (previewTex == null)
            {
                // Force generation
                var instanceId = asset.GetInstanceID();
                var needsGen = AssetPreview.IsLoadingAssetPreview(instanceId);
                var tries = 0;
                while (AssetPreview.IsLoadingAssetPreview(instanceId) && tries < 30)
                {
                    System.Threading.Thread.Sleep(100);
                    tries++;
                }

                previewTex = AssetPreview.GetAssetPreview(asset);
            }

            if (previewTex == null)
            {
                // Fallback: use the MiniThumbnail
                previewTex = AssetPreview.GetMiniThumbnail(asset);
            }

            if (previewTex == null)
                return UnityBridgeSkillResponse.Error(404, $"No preview available for asset type: {asset.GetType().Name}");

            try
            {
                var rt = RenderTexture.GetTemporary(width, height);
                var previousActive = RenderTexture.active;
                RenderTexture.active = rt;
                Graphics.Blit(previewTex, rt);

                var resultTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                resultTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                resultTex.Apply();

                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);

                var bytes = resultTex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(resultTex);

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    outputPath = $"Assets/unity_bridge_preview_{System.IO.Path.GetFileNameWithoutExtension(assetPath)}_{DateTime.Now:HHmmss}.png";
                }

                if (!outputPath.EndsWith(".png")) outputPath += ".png";
                var dir = System.IO.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(dir) && !SysIO.Directory.Exists(dir))
                    SysIO.Directory.CreateDirectory(dir);

                SysIO.File.WriteAllBytes(outputPath, bytes);
                AssetDatabase.Refresh();

                return UnityBridgeSkillResponse.Success(
                    $"{{\"generated\":true,\"path\":\"{Esc(outputPath)}\",\"width\":{width},\"height\":{height},\"sizeBytes\":{bytes.Length}}}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Preview generation failed: {ex.Message}");
            }
        }

        private static UnityBridgeSkillResponse GetAssetInfo(UnityBridgeSkillRequest request)
        {
            var query = ParseQuery(request.QueryString);
            var assetPath = query.GetValueOrDefault("path", "");

            if (string.IsNullOrWhiteSpace(assetPath))
                return UnityBridgeSkillResponse.Error(400, "Missing 'path' query parameter");

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
                return UnityBridgeSkillResponse.Error(404, $"Asset not found: {assetPath}");

            var typeName = asset.GetType().Name;
            var hasPreview = AssetPreview.GetAssetPreview(asset) != null;
            var hasMini = AssetPreview.GetMiniThumbnail(asset) != null;
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var importType = AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.Name ?? "Unknown";

            return UnityBridgeSkillResponse.Success(
                $"{{\"path\":\"{Esc(assetPath)}\",\"name\":\"{Esc(asset.name)}\",\"type\":\"{Esc(typeName)}\",\"importType\":\"{Esc(importType)}\",\"guid\":\"{Esc(guid)}\",\"hasPreview\":{(hasPreview ? "true" : "false")},\"hasThumbnail\":{(hasMini ? "true" : "false")}}}");
        }

        private static int ParseInt(Dictionary<string, string> body, string key, int fallback)
        {
            if (body != null && body.TryGetValue(key, out var val) && int.TryParse(val, out var result))
                return Math.Clamp(result, 32, 2048);
            return fallback;
        }

        private static Dictionary<string, string> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<PreviewBody>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.path != null) d["path"] = obj.path;
                if (obj.output != null) d["output"] = obj.output;
                if (obj.width != null) d["width"] = obj.width;
                if (obj.height != null) d["height"] = obj.height;
                return d;
            }
            catch { return null; }
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
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private class PreviewBody { public string path; public string output; public string width; public string height; }
    }
}
