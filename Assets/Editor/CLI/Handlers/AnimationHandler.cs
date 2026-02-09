using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class AnimationListResult
    {
        public List<AnimationInfo> animations = new();
    }

    [Serializable]
    public class AnimationInfo
    {
        public string name;
        public string path;
        public string guid;
        public float length;
        public float frameRate;
        public bool isLooping;
    }

    [Serializable]
    public class AnimationDetailInfo
    {
        public string name;
        public string path;
        public string guid;
        public float length;
        public float frameRate;
        public bool isLooping;
        public bool legacy;
        public List<string> animatedProperties = new();
        public List<AnimationEventInfo> events = new();
    }

    [Serializable]
    public class AnimationEventInfo
    {
        public float time;
        public string functionName;
    }

    [Serializable]
    public class AnimationOperationResult
    {
        public bool success;
        public string message;
        public string path;
    }

    public class AnimationHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_list_animations",
                "unity_get_animation"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_list_animations" => ListAnimations(args),
                "unity_get_animation" => GetAnimation(args),
                _ => JsonConvert.SerializeObject(new AnimationOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string ListAnimations(JObject args)
        {
            var pathFilter = args["path"]?.ToString();
            var guids = AssetDatabase.FindAssets("t:AnimationClip");
            var result = new AnimationListResult();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(pathFilter) && !path.StartsWith(pathFilter))
                    continue;

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                result.animations.Add(new AnimationInfo
                {
                    name = clip.name,
                    path = path,
                    guid = guid,
                    length = clip.length,
                    frameRate = clip.frameRate,
                    isLooping = clip.isLooping
                });
            }

            return JsonConvert.SerializeObject(result);
        }

        private string GetAnimation(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new AnimationOperationResult { success = false, message = "path is required" });
            }
            
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip == null)
            {
                return JsonConvert.SerializeObject(new AnimationOperationResult
                {
                    success = false,
                    message = $"Animation clip not found: {path}"
                });
            }

            var info = new AnimationDetailInfo
            {
                name = clip.name,
                path = path,
                guid = AssetDatabase.AssetPathToGUID(path),
                length = clip.length,
                frameRate = clip.frameRate,
                isLooping = clip.isLooping,
                legacy = clip.legacy
            };

            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                info.animatedProperties.Add($"{binding.path}/{binding.propertyName}");
            }

            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (var binding in objectBindings)
            {
                info.animatedProperties.Add($"{binding.path}/{binding.propertyName} (Object)");
            }

            var events = AnimationUtility.GetAnimationEvents(clip);
            foreach (var evt in events)
            {
                info.events.Add(new AnimationEventInfo
                {
                    time = evt.time,
                    functionName = evt.functionName
                });
            }

            return JsonConvert.SerializeObject(info);
        }
    }
}
