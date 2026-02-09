using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class TextureListResult
    {
        public List<TextureInfo> textures = new();
    }

    [Serializable]
    public class TextureInfo
    {
        public string name;
        public string path;
        public string guid;
        public int width;
        public int height;
        public string format;
        public string textureType;
    }

    [Serializable]
    public class TextureOperationResult
    {
        public bool success;
        public string message;
        public string path;
    }

    public class TextureHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_list_textures",
                "unity_get_texture",
                "unity_update_texture"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_list_textures" => ListTextures(args),
                "unity_get_texture" => GetTexture(args),
                "unity_update_texture" => UpdateTexture(args),
                _ => JsonConvert.SerializeObject(new TextureOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string ListTextures(JObject args)
        {
            var pathFilter = args["path"]?.ToString();
            var typeFilter = args["type"]?.ToString();
            var guids = AssetDatabase.FindAssets("t:Texture2D");
            var result = new TextureListResult();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(pathFilter) && !path.StartsWith(pathFilter))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                if (!string.IsNullOrEmpty(typeFilter))
                {
                    if (!importer.textureType.ToString().Equals(typeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;

                result.textures.Add(new TextureInfo
                {
                    name = texture.name,
                    path = path,
                    guid = guid,
                    width = texture.width,
                    height = texture.height,
                    format = texture.format.ToString(),
                    textureType = importer.textureType.ToString()
                });
            }

            return JsonConvert.SerializeObject(result);
        }

        private string GetTexture(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new TextureOperationResult { success = false, message = "path is required" });
            }
            
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (texture == null)
            {
                return JsonConvert.SerializeObject(new TextureOperationResult { success = false, message = $"Texture not found: {path}" });
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var guid = AssetDatabase.AssetPathToGUID(path);

            var info = new
            {
                name = texture.name,
                path = path,
                guid = guid,
                width = texture.width,
                height = texture.height,
                format = texture.format.ToString(),
                textureType = importer?.textureType.ToString() ?? "Unknown",
                maxSize = importer?.maxTextureSize ?? 0,
                compression = importer?.textureCompression.ToString() ?? "Unknown",
                mipmapEnabled = importer?.mipmapEnabled ?? false,
                filterMode = texture.filterMode.ToString(),
                wrapMode = texture.wrapMode.ToString()
            };

            return JsonConvert.SerializeObject(info);
        }

        private string UpdateTexture(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new TextureOperationResult { success = false, message = "path is required" });
            }
            
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                return JsonConvert.SerializeObject(new TextureOperationResult { success = false, message = $"Texture not found: {path}" });
            }

            try
            {
                var changes = new List<string>();

                var textureType = args["textureType"]?.ToString();
                if (!string.IsNullOrEmpty(textureType) && Enum.TryParse<TextureImporterType>(textureType, true, out var type))
                {
                    importer.textureType = type;
                    changes.Add($"textureType={type}");
                }

                var maxSize = args["maxSize"]?.ToObject<int?>() ?? 0;
                if (maxSize > 0)
                {
                    importer.maxTextureSize = maxSize;
                    changes.Add($"maxSize={maxSize}");
                }

                var compression = args["compression"]?.ToString();
                if (!string.IsNullOrEmpty(compression) && Enum.TryParse<TextureImporterCompression>(compression, true, out var comp))
                {
                    importer.textureCompression = comp;
                    changes.Add($"compression={comp}");
                }

                importer.SaveAndReimport();

                return JsonConvert.SerializeObject(new TextureOperationResult
                {
                    success = true,
                    message = $"Updated: {string.Join(", ", changes)}",
                    path = path
                });
            }
            catch (Exception e)
            {
                return JsonConvert.SerializeObject(new TextureOperationResult { success = false, message = $"Failed to update texture: {e.Message}" });
            }
        }
    }
}
