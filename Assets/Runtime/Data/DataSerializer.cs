using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace GameDeveloperKit.Data
{
    /// <summary>
    /// 数据序列化器（使用Newtonsoft.Json + 主线程IO）
    /// </summary>
    internal static class DataSerializer
    {
        /// <summary>
        /// 序列化为 JSON（使用Newtonsoft.Json）
        /// </summary>
        public static string SerializeToJson<T>(T value)
        {
            return JsonConvert.SerializeObject(new Wrapper<T> { Value = value });
        }

        /// <summary>
        /// 从 JSON 反序列化（使用Newtonsoft.Json）
        /// </summary>
        public static T DeserializeFromJson<T>(string json)
        {
            var wrapper = JsonConvert.DeserializeObject<Wrapper<T>>(json);
            return wrapper != null ? wrapper.Value : default;
        }

        /// <summary>
        /// 保存到文件（主线程同步IO + Yield避免卡顿）
        /// </summary>
        public static async UniTask SaveToFileAsync(string filePath, Dictionary<string, string> data)
        {
            try
            {
                var wrapper = new DataWrapper { Data = data };
                var json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
                
                // 主线程同步IO（Unity推荐做法）
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(filePath, json);
                
                // Yield确保不阻塞单帧
                await UniTask.Yield();
                
                Game.Debug.Debug($"Data saved to {filePath}");
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"Failed to save data to file: {filePath}", ex);
                throw;
            }
        }

        /// <summary>
        /// 从文件加载（主线程同步IO + Yield避免卡顿）
        /// </summary>
        public static async UniTask<Dictionary<string, string>> LoadFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return new Dictionary<string, string>();

            try
            {
                // 主线程同步IO
                var json = File.ReadAllText(filePath);
                
                // Yield确保不阻塞单帧
                await UniTask.Yield();
                
                var wrapper = JsonConvert.DeserializeObject<DataWrapper>(json);
                var result = wrapper?.Data ?? new Dictionary<string, string>();
                
                Game.Debug.Debug($"Data loaded from {filePath}: {result.Count} entries");
                return result;
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"Failed to load data from file: {filePath}", ex);
                return new Dictionary<string, string>();
            }
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T Value;
        }

        [Serializable]
        private class DataWrapper
        {
            public Dictionary<string, string> Data;
        }
    }
}
