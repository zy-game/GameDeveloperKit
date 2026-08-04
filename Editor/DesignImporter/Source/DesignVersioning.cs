using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace GameDeveloperKit.DesignImporter
{
    internal enum DesignChangeKind
    {
        Unchanged,
        New,
        Updated,
        Deleted
    }

    internal sealed class DesignPageChange
    {
        public DesignPageChange(DesignPage page, DesignChangeKind kind)
        {
            Page = page;
            Kind = kind;
        }

        public DesignPage Page { get; }
        public DesignChangeKind Kind { get; }
    }

    internal sealed class DesignVersionDiffResult
    {
        public string PreviousRevision = string.Empty;
        public string CurrentRevision = string.Empty;
        public readonly List<DesignPageChange> Pages = new List<DesignPageChange>();
        public readonly Dictionary<string, DesignChangeKind> NodeChanges =
            new Dictionary<string, DesignChangeKind>(StringComparer.Ordinal);

        public DesignChangeKind NodeChange(string pageId, string nodeId)
        {
            return NodeChanges.TryGetValue(Key(pageId, nodeId), out var value)
                ? value
                : DesignChangeKind.Unchanged;
        }

        internal static string Key(string pageId, string nodeId)
        {
            return (pageId ?? string.Empty) + ":" + (nodeId ?? string.Empty);
        }
    }

    internal static class DesignVersionDiff
    {
        private static readonly JsonSerializerSettings s_HashSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.None
        };

        public static DesignVersionDiffResult Compare(DesignDocument previous, DesignDocument current)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            current.Normalize();
            previous?.Normalize();
            var result = new DesignVersionDiffResult
            {
                PreviousRevision = previous == null ? string.Empty : ComputeRevision(previous),
                CurrentRevision = ComputeRevision(current)
            };
            var previousPages = (previous?.Pages ?? new List<DesignPage>())
                .Where(page => page != null)
                .ToDictionary(page => page.Id, StringComparer.Ordinal);
            var currentPages = current.Pages
                .Where(page => page != null)
                .ToDictionary(page => page.Id, StringComparer.Ordinal);

            foreach (var page in current.Pages.Where(page => page != null))
            {
                var kind = !previousPages.TryGetValue(page.Id, out var oldPage)
                    ? DesignChangeKind.New
                    : HashPage(previous, oldPage) == HashPage(current, page)
                        ? DesignChangeKind.Unchanged
                        : DesignChangeKind.Updated;
                result.Pages.Add(new DesignPageChange(page, kind));
                CompareNodes(page.Id, oldPage?.Root, page.Root, result.NodeChanges);
            }

            if (previous != null)
            {
                foreach (var page in previous.Pages.Where(page => page != null && !currentPages.ContainsKey(page.Id)))
                {
                    page.Selected = false;
                    result.Pages.Add(new DesignPageChange(page, DesignChangeKind.Deleted));
                    AddNodeState(page.Id, page.Root, DesignChangeKind.Deleted, result.NodeChanges);
                }
            }

            return result;
        }

        public static string ComputeRevision(DesignDocument document)
        {
            var pageHashes = document.Pages
                .Where(page => page != null)
                .OrderBy(page => page.Id, StringComparer.Ordinal)
                .Select(page => page.Id + "=" + HashPage(document, page));
            return Hash(string.Join("\n", pageHashes));
        }

        private static string HashPage(DesignDocument document, DesignPage page)
        {
            var assetIds = new HashSet<string>(
                page.Root?.DescendantsAndSelf()
                    .Where(node => node.Kind == DesignNodeKind.Image)
                    .Select(node => node.AssetId) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);
            var assets = document.Assets
                .Where(asset => asset != null && assetIds.Contains(asset.Id))
                .OrderBy(asset => asset.Id, StringComparer.Ordinal)
                .ToArray();
            return Hash(JsonConvert.SerializeObject(new { page, assets }, s_HashSettings));
        }

        private static void CompareNodes(
            string pageId,
            DesignNode previous,
            DesignNode current,
            IDictionary<string, DesignChangeKind> changes)
        {
            var oldNodes = Flatten(previous);
            var newNodes = Flatten(current);
            foreach (var pair in newNodes)
            {
                changes[DesignVersionDiffResult.Key(pageId, pair.Key)] = !oldNodes.TryGetValue(pair.Key, out var oldNode)
                    ? DesignChangeKind.New
                    : HashNode(oldNode) == HashNode(pair.Value)
                        ? DesignChangeKind.Unchanged
                        : DesignChangeKind.Updated;
            }

            foreach (var pair in oldNodes.Where(pair => !newNodes.ContainsKey(pair.Key)))
            {
                changes[DesignVersionDiffResult.Key(pageId, pair.Key)] = DesignChangeKind.Deleted;
            }
        }

        private static Dictionary<string, DesignNode> Flatten(DesignNode root)
        {
            return root?.DescendantsAndSelf().ToDictionary(node => node.Id, StringComparer.Ordinal)
                   ?? new Dictionary<string, DesignNode>(StringComparer.Ordinal);
        }

        private static void AddNodeState(
            string pageId,
            DesignNode node,
            DesignChangeKind kind,
            IDictionary<string, DesignChangeKind> changes)
        {
            if (node == null)
            {
                return;
            }

            foreach (var item in node.DescendantsAndSelf())
            {
                changes[DesignVersionDiffResult.Key(pageId, item.Id)] = kind;
            }
        }

        private static string HashNode(DesignNode node)
        {
            return Hash(JsonConvert.SerializeObject(node, s_HashSettings));
        }

        private static string Hash(string value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
