using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Logic;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.Logic
{
    internal static class LogicNodeSchemaResolver
    {
        public static NodeSchema Resolve(AuthoringNode node, LogicDefinitionCatalog catalog = null)
        {
            if (node == null || node.NodeKind != NodeKind.Logic)
            {
                return node == null ? null : NodeSchemaRegistry.Get(node.NodeKind);
            }

            catalog ??= LogicDefinitionCatalog.Shared;
            var schema = Resolve(GetParameter(node, LogicCommandCodec.LogicIdParameter), catalog);
            var parameters = new List<NodeParameterDefinition>(schema.Parameters);
            var declaredKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < parameters.Count; i++)
            {
                declaredKeys.Add(parameters[i].Key);
            }

            for (var i = 0; i < node.Parameters.Count; i++)
            {
                var parameter = node.Parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key) ||
                    declaredKeys.Add(parameter.Key) is false)
                {
                    continue;
                }

                parameters.Add(new NodeParameterDefinition(
                    parameter.Key,
                    $"已失效参数：{parameter.Key}",
                    ParameterValueType.String,
                    tooltip: "该参数不属于当前代码节点定义，数据已保留，请确认后修复。"));
            }

            return new NodeSchema(
                schema.Kind,
                schema.Category,
                schema.DisplayName,
                schema.RuntimeNode,
                schema.Ports,
                parameters);
        }

        public static NodeSchema Resolve(string logicId, LogicDefinitionCatalog catalog = null)
        {
            catalog ??= LogicDefinitionCatalog.Shared;
            catalog.TryGet(logicId, out var definition);
            var logicIds = new string[catalog.Definitions.Count];
            for (var i = 0; i < catalog.Definitions.Count; i++)
            {
                logicIds[i] = catalog.Definitions[i].LogicId;
            }

            var parameters = new List<NodeParameterDefinition>
            {
                new NodeParameterDefinition(
                    LogicCommandCodec.LogicIdParameter,
                    "代码逻辑",
                    ParameterValueType.Option,
                    true,
                    options: logicIds)
            };
            if (definition != null)
            {
                parameters.AddRange(definition.Parameters);
            }

            return new NodeSchema(
                NodeKind.Logic,
                NodeCategory.Action,
                definition?.DisplayName ?? "缺失代码节点",
                true,
                definition?.Ports ?? Array.Empty<PortDefinition>(),
                parameters);
        }

        private static string GetParameter(AuthoringNode node, string key)
        {
            for (var i = 0; i < node.Parameters.Count; i++)
            {
                var parameter = node.Parameters[i];
                if (parameter != null && string.Equals(parameter.Key, key, StringComparison.Ordinal))
                {
                    return parameter.Value;
                }
            }

            return null;
        }
    }
}
