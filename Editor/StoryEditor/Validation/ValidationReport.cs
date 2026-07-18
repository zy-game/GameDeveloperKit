using System;
using System.Collections.Generic;

namespace GameDeveloperKit.StoryEditor.Validation
{
    /// <summary>
    /// Story validation report。
    /// </summary>
    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> m_Issues = new List<ValidationIssue>();

        public IReadOnlyList<ValidationIssue> Issues => m_Issues;

        public bool HasErrors
        {
            get
            {
                for (var i = 0; i < m_Issues.Count; i++)
                {
                    if (m_Issues[i].Severity == ValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Add(ValidationSeverity severity, string source, string message)
        {
            m_Issues.Add(new ValidationIssue(severity, source, message));
        }

        public void AddError(string source, string message)
        {
            Add(ValidationSeverity.Error, source, message);
        }

        public void AddWarning(string source, string message)
        {
            Add(ValidationSeverity.Warning, source, message);
        }
    }

    /// <summary>
    /// Story validation severity。
    /// </summary>
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Story validation issue。
    /// </summary>
    public sealed class ValidationIssue
    {
        public ValidationIssue(ValidationSeverity severity, string source, string message)
        {
            Severity = severity;
            Source = source ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ValidationSeverity Severity { get; }

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
    public sealed class ExportReport
    {
        public ExportReport(string outputPath, string schemaVersion, int chapterCount, int volumeCount, int nodeCount, IReadOnlyList<ValidationIssue> issues)
        {
            OutputPath = outputPath ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            ChapterCount = chapterCount;
            VolumeCount = volumeCount;
            NodeCount = nodeCount;
            Issues = issues ?? Array.Empty<ValidationIssue>();
        }

        public string OutputPath { get; }

        public string SchemaVersion { get; }

        public int ChapterCount { get; }

        public int VolumeCount { get; }

        public int NodeCount { get; }

        public IReadOnlyList<ValidationIssue> Issues { get; }
    }
}
