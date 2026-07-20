using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.StoryEditor.Logic;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.Graph
{
    internal static class NodeSchemaResolver
    {
        public static NodeSchema Resolve(
            AuthoringNode node,
            LogicDefinitionCatalog logicCatalog = null)
        {
            if (node == null)
            {
                return null;
            }

            if (node.NodeKind == NodeKind.Logic)
            {
                return LogicNodeSchemaResolver.Resolve(node, logicCatalog);
            }

            if (NodeSchemaRegistry.TryGet(node.NodeKind, out var schema))
            {
                return schema;
            }

            return new NodeSchema(
                node.NodeKind,
                NodeCategory.Action,
                $"已停用节点 ({(int)node.NodeKind})",
                false);
        }
    }
}
