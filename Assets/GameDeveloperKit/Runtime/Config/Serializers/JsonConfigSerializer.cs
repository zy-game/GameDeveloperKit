using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.Config.Serializers
{
    public sealed class JsonConfigSerializer : IConfigSerializer
    {
        public ConfigFormat Format => ConfigFormat.Json;

        public UniTask<IList> DeserializeAsync(ConfigSerializerContext context, Type rowType)
        {
            try
            {
                var token = JToken.Parse(context.Payload.Text);
                JToken rowsToken;
                if (token.Type == JTokenType.Array)
                {
                    rowsToken = token;
                }
                else if (token.Type == JTokenType.Object && token["rows"] != null)
                {
                    rowsToken = token["rows"];
                }
                else
                {
                    throw new GameException(
                        $"Config source '{context.Source.Name}' JSON root must be an array or contain a rows array.");
                }

                var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(rowType);
                var rows = (IList)rowsToken.ToObject(listType, JsonSerializer.CreateDefault());
                return UniTask.FromResult(rows);
            }
            catch (GameException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new GameException(
                    $"Config source '{context.Source.Name}' JSON deserialize failed. Location: {context.Source.Location}, row type: {rowType.FullName}.",
                    exception);
            }
        }
    }
}
