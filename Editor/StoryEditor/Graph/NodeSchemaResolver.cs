using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.Graph
{
    internal static class NodeSchemaResolver
    {
        public static NodeSchema Resolve(
            AuthoringNode node)
        {
            if (node == null)
            {
                return null;
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
