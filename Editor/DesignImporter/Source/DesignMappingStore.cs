using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using IOFile = System.IO.File;

namespace GameDeveloperKit.DesignImporter
{
    [Serializable]
    internal sealed class DesignPageMapping
    {
        [JsonProperty("pageId")]
        public string PageId = string.Empty;

        [JsonProperty("updatedAtUtc")]
        public string UpdatedAtUtc = string.Empty;

        [JsonProperty("nodes")]
        public List<DesignNodeMapping> Nodes = new List<DesignNodeMapping>();
    }

    [Serializable]
    internal sealed class DesignNodeMapping
    {
        [JsonProperty("nodeId")]
        public string NodeId = string.Empty;

        [JsonProperty("parentId")]
        public string ParentId = string.Empty;

        [JsonProperty("siblingIndex")]
        public int SiblingIndex;

        [JsonProperty("visible")]
        public bool Visible = true;

        [JsonProperty("x")]
        public float X;

        [JsonProperty("y")]
        public float Y;

        [JsonProperty("width")]
        public float Width;

        [JsonProperty("height")]
        public float Height;

        [JsonProperty("anchorMin")]
        public DesignVector2 AnchorMin;

        [JsonProperty("anchorMax")]
        public DesignVector2 AnchorMax;

        [JsonProperty("pivot")]
        public DesignVector2 Pivot;

        [JsonProperty("fontName")]
        public string FontName = string.Empty;

        [JsonProperty("fontSize")]
        public float FontSize;

        [JsonProperty("component")]
        public DesignComponentKind Component;

        [JsonProperty("bindingName")]
        public string BindingName = string.Empty;

        [JsonProperty("interactable")]
        public bool Interactable = true;

        [JsonProperty("toggleValue")]
        public bool ToggleValue;

        [JsonProperty("sliderMinValue")]
        public float SliderMinValue;

        [JsonProperty("sliderMaxValue")]
        public float SliderMaxValue = 1f;

        [JsonProperty("sliderValue")]
        public float SliderValue;

        [JsonProperty("sliderWholeNumbers")]
        public bool SliderWholeNumbers;

        [JsonProperty("scrollHorizontal")]
        public bool ScrollHorizontal = true;

        [JsonProperty("scrollVertical")]
        public bool ScrollVertical = true;
    }

    internal static class DesignMappingStore
    {
        public static void Save(string projectCacheRoot, DesignPage page)
        {
            if (string.IsNullOrWhiteSpace(projectCacheRoot) || page?.Root == null)
            {
                return;
            }

            var mapping = new DesignPageMapping
            {
                PageId = page.Id,
                UpdatedAtUtc = DateTime.UtcNow.ToString("O")
            };
            AddMappings(page.Root, string.Empty, mapping.Nodes);
            var path = MappingPath(projectCacheRoot, page.Id);
            if (IOFile.Exists(path))
            {
                var previous = JsonConvert.DeserializeObject<DesignPageMapping>(IOFile.ReadAllText(path));
                var currentIds = new HashSet<string>(mapping.Nodes.Select(item => item.NodeId), StringComparer.Ordinal);
                mapping.Nodes.AddRange((previous?.Nodes ?? new List<DesignNodeMapping>())
                    .Where(item => item != null && !currentIds.Contains(item.NodeId)));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? projectCacheRoot);
            IOFile.WriteAllText(path, JsonConvert.SerializeObject(mapping, Formatting.Indented));
        }

        public static bool Apply(string projectCacheRoot, DesignPage page)
        {
            var path = MappingPath(projectCacheRoot, page?.Id);
            if (page?.Root == null || !IOFile.Exists(path))
            {
                return false;
            }

            var mapping = JsonConvert.DeserializeObject<DesignPageMapping>(IOFile.ReadAllText(path));
            if (mapping?.Nodes == null)
            {
                return false;
            }

            var nodes = page.Root.DescendantsAndSelf().ToDictionary(node => node.Id, StringComparer.Ordinal);
            var mapped = mapping.Nodes.ToDictionary(item => item.NodeId, StringComparer.Ordinal);
            foreach (var pair in mapped)
            {
                if (!nodes.TryGetValue(pair.Key, out var node))
                {
                    continue;
                }

                ApplyValues(node, pair.Value);
            }

            var originalParent = BuildParentMap(page.Root);
            foreach (var node in nodes.Values)
            {
                node.Children.Clear();
            }

            var childGroups = nodes.Values
                .Where(node => !ReferenceEquals(node, page.Root))
                .GroupBy(node => ResolveParentId(node, page.Root, nodes, mapped, originalParent), StringComparer.Ordinal);
            foreach (var group in childGroups)
            {
                if (!nodes.TryGetValue(group.Key, out var parent))
                {
                    parent = page.Root;
                }

                foreach (var child in group.OrderBy(node => SiblingIndex(node, mapped, originalParent)))
                {
                    parent.Children.Add(child);
                }
            }

            return true;
        }

        public static bool MoveNode(DesignPage page, DesignNode node, DesignNode newParent, int siblingIndex)
        {
            if (page?.Root == null || node == null || newParent == null ||
                ReferenceEquals(node, page.Root) ||
                ReferenceEquals(node, newParent) || node.DescendantsAndSelf().Contains(newParent))
            {
                return false;
            }

            var currentParent = FindParent(page.Root, node);
            if (currentParent == null)
            {
                return false;
            }

            var currentIndex = currentParent.Children.IndexOf(node);
            currentParent.Children.RemoveAt(currentIndex);
            if (ReferenceEquals(currentParent, newParent) && currentIndex < siblingIndex)
            {
                siblingIndex--;
            }

            var targetIndex = Math.Max(0, Math.Min(siblingIndex, newParent.Children.Count));
            newParent.Children.Insert(targetIndex, node);
            return true;
        }

