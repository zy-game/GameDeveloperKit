using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    internal static class EntryTable
    {
        public static VisualElement Create(Package package, Bundle bundle, Action onChanged)
        {
            var root = new VisualElement();
            root.AddToClassList("entry-table");
            root.RegisterCallback<DragUpdatedEvent>(evt => OnDragUpdated(evt));
            root.RegisterCallback<DragPerformEvent>(evt => OnDragPerform(evt, bundle, onChanged));

            var header = new VisualElement();
            header.AddToClassList("entry-table__header");
            header.Add(new Label("Address") { name = "entry-address" });
            header.Add(new Label("Asset Path") { name = "entry-path" });
            header.Add(new Label("Type") { name = "entry-type" });
            header.Add(new Label("Labels") { name = "entry-labels" });
            header.Add(new Label("Provider") { name = "entry-provider" });
            header.Add(new Label(string.Empty) { name = "entry-actions" });
            root.Add(header);

            var body = new VisualElement();
            body.AddToClassList("entry-table__body");
            if (bundle.Entries.Count == 0)
            {
                var empty = new Label("拖入 Project 资源或文件夹");
                empty.AddToClassList("entry-table__empty");
                body.Add(empty);
            }
            else
            {
                foreach (var entry in bundle.Entries.Where(entry => entry != null).OrderBy(entry => entry.Location, StringComparer.Ordinal))
                {
                    body.Add(CreateRow(package, bundle, entry, onChanged));
                }
            }

            root.Add(body);
            return root;
        }

        private static VisualElement CreateRow(Package package, Bundle bundle, AssetEntry entry, Action onChanged)
        {
            var row = new VisualElement();
            row.AddToClassList("entry-row");

            var address = new TextField { name = "entry-address", isDelayed = true };
            address.SetValueWithoutNotify(entry.Location);
            address.RegisterValueChangedCallback(evt =>
            {
                entry.Location = evt.newValue;
                onChanged?.Invoke();
            });

            var path = new Label(entry.AssetPath) { name = "entry-path" };
            var type = new Label(entry.TypeName) { name = "entry-type" };
            var labels = new Label(FormatLabels(entry.Labels)) { name = "entry-labels" };
            var provider = new Label(string.IsNullOrWhiteSpace(entry.ProviderId) ? bundle.ProviderId : entry.ProviderId) { name = "entry-provider" };
            var remove = new Button(() =>
            {
                bundle.Entries.Remove(entry);
                onChanged?.Invoke();
            })
            {
                text = "-"
            };
            remove.name = "entry-actions";
            remove.AddToClassList("icon-button");

            row.Add(address);
            row.Add(path);
            row.Add(type);
            row.Add(labels);
            row.Add(provider);
            row.Add(remove);
            return row;
        }

        private static void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = ResolveDraggedAssets().Count == 0
                ? DragAndDropVisualMode.Rejected
                : DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private static void OnDragPerform(DragPerformEvent evt, Bundle bundle, Action onChanged)
        {
            var paths = ResolveDraggedAssets();
            if (paths.Count == 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                evt.StopPropagation();
                return;
            }

            DragAndDrop.AcceptDrag();
            var changed = false;
            foreach (var path in ExpandAssetPaths(paths))
            {
                changed |= AddEntry(bundle, path);
            }

            if (changed)
            {
                onChanged?.Invoke();
            }

            evt.StopPropagation();
        }

        internal static List<string> ResolveDraggedAssets()
        {
            return DragAndDrop.objectReferences
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => string.IsNullOrWhiteSpace(path) is false)
                .Select(path => path.Replace('\\', '/'))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        internal static IEnumerable<string> ExpandAssetPaths(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (AssetDatabase.IsValidFolder(path) is false)
                {
                    yield return path;
                    continue;
                }

                foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { path }))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                    if (AssetDatabase.IsValidFolder(assetPath))
                    {
                        continue;
                    }

                    yield return assetPath;
                }
            }
        }

        internal static bool AddEntry(Bundle bundle, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || bundle.Entries.Any(entry => entry != null && entry.AssetPath == assetPath))
            {
                return false;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var labels = asset == null ? Array.Empty<string>() : AssetDatabase.GetLabels(asset);
            var entry = new AssetEntry
            {
                Guid = AssetDatabase.AssetPathToGUID(assetPath),
                AssetPath = assetPath,
                Location = ResourceProviderIds.IsResources(bundle.ProviderId)
                    ? GameDeveloperKit.ResourceEditor.Registry.UnityResourcesCollector.ToResourcesLocation(assetPath)
                    : GameDeveloperKit.ResourceEditor.Registry.ExplicitAssetCollector.NormalizeLocation(assetPath),
                TypeName = type?.Name ?? string.Empty,
                ProviderId = bundle.ProviderId
            };
            entry.EnsureDefaults(bundle.ProviderId);
            entry.Labels.AddRange(labels.Where(label => string.IsNullOrWhiteSpace(label) is false).Distinct(StringComparer.Ordinal));
            bundle.Entries.Add(entry);
            return true;
        }

        private static string FormatLabels(IReadOnlyList<string> labels)
        {
            var names = labels?
                .Where(label => string.IsNullOrWhiteSpace(label) is false)
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            return names.Length == 0 ? string.Empty : string.Join(", ", names);
        }
    }
}
