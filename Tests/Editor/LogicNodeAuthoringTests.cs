using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Logic;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Logic;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Tests
{
    public sealed class LogicNodeAuthoringTests
    {
        private const string LogicId = "tests.inventory.has-item";
        private const string RendererKey = "tests.item-selector";

        [Test]
        public void Catalog_WhenValidTypeProvided_ExposesDeclaredMetadataWithoutInstantiation()
        {
            InventoryCheckLogic.ConstructorCount = 0;

            var catalog = LogicDefinitionCatalog.Create(new[] { typeof(InventoryCheckLogic) });

            Assert.AreEqual(0, catalog.Errors.Count);
            Assert.AreEqual(1, catalog.Definitions.Count);
            var definition = catalog.Definitions[0];
            Assert.AreEqual(LogicId, definition.LogicId);
            Assert.AreEqual("道具检查", definition.DisplayName);
            Assert.AreEqual("数据检查", definition.Category);
            Assert.AreEqual("检查业务背包中是否持有指定道具。", definition.Description);
            Assert.AreEqual("检查", definition.InputLabel);
            CollectionAssert.AreEqual(
                new[] { "in", "has", "missing" },
                definition.Ports.Select(item => item.PortId).ToArray());
            Assert.AreEqual("道具 ID", definition.Parameters.Single().Label);
            Assert.AreEqual(RendererKey, definition.FieldRendererKeys["itemId"]);
            Assert.AreEqual(0, InventoryCheckLogic.ConstructorCount);
        }

        [Test]
        public void Catalog_WhenLogicIdDuplicated_RejectsEveryAmbiguousDefinition()
        {
            var catalog = LogicDefinitionCatalog.Create(new[]
            {
                typeof(InventoryCheckLogic),
                typeof(InventoryCheckLogic)
            });

            Assert.AreEqual(0, catalog.Definitions.Count);
            StringAssert.Contains("代码节点 ID 重复", string.Join("|", catalog.Errors));
            StringAssert.Contains(LogicId, string.Join("|", catalog.Errors));
        }

        [Test]
        public void Catalog_WhenTypeIsInvalid_ReturnsChineseErrorAndNoDefinition()
        {
            var catalog = LogicDefinitionCatalog.Create(new[] { typeof(string) });

            Assert.AreEqual(0, catalog.Definitions.Count);
            StringAssert.Contains("代码节点类型无效", string.Join("|", catalog.Errors));
        }

        [Test]
        public void Schema_WhenLogicResolved_ExposesBusinessFieldsPortsAndStaleParameter()
        {
            var catalog = LogicDefinitionCatalog.Create(new[] { typeof(InventoryCheckLogic) });
            var node = CreateLogicNode(LogicId);
            node.Parameters.Add(new AuthoringParameter { Key = "legacyReward", Value = "old" });

            var schema = LogicNodeSchemaResolver.Resolve(node, catalog);

            Assert.AreEqual("道具检查", schema.DisplayName);
            CollectionAssert.AreEqual(
                new[] { "has", "missing" },
                schema.Ports
                    .Where(item => item.Direction == PortDirection.Output)
                    .Select(item => item.PortId)
                    .ToArray());
            var itemId = schema.Parameters.Single(item => item.Key == "itemId");
            Assert.IsTrue(itemId.Required);
            Assert.AreEqual(ParameterValueType.String, itemId.ValueType);
            StringAssert.StartsWith(
                "已失效参数",
                schema.Parameters.Single(item => item.Key == "legacyReward").Label);
        }

        [Test]
        public void Diagnostics_WhenDefinitionOrMetadataIsStale_PreservesDataAndReportsChineseErrors()
        {
            var catalog = LogicDefinitionCatalog.Create(new[] { typeof(InventoryCheckLogic) });
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            try
            {
                asset.StoryId = "logic_story";
                var episode = new AuthoringEpisode
                {
                    EpisodeId = "episode_01",
                    EntryNodeId = "logic"
                };
                var logic = CreateLogicNode(LogicId);
                logic.Parameters.Add(new AuthoringParameter { Key = "legacyReward", Value = "old" });
                var end = new AuthoringNode { NodeId = "end", Title = "结束", NodeKind = NodeKind.End };
                episode.Nodes.Add(logic);
                episode.Nodes.Add(end);
                episode.Edges.Add(new AuthoringEdge
                {
                    EdgeId = "edge_stale",
                    FromNodeId = logic.NodeId,
                    FromPortId = "legacy-output",
                    FromPortLabel = "旧出口",
                    TargetKind = TransitionTargetKind.Node,
                    TargetNodeId = end.NodeId
                });
                asset.Episodes.Add(episode);

                var diagnostics = Diagnostics.BuildLocal(asset, episode, catalog);

                Assert.AreEqual("old", logic.Parameters.Single(item => item.Key == "legacyReward").Value);
                Assert.IsTrue(diagnostics.ForField("logic", "legacyReward")
                    .Any(item => item.Message.Contains("已失效参数")));
                Assert.IsTrue(diagnostics.ForPort("logic", "legacy-output")
                    .Any(item => item.Message.Contains("出口已失效")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Diagnostics_WhenLogicIdMissingOrUnknown_ReturnsLocatedChineseError()
        {
            var catalog = LogicDefinitionCatalog.Create(new[] { typeof(InventoryCheckLogic) });
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            try
            {
                var episode = new AuthoringEpisode { EpisodeId = "episode_01" };
                var missing = CreateLogicNode(string.Empty, "missing");
                var unknown = CreateLogicNode("tests.unknown", "unknown");
                episode.Nodes.Add(missing);
                episode.Nodes.Add(unknown);
                asset.Episodes.Add(episode);

                var diagnostics = Diagnostics.BuildLocal(asset, episode, catalog);

                Assert.IsTrue(diagnostics.ForField("missing", LogicCommandCodec.LogicIdParameter)
                    .Any(item => item.Message.Contains("尚未选择")));
                Assert.IsTrue(diagnostics.ForField("unknown", LogicCommandCodec.LogicIdParameter)
                    .Any(item => item.Message.Contains("定义不存在")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void GraphAdapter_WhenLogicFixtureDiscovered_BuildsTemplateAndVisibleStalePort()
        {
            var window = ScriptableObject.CreateInstance<MainWindow>();
            try
            {
                var episode = new AuthoringEpisode { EpisodeId = "episode_01", EntryNodeId = "logic" };
                var logic = CreateLogicNode(LogicId);
                episode.Nodes.Add(logic);
                episode.Nodes.Add(new AuthoringNode { NodeId = "end", NodeKind = NodeKind.End, Title = "结束" });
                episode.Edges.Add(new AuthoringEdge
                {
                    EdgeId = "edge_stale",
                    FromNodeId = "logic",
                    FromPortId = "legacy-output",
                    FromPortLabel = "旧出口",
                    TargetKind = TransitionTargetKind.Node,
                    TargetNodeId = "end"
                });
                SetPrivateField(window, "m_SelectedEpisode", episode);
                var adapter = new GraphAdapter(window);

                var template = adapter.Templates.Single(item => item.TemplateId == $"logic:{LogicId}");
                var node = adapter.Nodes.Single(item => item.NodeId == "logic");

                Assert.AreEqual("道具检查", template.DisplayName);
                Assert.AreEqual("代码节点 / 数据检查", template.Category);
                CollectionAssert.IsSubsetOf(
                    new[] { "has", "missing" },
                    template.Ports
                        .Where(item => item.Direction == EditorGraphPortDirection.Output)
                        .Select(item => item.PortId)
                        .ToArray());
                StringAssert.StartsWith(
                    "已失效",
                    node.OutputPorts.Single(item => item.PortId == "legacy-output").Label);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void GraphAdapter_WhenLogicRendererRegistered_UsesBusinessRenderer()
        {
            var window = ScriptableObject.CreateInstance<MainWindow>();
            LogicParameterRendererRegistry.Unregister(RendererKey);
            try
            {
                LogicParameterRendererRegistry.Register(
                    RendererKey,
                    (nodeId, field, changed) => new Label($"{nodeId}:{field.Value}"));
                var adapter = new GraphAdapter(window);
                var field = new EditorGraphFieldModel(
                    "itemId",
                    "道具 ID",
                    "item.sword",
                    EditorGraphFieldValueType.Custom,
                    customType: RendererKey);

                var element = adapter.CreateCustomField("logic", field, _ => { });

                Assert.IsInstanceOf<Label>(element);
                Assert.AreEqual("logic:item.sword", ((Label)element).text);
            }
            finally
            {
                LogicParameterRendererRegistry.Unregister(RendererKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Compiler_WhenLogicNodeIsValid_EmitsBlockingCommandAndProgramAssetRoundTrips()
        {
            var asset = CreateCompilerAsset();
            var programAsset = ScriptableObject.CreateInstance<GameDeveloperKit.Story.Model.ProgramAsset>();
            try
            {
                var program = ProgramCompiler.Compile(asset, out var report);

                Assert.IsNotNull(program, string.Join("\n", report.Issues.Select(item => item.ToString())));
                Assert.IsFalse(report.HasErrors);
                var command = program.Volumes[0].Episodes[0].Steps
                    .Single(item => item.StepId == "logic")
                    .Data.Command;
                Assert.IsTrue(command.WaitForCompletion);
                Assert.IsTrue(LogicCommandCodec.IsLogicCommand(command));
                Assert.AreEqual(LogicId, command.Name);
                Assert.AreEqual("item.sword", command.Arguments.GetString("itemId"));
                CollectionAssert.AreEqual(new[] { "has", "missing" }, command.OutcomePorts);
                Assert.AreEqual("owned", command.GetOutcomeTarget("has").StepId);
                Assert.AreEqual("not_owned", command.GetOutcomeTarget("missing").StepId);

                var definition = program.CommandSchema.Definitions.Single(item => item.Name == LogicId);
                Assert.IsTrue(definition.WaitForCompletion);
                CollectionAssert.AreEqual(new[] { "has", "missing" }, definition.OutcomePorts);
                CollectionAssert.IsSubsetOf(
                    new[] { LogicCommandCodec.MarkerArgument, "itemId" },
                    definition.ArgumentNames);

                programAsset.SetProgram(program);
                var restored = programAsset.ToProgram();
                var restoredCommand = restored.Volumes[0].Episodes[0].Steps
                    .Single(item => item.StepId == "logic")
                    .Data.Command;
                Assert.IsTrue(LogicCommandCodec.TryDecode(
                    restoredCommand,
                    out var restoredLogicId,
                    out var restoredArguments,
                    out var decodeError), decodeError);
                Assert.AreEqual(LogicId, restoredLogicId);
                Assert.AreEqual("item.sword", restoredArguments.GetString("itemId"));
                CollectionAssert.AreEqual(new[] { "has", "missing" }, restoredCommand.OutcomePorts);
                Assert.AreEqual("owned", restoredCommand.GetOutcomeTarget("has").StepId);
                Assert.AreEqual("not_owned", restoredCommand.GetOutcomeTarget("missing").StepId);
                var json = EditorJsonUtility.ToJson(programAsset);
                Assert.IsFalse(json.Contains(typeof(InventoryCheckLogic).FullName));
                Assert.IsFalse(json.Contains("AssemblyQualifiedName"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(programAsset);
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Compiler_WhenLogicDefinitionOrRequiredFieldIsMissing_ReturnsLocatedErrors()
        {
            var missingFieldAsset = CreateCompilerAsset();
            var unknownAsset = CreateCompilerAsset();
            try
            {
                var missingNode = missingFieldAsset.Episodes[0].Nodes.Single(item => item.NodeId == "logic");
                missingNode.Parameters.RemoveAll(item => item.Key == "itemId");
                var missingProgram = ProgramCompiler.Compile(missingFieldAsset, out var missingReport);

                var unknownNode = unknownAsset.Episodes[0].Nodes.Single(item => item.NodeId == "logic");
                unknownNode.Parameters.Single(item => item.Key == LogicCommandCodec.LogicIdParameter).Value = "tests.unknown";
                var unknownProgram = ProgramCompiler.Compile(unknownAsset, out var unknownReport);

                Assert.IsNull(missingProgram);
                Assert.IsTrue(missingReport.Issues.Any(item =>
                    item.Source.Contains($"/logic:{LogicId}/field:itemId") &&
                    item.Message.Contains("Required logic field")));
                Assert.IsNull(unknownProgram);
                Assert.IsTrue(unknownReport.Issues.Any(item =>
                    item.Source.Contains("/logic:tests.unknown") &&
                    item.Message.Contains("not registered")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(missingFieldAsset);
                UnityEngine.Object.DestroyImmediate(unknownAsset);
            }
        }

        [Test]
        public void Compiler_WhenLogicParameterOrOutputIsStale_RejectsBothWithoutDeletingAuthoringData()
        {
            var asset = CreateCompilerAsset();
            try
            {
                var episode = asset.Episodes[0];
                var logic = episode.Nodes.Single(item => item.NodeId == "logic");
                logic.Parameters.Add(new AuthoringParameter { Key = "legacyReward", Value = "old" });
                var hasEdge = episode.Edges.Single(item =>
                    item.FromNodeId == "logic" && item.FromPortId == "has");
                hasEdge.FromPortId = "legacy-output";
                hasEdge.FromPortLabel = "旧出口";

                var program = ProgramCompiler.Compile(asset, out var report);

                Assert.IsNull(program);
                Assert.AreEqual("old", logic.Parameters.Single(item => item.Key == "legacyReward").Value);
                Assert.IsTrue(episode.Edges.Contains(hasEdge));
                Assert.IsTrue(report.Issues.Any(item =>
                    item.Source.Contains($"/logic:{LogicId}/field:legacyReward") &&
                    item.Message.Contains("not declared")));
                Assert.IsTrue(report.Issues.Any(item =>
                    item.Source.Contains($"/logic:{LogicId}/port:has") &&
                    item.Message.Contains("target is missing")));
                Assert.IsTrue(report.Issues.Any(item =>
                    item.Source.Contains($"/logic:{LogicId}/port:legacy-output") &&
                    item.Message.Contains("not declared")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static AuthoringNode CreateLogicNode(string logicId, string nodeId = "logic")
        {
            var node = new AuthoringNode
            {
                NodeId = nodeId,
                Title = "代码节点",
                NodeKind = NodeKind.Logic
            };
            node.Parameters.Add(new AuthoringParameter
            {
                Key = LogicCommandCodec.LogicIdParameter,
                Value = logicId
            });
            node.Parameters.Add(new AuthoringParameter { Key = "itemId", Value = "item.sword" });
            return node;
        }

        private static AuthoringAsset CreateCompilerAsset()
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            asset.StoryId = "logic_story";
            asset.Version = "1.0.0";
            asset.EnsureDefaults();
            var episode = asset.Volumes[0].Episodes[0];
            episode.EpisodeId = "episode_01";
            episode.Title = "代码节点测试";
            episode.EntryNodeId = "start";
            episode.Nodes.Clear();
            episode.Edges.Clear();
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = "start",
                Title = "开始",
                NodeKind = NodeKind.Start
            });
            episode.Nodes.Add(CreateLogicNode(LogicId));
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = "owned",
                Title = "持有",
                NodeKind = NodeKind.End
            });
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = "not_owned",
                Title = "未持有",
                NodeKind = NodeKind.End
            });
            episode.Edges.Add(CreateEdge("start_logic", "start", "completed", "logic"));
            episode.Edges.Add(CreateEdge("logic_owned", "logic", "has", "owned"));
            episode.Edges.Add(CreateEdge("logic_missing", "logic", "missing", "not_owned"));
            return asset;
        }

        private static AuthoringEdge CreateEdge(
            string edgeId,
            string fromNodeId,
            string fromPortId,
            string targetNodeId)
        {
            return new AuthoringEdge
            {
                EdgeId = edgeId,
                FromNodeId = fromNodeId,
                FromPortId = fromPortId,
                FromPortLabel = fromPortId,
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = targetNodeId
            };
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(instance, value);
        }
    }

    [LogicNode("tests.inventory.has-item", "道具检查", "数据检查")]
    [System.ComponentModel.Description("检查业务背包中是否持有指定道具。")]
    [InputPort("检查")]
    [LogicParameter(
        "itemId",
        "道具 ID",
        ParameterValueType.String,
        Required = true,
        Tooltip = "需要检查的业务道具 ID。",
        FieldRendererKey = "tests.item-selector")]
    [OutputPort("has", "持有")]
    [OutputPort("missing", "未持有")]
    internal sealed class InventoryCheckLogic : ILogicNode
    {
        public static int ConstructorCount;

        public InventoryCheckLogic()
        {
            ConstructorCount++;
        }

        public UniTask<LogicResult> ExecuteAsync(
            LogicContext context,
            CancellationToken cancellationToken)
        {
            return UniTask.FromResult(LogicResult.To("has"));
        }
    }
}
