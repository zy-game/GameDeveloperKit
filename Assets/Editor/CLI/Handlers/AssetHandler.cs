using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class AssetSearchResult
    {
        public List<AssetInfo> assets = new();
    }

    [Serializable]
    public class AssetInfo
    {
        public string name;
        public string path;
        public string guid;
        public string type;
    }

    [Serializable]
    public class AssetOperationResult
    {
        public bool success;
        public string message;
        public string path;
        public string guid;
    }

    [Serializable]
    public class AssetReferencesResult
    {
        public string assetPath;
        public List<string> referencedBy = new();
    }

    public class AssetHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_search_assets",
                "unity_get_asset",
                "unity_delete_asset",
                "unity_move_asset",
                "unity_copy_asset",
                "unity_rename_asset",
                "unity_find_references"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_search_assets" => SearchAssets(args),
                "unity_get_asset" => GetAsset(args),
                "unity_delete_asset" => DeleteAsset(args),
                "unity_move_asset" => MoveAsset(args),
                "unity_copy_asset" => CopyAsset(args),
                "unity_rename_asset" => RenameAsset(args),
                "unity_find_references" => FindReferences(args),
                _ => JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string SearchAssets(JObject args)
        {
            var filter = args["filter"]?.ToString() ?? "";
            var pathFilter = args["path"]?.ToString();
            
            string[] searchFolders = null;
            if (!string.IsNullOrEmpty(pathFilter))
            {
                searchFolders = new[] { pathFilter };
            }

            var guids = searchFolders != null 
                ? AssetDatabase.FindAssets(filter, searchFolders)
                : AssetDatabase.FindAssets(filter);
                
            var result = new AssetSearchResult();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);

                result.assets.Add(new AssetInfo
                {
                    name = asset?.name ?? Path.GetFileNameWithoutExtension(path),
                    path = path,
                    guid = guid,
                    type = asset?.GetType().Name ?? "Unknown"
                });
            }

            return JsonConvert.SerializeObject(result);
        }

        private string GetAsset(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = "path is required" });
            }
            
            var asset = AssetDatabase.LoadMainAssetAtPath(path);

            if (asset == null)
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = $"Asset not found: {path}" });
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            var info = new
            {
                name = asset.name,
                path = path,
                guid = guid,
                type = asset.GetType().Name,
                fullType = asset.GetType().FullName
            };

            return JsonConvert.SerializeObject(info);
        }

        private string DeleteAsset(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = "path is required" });
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = $"Asset not found: {path}" });
            }

            var success = AssetDatabase.DeleteAsset(path);

            return JsonConvert.SerializeObject(new AssetOperationResult
            {
                success = success,
                message = success ? "Asset deleted" : "Failed to delete asset",
                path = path
            });
        }

        private string MoveAsset(JObject args)
        {
            var sourcePath = args["source"]?.ToString();
            var destPath = args["destination"]?.ToString();
            
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destPath))
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = "source and destination are required" });
            }

            var error = AssetDatabase.MoveAsset(sourcePath, destPath);

            if (!string.IsNullOrEmpty(error))
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = error });
            }

            var guid = AssetDatabase.AssetPathToGUID(destPath);

            return JsonConvert.SerializeObject(new AssetOperationResult { success = true, message = "Asset moved", path = destPath, guid = guid });
        }

        private string CopyAsset(JObject args)
        {
            var sourcePath = args["source"]?.ToString();
            var destPath = args["destination"]?.ToString();
            
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destPath))
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = "source and destination are required" });
            }

            var success = AssetDatabase.CopyAsset(sourcePath, destPath);

            if (!success)
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = "Failed to copy asset" });
            }

            var guid = AssetDatabase.AssetPathToGUID(destPath);

            return JsonConvert.SerializeObject(new AssetOperationResult { success = true, message = "Asset copied", path = destPath, guid = guid });
        }

        private string RenameAsset(JObject args)
        {
            var path = args["path"]?.ToString();
            var newName = args["newName"]?.ToString();
            
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(newName))
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = "path and newName are required" });
            }

            var error = AssetDatabase.RenameAsset(path, newName);

            if (!string.IsNullOrEmpty(error))
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = error });
            }

            var dir = Path.GetDirectoryName(path);
            var ext = Path.GetExtension(path);
            var newPath = Path.Combine(dir, newName + ext);
            var guid = AssetDatabase.AssetPathToGUID(newPath);

            return JsonConvert.SerializeObject(new AssetOperationResult { success = true, message = "Asset renamed", path = newPath, guid = guid });
        }

        private string FindReferences(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = "path is required" });
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                return JsonConvert.SerializeObject(new AssetOperationResult { success = false, message = $"Asset not found: {path}" });
            }

            var result = new AssetReferencesResult { assetPath = path };

            var allAssets = AssetDatabase.GetAllAssetPaths();
            foreach (var assetPath in allAssets)
            {
                if (assetPath == path) continue;
                if (!assetPath.StartsWith("Assets/")) continue;

                var dependencies = AssetDatabase.GetDependencies(assetPath, false);
                if (dependencies.Contains(path))
                {
                    result.referencedBy.Add(assetPath);
                }
            }

            return JsonConvert.SerializeObject(result);
        }
    }
}
