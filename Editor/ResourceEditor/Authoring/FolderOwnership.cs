using System;
using System.Linq;

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    internal static class FolderOwnership
    {
        public static bool TryFindConflict(
            Settings settings,
            Bundle target,
            string sourceFolder,
            out Bundle conflictingGroup)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var normalized = Normalize(sourceFolder);
            conflictingGroup = null;
            if (normalized.Length == 0)
            {
                return false;
            }

            conflictingGroup = settings.Packages
                .Where(package => package != null)
                .SelectMany(package => package.Bundles.Where(group => group != null))
                .FirstOrDefault(group =>
                    ReferenceEquals(group, target) is false &&
                    Overlaps(normalized, group.SourceFolder));
            return conflictingGroup != null;
        }

        internal static bool Overlaps(string left, string right)
        {
            var normalizedLeft = Normalize(left);
            var normalizedRight = Normalize(right);
            if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
            {
                return false;
            }

            return string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal) ||
                   normalizedLeft.StartsWith(normalizedRight + "/", StringComparison.Ordinal) ||
                   normalizedRight.StartsWith(normalizedLeft + "/", StringComparison.Ordinal);
        }

        internal static string Normalize(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim().TrimEnd('/');
        }
    }
}
