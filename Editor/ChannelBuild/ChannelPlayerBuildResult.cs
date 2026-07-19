using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelPlayerBuildResult
    {
        internal ChannelPlayerBuildResult(
            ChannelBuildExitCode exitCode,
            string playerOutputRoot,
            ChannelBuildResponderExecution execution = null,
            IReadOnlyList<ChannelBuildArtifact> artifacts = null,
            IReadOnlyList<ChannelBuildStepReport> steps = null,
            IReadOnlyList<string> warnings = null)
        {
            if (exitCode != ChannelBuildExitCode.Success &&
                exitCode != ChannelBuildExitCode.PipelineFailed &&
                exitCode != ChannelBuildExitCode.ResourceBuildFailed &&
                exitCode != ChannelBuildExitCode.PlayerBuildFailed)
            {
                throw new ArgumentException("Player build result exit code is invalid.", nameof(exitCode));
            }
            if (exitCode == ChannelBuildExitCode.Success && execution?.Success != true)
            {
                throw new ArgumentException("Successful player build requires a successful execution.", nameof(execution));
            }

            ExitCode = exitCode;
            PlayerOutputRoot = ChannelBuildContext.ValidateOptionalText(
                playerOutputRoot,
                nameof(playerOutputRoot));
            Execution = execution;
            Artifacts = Copy(artifacts);
            Steps = Copy(steps);
            ChannelBuildContext.ValidateTextList(warnings, nameof(warnings));
            Warnings = ChannelBuildContext.CopyList(warnings);
        }

        public bool Success => ExitCode == ChannelBuildExitCode.Success;
        public ChannelBuildExitCode ExitCode { get; }
        public string PlayerOutputRoot { get; }
        public ChannelBuildResponderExecution Execution { get; }
        public IReadOnlyList<ChannelBuildArtifact> Artifacts { get; }
        public IReadOnlyList<ChannelBuildStepReport> Steps { get; }
        public IReadOnlyList<string> Warnings { get; }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values) where T : class
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<T>();
            }

            var copy = new List<T>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == null)
                {
                    throw new ArgumentException("Player build result collection cannot contain null.", nameof(values));
                }
                copy.Add(values[i]);
            }
            return copy.AsReadOnly();
        }
    }
}
