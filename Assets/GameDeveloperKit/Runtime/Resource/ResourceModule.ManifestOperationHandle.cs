using System;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.Operation;
using Newtonsoft.Json;

namespace GameDeveloperKit.Resource
{
    public sealed partial class ResourceModule
    {
        public sealed class ManifestOperationHandle : OperationHandle<ManifestInfo>
        {
            public override async void Execute(params object[] args)
            {
                try
                {
                    string url = args[0] as string;
                    var operation = Super.Download.DownloadAsync(url);
                    await operation.WaitCompletionAsync();
                    if (operation.Status is not DownloadStatus.Completed)
                    {
                        SetException(new GameException(operation.Error));
                        return;
                    }

                    var bytes = await System.IO.File.ReadAllBytesAsync(operation.TempPath);
                    if (bytes is null || bytes.Length == 0)
                    {
                        SetException(new GameException(operation.Error));
                        return;
                    }

                    var text = Encoding.UTF8.GetString(bytes);
                    if (string.IsNullOrEmpty(text))
                    {
                        SetException(new GameException(operation.Error));
                        return;
                    }

                    var mainfest = JsonConvert.DeserializeObject<ManifestInfo>(text);
                    if (mainfest is null)
                    {
                        SetException(new GameException("unable to find mainfest"));
                        return;
                    }

                    SetResult(mainfest);
                }
                catch (Exception e)
                {
                    SetException(e);
                }
            }
        }
    }
}
