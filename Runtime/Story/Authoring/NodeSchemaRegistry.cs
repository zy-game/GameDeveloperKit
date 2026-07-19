using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Event;

namespace GameDeveloperKit.Story.Authoring
{
    /// <summary>
    /// 语义节点 schema registry。
    /// </summary>
    public static class NodeSchemaRegistry
    {
        private static readonly Dictionary<NodeKind, NodeSchema> s_Schemas =
            new Dictionary<NodeKind, NodeSchema>();

        static NodeSchemaRegistry()
        {
            RegisterDefaults();
        }

        /// <summary>
        /// 所有已注册 schema。
        /// </summary>
        public static IReadOnlyCollection<NodeSchema> Schemas => s_Schemas.Values;

        /// <summary>
        /// 注册节点 schema。
        /// </summary>
        /// <param name="schema">节点 schema。</param>
        public static void Register(NodeSchema schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            s_Schemas[schema.Kind] = schema;
        }

        /// <summary>
        /// 获取节点 schema。
        /// </summary>
        /// <param name="kind">节点类型。</param>
        /// <returns>节点 schema。</returns>
        public static NodeSchema Get(NodeKind kind)
        {
            if (!s_Schemas.TryGetValue(kind, out var schema))
            {
                throw new GameException($"Story node schema is not registered. kind:{kind}");
            }

            return schema;
        }

        /// <summary>
        /// 尝试获取节点 schema。
        /// </summary>
        /// <param name="kind">节点类型。</param>
        /// <param name="schema">节点 schema。</param>
        /// <returns>找到时返回 true。</returns>
        public static bool TryGet(NodeKind kind, out NodeSchema schema)
        {
            return s_Schemas.TryGetValue(kind, out schema);
        }

        /// <summary>
        /// 判断节点是否属于 Story 默认作者主路径。
        /// </summary>
        /// <param name="kind">节点类型。</param>
        /// <returns>属于默认作者主路径时返回 true。</returns>
        public static bool IsDefaultAuthoringNode(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Start:
                case NodeKind.End:
                case NodeKind.JumpChapter:
                case NodeKind.Parallel:
                case NodeKind.Merge:
                case NodeKind.Wait:
                case NodeKind.Dialogue:
                case NodeKind.Narration:
                case NodeKind.PlayVideo:
                case NodeKind.ShowImage:
                case NodeKind.PlayAudio:
                case NodeKind.Event:
                case NodeKind.Choice:
                case NodeKind.SettleEpisode:
                    return true;
                default:
                    return false;
            }
        }

        private static void RegisterDefaults()
        {
            RegisterFlow(NodeKind.Start, "开始", Out("completed", "完成"));
            RegisterFlow(NodeKind.End, "结束");
            RegisterFlow(NodeKind.JumpChapter, "跳转章节", Out("completed", "完成"), Param("chapterId", "章节", ParameterValueType.String, true));
            RegisterFlow(NodeKind.Parallel, "并行", Out("branch", "新增轨道", true));
            RegisterFlow(NodeKind.Wait, "等待", Out("completed", "完成"), Param("duration", "时长", ParameterValueType.Number));
            RegisterFlow(NodeKind.Merge, "等待全部完成", Out("completed", "完成"));

            RegisterAction(NodeKind.Dialogue, "对白", Param("textKey", "文本", ParameterValueType.String, true), Param("speaker", "说话人", ParameterValueType.String));
            RegisterAction(NodeKind.Narration, "旁白", Param("textKey", "文本", ParameterValueType.String, true));
            RegisterAction(
                NodeKind.PlayVideo,
                "播放视频",
                Asset(MediaCommandNames.ClipArgument, "视频", "video", true),
                Param("wait", "等待完成", ParameterValueType.Boolean),
                Param("loop", "循环播放", ParameterValueType.Boolean),
                Param("allowSeek", "允许 Seek", ParameterValueType.Boolean));
            RegisterAction(NodeKind.ShowImage, "显示图片", Asset("image", "图片", "image", true));
            RegisterAction(NodeKind.PlayAudio, "播放音频", Asset("clip", "音频", "audio", true), Param("loop", "循环播放", ParameterValueType.Boolean));
            RegisterSchema(
                NodeKind.Event,
                NodeCategory.Action,
                "事件",
                true,
                Out("completed", "完成"),
                Option(EventCommandCodec.EventIdParameter, "事件", true),
                Option(
                    EventCommandCodec.ModeParameter,
                    "模式",
                    true,
                    EventCommandCodec.NotifyMode,
                    EventCommandCodec.RequestMode));

            RegisterInteraction(NodeKind.Choice, "选项", Out("selected", "选择后"), Param("textKey", "选项文本", ParameterValueType.String, true));
            RegisterSchema(
                NodeKind.SettleEpisode,
                NodeCategory.Action,
                "剧情段结算",
                true,
                Out(SettlementCommandNames.CompletedOutcome, "完成"),
                Out(SettlementCommandNames.FailedOutcome, "失败"),
                Param(SettlementCommandNames.PlanArgument, "结算计划", ParameterValueType.String, true));
        }

        private static void RegisterFlow(NodeKind kind, string displayName, params object[] definitions)
        {
            RegisterSchema(kind, NodeCategory.Flow, displayName, true, definitions);
        }

        private static void RegisterAction(NodeKind kind, string displayName, params NodeParameterDefinition[] parameters)
        {
            RegisterSchema(kind, NodeCategory.Action, displayName, true, WithCompleted(parameters));
        }

        private static void RegisterInteraction(NodeKind kind, string displayName, params object[] definitions)
        {
            RegisterSchema(kind, NodeCategory.Interaction, displayName, true, definitions);
        }

        private static void RegisterSchema(
            NodeKind kind,
            NodeCategory category,
            string displayName,
            bool runtimeNode,
            params object[] definitions)
        {
            var ports = new List<PortDefinition>();
            var parameters = new List<NodeParameterDefinition>();
            for (var i = 0; i < definitions.Length; i++)
            {
                switch (definitions[i])
                {
                    case PortDefinition port:
                        ports.Add(port);
                        break;
                    case NodeParameterDefinition parameter:
                        parameters.Add(parameter);
                        break;
                }
            }

            Register(new NodeSchema(kind, category, displayName, runtimeNode, ports, parameters));
        }

        private static object[] WithCompleted(IReadOnlyList<NodeParameterDefinition> parameters)
        {
            var definitions = new object[(parameters?.Count ?? 0) + 1];
            definitions[0] = Out("completed", "完成");
            if (parameters != null)
            {
                for (var i = 0; i < parameters.Count; i++)
                {
                    definitions[i + 1] = parameters[i];
                }
            }

            return definitions;
        }

        private static PortDefinition Out(string portId, string label, bool multiple = false)
        {
            return new PortDefinition(portId, label, PortDirection.Output, multiple);
        }

        private static NodeParameterDefinition Param(string key, string label, ParameterValueType valueType, bool required = false)
        {
            return new NodeParameterDefinition(key, label, valueType, required);
        }

        private static NodeParameterDefinition Asset(string key, string label, string resourceType, bool required = false)
        {
            return new NodeParameterDefinition(key, label, ParameterValueType.AssetReference, required, resourceType: resourceType);
        }

        private static NodeParameterDefinition Option(string key, string label, bool required = false, params string[] options)
        {
            return new NodeParameterDefinition(key, label, ParameterValueType.Option, required, options: options);
        }
    }
}
