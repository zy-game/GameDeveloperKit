using System;
using System.Collections.Generic;
using GameDeveloperKit.Story;

namespace GameDeveloperKit.StoryEditor
{
    internal readonly struct StoryEditorPlaybackPreviewResult
    {
        public StoryEditorPlaybackPreviewResult(bool success, string message, int outputCount)
        {
            Success = success;
            Message = message ?? string.Empty;
            OutputCount = outputCount;
        }

        public bool Success { get; }

        public string Message { get; }

        public int OutputCount { get; }
    }

    internal static class StoryEditorPlaybackPreview
    {
        private const int MaxOutputCount = 128;

        public static StoryEditorPlaybackPreviewResult Play(StoryProgram program, string chapterId)
        {
            if (program == null)
            {
                return Fail("播放失败：没有可运行的 StoryProgram。", 0);
            }

            if (string.IsNullOrWhiteSpace(chapterId))
            {
                return Fail("播放失败：当前章节无效。", 0);
            }

            try
            {
                var runner = new StoryRunner(program, StoryEditorPreviewFunctionResolver.Instance);
                var frame = runner.Start(chapterId);
                var outputCount = 0;
                if (frame == null)
                {
                    return Fail("播放失败：入口路径没有输出，请检查章节入口节点。", 0);
                }

                if (frame.IsCompleted)
                {
                    return Fail("播放结束：入口路径直接完成，没有可观察的对白、命令、等待或选项。请检查入口节点是否连接到有效流程。", 0);
                }

                while (frame != null && frame.IsCompleted is false)
                {
                    outputCount++;
                    if (outputCount > MaxOutputCount)
                    {
                        return Fail($"播放失败：超过 {MaxOutputCount} 步，可能存在循环。", outputCount);
                    }

                    frame = Advance(runner, frame);
                }

                return new StoryEditorPlaybackPreviewResult(true, $"播放通过：默认路径输出 {outputCount} 步。", outputCount);
            }
            catch (Exception ex)
            {
                return Fail($"播放失败：{ex.Message}", 0);
            }
        }

        private static StoryFrame Advance(StoryRunner runner, StoryFrame frame)
        {
            if (frame.WaitsForChoice)
            {
                if (frame.Choices == null || frame.Choices.Count == 0)
                {
                    throw new InvalidOperationException("选项节点没有可用选项。");
                }

                return runner.Select(frame.Choices[0].ChoiceId);
            }

            if (frame.WaitsForCommand)
            {
                return CompleteCommand(runner, frame);
            }

            if (frame.WaitsForTime)
            {
                return runner.Evaluate(WaitSeconds(frame));
            }

            return runner.Continue();
        }

        private static StoryFrame CompleteCommand(StoryRunner runner, StoryFrame frame)
        {
            var command = BlockingCommand(frame);
            if (command == null)
            {
                throw new InvalidOperationException("命令输出缺少命令数据。");
            }

            var outcomeId = FirstOutcomeId(command);
            if (string.IsNullOrWhiteSpace(outcomeId) && command.WaitForCompletion is false)
            {
                return runner.Continue();
            }

            return runner.CompleteCommand(command.CommandId, outcomeId);
        }

        private static StoryCommand BlockingCommand(StoryFrame frame)
        {
            if (frame?.Tracks == null)
            {
                return null;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == StoryFrameTrackKind.Command &&
                    track.Command != null &&
                    (track.Command.WaitForCompletion || track.Command.OutcomePorts.Count > 0))
                {
                    return track.Command;
                }
            }

            return null;
        }

        private static double WaitSeconds(StoryFrame frame)
        {
            if (frame?.Tracks == null)
            {
                return 0d;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == StoryFrameTrackKind.Wait)
                {
                    return track.WaitSeconds;
                }
            }

            return 0d;
        }

        private static string FirstOutcomeId(StoryCommand command)
        {
            if (command.OutcomePorts != null && command.OutcomePorts.Count > 0)
            {
                return command.OutcomePorts[0];
            }

            if (command.OutcomeTargets != null)
            {
                foreach (var pair in command.OutcomeTargets)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) is false)
                    {
                        return pair.Key;
                    }
                }
            }

            return null;
        }

        private static StoryEditorPlaybackPreviewResult Fail(string message, int outputCount)
        {
            return new StoryEditorPlaybackPreviewResult(false, message, outputCount);
        }
    }

    internal sealed class StoryEditorPreviewFunctionResolver : IStoryFunctionResolver
    {
        public static readonly StoryEditorPreviewFunctionResolver Instance = new StoryEditorPreviewFunctionResolver();

        private StoryEditorPreviewFunctionResolver()
        {
        }

        public StoryValue Evaluate(string functionName, IReadOnlyList<StoryValue> arguments, StoryRuntimeContext context)
        {
            switch (functionName)
            {
                case "once":
                case "cooldown_ready":
                    return StoryValue.FromBoolean(true);
                case "has_flag":
                default:
                    return StoryValue.FromBoolean(false);
            }
        }
    }
}
