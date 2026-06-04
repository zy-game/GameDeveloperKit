using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Logger
{
    public sealed class DebugBundle
    {
        public DebugBundle(
            string id,
            DateTimeOffset createdAt,
            IReadOnlyList<LogEntry> logs,
            DebugMetricSnapshot metrics,
            IReadOnlyList<CrashLogArtifact> crashLogs,
            IReadOnlyDictionary<string, string> environment)
        {
            Id = id;
            CreatedAt = createdAt;
            Logs = logs;
            Metrics = metrics;
            CrashLogs = crashLogs;
            Environment = environment;
        }

        public string Id { get; }

        public DateTimeOffset CreatedAt { get; }

        public IReadOnlyList<LogEntry> Logs { get; }

        public DebugMetricSnapshot Metrics { get; }

        public IReadOnlyList<CrashLogArtifact> CrashLogs { get; }

        public IReadOnlyDictionary<string, string> Environment { get; }
    }
}
