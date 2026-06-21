using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// Runtime-loadable compiled story program asset.
    /// </summary>
    [CreateAssetMenu(fileName = "StoryProgram", menuName = "GameDeveloperKit/Story/Program")]
    public sealed class StoryProgramAsset : ScriptableObject
    {
        [SerializeField] private string m_StoryId;
        [SerializeField] private string m_Version;
        [SerializeField] private string m_EntryChapterId;
        [SerializeField] private List<ChapterData> m_Chapters = new List<ChapterData>();
        [SerializeField] private VariableSchemaData m_VariableSchema = new VariableSchemaData();
        [SerializeField] private CommandSchemaData m_CommandSchema = new CommandSchemaData();

        /// <summary>
        /// Story id.
        /// </summary>
        public string StoryId => m_StoryId;

        /// <summary>
        /// Version.
        /// </summary>
        public string Version => m_Version;

        /// <summary>
        /// Entry chapter id.
        /// </summary>
        public string EntryChapterId => m_EntryChapterId;

        /// <summary>
        /// Replaces the serialized content with a compiled runtime program.
        /// </summary>
        /// <param name="program">Compiled program.</param>
        public void SetProgram(StoryProgram program)
        {
            if (program == null)
            {
                throw new ArgumentNullException(nameof(program));
            }

            m_StoryId = program.StoryId;
            m_Version = program.Version;
            m_EntryChapterId = program.EntryChapterId;
            m_Chapters = ChapterData.FromList(program.Chapters);
            m_VariableSchema = VariableSchemaData.FromSchema(program.VariableSchema);
            m_CommandSchema = CommandSchemaData.FromSchema(program.CommandSchema);
        }

        /// <summary>
        /// Builds a runtime StoryProgram from the serialized data.
        /// </summary>
        /// <returns>Runtime story program.</returns>
        public StoryProgram ToProgram()
        {
            return new StoryProgram(
                m_StoryId,
                m_Version,
                m_EntryChapterId,
                ChapterData.ToList(m_Chapters),
                m_VariableSchema?.ToSchema(),
                m_CommandSchema?.ToSchema());
        }

        [Serializable]
        private sealed class ChapterData
        {
            [SerializeField] private string m_ChapterId;
            [SerializeField] private string m_Title;
            [SerializeField] private string m_EntryStepId;
            [SerializeField] private List<StepData> m_Steps = new List<StepData>();

            public static List<ChapterData> FromList(IReadOnlyList<StoryChapter> chapters)
            {
                var result = new List<ChapterData>();
                if (chapters == null)
                {
                    return result;
                }

                for (var i = 0; i < chapters.Count; i++)
                {
                    if (chapters[i] != null)
                    {
                        result.Add(FromChapter(chapters[i]));
                    }
                }

                return result;
            }

            public static List<StoryChapter> ToList(IReadOnlyList<ChapterData> chapters)
            {
                var result = new List<StoryChapter>();
                if (chapters == null)
                {
                    return result;
                }

                for (var i = 0; i < chapters.Count; i++)
                {
                    if (chapters[i] != null)
                    {
                        result.Add(chapters[i].ToChapter());
                    }
                }

                return result;
            }

            private static ChapterData FromChapter(StoryChapter chapter)
            {
                return new ChapterData
                {
                    m_ChapterId = chapter.ChapterId,
                    m_Title = chapter.Title,
                    m_EntryStepId = chapter.EntryStepId,
                    m_Steps = StepData.FromList(chapter.Steps)
                };
            }

            private StoryChapter ToChapter()
            {
                return new StoryChapter(
                    m_ChapterId,
                    m_Title,
                    m_EntryStepId,
                    StepData.ToList(m_Steps));
            }
        }

        [Serializable]
        private sealed class StepData
        {
            [SerializeField] private string m_StepId;
            [SerializeField] private StoryStepKind m_Kind;
            [SerializeField] private StepPayloadData m_Data = new StepPayloadData();

            public static List<StepData> FromList(IReadOnlyList<StoryStep> steps)
            {
                var result = new List<StepData>();
                if (steps == null)
                {
                    return result;
                }

                for (var i = 0; i < steps.Count; i++)
                {
                    if (steps[i] != null)
                    {
                        result.Add(FromStep(steps[i]));
                    }
                }

                return result;
            }

            public static List<StoryStep> ToList(IReadOnlyList<StepData> steps)
            {
                var result = new List<StoryStep>();
                if (steps == null)
                {
                    return result;
                }

                for (var i = 0; i < steps.Count; i++)
                {
                    if (steps[i] != null)
                    {
                        result.Add(steps[i].ToStep());
                    }
                }

                return result;
            }

            private static StepData FromStep(StoryStep step)
            {
                return new StepData
                {
                    m_StepId = step.StepId,
                    m_Kind = step.Kind,
                    m_Data = StepPayloadData.FromPayload(step.Data)
                };
            }

            private StoryStep ToStep()
            {
                return new StoryStep(m_StepId, m_Kind, m_Data?.ToPayload(m_Kind));
            }
        }

        [Serializable]
        private sealed class StepPayloadData
        {
            [SerializeField] private string m_TextKey;
            [SerializeField] private string m_Speaker;
            [SerializeField] private CommandData m_Command;
            [SerializeField] private List<ChoiceData> m_Choices = new List<ChoiceData>();
            [SerializeField] private bool m_HasCondition;
            [SerializeField] private ExpressionData m_Condition;
            [SerializeField] private TargetData m_Target;
            [SerializeField] private double m_WaitSeconds;
            [SerializeField] private List<string> m_Tags = new List<string>();
            [SerializeField] private List<ParallelBranchData> m_Branches = new List<ParallelBranchData>();
            [SerializeField] private StoryMergePolicy m_MergePolicy = StoryMergePolicy.All;
            [SerializeField] private string m_ParallelStepId;

            public static StepPayloadData FromPayload(StoryStepData data)
            {
                if (data == null)
                {
                    return new StepPayloadData();
                }

                return new StepPayloadData
                {
                    m_TextKey = data.TextKey,
                    m_Speaker = data.Speaker,
                    m_Command = CommandData.FromCommand(data.Command),
                    m_Choices = ChoiceData.FromList(data.Choices),
                    m_HasCondition = data.Condition != null,
                    m_Condition = ExpressionData.FromExpression(data.Condition),
                    m_Target = TargetData.FromTarget(data.Target),
                    m_WaitSeconds = data.WaitSeconds,
                    m_Tags = CopyList(data.Tags),
                    m_Branches = ParallelBranchData.FromList(data.Branches),
                    m_MergePolicy = data.MergePolicy,
                    m_ParallelStepId = data.ParallelStepId
                };
            }

            public StoryStepData ToPayload(StoryStepKind stepKind)
            {
                return new StoryStepData(
                    m_TextKey,
                    m_Speaker,
                    stepKind == StoryStepKind.Command ? m_Command?.ToCommand() : null,
                    stepKind == StoryStepKind.Choice ? ChoiceData.ToList(m_Choices) : null,
                    stepKind == StoryStepKind.Branch ? ExpressionData.ToExpressionOrNull(m_Condition, m_HasCondition) : null,
                    ShouldRestoreTarget(stepKind) ? m_Target?.ToTarget() : null,
                    m_WaitSeconds,
                    CopyList(m_Tags),
                    stepKind == StoryStepKind.Parallel ? ParallelBranchData.ToList(m_Branches) : null,
                    m_MergePolicy,
                    stepKind == StoryStepKind.Merge ? m_ParallelStepId : null);
            }

            private static bool ShouldRestoreTarget(StoryStepKind stepKind)
            {
                switch (stepKind)
                {
                    case StoryStepKind.Line:
                    case StoryStepKind.Command:
                    case StoryStepKind.Branch:
                    case StoryStepKind.Jump:
                    case StoryStepKind.Wait:
                    case StoryStepKind.Merge:
                        return true;
                    default:
                        return false;
                }
            }
        }

        [Serializable]
        private sealed class ChoiceData
        {
            [SerializeField] private string m_ChoiceId;
            [SerializeField] private string m_TextKey;
            [SerializeField] private bool m_HasCondition;
            [SerializeField] private ExpressionData m_Condition;
            [SerializeField] private TargetData m_Target;
            [SerializeField] private List<string> m_Tags = new List<string>();
            [SerializeField] private string m_BranchId;

            public static List<ChoiceData> FromList(IReadOnlyList<StoryChoice> choices)
            {
                var result = new List<ChoiceData>();
                if (choices == null)
                {
                    return result;
                }

                for (var i = 0; i < choices.Count; i++)
                {
                    if (choices[i] != null)
                    {
                        result.Add(FromChoice(choices[i]));
                    }
                }

                return result;
            }

            public static List<StoryChoice> ToList(IReadOnlyList<ChoiceData> choices)
            {
                var result = new List<StoryChoice>();
                if (choices == null)
                {
                    return result;
                }

                for (var i = 0; i < choices.Count; i++)
                {
                    if (choices[i] != null)
                    {
                        result.Add(choices[i].ToChoice());
                    }
                }

                return result;
            }

            private static ChoiceData FromChoice(StoryChoice choice)
            {
                return new ChoiceData
                {
                    m_ChoiceId = choice.ChoiceId,
                    m_TextKey = choice.TextKey,
                    m_HasCondition = choice.Condition != null,
                    m_Condition = ExpressionData.FromExpression(choice.Condition),
                    m_Target = TargetData.FromTarget(choice.Target),
                    m_Tags = CopyList(choice.Tags),
                    m_BranchId = choice.BranchId
                };
            }

            private StoryChoice ToChoice()
            {
                return new StoryChoice(
                    m_ChoiceId,
                    m_TextKey,
                    ExpressionData.ToExpressionOrNull(m_Condition, m_HasCondition),
                    m_Target?.ToTarget(),
                    CopyList(m_Tags),
                    m_BranchId);
            }
        }

        [Serializable]
        private sealed class ParallelBranchData
        {
            [SerializeField] private string m_BranchId;
            [SerializeField] private string m_Label;
            [SerializeField] private TargetData m_Entry;

            public static List<ParallelBranchData> FromList(IReadOnlyList<StoryParallelBranch> branches)
            {
                var result = new List<ParallelBranchData>();
                if (branches == null)
                {
                    return result;
                }

                for (var i = 0; i < branches.Count; i++)
                {
                    if (branches[i] != null)
                    {
                        result.Add(FromBranch(branches[i]));
                    }
                }

                return result;
            }

            public static List<StoryParallelBranch> ToList(IReadOnlyList<ParallelBranchData> branches)
            {
                var result = new List<StoryParallelBranch>();
                if (branches == null)
                {
                    return result;
                }

                for (var i = 0; i < branches.Count; i++)
                {
                    if (branches[i] != null)
                    {
                        result.Add(branches[i].ToBranch());
                    }
                }

                return result;
            }

            private static ParallelBranchData FromBranch(StoryParallelBranch branch)
            {
                return new ParallelBranchData
                {
                    m_BranchId = branch.BranchId,
                    m_Label = branch.Label,
                    m_Entry = TargetData.FromTarget(branch.Entry)
                };
            }

            private StoryParallelBranch ToBranch()
            {
                return new StoryParallelBranch(m_BranchId, m_Label, m_Entry?.ToTarget());
            }
        }

        [Serializable]
        private sealed class CommandData
        {
            [SerializeField] private string m_CommandId;
            [SerializeField] private string m_Name;
            [SerializeField] private List<ArgumentData> m_Arguments = new List<ArgumentData>();
            [SerializeField] private bool m_WaitForCompletion;
            [SerializeField] private List<string> m_OutcomePorts = new List<string>();
            [SerializeField] private List<OutcomeTargetData> m_OutcomeTargets = new List<OutcomeTargetData>();

            public static CommandData FromCommand(StoryCommand command)
            {
                if (command == null)
                {
                    return null;
                }

                return new CommandData
                {
                    m_CommandId = command.CommandId,
                    m_Name = command.Name,
                    m_Arguments = ArgumentData.FromBag(command.Arguments),
                    m_WaitForCompletion = command.WaitForCompletion,
                    m_OutcomePorts = CopyList(command.OutcomePorts),
                    m_OutcomeTargets = OutcomeTargetData.FromDictionary(command.OutcomeTargets)
                };
            }

            public StoryCommand ToCommand()
            {
                if (string.IsNullOrWhiteSpace(m_CommandId) || string.IsNullOrWhiteSpace(m_Name))
                {
                    return null;
                }

                return new StoryCommand(
                    m_CommandId,
                    m_Name,
                    new StoryArgumentBag(ArgumentData.ToDictionary(m_Arguments)),
                    m_WaitForCompletion,
                    CopyList(m_OutcomePorts),
                    OutcomeTargetData.ToDictionary(m_OutcomeTargets));
            }
        }

        [Serializable]
        private sealed class ArgumentData
        {
            [SerializeField] private string m_Key;
            [SerializeField] private ValueData m_Value = new ValueData();

            public static List<ArgumentData> FromBag(StoryArgumentBag bag)
            {
                var result = new List<ArgumentData>();
                if (bag?.Values == null)
                {
                    return result;
                }

                foreach (var pair in bag.Values)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    result.Add(new ArgumentData
                    {
                        m_Key = pair.Key,
                        m_Value = ValueData.FromValue(pair.Value)
                    });
                }

                return result;
            }

            public static Dictionary<string, StoryValue> ToDictionary(IReadOnlyList<ArgumentData> arguments)
            {
                var result = new Dictionary<string, StoryValue>(StringComparer.Ordinal);
                if (arguments == null)
                {
                    return result;
                }

                for (var i = 0; i < arguments.Count; i++)
                {
                    var argument = arguments[i];
                    if (argument == null || string.IsNullOrWhiteSpace(argument.m_Key))
                    {
                        continue;
                    }

                    result[argument.m_Key] = argument.m_Value?.ToValue() ?? StoryValue.Null;
                }

                return result;
            }
        }

        [Serializable]
        private sealed class OutcomeTargetData
        {
            [SerializeField] private string m_PortId;
            [SerializeField] private TargetData m_Target;

            public static List<OutcomeTargetData> FromDictionary(IReadOnlyDictionary<string, StoryTarget> targets)
            {
                var result = new List<OutcomeTargetData>();
                if (targets == null)
                {
                    return result;
                }

                foreach (var pair in targets)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    result.Add(new OutcomeTargetData
                    {
                        m_PortId = pair.Key,
                        m_Target = TargetData.FromTarget(pair.Value)
                    });
                }

                return result;
            }

            public static Dictionary<string, StoryTarget> ToDictionary(IReadOnlyList<OutcomeTargetData> targets)
            {
                var result = new Dictionary<string, StoryTarget>(StringComparer.Ordinal);
                if (targets == null)
                {
                    return result;
                }

                for (var i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (target == null || string.IsNullOrWhiteSpace(target.m_PortId))
                    {
                        continue;
                    }

                    result[target.m_PortId] = target.m_Target?.ToTarget();
                }

                return result;
            }
        }

        [Serializable]
        private sealed class TargetData
        {
            [SerializeField] private StoryTargetKind m_TargetKind = StoryTargetKind.StoryEnd;
            [SerializeField] private string m_ChapterId;
            [SerializeField] private string m_StepId;

            public static TargetData FromTarget(StoryTarget target)
            {
                if (target == null)
                {
                    return null;
                }

                return new TargetData
                {
                    m_TargetKind = target.TargetKind,
                    m_ChapterId = target.ChapterId,
                    m_StepId = target.StepId
                };
            }

            public StoryTarget ToTarget()
            {
                switch (m_TargetKind)
                {
                    case StoryTargetKind.Step:
                        return StoryTarget.Step(m_ChapterId, m_StepId);
                    case StoryTargetKind.Chapter:
                        return StoryTarget.Chapter(m_ChapterId);
                    case StoryTargetKind.StoryEnd:
                        return StoryTarget.StoryEnd();
                    default:
                        throw new GameException($"Story target kind is invalid: {m_TargetKind}");
                }
            }
        }

        [Serializable]
        private sealed class ExpressionData
        {
            [SerializeField] private StoryExpressionKind m_Kind;
            [SerializeField] private ValueData m_Literal = new ValueData();
            [SerializeField] private string m_VariableName;
            [SerializeField] private string m_FunctionName;
            [SerializeField] private List<ExpressionData> m_Inputs = new List<ExpressionData>();

            public static ExpressionData FromExpression(StoryExpression expression)
            {
                if (expression == null)
                {
                    return null;
                }

                return new ExpressionData
                {
                    m_Kind = expression.Kind,
                    m_Literal = ValueData.FromValue(expression.Literal),
                    m_VariableName = expression.VariableName,
                    m_FunctionName = expression.FunctionName,
                    m_Inputs = FromList(expression.Inputs)
                };
            }

            public StoryExpression ToExpression()
            {
                switch (m_Kind)
                {
                    case StoryExpressionKind.Literal:
                        return StoryExpression.FromLiteral(m_Literal?.ToValue() ?? StoryValue.Null);
                    case StoryExpressionKind.Variable:
                        return StoryExpression.FromVariable(m_VariableName);
                    case StoryExpressionKind.Function:
                        return StoryExpression.FromFunction(m_FunctionName, ToArray(m_Inputs));
                    case StoryExpressionKind.Not:
                        return StoryExpression.CreateNot(RequireInput(0));
                    case StoryExpressionKind.And:
                        return StoryExpression.CreateAnd(ToArray(m_Inputs));
                    case StoryExpressionKind.Or:
                        return StoryExpression.CreateOr(ToArray(m_Inputs));
                    case StoryExpressionKind.Equal:
                        return StoryExpression.CreateEqual(RequireInput(0), RequireInput(1));
                    case StoryExpressionKind.NotEqual:
                        return StoryExpression.CreateNotEqual(RequireInput(0), RequireInput(1));
                    case StoryExpressionKind.Greater:
                        return StoryExpression.CreateGreater(RequireInput(0), RequireInput(1));
                    case StoryExpressionKind.GreaterOrEqual:
                        return StoryExpression.CreateGreaterOrEqual(RequireInput(0), RequireInput(1));
                    case StoryExpressionKind.Less:
                        return StoryExpression.CreateLess(RequireInput(0), RequireInput(1));
                    case StoryExpressionKind.LessOrEqual:
                        return StoryExpression.CreateLessOrEqual(RequireInput(0), RequireInput(1));
                    default:
                        throw new GameException($"Story expression kind is invalid: {m_Kind}");
                }
            }

            public static StoryExpression ToExpressionOrNull(ExpressionData data, bool hasExpression)
            {
                if (data == null)
                {
                    return null;
                }

                if (hasExpression is false && data.IsEmptyNullLiteral())
                {
                    return null;
                }

                return data.ToExpression();
            }

            private static List<ExpressionData> FromList(IReadOnlyList<StoryExpression> expressions)
            {
                var result = new List<ExpressionData>();
                if (expressions == null)
                {
                    return result;
                }

                for (var i = 0; i < expressions.Count; i++)
                {
                    if (expressions[i] != null)
                    {
                        result.Add(FromExpression(expressions[i]));
                    }
                }

                return result;
            }

            private static StoryExpression[] ToArray(IReadOnlyList<ExpressionData> expressions)
            {
                if (expressions == null || expressions.Count == 0)
                {
                    return Array.Empty<StoryExpression>();
                }

                var result = new List<StoryExpression>();
                for (var i = 0; i < expressions.Count; i++)
                {
                    if (expressions[i] != null)
                    {
                        result.Add(expressions[i].ToExpression());
                    }
                }

                return result.ToArray();
            }

            private StoryExpression RequireInput(int index)
            {
                if (m_Inputs == null || index < 0 || index >= m_Inputs.Count || m_Inputs[index] == null)
                {
                    throw new GameException($"Story expression input is missing. kind:{m_Kind} index:{index}");
                }

                return m_Inputs[index].ToExpression();
            }

            private bool IsEmptyNullLiteral()
            {
                return m_Kind == StoryExpressionKind.Literal &&
                       (m_Literal == null || m_Literal.IsNull) &&
                       string.IsNullOrWhiteSpace(m_VariableName) &&
                       string.IsNullOrWhiteSpace(m_FunctionName) &&
                       (m_Inputs == null || m_Inputs.Count == 0);
            }
        }

        [Serializable]
        private sealed class ValueData
        {
            [SerializeField] private StoryValueKind m_Kind;
            [SerializeField] private bool m_BooleanValue;
            [SerializeField] private double m_NumberValue;
            [SerializeField] private string m_StringValue;

            public static ValueData FromValue(StoryValue value)
            {
                return new ValueData
                {
                    m_Kind = value.Kind,
                    m_BooleanValue = value.BooleanValue,
                    m_NumberValue = value.NumberValue,
                    m_StringValue = value.StringValue
                };
            }

            public StoryValue ToValue()
            {
                switch (m_Kind)
                {
                    case StoryValueKind.Boolean:
                        return StoryValue.FromBoolean(m_BooleanValue);
                    case StoryValueKind.Number:
                        return StoryValue.FromNumber(m_NumberValue);
                    case StoryValueKind.String:
                        return StoryValue.FromString(m_StringValue);
                    default:
                        return StoryValue.Null;
                }
            }

            public bool IsNull => m_Kind == StoryValueKind.Null;
        }

        [Serializable]
        private sealed class VariableSchemaData
        {
            [SerializeField] private List<VariableDefinitionData> m_Definitions = new List<VariableDefinitionData>();

            public static VariableSchemaData FromSchema(StoryVariableSchema schema)
            {
                return new VariableSchemaData
                {
                    m_Definitions = VariableDefinitionData.FromList(schema?.Definitions)
                };
            }

            public StoryVariableSchema ToSchema()
            {
                return new StoryVariableSchema(VariableDefinitionData.ToList(m_Definitions));
            }
        }

        [Serializable]
        private sealed class VariableDefinitionData
        {
            [SerializeField] private string m_Name;
            [SerializeField] private StoryVariableType m_Type;
            [SerializeField] private ValueData m_DefaultValue = new ValueData();

            public static List<VariableDefinitionData> FromList(IReadOnlyList<StoryVariableDefinition> definitions)
            {
                var result = new List<VariableDefinitionData>();
                if (definitions == null)
                {
                    return result;
                }

                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    result.Add(new VariableDefinitionData
                    {
                        m_Name = definition.Name,
                        m_Type = definition.Type,
                        m_DefaultValue = ValueData.FromValue(definition.DefaultValue)
                    });
                }

                return result;
            }

            public static List<StoryVariableDefinition> ToList(IReadOnlyList<VariableDefinitionData> definitions)
            {
                var result = new List<StoryVariableDefinition>();
                if (definitions == null)
                {
                    return result;
                }

                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.m_Name))
                    {
                        continue;
                    }

                    result.Add(new StoryVariableDefinition(
                        definition.m_Name,
                        definition.m_Type,
                        definition.m_DefaultValue?.ToValue() ?? StoryValue.Null));
                }

                return result;
            }
        }

        [Serializable]
        private sealed class CommandSchemaData
        {
            [SerializeField] private List<CommandDefinitionData> m_Definitions = new List<CommandDefinitionData>();

            public static CommandSchemaData FromSchema(StoryCommandSchema schema)
            {
                return new CommandSchemaData
                {
                    m_Definitions = CommandDefinitionData.FromList(schema?.Definitions)
                };
            }

            public StoryCommandSchema ToSchema()
            {
                return new StoryCommandSchema(CommandDefinitionData.ToList(m_Definitions));
            }
        }

        [Serializable]
        private sealed class CommandDefinitionData
        {
            [SerializeField] private string m_Name;
            [SerializeField] private string m_DisplayName;
            [SerializeField] private bool m_WaitForCompletion;
            [SerializeField] private List<CommandArgumentDefinitionData> m_ArgumentDefinitions = new List<CommandArgumentDefinitionData>();
            [SerializeField] private List<string> m_OutcomePorts = new List<string>();

            public static List<CommandDefinitionData> FromList(IReadOnlyList<StoryCommandDefinition> definitions)
            {
                var result = new List<CommandDefinitionData>();
                if (definitions == null)
                {
                    return result;
                }

                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    result.Add(new CommandDefinitionData
                    {
                        m_Name = definition.Name,
                        m_DisplayName = definition.DisplayName,
                        m_WaitForCompletion = definition.WaitForCompletion,
                        m_ArgumentDefinitions = CommandArgumentDefinitionData.FromList(definition.ArgumentDefinitions),
                        m_OutcomePorts = CopyList(definition.OutcomePorts)
                    });
                }

                return result;
            }

            public static List<StoryCommandDefinition> ToList(IReadOnlyList<CommandDefinitionData> definitions)
            {
                var result = new List<StoryCommandDefinition>();
                if (definitions == null)
                {
                    return result;
                }

                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.m_Name))
                    {
                        continue;
                    }

                    result.Add(new StoryCommandDefinition(
                        definition.m_Name,
                        definition.m_DisplayName,
                        definition.m_WaitForCompletion,
                        CommandArgumentDefinitionData.ToList(definition.m_ArgumentDefinitions),
                        CopyList(definition.m_OutcomePorts)));
                }

                return result;
            }
        }

        [Serializable]
        private sealed class CommandArgumentDefinitionData
        {
            [SerializeField] private string m_Key;
            [SerializeField] private string m_Label;
            [SerializeField] private ParameterValueType m_ValueType;
            [SerializeField] private bool m_Required;
            [SerializeField] private string m_ResourceType;
            [SerializeField] private List<string> m_Options = new List<string>();
            [SerializeField] private string m_Tooltip;

            public static List<CommandArgumentDefinitionData> FromList(IReadOnlyList<StoryCommandArgumentDefinition> definitions)
            {
                var result = new List<CommandArgumentDefinitionData>();
                if (definitions == null)
                {
                    return result;
                }

                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    result.Add(new CommandArgumentDefinitionData
                    {
                        m_Key = definition.Key,
                        m_Label = definition.Label,
                        m_ValueType = definition.ValueType,
                        m_Required = definition.Required,
                        m_ResourceType = definition.ResourceType,
                        m_Options = CopyList(definition.Options),
                        m_Tooltip = definition.Tooltip
                    });
                }

                return result;
            }

            public static List<StoryCommandArgumentDefinition> ToList(IReadOnlyList<CommandArgumentDefinitionData> definitions)
            {
                var result = new List<StoryCommandArgumentDefinition>();
                if (definitions == null)
                {
                    return result;
                }

                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.m_Key))
                    {
                        continue;
                    }

                    result.Add(new StoryCommandArgumentDefinition(
                        definition.m_Key,
                        definition.m_Label,
                        definition.m_ValueType,
                        definition.m_Required,
                        definition.m_ResourceType,
                        CopyList(definition.m_Options),
                        definition.m_Tooltip));
                }

                return result;
            }
        }

        private static List<T> CopyList<T>(IReadOnlyList<T> values)
        {
            var result = new List<T>();
            if (values == null)
            {
                return result;
            }

            for (var i = 0; i < values.Count; i++)
            {
                result.Add(values[i]);
            }

            return result;
        }
    }
}
