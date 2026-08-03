using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.MediaEditor;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class HlsBatchPublishCandidate
    {
        public HlsBatchPublishCandidate(string sourcePath, string fingerprint, CatalogItem existing)
        {
            SourcePath = sourcePath;
            Fingerprint = fingerprint;
            Existing = existing;
        }

        public string SourcePath { get; }
        public string Fingerprint { get; }
        public CatalogItem Existing { get; }
    }

    internal sealed class HlsBatchPublishRejectedSource
    {
        public HlsBatchPublishRejectedSource(string sourcePath, string reason)
        {
            SourcePath = sourcePath ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string SourcePath { get; }
        public string Reason { get; }
    }

    internal sealed class HlsBatchPublishPreflightResult
    {
        public HlsBatchPublishPreflightResult(
            IReadOnlyList<HlsBatchPublishCandidate> candidates,
            IReadOnlyList<HlsBatchPublishRejectedSource> rejected)
        {
            Candidates = candidates;
            Rejected = rejected;
        }

        public IReadOnlyList<HlsBatchPublishCandidate> Candidates { get; }
        public IReadOnlyList<HlsBatchPublishRejectedSource> Rejected { get; }
        public int ExistingCount => Candidates.Count(candidate => candidate.Existing != null);

        public IReadOnlyList<HlsPublishIntent> CreateIntents(bool overwriteExisting)
        {
            var intents = Candidates
                .Where(candidate => overwriteExisting || candidate.Existing == null)
                .Select(candidate => new HlsPublishIntent(
                    candidate.SourcePath,
                    candidate.Existing?.Name ?? Path.GetFileNameWithoutExtension(candidate.SourcePath),
                    candidate.Fingerprint,
                    candidate.Existing?.MediaId ?? HlsPublishWorkflow.CreateMediaId(),
                    candidate.Existing != null,
                    candidate.Existing?.CreatedAtUtc,
                    candidate.Existing?.UpdatedAtUtc))
                .ToArray();
            return new ReadOnlyCollection<HlsPublishIntent>(intents);
        }
    }

    internal static class HlsBatchPublishPreflight
    {
        public static async UniTask<HlsBatchPublishPreflightResult> CreateAsync(
            IEnumerable<string> sourcePaths,
            HlsCatalogDocument catalog,
            Func<string, CancellationToken, UniTask<string>> fingerprintAsync,
            CancellationToken cancellationToken,
            Action<int, int, string> progress = null)
        {
            if (sourcePaths == null)
            {
                throw new ArgumentNullException(nameof(sourcePaths));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (fingerprintAsync == null)
            {
                throw new ArgumentNullException(nameof(fingerprintAsync));
            }

            var rejected = new List<HlsBatchPublishRejectedSource>();
            var validPaths = NormalizePaths(sourcePaths, rejected);
            var candidates = new List<HlsBatchPublishCandidate>(validPaths.Count);
            var fingerprints = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < validPaths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = validPaths[i];
                progress?.Invoke(i + 1, validPaths.Count, path);
                string fingerprint;
                try
                {
                    fingerprint = await fingerprintAsync(path, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    rejected.Add(new HlsBatchPublishRejectedSource(
                        path,
                        "无法计算源视频指纹：" + exception.Message));
                    continue;
                }

                if (fingerprints.Add(fingerprint) is false)
                {
                    rejected.Add(new HlsBatchPublishRejectedSource(path, "与本批次中的其他文件内容重复。"));
                    continue;
                }

                var existing = catalog.Items.FirstOrDefault(item =>
                    string.Equals(item.SourceSha256, fingerprint, StringComparison.Ordinal));
                candidates.Add(new HlsBatchPublishCandidate(path, fingerprint, existing));
            }

            return new HlsBatchPublishPreflightResult(
                new ReadOnlyCollection<HlsBatchPublishCandidate>(candidates),
                new ReadOnlyCollection<HlsBatchPublishRejectedSource>(rejected));
        }

        private static List<string> NormalizePaths(
            IEnumerable<string> sourcePaths,
            ICollection<HlsBatchPublishRejectedSource> rejected)
        {
            var comparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var seen = new HashSet<string>(comparer);
            var result = new List<string>();
            foreach (var sourcePath in sourcePaths)
            {
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    rejected.Add(new HlsBatchPublishRejectedSource(sourcePath, "路径为空。"));
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(sourcePath).Replace('\\', '/');
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is PathTooLongException ||
                    exception is System.Security.SecurityException)
                {
                    rejected.Add(new HlsBatchPublishRejectedSource(sourcePath, "路径无效。"));
                    continue;
                }

                if (System.IO.File.Exists(fullPath) is false)
                {
                    rejected.Add(new HlsBatchPublishRejectedSource(fullPath, "文件不存在。"));
                    continue;
                }

                if (string.Equals(Path.GetExtension(fullPath), ".mp4", StringComparison.OrdinalIgnoreCase) is false)
                {
                    rejected.Add(new HlsBatchPublishRejectedSource(fullPath, "只支持 MP4 文件。"));
                    continue;
                }

                if (seen.Add(fullPath) is false)
                {
                    rejected.Add(new HlsBatchPublishRejectedSource(fullPath, "路径重复。"));
                    continue;
                }

                result.Add(fullPath);
            }

            return result;
        }
    }
}
