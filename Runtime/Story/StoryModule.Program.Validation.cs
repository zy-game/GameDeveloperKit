using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情运行时模块的 Program 校验逻辑。
    /// </summary>
    public sealed partial class StoryModule
    {
        private static void ValidateProgram(Program program)
        {
            ValidateId(program.StoryId, "story", program.StoryId);
            ValidateId(program.Version, "version", program.StoryId);
            ValidateId(program.EntryChapterId, "entryChapter", program.StoryId);

            var chapters = BuildChapterMap(program);
            if (!chapters.ContainsKey(program.EntryChapterId))
            {
                throw new GameException($"Story entry chapter does not exist. story:{program.StoryId} chapter:{program.EntryChapterId}");
            }

            var stepMaps = BuildProgramStepMaps(program.StoryId, program.Chapters);
            for (var i = 0; i < program.Chapters.Count; i++)
            {
                ValidateChapter(program.StoryId, program.Chapters[i], chapters, stepMaps, program);
            }
        }

        private static Dictionary<string, Chapter> BuildChapterMap(Program program)
        {
            var chapters = new Dictionary<string, Chapter>(StringComparer.Ordinal);
            for (var i = 0; i < program.Chapters.Count; i++)
            {
                var chapter = program.Chapters[i];
                if (chapter == null)
                {
                    throw new GameException($"Story chapter cannot be null. story:{program.StoryId} index:{i}");
                }

                ValidateId(chapter.ChapterId, "chapter", program.StoryId);
                if (chapters.ContainsKey(chapter.ChapterId))
                {
                    throw new GameException($"Duplicate story chapter id. story:{program.StoryId} chapter:{chapter.ChapterId}");
                }

                chapters.Add(chapter.ChapterId, chapter);
            }

            return chapters;
        }

        private static void ValidateChapter(
            string storyId,
            Chapter chapter,
            IReadOnlyDictionary<string, Chapter> chapters,
            IReadOnlyDictionary<string, Dictionary<string, Step>> stepMaps,
            Program program)
        {
            ValidateId(chapter.EntryStepId, "chapterEntryStep", storyId);
            var steps = stepMaps[chapter.ChapterId];
            if (!steps.ContainsKey(chapter.EntryStepId))
            {
                throw new GameException($"Story chapter entry step does not exist. story:{storyId} chapter:{chapter.ChapterId} step:{chapter.EntryStepId}");
            }

            for (var i = 0; i < chapter.Steps.Count; i++)
            {
                ValidateStep(storyId, chapter.ChapterId, chapter.Steps[i], steps, chapters, stepMaps, program);
            }
        }

        private static Dictionary<string, Dictionary<string, Step>> BuildProgramStepMaps(
            string storyId,
            IReadOnlyList<Chapter> chapters)
        {
            var stepMaps = new Dictionary<string, Dictionary<string, Step>>(StringComparer.Ordinal);
            for (var i = 0; i < chapters.Count; i++)
            {
                var chapter = chapters[i];
                if (chapter == null)
                {
                    continue;
                }

                stepMaps[chapter.ChapterId] = BuildStepMap(storyId, chapter.ChapterId, chapter.Steps);
            }

            return stepMaps;
        }

        private static Dictionary<string, Step> BuildStepMap(
            string storyId,
            string chapterId,
            IReadOnlyList<Step> stepList)
        {
            var steps = new Dictionary<string, Step>(StringComparer.Ordinal);
            for (var i = 0; i < stepList.Count; i++)
            {
                var step = stepList[i];
                if (step == null)
                {
                    throw new GameException($"Story step cannot be null. story:{storyId} chapter:{chapterId} index:{i}");
                }

                ValidateId(step.StepId, "step", storyId);
                if (steps.ContainsKey(step.StepId))
                {
                    throw new GameException($"Duplicate story step id. story:{storyId} chapter:{chapterId} step:{step.StepId}");
                }

                steps.Add(step.StepId, step);
            }

            return steps;
        }

        private static void ValidateStep(
            string storyId,
            string chapterId,
            Step step,
            IReadOnlyDictionary<string, Step> steps,
            IReadOnlyDictionary<string, Chapter> chapters,
            IReadOnlyDictionary<string, Dictionary<string, Step>> stepMaps,
            Program program)
        {
            switch (step.Kind)
            {
                case StepKind.Start:
                    break;
                case StepKind.Line:
                    ValidateLineStep(storyId, chapterId, step);
                    ValidateTarget(storyId, chapterId, step.StepId, step.Data.Target, chapters, stepMaps, "line target");
                    break;
                case StepKind.Choice:
                    ValidateChoiceStep(storyId, chapterId, step, chapters, stepMaps);
                    break;
                case StepKind.Command:
                    ValidateCommandStep(storyId, chapterId, step, chapters, stepMaps, program);
                    break;
                case StepKind.Branch:
                    ValidateBranchStep(storyId, chapterId, step, chapters, stepMaps);
                    break;
                case StepKind.Jump:
                    ValidateJumpStep(storyId, chapterId, step, chapters, stepMaps);
                    break;
                case StepKind.Wait:
                    if (TimeRules.IsFiniteNonNegative(step.Data.WaitSeconds) is false)
                    {
                        throw new GameException($"Story wait seconds must be finite and non-negative. story:{storyId} chapter:{chapterId} step:{step.StepId}");
                    }

                    ValidateTarget(storyId, chapterId, step.StepId, step.Data.Target, chapters, stepMaps, "wait target");
                    break;
                case StepKind.End:
                    break;
                case StepKind.Parallel:
                    ValidateParallelStep(storyId, chapterId, step, steps);
                    break;
                case StepKind.Merge:
                    ValidateMergeStep(storyId, chapterId, step, steps);
                    ValidateTarget(storyId, chapterId, step.StepId, step.Data.Target, chapters, stepMaps, "merge target");
                    break;
                default:
                    throw new GameException($"Story step kind is invalid. story:{storyId} chapter:{chapterId} step:{step.StepId} kind:{step.Kind}");
            }
        }

        private static void ValidateLineStep(string storyId, string chapterId, Step step)
        {
            if (string.IsNullOrWhiteSpace(step.Data.TextKey))
            {
                throw new GameException($"Story line text key cannot be empty. story:{storyId} chapter:{chapterId} step:{step.StepId}");
            }
        }

        private static void ValidateChoiceStep(
            string storyId,
            string chapterId,
            Step step,
            IReadOnlyDictionary<string, Chapter> chapters,
            IReadOnlyDictionary<string, Dictionary<string, Step>> stepMaps)
        {
            if (step.Choices.Count == 0)
            {
                throw new GameException($"Story choice step has no options. story:{storyId} chapter:{chapterId} step:{step.StepId}");
            }

            var choiceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < step.Choices.Count; i++)
            {
                var choice = step.Choices[i];
                if (choice == null)
                {
                    throw new GameException($"Story choice cannot be null. story:{storyId} chapter:{chapterId} step:{step.StepId} index:{i}");
                }

                ValidateId(choice.ChoiceId, "choice", storyId);
                if (!choiceIds.Add(choice.ChoiceId))
                {
                    throw new GameException($"Duplicate story choice id. story:{storyId} chapter:{chapterId} step:{step.StepId} choice:{choice.ChoiceId}");
                }

                if (choice.Target == null)
                {
                    throw new GameException($"Story choice target cannot be null. story:{storyId} chapter:{chapterId} step:{step.StepId} choice:{choice.ChoiceId}");
                }

                ValidateTarget(storyId, chapterId, step.StepId, choice.Target, chapters, stepMaps, $"choice:{choice.ChoiceId}");
            }
        }

        private static void ValidateCommandStep(
            string storyId,
            string chapterId,
            Step step,
            IReadOnlyDictionary<string, Chapter> chapters,
            IReadOnlyDictionary<string, Dictionary<string, Step>> stepMaps,
            Program program)
        {
            if (step.Data.Command == null)
            {
                throw new GameException($"Story command cannot be null. story:{storyId} chapter:{chapterId} step:{step.StepId}");
            }

            CommandDefinition commandDefinition = null;
            if (program.CommandSchema?.Definitions != null)
            {
                for (var i = 0; i < program.CommandSchema.Definitions.Count; i++)
                {
                    var definition = program.CommandSchema.Definitions[i];
                    if (definition != null && string.Equals(definition.Name, step.Data.Command.Name, StringComparison.Ordinal))
                    {
                        commandDefinition = definition;
                        break;
                    }
                }

                if (commandDefinition == null)
                {
                    throw new GameException($"Story command schema is not registered. story:{storyId} chapter:{chapterId} step:{step.StepId} command:{step.Data.Command.Name}");
                }
            }

            if (commandDefinition != null)
            {
                ValidateCommandArguments(storyId, chapterId, step, commandDefinition);
                ValidateCommandOutcomePorts(storyId, chapterId, step, commandDefinition);
            }

            ValidateBuiltInCommand(storyId, chapterId, step);

            ValidateTarget(storyId, chapterId, step.StepId, step.Data.Target, chapters, stepMaps, "command target");
            foreach (var pair in step.Data.Command.OutcomeTargets)
            {
                ValidateTarget(storyId, chapterId, step.StepId, pair.Value, chapters, stepMaps, $"command outcome:{pair.Key}");
            }
        }

        private static void ValidateBuiltInCommand(string storyId, string chapterId, Step step)
        {
            var command = step.Data.Command;
            if (!string.Equals(command.Name, InteractionCommandNames.Qte, StringComparison.Ordinal))
            {
                return;
            }

            var duration = command.Arguments.GetNumber(InteractionCommandNames.DurationSecondsArgument);
            if (TimeRules.IsFinitePositive(duration) is false)
            {
                throw new GameException(
                    $"Story QTE duration must be finite and greater than zero. story:{storyId} chapter:{chapterId} step:{step.StepId}");
            }

            var requiredCount = command.Arguments.GetNumber(InteractionCommandNames.RequiredCountArgument, 1d);
            if (TimeRules.IsFinitePositive(requiredCount) is false)
            {
                throw new GameException(
                    $"Story QTE required count must be finite and greater than zero. story:{storyId} chapter:{chapterId} step:{step.StepId}");
            }
        }

        private static void ValidateCommandOutcomePorts(
            string storyId,
            string chapterId,
            Step step,
            CommandDefinition commandDefinition)
        {
            var command = step.Data.Command;
            if (command.OutcomePorts.Count == 0 && command.OutcomeTargets.Count == 0)
            {
                return;
            }

            for (var i = 0; i < command.OutcomePorts.Count; i++)
            {
                var outcomePort = command.OutcomePorts[i];
                if (ContainsOutcomePort(commandDefinition, outcomePort) is false)
                {
                    throw new GameException($"Story command outcome is not declared in schema. story:{storyId} chapter:{chapterId} step:{step.StepId} command:{command.Name} outcome:{outcomePort}");
                }
            }

            foreach (var pair in command.OutcomeTargets)
            {
                if (ContainsOutcomePort(commandDefinition, pair.Key) is false)
                {
                    throw new GameException($"Story command outcome is not declared in schema. story:{storyId} chapter:{chapterId} step:{step.StepId} command:{command.Name} outcome:{pair.Key}");
                }
            }
        }

        private static bool ContainsOutcomePort(CommandDefinition commandDefinition, string outcomePort)
        {
            if (commandDefinition.OutcomePorts == null || string.IsNullOrWhiteSpace(outcomePort))
            {
                return false;
            }

            for (var i = 0; i < commandDefinition.OutcomePorts.Count; i++)
            {
                if (string.Equals(commandDefinition.OutcomePorts[i], outcomePort, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateCommandArguments(
            string storyId,
            string chapterId,
            Step step,
            CommandDefinition commandDefinition)
        {
            for (var i = 0; i < commandDefinition.ArgumentDefinitions.Count; i++)
            {
                var argumentDefinition = commandDefinition.ArgumentDefinitions[i];
                if (argumentDefinition == null || string.IsNullOrWhiteSpace(argumentDefinition.Key))
                {
                    continue;
                }

                var source = $"story:{storyId} chapter:{chapterId} step:{step.StepId} command:{step.Data.Command.Name} argument:{argumentDefinition.Key}";
                if (step.Data.Command.Arguments.TryGetValue(argumentDefinition.Key, out var value) is false)
                {
                    if (argumentDefinition.Required)
                    {
                        throw new GameException($"Story command required argument is missing. {source}");
                    }

                    continue;
                }

                if (argumentDefinition.Required && IsEmptyArgument(value))
                {
                    throw new GameException($"Story command required argument is empty. {source}");
                }

                if (IsCommandArgumentTypeValid(argumentDefinition, value) is false)
                {
                    throw new GameException($"Story command argument type is invalid. {source}");
                }
            }
        }

        private static bool IsEmptyArgument(Value value)
        {
            return value.IsNull || (value.IsString && string.IsNullOrWhiteSpace(value.StringValue));
        }

        private static bool IsCommandArgumentTypeValid(CommandArgumentDefinition argumentDefinition, Value value)
        {
            if (value.IsNull)
            {
                return argumentDefinition.Required is false;
            }

            switch (argumentDefinition.ValueType)
            {
                case ParameterValueType.Number:
                    return value.IsNumber;
                case ParameterValueType.Boolean:
                    return value.IsBoolean;
                case ParameterValueType.String:
                case ParameterValueType.AssetReference:
                    return value.IsString;
                case ParameterValueType.Option:
                    return value.IsString && IsOptionArgumentValueValid(argumentDefinition, value.StringValue);
                default:
                    return value.IsString;
            }
        }

        private static bool IsOptionArgumentValueValid(CommandArgumentDefinition argumentDefinition, string value)
        {
            if (argumentDefinition.Options == null || argumentDefinition.Options.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < argumentDefinition.Options.Count; i++)
            {
                if (string.Equals(argumentDefinition.Options[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateBranchStep(
            string storyId,
            string chapterId,
            Step step,
            IReadOnlyDictionary<string, Chapter> chapters,
            IReadOnlyDictionary<string, Dictionary<string, Step>> stepMaps)
        {
            if (step.Data.Condition == null)
            {
                throw new GameException($"Story branch condition cannot be null. story:{storyId} chapter:{chapterId} step:{step.StepId}");
            }

            if (step.Data.Target == null)
            {
                throw new GameException($"Story branch target cannot be null. story:{storyId} chapter:{chapterId} step:{step.StepId}");
            }

            ValidateTarget(storyId, chapterId, step.StepId, step.Data.Target, chapters, stepMaps, "branch target");
        }

        private static void ValidateJumpStep(
            string storyId,
            string chapterId,
            Step step,
            IReadOnlyDictionary<string, Chapter> chapters,
            IReadOnlyDictionary<string, Dictionary<string, Step>> stepMaps)
        {
            if (step.Data.Target == null)
            {
                throw new GameException($"Story jump target cannot be null. story:{storyId} chapter:{chapterId} step:{step.StepId}");
            }

            ValidateTarget(storyId, chapterId, step.StepId, step.Data.Target, chapters, stepMaps, "jump target");
        }

        private static void ValidateParallelStep(
            string storyId,
            string chapterId,
            Step step,
            IReadOnlyDictionary<string, Step> steps)
        {
            if (step.Data.Branches.Count < 2)
            {
                throw new GameException($"Story parallel step must have at least two branches. story:{storyId} chapter:{chapterId} step:{step.StepId}");
            }

            var branchIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < step.Data.Branches.Count; i++)
            {
                var branch = step.Data.Branches[i];
                if (branch == null)
                {
                    throw new GameException($"Story parallel branch cannot be null. story:{storyId} chapter:{chapterId} step:{step.StepId} index:{i}");
                }

                ValidateId(branch.BranchId, "parallelBranch", storyId);
                if (!branchIds.Add(branch.BranchId))
                {
                    throw new GameException($"Duplicate story parallel branch id. story:{storyId} chapter:{chapterId} step:{step.StepId} branch:{branch.BranchId}");
                }

                ValidateParallelBranchEntry(storyId, chapterId, step, branch, steps);
            }
        }

        private static void ValidateParallelBranchEntry(
            string storyId,
            string chapterId,
            Step step,
            ParallelBranch branch,
            IReadOnlyDictionary<string, Step> steps)
        {
            if (branch.Entry == null)
            {
                throw new GameException($"Story parallel branch entry cannot be null. story:{storyId} chapter:{chapterId} step:{step.StepId} branch:{branch.BranchId}");
            }

            if (branch.Entry.TargetKind != TargetKind.Step)
            {
                throw new GameException($"Story parallel branch entry must target a step. story:{storyId} chapter:{chapterId} step:{step.StepId} branch:{branch.BranchId}");
            }

            if (!string.Equals(branch.Entry.ChapterId, chapterId, StringComparison.Ordinal))
            {
                throw new GameException($"Story parallel branch entry must stay in the same chapter. story:{storyId} chapter:{chapterId} step:{step.StepId} branch:{branch.BranchId} targetChapter:{branch.Entry.ChapterId}");
            }

            if (string.IsNullOrWhiteSpace(branch.Entry.StepId) || steps.ContainsKey(branch.Entry.StepId) is false)
            {
                throw new GameException($"Story parallel branch entry step does not exist. story:{storyId} chapter:{chapterId} step:{step.StepId} branch:{branch.BranchId} targetStep:{branch.Entry.StepId}");
            }
        }

        private static void ValidateMergeStep(
            string storyId,
            string chapterId,
            Step step,
            IReadOnlyDictionary<string, Step> steps)
        {
            if (step.Data.MergePolicy != MergePolicy.All)
            {
                throw new GameException($"Story merge policy is invalid. story:{storyId} chapter:{chapterId} step:{step.StepId} policy:{step.Data.MergePolicy}");
            }

            if (string.IsNullOrWhiteSpace(step.Data.ParallelStepId))
            {
                throw new GameException($"Story merge parallel step id cannot be empty. story:{storyId} chapter:{chapterId} step:{step.StepId}");
            }

            if (steps.TryGetValue(step.Data.ParallelStepId, out var parallelStep) is false ||
                parallelStep.Kind != StepKind.Parallel)
            {
                throw new GameException($"Story merge parallel step does not exist. story:{storyId} chapter:{chapterId} step:{step.StepId} parallel:{step.Data.ParallelStepId}");
            }
        }

        private static void ValidateTarget(
            string storyId,
            string sourceChapterId,
            string sourceStepId,
            Target target,
            IReadOnlyDictionary<string, Chapter> chapters,
            IReadOnlyDictionary<string, Dictionary<string, Step>> stepMaps,
            string label)
        {
            if (target == null)
            {
                return;
            }

            switch (target.TargetKind)
            {
                case TargetKind.StoryEnd:
                    return;
                case TargetKind.Chapter:
                    if (string.IsNullOrWhiteSpace(target.ChapterId) || chapters.ContainsKey(target.ChapterId) is false)
                    {
                        throw new GameException($"Story target chapter does not exist. story:{storyId} chapter:{sourceChapterId} step:{sourceStepId} {label} chapter:{target.ChapterId}");
                    }

                    return;
                case TargetKind.Step:
                    if (string.IsNullOrWhiteSpace(target.ChapterId) || chapters.ContainsKey(target.ChapterId) is false)
                    {
                        throw new GameException($"Story target chapter does not exist. story:{storyId} chapter:{sourceChapterId} step:{sourceStepId} {label} chapter:{target.ChapterId}");
                    }

                    if (string.IsNullOrWhiteSpace(target.StepId) ||
                        stepMaps.TryGetValue(target.ChapterId, out var targetSteps) is false ||
                        targetSteps.ContainsKey(target.StepId) is false)
                    {
                        throw new GameException($"Story target step does not exist. story:{storyId} chapter:{sourceChapterId} step:{sourceStepId} {label} targetChapter:{target.ChapterId} targetStep:{target.StepId}");
                    }

                    return;
                default:
                    throw new GameException($"Story target kind is invalid. story:{storyId} chapter:{sourceChapterId} step:{sourceStepId} {label} kind:{target.TargetKind}");
            }
        }

        private static void ValidateText(string value, string parameterName, string message)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(message, parameterName);
            }
        }

        private static void ValidateId(string value, string fieldName, string storyId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new GameException($"Story {fieldName} cannot be empty. story:{storyId}");
            }
        }
    }
}
