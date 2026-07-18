using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    internal sealed class AssetChangeSet
    {
        public AssetChangeSet(
            IEnumerable<string> importedAssets = null,
            IEnumerable<string> deletedAssets = null,
            IEnumerable<AssetMove> movedAssets = null,
            bool fullReconcile = false)
        {
            ImportedAssets = NormalizePaths(importedAssets);
            DeletedAssets = NormalizePaths(deletedAssets);
            MovedAssets = (movedAssets ?? Enumerable.Empty<AssetMove>())
                .Where(move => string.IsNullOrWhiteSpace(move.FromPath) is false ||
                               string.IsNullOrWhiteSpace(move.ToPath) is false)
                .Distinct()
                .OrderBy(move => move.FromPath, StringComparer.Ordinal)
                .ThenBy(move => move.ToPath, StringComparer.Ordinal)
                .ToArray();
            FullReconcile = fullReconcile;
        }

        public IReadOnlyList<string> ImportedAssets { get; }

        public IReadOnlyList<string> DeletedAssets { get; }

        public IReadOnlyList<AssetMove> MovedAssets { get; }

        public bool FullReconcile { get; }

        private static IReadOnlyList<string> NormalizePaths(IEnumerable<string> paths)
        {
            return (paths ?? Enumerable.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) is false)
                .Select(path => path.Replace('\\', '/').Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
    }

    internal readonly struct AssetMove : IEquatable<AssetMove>
    {
        public AssetMove(string fromPath, string toPath)
        {
            FromPath = NormalizePath(fromPath);
            ToPath = NormalizePath(toPath);
        }

        public string FromPath { get; }

        public string ToPath { get; }

        public bool Equals(AssetMove other)
        {
            return string.Equals(FromPath, other.FromPath, StringComparison.Ordinal) &&
                   string.Equals(ToPath, other.ToPath, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AssetMove other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((FromPath != null ? FromPath.GetHashCode() : 0) * 397) ^
                       (ToPath != null ? ToPath.GetHashCode() : 0);
            }
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim();
        }
    }
}