        public static DesignNode ParentOf(DesignPage page, DesignNode node)
        {
            return page?.Root == null || node == null ? null : FindParent(page.Root, node);
        }

        public static void Reset(string projectCacheRoot, string pageId)
        {
            var path = MappingPath(projectCacheRoot, pageId);
            if (IOFile.Exists(path))
            {
                IOFile.Delete(path);
            }
        }

        private static void AddMappings(DesignNode parent, string parentId, List<DesignNodeMapping> output)
        {
            output.Add(new DesignNodeMapping
            {
                NodeId = parent.Id,
                ParentId = parentId,
                SiblingIndex = 0,
                Visible = parent.Visible,
                X = parent.X,
                Y = parent.Y,
                Width = parent.Width,
                Height = parent.Height,
                AnchorMin = Clone(parent.AnchorMin),
                AnchorMax = Clone(parent.AnchorMax),
                Pivot = Clone(parent.Pivot),
                FontName = parent.FontName,
                FontSize = parent.FontSize,
                Component = parent.Component,
                BindingName = parent.BindingName,
                Interactable = parent.Interactable,
                ToggleValue = parent.ToggleValue,
                SliderMinValue = parent.SliderMinValue,
                SliderMaxValue = parent.SliderMaxValue,
                SliderValue = parent.SliderValue,
                SliderWholeNumbers = parent.SliderWholeNumbers,
                ScrollHorizontal = parent.ScrollHorizontal,
                ScrollVertical = parent.ScrollVertical
            });
            for (var i = 0; i < parent.Children.Count; i++)
            {
                var before = output.Count;
                AddMappings(parent.Children[i], parent.Id, output);
                output[before].SiblingIndex = i;
            }
        }

        private static void ApplyValues(DesignNode node, DesignNodeMapping mapping)
        {
            node.Visible = mapping.Visible;
            node.X = mapping.X;
            node.Y = mapping.Y;
            node.Width = Math.Max(1f, mapping.Width);
            node.Height = Math.Max(1f, mapping.Height);
            node.AnchorMin = Clone(mapping.AnchorMin);
            node.AnchorMax = Clone(mapping.AnchorMax);
            node.Pivot = Clone(mapping.Pivot);
            node.FontName = mapping.FontName ?? node.FontName;
            node.FontSize = Math.Max(1f, mapping.FontSize);
            node.Component = mapping.Component;
            node.BindingName = mapping.BindingName ?? string.Empty;
            node.Interactable = mapping.Interactable;
            node.ToggleValue = mapping.ToggleValue;
            node.SliderMinValue = mapping.SliderMinValue;
            node.SliderMaxValue = Math.Max(mapping.SliderMinValue, mapping.SliderMaxValue);
            node.SliderValue = Math.Max(node.SliderMinValue, Math.Min(mapping.SliderValue, node.SliderMaxValue));
            node.SliderWholeNumbers = mapping.SliderWholeNumbers;
            node.ScrollHorizontal = mapping.ScrollHorizontal;
            node.ScrollVertical = mapping.ScrollVertical;
        }

        private static Dictionary<string, string> BuildParentMap(DesignNode root)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            AddParents(root, result);
            return result;
        }

        private static void AddParents(DesignNode parent, IDictionary<string, string> result)
        {
            for (var i = 0; i < parent.Children.Count; i++)
            {
                var child = parent.Children[i];
                result[child.Id] = parent.Id + "\n" + i;
                AddParents(child, result);
            }
        }

        private static string ResolveParentId(
            DesignNode node,
            DesignNode root,
            IReadOnlyDictionary<string, DesignNode> nodes,
            IReadOnlyDictionary<string, DesignNodeMapping> mapped,
            IReadOnlyDictionary<string, string> originalParent)
        {
            var parentId = mapped.TryGetValue(node.Id, out var mapping) ? mapping.ParentId : OriginalParentId(node.Id, originalParent);
            if (string.IsNullOrWhiteSpace(parentId) || !nodes.TryGetValue(parentId, out var parent) ||
                node.DescendantsAndSelf().Contains(parent))
            {
                return root.Id;
            }

            return parentId;
        }

        private static int SiblingIndex(
            DesignNode node,
            IReadOnlyDictionary<string, DesignNodeMapping> mapped,
            IReadOnlyDictionary<string, string> originalParent)
        {
            if (mapped.TryGetValue(node.Id, out var mapping))
            {
                return mapping.SiblingIndex;
            }

            return originalParent.TryGetValue(node.Id, out var value) && int.TryParse(value.Split('\n').Last(), out var index)
                ? index
                : int.MaxValue;
        }

        private static string OriginalParentId(string nodeId, IReadOnlyDictionary<string, string> originalParent)
        {
            return originalParent.TryGetValue(nodeId, out var value) ? value.Split('\n')[0] : string.Empty;
        }

        private static DesignNode FindParent(DesignNode parent, DesignNode target)
        {
            foreach (var child in parent.Children)
            {
                if (ReferenceEquals(child, target))
                {
                    return parent;
                }

                var nested = FindParent(child, target);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static DesignVector2 Clone(DesignVector2 value)
        {
            return value == null ? null : new DesignVector2(value.X, value.Y);
        }

        private static string MappingPath(string root, string pageId)
        {
            return Path.Combine(root, "mapping", SafeSegment(pageId) + ".json");
        }

        private static string SafeSegment(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string((value ?? "unknown").Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        }
    }
}
