using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ChannelBuild
{
    public enum ChannelBuildResponderPhase
    {
        Prepare,
        Apply,
        Operation,
        Restore
    }

    public sealed class ChannelBuildStepResult
    {
        public ChannelBuildStepResult(
            string responderId,
            ChannelBuildResponderPhase phase,
            bool success,
            string message = null,
            IReadOnlyDictionary<string, string> outputs = null,
            IReadOnlyList<string> warnings = null)
        {
            ResponderId = ChannelBuildContext.RequireSafeSegment(responderId, nameof(responderId));
            if (Enum.IsDefined(typeof(ChannelBuildResponderPhase), phase) is false)
            {
                throw new ArgumentException("Channel build responder phase is not defined.", nameof(phase));
            }
            if (success is false && string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Failed channel build step requires a message.", nameof(message));
            }

            ChannelBuildContext.ValidateDictionary(outputs, nameof(outputs), true);
            ChannelBuildContext.ValidateTextList(warnings, nameof(warnings));
            Phase = phase;
            Success = success;
            Message = ChannelBuildContext.ValidateOptionalText(message, nameof(message));
            Outputs = ChannelBuildContext.CopyDictionary(outputs);
            Warnings = ChannelBuildContext.CopyList(warnings);
        }

        public string ResponderId { get; }

        public ChannelBuildResponderPhase Phase { get; }

        public bool Success { get; }

        public string Message { get; }

        public IReadOnlyDictionary<string, string> Outputs { get; }

        public IReadOnlyList<string> Warnings { get; }
    }

    public interface IChannelBuildResponder
    {
        string Id { get; }

        int Order { get; }

        IReadOnlyList<string> DependsOn { get; }

        ChannelBuildStepResult Prepare(ChannelBuildContext context);

        ChannelBuildStepResult Apply(ChannelBuildContext context);

        ChannelBuildStepResult Restore(ChannelBuildContext context);
    }

    public delegate ChannelBuildStepResult ChannelBuildOperation(ChannelBuildContext context);
}
