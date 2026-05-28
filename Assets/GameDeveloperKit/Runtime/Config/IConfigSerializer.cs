using System;
using System.Collections;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Config
{
    public interface IConfigSerializer
    {
        ConfigFormat Format { get; }

        UniTask<IList> DeserializeAsync(ConfigSerializerContext context, Type rowType);
    }
}
