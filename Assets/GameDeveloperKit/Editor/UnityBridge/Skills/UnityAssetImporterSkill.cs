using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SysIO = System.IO;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityAssetImporterSkill : IUnityBridgeSkill
    {
        public string Name => "unity-asset-importer";

        public string Description =>
            "Configure asset import settings: texture compression, sprite slicing, model import options, audio settings. " +
            "This is the gateway to controlling how Unity processes imported assets.";

        public string Trigger =>
            "Use when the user wants to change import settings, slice sprites from a texture sheet, " +
            "configure model import scale/materials/animation, set audio compression, or batch-modify importer settings.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/importer?path=Assets/tex.png", "Returns the asset importer type and key settings."),
            new UnityBridgeSkillEndpoint("POST", "/importer/texture", "Configures TextureImporter settings.",
                "{\"path\":\"Assets/tex.png\",\"maxSize\":1024,\"textureType\":\"Sprite\",\"mipmap\":false}"),
            new UnityBridgeSkillEndpoint("POST", "/importer/model", "Configures ModelImporter settings.",
                "{\"path\":\"Assets/model.fbx\",\"importMaterials\":false,\"meshCompression\":\"Medium\",\"scaleFactor\":1}"),
            new UnityBridgeSkillEndpoint("POST", "/importer/sprite/slice", "Slices a texture into sprites (automatic or grid).",
                "{\"path\":\"Assets/sheet.png\",\"mode\":\"Automatic\",\"pivot\":[0.5,0.5],\"gridSize\":[32,32]}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js importer --path Assets/tex.png`",
            "`node scripts/unity-bridge.js importer-texture --path Assets/tex.png --maxSize 1024 --type Sprite`",
            "`node scripts/unity-bridge.js sprite-slice --path Assets/sheet.png --mode Grid --gridSize 32,32`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Sprite slicing requires the texture to be set to Sprite type first.",
            "Setting importer values automatically calls SaveAndReimport.",
            "Model scale factor is applied globally; use 1 for metric, 0.01 for cm-to-m."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return (request.Method == "GET" && path == "importer")
                || (request.Method == "POST" && path == "importer/texture")
                || (request.Method == "POST" && path == "importer/model")
                || (request.Method == "POST" && path == "importer/sprite/slice");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return path switch
            {
                "importer" => GetImporter(request),
                "importer/texture" => SetTextureImporter(request),
                "importer/model" => SetModelImporter(request),
                "importer/sprite/slice" => SliceSprite(request),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{path}")
            };
        }

        private static UnityBridgeSkillResponse GetImporter(UnityBridgeSkillRequest request)
        {
            var query = ParseQuery(request.QueryString);
            var assetPath = query.GetValueOrDefault("path", "");
            if (string.IsNullOrWhiteSpace(assetPath))
                return UnityBridgeSkillResponse.Error(400, "Missing 'path' parameter");

            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
                return UnityBridgeSkillResponse.Error(404, $"Asset not found or no importer: {assetPath}");

            var importerType = importer.GetType().Name;

            var info = $"{{\"path\":\"{Esc(assetPath)}\",\"importerType\":\"{Esc(importerType)}\"";

            if (importer is TextureImporter ti)
            {
                info += $",\"textureType\":\"{ti.textureType}\",\"maxSize\":{ti.maxTextureSize},\"mipmap\":{(ti.mipmapEnabled ? "true" : "false")},\"spriteMode\":\"{ti.spriteImportMode}\",\"isReadable\":{(ti.isReadable ? "true" : "false")}";
            }
            else if (importer is ModelImporter mi)
            {
                info += $",\"materialImportMode\":\"{mi.materialImportMode}\",\"importAnimation\":{(mi.importAnimation ? "true" : "false")},\"meshCompression\":\"{mi.meshCompression}\",\"globalScale\":{mi.globalScale:F4},\"isReadable\":{(mi.isReadable ? "true" : "false")}";
            }
            else if (importer is AudioImporter ai)
            {
                info += $",\"forceToMono\":{(ai.forceToMono ? "true" : "false")},\"loadInBackground\":{(ai.loadInBackground ? "true" : "false")}";
            }

            info += "}";
            return UnityBridgeSkillResponse.Success(info);
        }

        private static UnityBridgeSkillResponse SetTextureImporter(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var assetPath = body?.GetValueOrDefault("path", "");
            if (string.IsNullOrWhiteSpace(assetPath))
                return UnityBridgeSkillResponse.Error(400, "Missing 'path' field");

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return UnityBridgeSkillResponse.Error(404, $"Texture not found: {assetPath}");

            var changed = new List<string>();

            if (body.TryGetValue("textureType", out var tt) && Enum.TryParse<TextureImporterType>(tt, true, out var ttype))
            { importer.textureType = ttype; changed.Add("textureType"); }

            if (body.TryGetValue("spriteMode", out var sm))
            {
                if (sm.Equals("Single", StringComparison.OrdinalIgnoreCase)) importer.spriteImportMode = SpriteImportMode.Single;
                else if (sm.Equals("Multiple", StringComparison.OrdinalIgnoreCase)) importer.spriteImportMode = SpriteImportMode.Multiple;
                else if (sm.Equals("Polygon", StringComparison.OrdinalIgnoreCase)) importer.spriteImportMode = SpriteImportMode.Polygon;
                changed.Add("spriteImportMode");
            }

            if (body.TryGetValue("maxSize", out var ms) && int.TryParse(ms, out var maxSize))
            { importer.maxTextureSize = Mathf.Clamp(maxSize, 32, 8192); changed.Add("maxTextureSize"); }

            if (body.TryGetValue("mipmap", out var mm))
            { importer.mipmapEnabled = !mm.Equals("false", StringComparison.OrdinalIgnoreCase) && mm != "0"; changed.Add("mipmapEnabled"); }

            if (body.TryGetValue("isReadable", out var rd))
            { importer.isReadable = !rd.Equals("false", StringComparison.OrdinalIgnoreCase) && rd != "0"; changed.Add("isReadable"); }

            if (body.TryGetValue("filterMode", out var fm) && Enum.TryParse<FilterMode>(fm, true, out var filter))
            { importer.filterMode = filter; changed.Add("filterMode"); }

            if (body.TryGetValue("wrapMode", out var wm) && Enum.TryParse<TextureWrapMode>(wm, true, out var wrap))
            { importer.wrapMode = wrap; changed.Add("wrapMode"); }

            if (body.TryGetValue("compressionQuality", out var cq) && int.TryParse(cq, out var qual))
            { importer.compressionQuality = Mathf.Clamp(qual, 0, 100); changed.Add("compressionQuality"); }

            if (changed.Count > 0)
            {
                importer.SaveAndReimport();
                AssetDatabase.Refresh();
            }

            return UnityBridgeSkillResponse.Success(
                $"{{\"configured\":true,\"path\":\"{Esc(assetPath)}\",\"changed\":[{string.Join(",", changed.Select(c => $"\"{Esc(c)}\""))}]}}");
        }

        private static UnityBridgeSkillResponse SetModelImporter(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var assetPath = body?.GetValueOrDefault("path", "");
            if (string.IsNullOrWhiteSpace(assetPath))
                return UnityBridgeSkillResponse.Error(400, "Missing 'path' field");

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
                return UnityBridgeSkillResponse.Error(404, $"Model not found: {assetPath}");

            var changed = new List<string>();

            if (body.TryGetValue("importMaterials", out var im))
            { importer.materialImportMode = im.Equals("false", StringComparison.OrdinalIgnoreCase) || im == "0" ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportViaMaterialDescription; changed.Add("materialImportMode"); }

            if (body.TryGetValue("importAnimation", out var ia))
            { importer.importAnimation = !ia.Equals("false", StringComparison.OrdinalIgnoreCase) && ia != "0"; changed.Add("importAnimation"); }

            if (body.TryGetValue("meshCompression", out var mc) && Enum.TryParse<ModelImporterMeshCompression>(mc, true, out var comp))
            { importer.meshCompression = comp; changed.Add("meshCompression"); }

            if (body.TryGetValue("scaleFactor", out var sf) && float.TryParse(sf, out var scale))
            { importer.globalScale = scale; changed.Add("globalScale"); }

            if (body.TryGetValue("isReadable", out var rd))
            { importer.isReadable = !rd.Equals("false", StringComparison.OrdinalIgnoreCase) && rd != "0"; changed.Add("isReadable"); }

            if (body.TryGetValue("importBlendShapes", out var ib))
            { importer.importBlendShapes = !ib.Equals("false", StringComparison.OrdinalIgnoreCase) && ib != "0"; changed.Add("importBlendShapes"); }

            if (changed.Count > 0)
            {
                importer.SaveAndReimport();
                AssetDatabase.Refresh();
            }

            return UnityBridgeSkillResponse.Success(
                $"{{\"configured\":true,\"path\":\"{Esc(assetPath)}\",\"changed\":[{string.Join(",", changed.Select(c => $"\"{Esc(c)}\""))}]}}");
        }

        private static UnityBridgeSkillResponse SliceSprite(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var assetPath = body?.GetValueOrDefault("path", "");
            if (string.IsNullOrWhiteSpace(assetPath))
                return UnityBridgeSkillResponse.Error(400, "Missing 'path' field");

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return UnityBridgeSkillResponse.Error(404, $"Texture not found: {assetPath}");

            var mode = body?.GetValueOrDefault("mode", "Automatic");
            var pivotX = ParseFloat(body, "pivotX", 0.5f);
            var pivotY = ParseFloat(body, "pivotY", 0.5f);

            if (ParseVec2(body, "pivot", out var pv))
            {
                pivotX = pv.x;
                pivotY = pv.y;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            if (mode.Equals("Grid", StringComparison.OrdinalIgnoreCase))
            {
                var gridW = ParseFloat(body, "gridWidth", 32f);
                var gridH = ParseFloat(body, "gridHeight", 32f);
                if (ParseVec2(body, "gridSize", out var gs))
                {
                    gridW = gs.x;
                    gridH = gs.y;
                }

                var cols = body?.GetValueOrDefault("columns", "");
                var rows = body?.GetValueOrDefault("rows", "");

                // Load the texture to get actual dimensions
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                var texW = tex != null ? tex.width : 512;
                var texH = tex != null ? tex.height : 512;

                if (!string.IsNullOrWhiteSpace(cols) && int.TryParse(cols, out var colCount) && colCount > 0)
                    gridW = (float)texW / colCount;
                if (!string.IsNullOrWhiteSpace(rows) && int.TryParse(rows, out var rowCount) && rowCount > 0)
                    gridH = (float)texH / rowCount;

                var sheet = new List<SpriteMetaData>();
                var xCells = Mathf.Max(1, Mathf.FloorToInt(texW / gridW));
                var yCells = Mathf.Max(1, Mathf.FloorToInt(texH / gridH));
                var idx = 0;

                for (int y = 0; y < yCells; y++)
                {
                    for (int x = 0; x < xCells; x++)
                    {
                        sheet.Add(new SpriteMetaData
                        {
                            name = $"{SysIO.Path.GetFileNameWithoutExtension(assetPath)}_{idx++}",
                            rect = new Rect(x * gridW, texH - (y + 1) * gridH, gridW, gridH),
                            pivot = new Vector2(pivotX, pivotY),
                            alignment = (int)SpriteAlignment.Custom
                        });
                    }
                }

                importer.spritesheet = sheet.ToArray();
            }
            else
            {
                // Automatic mode: tell Unity to auto-detect sprites
                importer.spritesheet = Array.Empty<SpriteMetaData>();
            }

            importer.SaveAndReimport();
            AssetDatabase.Refresh();

            return UnityBridgeSkillResponse.Success(
                $"{{\"sliced\":true,\"path\":\"{Esc(assetPath)}\",\"mode\":\"{Esc(mode)}\",\"pivot\":[{pivotX:F2},{pivotY:F2}]}}");
        }

        private static float ParseFloat(Dictionary<string, string> body, string key, float fallback)
        {
            if (body != null && body.TryGetValue(key, out var val) && float.TryParse(val, out var result))
                return result;
            return fallback;
        }

        private static bool ParseVec2(Dictionary<string, string> body, string key, out Vector2 v)
        {
            v = Vector2.zero;
            if (body == null || !body.TryGetValue(key, out var val)) return false;
            var parts = val.Trim('[', ']').Split(',');
            if (parts.Length == 2 && float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y))
            { v = new Vector2(x, y); return true; }
            return false;
        }

        private static Dictionary<string, string> ParseQuery(string q)
        {
            var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(q)) return r;
            q = q.TrimStart('?');
            foreach (var p in q.Split('&'))
            {
                var kv = p.Split('=');
                if (kv.Length == 2) r[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
            }
            return r;
        }

        private static Dictionary<string, string> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<Body>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.path != null) d["path"] = obj.path;
                if (obj.textureType != null) d["textureType"] = obj.textureType;
                if (obj.spriteMode != null) d["spriteMode"] = obj.spriteMode;
                if (obj.maxSize != null) d["maxSize"] = obj.maxSize;
                if (obj.mipmap != null) d["mipmap"] = obj.mipmap;
                if (obj.isReadable != null) d["isReadable"] = obj.isReadable;
                if (obj.filterMode != null) d["filterMode"] = obj.filterMode;
                if (obj.wrapMode != null) d["wrapMode"] = obj.wrapMode;
                if (obj.compressionQuality != null) d["compressionQuality"] = obj.compressionQuality;
                if (obj.importMaterials != null) d["importMaterials"] = obj.importMaterials;
                if (obj.importAnimation != null) d["importAnimation"] = obj.importAnimation;
                if (obj.meshCompression != null) d["meshCompression"] = obj.meshCompression;
                if (obj.scaleFactor != null) d["scaleFactor"] = obj.scaleFactor;
                if (obj.importBlendShapes != null) d["importBlendShapes"] = obj.importBlendShapes;
                if (obj.mode != null) d["mode"] = obj.mode;
                if (obj.pivot != null) d["pivot"] = obj.pivot;
                if (obj.gridSize != null) d["gridSize"] = obj.gridSize;
                if (obj.gridWidth != null) d["gridWidth"] = obj.gridWidth;
                if (obj.gridHeight != null) d["gridHeight"] = obj.gridHeight;
                if (obj.columns != null) d["columns"] = obj.columns;
                if (obj.rows != null) d["rows"] = obj.rows;
                return d;
            }
            catch { return null; }
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable] private class Body
        {
            public string path, textureType, spriteMode, maxSize, mipmap, isReadable;
            public string filterMode, wrapMode, compressionQuality;
            public string importMaterials, importAnimation, meshCompression, scaleFactor, importBlendShapes;
            public string mode, pivot, gridSize, gridWidth, gridHeight, columns, rows;
        }
    }
}
