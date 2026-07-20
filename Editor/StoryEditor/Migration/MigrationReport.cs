using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.StoryEditor.Model;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Migration
{
    internal enum MigrationIssueSeverity
    {
        Warning,
        Conflict
    }

    internal enum MigrationChangeKind
    {
        Added,
        Renamed,
        Split,
        Converted
    }

    internal readonly struct MigrationIssue
    {
        public MigrationIssue(MigrationIssueSeverity severity, string code, string location, string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Location = location ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public MigrationIssueSeverity Severity { get; }

        public string Code { get; }

        public string Location { get; }

        public string Message { get; }
    }

    internal readonly struct MigrationChange
    {
        public MigrationChange(MigrationChangeKind kind, string location, string description)
        {
            Kind = kind;
            Location = location ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public MigrationChangeKind Kind { get; }

        public string Location { get; }

        public string Description { get; }
    }

    internal sealed class MigrationReport
    {
        private readonly List<MigrationChange> m_Changes = new List<MigrationChange>();
        private readonly List<MigrationIssue> m_Issues = new List<MigrationIssue>();

        public bool CanApply => m_Issues.All(x => x.Severity != MigrationIssueSeverity.Conflict);

        public IReadOnlyList<MigrationChange> Changes => m_Changes;

        public IReadOnlyList<MigrationIssue> Issues => m_Issues;

        public bool HasWarnings => m_Issues.Any(x => x.Severity == MigrationIssueSeverity.Warning);

        public void AddChange(MigrationChangeKind kind, string location, string description)
        {
            m_Changes.Add(new MigrationChange(kind, location, description));
        }

        public void AddWarning(string code, string location, string message)
        {
            m_Issues.Add(new MigrationIssue(MigrationIssueSeverity.Warning, code, location, message));
        }

        public void AddConflict(string code, string location, string message)
        {
            m_Issues.Add(new MigrationIssue(MigrationIssueSeverity.Conflict, code, location, message));
        }

        internal void Sort()
        {
            m_Changes.Sort(CompareChanges);
            m_Issues.Sort(CompareIssues);
        }

        private static int CompareChanges(MigrationChange left, MigrationChange right)
        {
            var result = string.Compare(left.Location, right.Location, StringComparison.Ordinal);
            if (result != 0)
            {
                return result;
            }

            result = left.Kind.CompareTo(right.Kind);
            return result != 0
                ? result
                : string.Compare(left.Description, right.Description, StringComparison.Ordinal);
        }

        private static int CompareIssues(MigrationIssue left, MigrationIssue right)
        {
            var result = string.Compare(left.Location, right.Location, StringComparison.Ordinal);
            if (result != 0)
            {
                return result;
            }

            result = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
            return result != 0 ? result : left.Severity.CompareTo(right.Severity);
        }
    }

    internal sealed class MigrationPreview : IDisposable
    {
        public MigrationPreview(AuthoringAsset candidate, MigrationReport report, bool isNoOp)
        {
            Candidate = candidate;
            Report = report ?? throw new ArgumentNullException(nameof(report));
            IsNoOp = isNoOp;
        }

        public AuthoringAsset Candidate { get; private set; }

        public MigrationReport Report { get; }

        public bool IsNoOp { get; }

        public void Dispose()
        {
            if (Candidate != null)
            {
                UnityEngine.Object.DestroyImmediate(Candidate);
                Candidate = null;
            }
        }
    }

    internal enum MigrationApplyStatus
    {
        Applied,
        NoOp,
        Blocked,
        WarningConfirmationRequired
    }

    internal readonly struct MigrationResult
    {
        public MigrationResult(MigrationApplyStatus status, MigrationReport report)
        {
            Status = status;
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public MigrationApplyStatus Status { get; }

        public MigrationReport Report { get; }

        public bool Succeeded => Status == MigrationApplyStatus.Applied || Status == MigrationApplyStatus.NoOp;
    }
}
