using System;
using System.Collections.Generic;

namespace GameDeveloperKit.StoryEditor
{
    /// <summary>
    /// Story validation report。
    /// </summary>
    public sealed class StoryValidationReport
    {
        private readonly List<StoryValidationIssue> m_Issues = new List<StoryValidationIssue>();

        public IReadOnlyList<StoryValidationIssue> Issues => m_Issues;

        public bool HasErrors
        {
            get
            {
                for (var i = 0; i < m_Issues.Count; i++)
                {
                    if (m_Issues[i].Severity == StoryValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Add(StoryValidationSeverity severity, string source, string message)
        {
            m_Issues.Add(new StoryValidationIssue(severity, source, message));
        }

        public void AddError(string source, string message)
        {
            Add(StoryValidationSeverity.Error, source, message);
        }

        public void AddWarning(string source, string message)
        {
            Add(StoryValidationSeverity.Warning, source, message);
        }
    }

    /// <summary>
    /// Story validation severity。
    /// </summary>
    public enum StoryValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Story validation issue。
    /// </summary>
    public sealed class StoryValidationIssue
    {
        public StoryValidationIssue(StoryValidationSeverity severity, string source, string message)
        {
            Severity = severity;
            Source = source ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public StoryValidationSeverity Severity { get; }

        public string Source { get; }

        public string Message { get; }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Source)
                ? $"{Severity}: {Message}"
                : $"{Severity}: {Source}: {Message}";
        }
    }

    /// <summary>
    /// Story export report。
    /// </summary>
    public sealed class StoryExportReport
    {
        public StoryExportReport(string outputPath, string schemaVersion, int chapterCount, int volumeCount, int nodeCount, IReadOnlyList<StoryValidationIssue> issues)
        {
            OutputPath = outputPath ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            ChapterCount = chapterCount;
            VolumeCount = volumeCount;
            NodeCount = nodeCount;
            Issues = issues ?? Array.Empty<StoryValidationIssue>();
        }

        public string OutputPath { get; }

        public string SchemaVersion { get; }

        public int ChapterCount { get; }

        public int VolumeCount { get; }

        public int NodeCount { get; }

        public IReadOnlyList<StoryValidationIssue> Issues { get; }
    }
}
