using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class AudioListResult
    {
        public List<AudioInfo> clips = new();
    }

    [Serializable]
    public class AudioInfo
    {
        public string name;
        public string path;
        public string guid;
        public float length;
        public int channels;
        public int frequency;
        public string loadType;
    }

    [Serializable]
    public class AudioOperationResult
    {
        public bool success;
        public string message;
        public string path;
    }

    public class AudioHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_list_audio",
                "unity_get_audio",
                "unity_update_audio"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_list_audio" => ListAudio(args),
                "unity_get_audio" => GetAudio(args),
                "unity_update_audio" => UpdateAudio(args),
                _ => JsonConvert.SerializeObject(new AudioOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string ListAudio(JObject args)
        {
            var pathFilter = args["path"]?.ToString();
            var guids = AssetDatabase.FindAssets("t:AudioClip");
            var result = new AudioListResult();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(pathFilter) && !path.StartsWith(pathFilter))
                    continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                var importer = AssetImporter.GetAtPath(path) as AudioImporter;

                result.clips.Add(new AudioInfo
                {
                    name = clip.name,
                    path = path,
                    guid = guid,
                    length = clip.length,
                    channels = clip.channels,
                    frequency = clip.frequency,
                    loadType = importer?.defaultSampleSettings.loadType.ToString() ?? "Unknown"
                });
            }

            return JsonConvert.SerializeObject(result);
        }

        private string GetAudio(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new AudioOperationResult { success = false, message = "path is required" });
            }
            
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            if (clip == null)
            {
                return JsonConvert.SerializeObject(new AudioOperationResult { success = false, message = $"Audio clip not found: {path}" });
            }

            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            var guid = AssetDatabase.AssetPathToGUID(path);

            var info = new
            {
                name = clip.name,
                path = path,
                guid = guid,
                length = clip.length,
                channels = clip.channels,
                frequency = clip.frequency,
                samples = clip.samples,
                loadType = importer?.defaultSampleSettings.loadType.ToString() ?? "Unknown",
                compressionFormat = importer?.defaultSampleSettings.compressionFormat.ToString() ?? "Unknown",
                quality = importer?.defaultSampleSettings.quality ?? 0,
                loadInBackground = importer?.loadInBackground ?? false,
                preloadAudioData = importer?.defaultSampleSettings.preloadAudioData ?? false
            };

            return JsonConvert.SerializeObject(info);
        }

        private string UpdateAudio(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new AudioOperationResult { success = false, message = "path is required" });
            }
            
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;

            if (importer == null)
            {
                return JsonConvert.SerializeObject(new AudioOperationResult { success = false, message = $"Audio clip not found: {path}" });
            }

            try
            {
                var changes = new List<string>();
                var settings = importer.defaultSampleSettings;

                var loadType = args["loadType"]?.ToString();
                if (!string.IsNullOrEmpty(loadType) && Enum.TryParse<AudioClipLoadType>(loadType, true, out var lt))
                {
                    settings.loadType = lt;
                    changes.Add($"loadType={lt}");
                }

                var compressionFormat = args["compressionFormat"]?.ToString();
                if (!string.IsNullOrEmpty(compressionFormat) && Enum.TryParse<AudioCompressionFormat>(compressionFormat, true, out var cf))
                {
                    settings.compressionFormat = cf;
                    changes.Add($"compressionFormat={cf}");
                }

                var quality = args["quality"]?.ToObject<float?>();
                if (quality.HasValue)
                {
                    settings.quality = quality.Value;
                    changes.Add($"quality={quality.Value}");
                }

                importer.defaultSampleSettings = settings;

                var loadInBackground = args["loadInBackground"]?.ToObject<bool?>();
                if (loadInBackground.HasValue)
                {
                    importer.loadInBackground = loadInBackground.Value;
                    changes.Add($"loadInBackground={loadInBackground.Value}");
                }

                var preloadAudioData = args["preloadAudioData"]?.ToObject<bool?>();
                if (preloadAudioData.HasValue)
                {
                    settings.preloadAudioData = preloadAudioData.Value;
                    changes.Add($"preloadAudioData={preloadAudioData.Value}");
                }

                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();

                return JsonConvert.SerializeObject(new AudioOperationResult
                {
                    success = true,
                    message = $"Updated: {string.Join(", ", changes)}",
                    path = path
                });
            }
            catch (Exception e)
            {
                return JsonConvert.SerializeObject(new AudioOperationResult { success = false, message = $"Failed to update audio: {e.Message}" });
            }
        }
    }
}
