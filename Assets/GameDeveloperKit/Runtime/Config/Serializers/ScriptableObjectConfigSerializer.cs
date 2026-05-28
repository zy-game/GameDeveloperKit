using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Config.Serializers
{
    public sealed class ScriptableObjectConfigSerializer : IConfigSerializer
    {
        public ConfigFormat Format => ConfigFormat.ScriptableObject;

        public UniTask<IList> DeserializeAsync(ConfigSerializerContext context, Type rowType)
        {
            var asset = context.Payload.Asset;
            if (asset == null)
            {
                throw new GameException(
                    $"Config source '{context.Source.Name}' ScriptableObject asset is null. Location: {context.Source.Location}.");
            }

            if (asset is not ScriptableObject)
            {
                throw new GameException(
                    $"Config source '{context.Source.Name}' asset '{asset.GetType().FullName}' is not a ScriptableObject.");
            }

            if (asset is not IConfigAsset configAsset)
            {
                throw new GameException(
                    $"Config source '{context.Source.Name}' asset '{asset.GetType().FullName}' must implement IConfigAsset.");
            }

            if (configAsset.RowType == null)
            {
                throw new GameException(
                    $"Config source '{context.Source.Name}' asset '{asset.GetType().FullName}' RowType is null.");
            }

            if (!rowType.IsAssignableFrom(configAsset.RowType))
            {
                throw new GameException(
                    $"Config source '{context.Source.Name}' asset '{asset.GetType().FullName}' RowType '{configAsset.RowType.FullName}' does not match requested row type '{rowType.FullName}'.");
            }

            var sourceRows = configAsset.GetRows();
            if (sourceRows == null)
            {
                throw new GameException(
                    $"Config source '{context.Source.Name}' asset '{asset.GetType().FullName}' rows are null.");
            }

            var listType = typeof(List<>).MakeGenericType(rowType);
            var rows = (IList)Activator.CreateInstance(listType);
            foreach (var row in sourceRows)
            {
                if (row == null)
                {
                    throw new GameException(
                        $"Config source '{context.Source.Name}' asset '{asset.GetType().FullName}' contains a null row.");
                }

                if (!rowType.IsInstanceOfType(row))
                {
                    throw new GameException(
                        $"Config source '{context.Source.Name}' asset '{asset.GetType().FullName}' contains row type '{row.GetType().FullName}', expected '{rowType.FullName}'.");
                }

                rows.Add(row);
            }

            return UniTask.FromResult(rows);
        }
    }
}
