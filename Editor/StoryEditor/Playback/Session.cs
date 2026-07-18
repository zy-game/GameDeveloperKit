using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Story;
using GameDeveloperKit.StoryEditor;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Validation;

namespace GameDeveloperKit.StoryEditor.Playback
{
    public sealed class Session : IDisposable
    {
        private readonly AuthoringAsset m_Asset;
        private readonly string m_ChapterId;
        private readonly IFunctionResolver m_FunctionResolver;
        private readonly List<Record> m_History = new List<Record>();

        private StoryModule m_Module;

        public Session(AuthoringAsset asset, string chapterId, IFunctionResolver functionResolver = null)
        {
            m_Asset = asset;
            m_ChapterId = chapterId;
            m_FunctionResolver = functionResolver ?? PreviewFunctionResolver.Instance;
        }

        public AuthoringAsset Asset => m_Asset;

        public string ChapterId => m_ChapterId;

        public Program Program { get; private set; }

        public ValidationReport Report { get; private set; } = new ValidationReport();

        public Frame CurrentFrame { get; private set; }

        public string ErrorMessage { get; private set; }

        public bool Started => m_Module != null && CurrentFrame != null && string.IsNullOrWhiteSpace(ErrorMessage);

        public double CurrentTime => m_Module?.CurrentRunner?.CurrentTime ?? 0d;

        public IReadOnlyList<Record> History => m_History;

        public bool Start()
        {
            Shutdown();
            ClearError();
            m_History.Clear();
            Program = null;
            CurrentFrame = null;
            Report = new ValidationReport();

            if (m_Asset == null)
            {
                SetError("播放失败：没有剧情资源。");
                return false;
            }

            try
            {
                m_Asset.EnsureDefaults();
                Program = ProgramCompiler.Compile(m_Asset, out var report);
                Report = report ?? new ValidationReport();
                if (Report.HasErrors || Program == null)
                {
                    SetError($"编译失败：{CountErrors(Report)} 个错误。");
                    return false;
                }

                m_Module = new StoryModule();
                m_Module.Startup();
                m_Module.SetFunctionResolver(m_FunctionResolver);
                var runner = m_Module.Start(Program, m_ChapterId);
                CurrentFrame = runner.CurrentFrame;
                AddRecord("启动", CurrentFrame);
                return true;
            }
            catch (Exception ex)
            {
                SetError($"播放失败：{ex.Message}");
                Shutdown();
                return false;
            }
        }

        public Frame Continue()
        {
            return Advance("继续", () => m_Module.Continue());
        }

        public Frame Select(string choiceId)
        {
            return Advance($"选择 {choiceId}", () => m_Module.Select(choiceId));
        }

        public Frame CompleteCommand(string commandId, string outcomeId)
        {
            var action = string.IsNullOrWhiteSpace(outcomeId)
                ? $"完成命令 {commandId}"
                : $"完成命令 {commandId}:{outcomeId}";
            return Advance(action, () => m_Module.CompleteCommand(commandId, outcomeId));
        }

        public Frame Evaluate(double time)
        {
            return Advance($"推进等待 {time:0.###}s", () => m_Module.Evaluate(time));
        }

        public void Dispose()
        {
            Shutdown();
        }

        public void Shutdown()
        {
            if (m_Module != null)
            {
                m_Module.Shutdown();
                m_Module = null;
            }

            CurrentFrame = null;
        }

        private Frame Advance(string action, Func<Frame> advance)
        {
            ClearError();
            if (m_Module == null)
            {
                SetError("播放失败：剧情尚未启动。");
                return CurrentFrame;
            }

            try
            {
                CurrentFrame = advance();
                AddRecord(action, CurrentFrame);
            }
            catch (Exception ex)
            {
                SetError($"播放失败：{ex.Message}");
            }

            return CurrentFrame;
        }

        private void AddRecord(string action, Frame frame)
        {
            m_History.Add(Record.FromFrame(m_History.Count + 1, action, frame));
        }

        private void SetError(string message)
        {
            ErrorMessage = message ?? string.Empty;
        }

        private void ClearError()
        {
            ErrorMessage = string.Empty;
        }

        private static int CountErrors(ValidationReport report)
        {
            return report?.Issues.Count(x => x.Severity == ValidationSeverity.Error) ?? 0;
        }
    }

    public readonly struct Record
    {
        public Record(int index, string action, string chapterId, string stepId, string kind, string summary)
        {
            Index = index;
            Action = action ?? string.Empty;
            ChapterId = chapterId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            Kind = kind ?? string.Empty;
            Summary = summary ?? string.Empty;
        }

        public int Index { get; }

        public string Action { get; }

        public string ChapterId { get; }

        public string StepId { get; }

        public string Kind { get; }

        public string Summary { get; }

        public static Record FromFrame(int index, string action, Frame frame)
        {
            if (frame == null)
            {
                return new Record(index, action, string.Empty, string.Empty, "None", "无帧");
            }

            return new Record(
                index,
                action,
                frame.Chapter?.ChapterId,
                frame.AnchorStep?.StepId,
                KindName(frame),
                Summarize(frame));
        }

        private static string KindName(Frame frame)
        {
            if (frame.IsCompleted)
            {
                return "Completed";
            }

            if (frame.Choices.Count > 0)
            {
                return "Choice";
            }

            if (frame.Tracks.Count > 0)
            {
                return frame.Tracks[0].Kind.ToString();
            }

            return "Frame";
        }

        private static string Summarize(Frame frame)
        {
            if (frame.IsCompleted)
            {
                return "完成";
            }

            if (frame.Choices.Count > 0)
            {
                var choices = string.Join(", ", frame.Choices.Select(x => string.IsNullOrWhiteSpace(x.BranchId) ? x.ChoiceId : $"{x.BranchId}:{x.ChoiceId}"));
                return frame.Tracks.Count > 0
                    ? $"轨道 {frame.Tracks.Count} / 选项 {choices}"
                    : $"选项 {choices}";
            }

            if (frame.Tracks.Count == 0)
            {
                return "空帧";
            }

            var track = frame.Tracks[0];
            if (frame.Tracks.Count > 1)
            {
                return string.Join(" + ", frame.Tracks.Select(SummarizeTrack));
            }

            return SummarizeTrack(track);
        }

        private static string SummarizeTrack(FrameTrack track)
        {
            var prefix = string.IsNullOrWhiteSpace(track.BranchId) ? string.Empty : $"{track.BranchId}:";
            switch (track.Kind)
            {
                case FrameTrackKind.Text:
                    var text = string.IsNullOrWhiteSpace(track.Speaker)
                        ? track.TextKey
                        : $"{track.Speaker}: {track.TextKey}";
                    return prefix + text;
                case FrameTrackKind.Command:
                    return prefix + (track.Command == null ? "命令" : $"命令 {track.Command.Name}");
                case FrameTrackKind.Wait:
                    return $"{prefix}等待 {track.WaitSeconds:0.###}s";
                default:
                    return prefix + track.Kind;
            }
        }
    }
}
