using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelBuildReportContext
    {
        public ChannelBuildReportContext(string channel, string platform, string version)
        {
            Channel = ChannelBuildContext.RequireSafeSegment(channel, nameof(channel));
            Platform = ChannelBuildContext.RequireSafeSegment(platform, nameof(platform));
            Version = ChannelBuildContext.RequireSafeSegment(version, nameof(version));
        }

        [JsonProperty("channel")]
        public string Channel { get; }

        [JsonProperty("platform")]
        public string Platform { get; }

        [JsonProperty("version")]
        public string Version { get; }

        internal static ChannelBuildReportContext FromContext(ChannelBuildContext context)
        {
            return context == null
                ? null
                : new ChannelBuildReportContext(context.Channel, context.Platform, context.Version);
        }
    }

    public sealed class ChannelBuildArtifact
    {
        internal ChannelBuildArtifact(string kind, string path, string sha256, long sizeBytes)
        {
            Kind = ChannelBuildContext.RequireSafeSegment(kind, nameof(kind));
            Path = ChannelBuildContext.RequireText(path, nameof(path));
            Sha256 = ChannelBuildContext.RequireText(sha256, nameof(sha256));
            if (sha256.Length != 64)
            {
                throw new ArgumentException("Artifact SHA-256 must contain 64 hexadecimal characters.", nameof(sha256));
            }

            for (var i = 0; i < sha256.Length; i++)
            {
                var character = sha256[i];
                if ((character < '0' || character > '9') && (character < 'a' || character > 'f'))
                {
                    throw new ArgumentException("Artifact SHA-256 must be lowercase hexadecimal.", nameof(sha256));
                }
            }

            if (sizeBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeBytes));
            }

            SizeBytes = sizeBytes;
        }

        [JsonProperty("kind")]
        public string Kind { get; }

        [JsonProperty("path")]
        public string Path { get; }

        [JsonProperty("sha256")]
        public string Sha256 { get; }

        [JsonProperty("sizeBytes")]
        public long SizeBytes { get; }
    }

    public sealed class ChannelBuildStepReport
    {
        public ChannelBuildStepReport(string id, string status, string message = null)
        {
            Id = ChannelBuildContext.RequireSafeSegment(id, nameof(id));
            Status = ChannelBuildContext.RequireSafeSegment(status, nameof(status));
            Message = ChannelBuildContext.ValidateOptionalText(message, nameof(message));
        }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("status")]
        public string Status { get; }

        [JsonProperty("message")]
        public string Message { get; }
    }

    public sealed class ChannelBuildReport
    {
        public const string SucceededStatus = "succeeded";
        public const string FailedStatus = "failed";
        public const string NoFailure = "none";
        public const string InvalidInputFailure = "invalid-input";
        public const string PipelineFailure = "pipeline";
        public const string ResourceBuildFailure = "resource-build";
        public const string PlayerBuildFailure = "player-build";
        public const string ReportFailure = "report";

        public ChannelBuildReport(
            string status,
            string failureKind,
            ChannelBuildExitCode exitCode,
            ChannelBuildReportContext context,
            CiBuildMetadata ci,
            IReadOnlyList<ChannelBuildArtifact> artifacts,
            IReadOnlyList<ChannelBuildStepReport> steps,
            IReadOnlyList<string> warnings,
            DateTime startedAtUtc,
            DateTime finishedAtUtc)
        {
            ValidateOutcome(status, failureKind, exitCode, context);
            ValidateTiming(startedAtUtc, finishedAtUtc);
            Artifacts = CopyUnique(artifacts, artifact => artifact.Path, nameof(artifacts));
            Steps = CopyUnique(steps, step => step.Id, nameof(steps));
            ChannelBuildContext.ValidateTextList(warnings, nameof(warnings));

            SchemaVersion = ChannelBuildReportWriter.CurrentSchemaVersion;
            Status = status;
            FailureKind = failureKind;
            ExitCode = (int)exitCode;
            Context = context;
            Ci = ci;
            Warnings = ChannelBuildContext.CopyList(warnings);
            StartedAtUtc = startedAtUtc;
            FinishedAtUtc = finishedAtUtc;
        }

        [JsonProperty("schemaVersion", Order = 1)]
        public int SchemaVersion { get; }

        [JsonProperty("status", Order = 2)]
        public string Status { get; }

        [JsonProperty("failureKind", Order = 3)]
        public string FailureKind { get; }

        [JsonProperty("exitCode", Order = 4)]
        public int ExitCode { get; }

        [JsonProperty("context", Order = 5)]
        public ChannelBuildReportContext Context { get; }

        [JsonProperty("ci", Order = 6)]
        public CiBuildMetadata Ci { get; }

        [JsonProperty("artifacts", Order = 7)]
        public IReadOnlyList<ChannelBuildArtifact> Artifacts { get; }

        [JsonProperty("steps", Order = 8)]
        public IReadOnlyList<ChannelBuildStepReport> Steps { get; }

        [JsonProperty("warnings", Order = 9)]
        public IReadOnlyList<string> Warnings { get; }

        [JsonProperty("startedAtUtc", Order = 10)]
        public DateTime StartedAtUtc { get; }

        [JsonProperty("finishedAtUtc", Order = 11)]
        public DateTime FinishedAtUtc { get; }

        private static void ValidateOutcome(
            string status,
            string failureKind,
            ChannelBuildExitCode exitCode,
            ChannelBuildReportContext context)
        {
            var succeeded = string.Equals(status, SucceededStatus, StringComparison.Ordinal);
            var failed = string.Equals(status, FailedStatus, StringComparison.Ordinal);
            if ((succeeded || failed) is false)
            {
                throw new ArgumentException("Report status is invalid.", nameof(status));
            }

            if (succeeded &&
                (exitCode != ChannelBuildExitCode.Success || failureKind != NoFailure || context == null))
            {
                throw new ArgumentException("Succeeded report outcome is inconsistent.", nameof(exitCode));
            }

            var expectedFailureKind = FailureKindFor(exitCode);
            if (failed && (expectedFailureKind == null || expectedFailureKind != failureKind))
            {
                throw new ArgumentException("Failed report outcome is inconsistent.", nameof(exitCode));
            }
        }

        internal static string FailureKindFor(ChannelBuildExitCode exitCode)
        {
            switch (exitCode)
            {
                case ChannelBuildExitCode.InvalidInput:
                    return InvalidInputFailure;
                case ChannelBuildExitCode.PipelineFailed:
                    return PipelineFailure;
                case ChannelBuildExitCode.ResourceBuildFailed:
                    return ResourceBuildFailure;
                case ChannelBuildExitCode.PlayerBuildFailed:
                    return PlayerBuildFailure;
                case ChannelBuildExitCode.ReportFailed:
                    return ReportFailure;
                default:
                    return null;
            }
        }

        private static void ValidateTiming(DateTime startedAtUtc, DateTime finishedAtUtc)
        {
            if (startedAtUtc.Kind != DateTimeKind.Utc || finishedAtUtc.Kind != DateTimeKind.Utc || finishedAtUtc < startedAtUtc)
            {
                throw new ArgumentException("Report timing must be ordered UTC values.", nameof(finishedAtUtc));
            }
        }

        private static IReadOnlyList<T> CopyUnique<T>(
            IReadOnlyList<T> values,
            Func<T, string> keySelector,
            string parameterName) where T : class
        {
            var result = new List<T>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (values != null)
            {
                for (var i = 0; i < values.Count; i++)
                {
                    var value = values[i];
                    if (value == null || keys.Add(keySelector(value)) is false)
                    {
                        throw new ArgumentException("Report collection contains null or duplicate entries.", parameterName);
                    }

                    result.Add(value);
                }
            }

            return result.AsReadOnly();
        }
    }
}
