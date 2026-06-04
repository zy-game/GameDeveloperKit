using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.Operation;
using Newtonsoft.Json;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    public sealed partial class ResourceModule
    {
        /// <summary>
        /// 清单操作句柄类，继承自OperationHandle<ManifestInfo>，用于处理资源清单的加载和解析操作。它包含一个Execute方法，该方法接受一个参数数组，主要用于执行清单加载的具体逻辑，包括从指定URL下载清单文件、读取文件内容、解析JSON数据并将其转换为ManifestInfo对象。如果在执行过程中发生任何异常或错误，都会通过SetException方法将异常信息传递给调用者，以便进行适当的错误处理。这种设计模式有助于将资源清单的加载和解析逻辑封装在一个独立的操作句柄中，提高代码的可读性和维护性，同时也方便了异步操作的管理和错误处理。 --- IGNORE ---
        /// </summary>
        public sealed class ManifestOperationHandle : OperationHandle<ManifestInfo>
        {
            /// <summary>
            /// 执行操作句柄逻辑。
            /// </summary>
            /// <param name="args">操作参数。</param>
            public override async void Execute(params object[] args)
            {
                try
                {
                    var location = args.Length > 0 ? args[0] as string : null;
                    if (string.IsNullOrWhiteSpace(location))
                    {
                        SetException(new ArgumentException("Manifest location cannot be empty.", nameof(location)));
                        return;
                    }

                    var bytes = await ReadManifestBytesAsync(location);
                    if (bytes is null || bytes.Length == 0)
                    {
                        SetException(new GameException("Manifest file is empty."));
                        return;
                    }

                    var text = Encoding.UTF8.GetString(bytes);
                    if (string.IsNullOrEmpty(text))
                    {
                        SetException(new GameException("Manifest text is empty."));
                        return;
                    }
Debug.Log($"Manifest loaded from: {location} Content: {text}");
                    var manifest = JsonConvert.DeserializeObject<ManifestInfo>(text);
                    if (manifest is null)
                    {
                        SetException(new GameException("Unable to deserialize manifest."));
                        return;
                    }

                    SetResult(manifest);
                }
                catch (Exception e)
                {
                    SetException(e);
                }
            }

            private static async UniTask<byte[]> ReadManifestBytesAsync(string location)
            {
                if (Uri.TryCreate(location, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    var operation = Super.Download.DownloadAsync(location);
                    await operation.WaitCompletionAsync();
                    if (operation.Status is not OperationStatus.Succeeded)
                    {
                        throw operation.Error ?? new GameException($"Manifest download failed: {location}");
                    }

                    return await System.IO.File.ReadAllBytesAsync(operation.TempPath);
                }

                var path = ResolveLocalManifestPath(location);
                return await System.IO.File.ReadAllBytesAsync(path);
            }

            private static string ResolveLocalManifestPath(string location)
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

                var fallbackPath = Path.Combine(Application.streamingAssetsPath, ResourceSettings.MANIFEST_NAME);
                if (System.IO.File.Exists(fallbackPath))
                {
                    return fallbackPath;
                }

                return Path.IsPathRooted(location) ? location : streamingAssetsPath;
            }
        }
    }
}
