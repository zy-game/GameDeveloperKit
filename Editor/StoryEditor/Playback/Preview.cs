using System;
using System.Collections.Generic;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;

namespace GameDeveloperKit.StoryEditor.Playback
{
    internal readonly struct PreviewResult
    {
        public PreviewResult(bool success, string message, int outputCount)
        {
            Success = success;
            Message = message ?? string.Empty;
            OutputCount = outputCount;
        }

        public bool Success { get; }

        public string Message { get; }

        public int OutputCount { get; }
    }

    internal static class Preview
    {
        private const int MaxOutputCount = 128;

        public static PreviewResult Play(Program program, string chapterId)
        {
            if (program == null)
            {
                return Fail("播放失败：没有可运行的 Program。", 0);
            }

            if (string.IsNullOrWhiteSpace(chapterId))
            {
                return Fail("播放失败：当前章节无效。", 0);
            }

            try
            {
                var runner = new Runner(program, PreviewFunctionResolver.Instance);
                var frame = runner.Start(FindVolumeId(program, chapterId), chapterId);
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

                return new PreviewResult(true, $"播放通过：默认路径输出 {outputCount} 步。", outputCount);
            }
            catch (Exception ex)
            {
                return Fail($"播放失败：{ex.Message}", 0);
            }
        }

        private static Frame Advance(Runner runner, Frame frame)
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

        private static Frame CompleteCommand(Runner runner, Frame frame)
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

        private static global::GameDeveloperKit.Story.Model.Command BlockingCommand(Frame frame)
        {
            if (frame?.Tracks == null)
            {
                return null;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == FrameTrackKind.Command &&
                    track.Command != null &&
                    (track.Command.WaitForCompletion || track.Command.OutcomePorts.Count > 0))
                {
                    return track.Command;
                }
            }

            return null;
        }

        private static double WaitSeconds(Frame frame)
        {
            if (frame?.Tracks == null)
            {
                return 0d;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == FrameTrackKind.Wait)
                {
                    return track.WaitSeconds;
                }
            }

            return 0d;
        }

        private static string FirstOutcomeId(global::GameDeveloperKit.Story.Model.Command command)
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

        private static PreviewResult Fail(string message, int outputCount)
        {
            return new PreviewResult(false, message, outputCount);
        }

        private static string FindVolumeId(Program program, string episodeId)
        {
            for (var volumeIndex = 0; volumeIndex < program.Volumes.Count; volumeIndex++)
            {
                var volume = program.Volumes[volumeIndex];
                for (var episodeIndex = 0; episodeIndex < volume.Episodes.Count; episodeIndex++)
                {
                    if (string.Equals(volume.Episodes[episodeIndex]?.EpisodeId, episodeId, StringComparison.Ordinal))
                    {
                        return volume.VolumeId;
                    }
                }
            }

            throw new GameException($"Story preview episode does not belong to a volume. story:{program.StoryId} episode:{episodeId}");
        }
    }

    internal sealed class PreviewFunctionResolver : IFunctionResolver
    {
        public static readonly PreviewFunctionResolver Instance = new PreviewFunctionResolver();

        private PreviewFunctionResolver()
        {
        }

        public Value Evaluate(string functionName, IReadOnlyList<Value> arguments, RuntimeContext context)
        {
            switch (functionName)
            {
                case "once":
                case "cooldown_ready":
                    return Value.FromBoolean(true);
                case "has_flag":
                default:
                    return Value.FromBoolean(false);
            }
        }
    }
}
