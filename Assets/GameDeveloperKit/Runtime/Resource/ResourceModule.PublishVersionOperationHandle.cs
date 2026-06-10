using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using Newtonsoft.Json;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 定义 Resource Module 类型。
    /// </summary>
    public sealed partial class ResourceModule
    {
        /// <summary>
        /// 定义 Publish Version Operation Handle 类型。
        /// </summary>
        public sealed class PublishVersionOperationHandle : OperationHandle<string>
        {
            /// <summary>
            /// 执行 Execute。
            /// </summary>
            /// <param name="args">args 参数。</param>
            public override async void Execute(params object[] args)
            {
                try
                {
                    var location = args.Length > 0 ? args[0] as string : null;
                    if (string.IsNullOrWhiteSpace(location))
                    {
                        SetException(new ArgumentException("Publish location cannot be empty.", nameof(location)));
                        return;
                    }

                    var bytes = await ReadPublishBytesAsync(location);
                    if (bytes is null || bytes.Length == 0)
                    {
                        SetException(new GameException("Publish file is empty."));
                        return;
                    }

                    var text = Encoding.UTF8.GetString(bytes);
                    if (string.IsNullOrEmpty(text))
                    {
                        SetException(new GameException("Publish text is empty."));
                        return;
                    }

                    var pointer = JsonConvert.DeserializeObject<ResourcePublishPointer>(text);
                    if (pointer == null || string.IsNullOrWhiteSpace(pointer.version))
                    {
                        SetException(new GameException("Publish version is empty."));
                        return;
                    }

                    App.Debug.Info($"Publish version loaded from: {location} Version: {pointer.version}");
                    SetResult(pointer.version);
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            /// <summary>
            /// 读取 Publish Bytes Async。
            /// </summary>
            /// <param name="location">location 参数。</param>
            /// <returns>操作完成任务。</returns>
            private static async UniTask<byte[]> ReadPublishBytesAsync(string location)
            {
                if (Uri.TryCreate(location, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    var operation = App.Download.DownloadAsync(location);
                    await operation.WaitCompletionAsync();
                    if (operation.Status is not OperationStatus.Succeeded)
                    {
                        throw operation.Error ?? new GameException($"Publish download failed: {location}");
                    }

                    return await System.IO.File.ReadAllBytesAsync(operation.TempPath);
                }

                var path = ResolveLocalPublishPath(location);
                return await System.IO.File.ReadAllBytesAsync(path);
            }

            /// <summary>
            /// 解析 Local Publish Path。
            /// </summary>
            /// <param name="location">location 参数。</param>
            /// <returns>执行结果。</returns>
            private static string ResolveLocalPublishPath(string location)
            {
                if (Path.IsPathRooted(location) && System.IO.File.Exists(location))
                {
                    return location;
                }

                var streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, location);
                if (System.IO.File.Exists(streamingAssetsPath))
                {
                    return streamingAssetsPath;
                }

                return Path.IsPathRooted(location) ? location : streamingAssetsPath;
            }

            /// <summary>
            /// 定义 Resource Publish Pointer 类型。
            /// </summary>
            private sealed class ResourcePublishPointer
            {
                public string version { get; set; }
            }
        }
    }
}