using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    internal sealed class ResourcePackageDetailViewBuilder
    {
        private readonly Action _onSettingsChanged;
        private readonly Action _onCollectorChanged;

        public ResourcePackageDetailViewBuilder(Action onSettingsChanged, Action onCollectorChanged)
        {
            _onSettingsChanged = onSettingsChanged ?? throw new ArgumentNullException(nameof(onSettingsChanged));
            _onCollectorChanged = onCollectorChanged ?? throw new ArgumentNullException(nameof(onCollectorChanged));
        }

        public VisualElement Build(ResourcePackageDefinition package, ResourceProjectSettingsData settings, bool hasUnsavedChanges)
        {
            var container = new VisualElement();
            container.AddToClassList("groups-section");
            container.Add(CreatePackageSummaryCard(package, settings, hasUnsavedChanges));
            container.Add(CreatePackageInfoCard(package));
            if (package.CollectionStrategy != ResourcePackageCollectionStrategy.ManualEntries)
            {
                container.Add(CreateCollectorCard(package));
                container.Add(CreateEntriesCard(package));
            }

            return container;
        }

        private VisualElement CreatePackageSummaryCard(ResourcePackageDefinition package, ResourceProjectSettingsData settings, bool hasUnsavedChanges)
        {
            var card = CreateInfoCard("Package Summary");
            var packageIndex = settings?.Packages?.IndexOf(package) ?? -1;
            var packageValidation = ResourceValidationService.ValidatePackageSummary(settings, package, packageIndex);

            var collectorName = ResourceCollectionService.GetCollectionStrategyDisplayName(package.CollectionStrategy);
            var summary = new Label(
                $"Collector: {collectorName}\n" +
                $"Entries: {package.Entries?.Count ?? 0}\n" +
                $"State: {(hasUnsavedChanges ? "Unsaved changes" : "Saved")}");
            summary.style.whiteSpace = WhiteSpace.Normal;
            card.Add(summary);

            card.Add(new HelpBox(
                packageValidation.Message,
                packageValidation.Status switch
                {
                    ResourcePackageValidationStatus.Error => HelpBoxMessageType.Error,
                    ResourcePackageValidationStatus.Warning => HelpBoxMessageType.Warning,
                    _ => HelpBoxMessageType.Info
                }));

            if (settings != null)
            {
                var validation = ResourceCollectionService.ValidateSettings(settings);
                card.Add(new HelpBox(validation.Message, validation.MessageType));
            }
            return card;
        }

        private VisualElement CreatePackageInfoCard(ResourcePackageDefinition package)
        {
            var card = CreateInfoCard("Package");
            card.Add(CreateDelayedTextField("Package Name", package.PackageName, value =>
            {
                package.PackageName = value;
                _onSettingsChanged();
            }));
            card.Add(CreateVersionSelector(package));
            card.Add(CreateEnumDropdownField("Role", package.Role, value =>
            {
                package.Role = value;
                _onSettingsChanged();
            }));
            card.Add(CreateEnumDropdownField("Build Strategy", package.BuildStrategy, value =>
            {
                package.BuildStrategy = value;
                _onSettingsChanged();
            }));
            card.Add(CreateCollectionStrategyField(package));
            return card;
        }

        private VisualElement CreateCollectionStrategyField(ResourcePackageDefinition package)
        {
            ResourceCollectionService.NormalizePackage(package);

            return CreateSingleSelectDropdownField(
                "Collector",
                ResourceCollectionService.GetSupportedStrategies().Select(ResourceCollectionService.GetCollectionStrategyDisplayName),
                ResourceCollectionService.GetCollectionStrategyDisplayName(package.CollectionStrategy),
                value =>
                {
                    ResourcePackageCollectionStrategy strategy;
                    if (string.IsNullOrWhiteSpace(value) || value == "None")
                    {
                        strategy = ResourcePackageCollectionStrategy.ManualEntries;
                    }
                    else if (!Enum.TryParse<ResourcePackageCollectionStrategy>(value, out strategy))
                    {
                        return;
                    }

                    package.CollectionStrategy = strategy;
                    _onCollectorChanged();
                });
        }

        private VisualElement CreateVersionSelector(ResourcePackageDefinition package)
        {
            var current = package.Version ?? "1.0.0";
            return CreateSingleSelectDropdownField("Version", BuildVersionChoices(current), current, value =>
            {
                package.Version = string.IsNullOrWhiteSpace(value) ? current : value;
                _onSettingsChanged();
            });
        }

        private static List<string> BuildVersionChoices(string current)
        {
            var choices = new List<string> { current };
            var parts = current.Split('.');

            if (parts.Length >= 3 && int.TryParse(parts[2], out var patch))
            {
                var bumped = $"{parts[0]}.{parts[1]}.{patch + 1}";
                if (!choices.Contains(bumped))
                {
                    choices.Add(bumped);
                }
            }

            if (parts.Length >= 2 && int.TryParse(parts[1], out var minor))
            {
                var bumped = $"{parts[0]}.{minor + 1}.0";
                if (!choices.Contains(bumped))
                {
                    choices.Add(bumped);
                }
            }

            if (parts.Length >= 1 && int.TryParse(parts[0], out var major))
            {
                var bumped = $"{major + 1}.0.0";
                if (!choices.Contains(bumped))
                {
                    choices.Add(bumped);
                }
            }

            return choices;
        }

        private VisualElement CreateCollectorCard(ResourcePackageDefinition package)
        {
            var card = CreateInfoCard("Collector Settings");
            card.Add(CreateCollectorEditor(package));
            return card;
        }

        private VisualElement CreateEntriesCard(ResourcePackageDefinition package)
        {
            var card = CreateInfoCard($"Entries ({package.Entries?.Count ?? 0})");
            card.Add(CreateEntriesPreview(package));
            return card;
        }

        private VisualElement CreateCollectorEditor(ResourcePackageDefinition package)
        {
            return CreateCollectorEditorInternal(package).Build(package);
        }

        private ICollectorEditor CreateCollectorEditorInternal(ResourcePackageDefinition package)
        {
            ResourceCollectionService.NormalizePackage(package);
            return package.CollectionStrategy switch
            {
                ResourcePackageCollectionStrategy.Directory => new DirectoryCollectorEditor(this),
                ResourcePackageCollectionStrategy.Label => new LabelCollectorEditor(this),
                ResourcePackageCollectionStrategy.Type => new TypeCollectorEditor(this),
                ResourcePackageCollectionStrategy.Dependency => new DependencyCollectorEditor(this),
                ResourcePackageCollectionStrategy.Query => new QueryCollectorEditor(this),
                _ => new DirectoryCollectorEditor(this)
            };
        }

        private VisualElement CreateCollectRootsEditor(ResourcePackageDefinition package, string title)
        {
            var container = new VisualElement();
            container.AddToClassList("resource-list-editor");

            var header = new VisualElement();
            header.AddToClassList("resource-list-header");
            var label = new Label(title);
            label.AddToClassList("resource-list-title");
            header.Add(label);
            header.Add(CreateButton("Add Root", () =>
            {
                package.CollectRoots ??= new List<string>();
                package.CollectRoots.Add("Assets/");
                _onCollectorChanged();
            }));
            container.Add(header);

            package.CollectRoots ??= new List<string>();
            for (var i = 0; i < package.CollectRoots.Count; i++)
            {
                var index = i;
                var row = new VisualElement();
                row.AddToClassList("resource-list-row");

                var field = new TextField { value = package.CollectRoots[index] ?? string.Empty, isDelayed = true };
                field.AddToClassList("resource-list-row-field");
                field.RegisterValueChangedCallback(evt =>
                {
                    package.CollectRoots[index] = evt.newValue;
                    _onCollectorChanged();
                });
                row.Add(field);

                var actions = new VisualElement();
                actions.AddToClassList("resource-list-row-actions");
                actions.Add(CreateCompactButton("...", "Pick folder", () =>
                {
                    var selected = EditorUtility.OpenFolderPanel("Select Folder", Application.dataPath, string.Empty);
                    if (string.IsNullOrWhiteSpace(selected))
                    {
                        return;
                    }

                    selected = ResourceCollectionService.ToAssetPath(selected) ?? selected;
                    package.CollectRoots[index] = selected;
                    _onCollectorChanged();
                }));
                actions.Add(CreateCompactButton("×", "Remove root", () =>
                {
                    package.CollectRoots.RemoveAt(index);
                    _onCollectorChanged();
                }));
                row.Add(actions);
                container.Add(row);
            }

            return container;
        }

        private VisualElement CreateEntriesPreview(ResourcePackageDefinition package)
        {
            var container = new VisualElement();
            container.AddToClassList("resource-entry-list");
            if (package.Entries == null || package.Entries.Count == 0)
            {
                container.Add(new HelpBox("No entries collected.", HelpBoxMessageType.Info));
                return container;
            }

            container.Add(CreateEntryTableHeader());
            var maxCount = Mathf.Min(20, package.Entries.Count);
            for (var i = 0; i < maxCount; i++)
            {
                container.Add(CreateEntryTableRow(package.Entries[i]));
            }

            if (package.Entries.Count > maxCount)
            {
                var more = new Label($"... and {package.Entries.Count - maxCount} more");
                more.AddToClassList("resource-entry-more");
                container.Add(more);
            }

            return container;
        }

        private static VisualElement CreateEntryTableHeader()
        {
            var row = new VisualElement();
            row.AddToClassList("resource-entry-table-header");
            row.Add(CreateEntryTableCell("Name", "resource-entry-col-name", true));
            row.Add(CreateEntryTableCell("Kind", "resource-entry-col-kind", true));
            row.Add(CreateEntryTableCell("Type", "resource-entry-col-type", true));
            row.Add(CreateEntryTableCell("Path", "resource-entry-col-path", true));
            return row;
        }

        private static VisualElement CreateEntryTableRow(ResourceEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("resource-entry-table-row");
            row.Add(CreateEntryTableCell(entry?.Name ?? string.Empty, "resource-entry-col-name"));
            row.Add(CreateEntryTableCell(entry?.Kind.ToString() ?? string.Empty, "resource-entry-col-kind"));
            row.Add(CreateEntryTableCell(entry?.AssetType?.Name ?? "-", "resource-entry-col-type"));
            row.Add(CreateEntryTableCell(entry?.FullPath ?? string.Empty, "resource-entry-col-path"));
            return row;
        }

        private static Label CreateEntryTableCell(string text, string className, bool header = false)
        {
            var label = new Label(text);
            label.AddToClassList(header ? "resource-entry-table-cell-header" : "resource-entry-table-cell");
            label.AddToClassList(className);
            return label;
        }

        private static VisualElement CreateSectionTitle(string text)
        {
            var label = new Label(text);
            label.AddToClassList("resource-section-title");
            return label;
        }

        private static VisualElement CreateInfoCard(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("info-card");
            card.Add(CreateSectionTitle(title));
            return card;
        }

        private static VisualElement CreateDelayedTextField(string label, string value, Action<string> onChanged)
        {
            var field = new TextField(label) { value = value ?? string.Empty, isDelayed = true };
            field.AddToClassList("resource-field");
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return field;
        }

        private static VisualElement CreateCsvTextField(string label, IList<string> values, Action<List<string>> onChanged)
        {
            return CreateDelayedTextField(label, values == null ? string.Empty : string.Join(", ", values), value =>
            {
                var list = string.IsNullOrWhiteSpace(value)
                    ? new List<string>()
                    : value.Split(',').Select(static item => item.Trim()).Where(static item => !string.IsNullOrWhiteSpace(item)).ToList();
                onChanged(list);
            });
        }

        private static VisualElement CreateMultiSelectDropdownField(
            string title,
            IEnumerable<string> options,
            IEnumerable<string> selectedValues,
            Action<List<string>> onChanged,
            bool includeEverything = false)
        {
            var field = new MultiSelectDropdownField(title)
            {
                IncludeEverything = includeEverything
            };
            field.SetOptions(options);
            field.SetValue(selectedValues);
            field.ValueChanged += values => onChanged(values.ToList());
            return field;
        }

        private static VisualElement CreateSingleSelectDropdownField(
            string title,
            IEnumerable<string> options,
            string selectedValue,
            Action<string> onChanged)
        {
            var field = new SingleSelectDropdownField(title);
            field.SetOptions(options);
            field.SetValue(selectedValue);
            field.ValueChanged += onChanged;
            return field;
        }

        private static VisualElement CreateEnumDropdownField<TEnum>(string title, TEnum selectedValue, Action<TEnum> onChanged)
            where TEnum : struct, Enum
        {
            var options = Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Select(static value => value.ToString())
                .ToList();

            return CreateSingleSelectDropdownField(title, options, selectedValue.ToString(), value =>
            {
                if (Enum.TryParse<TEnum>(value, out var parsed))
                {
                    onChanged(parsed);
                }
            });
        }

        private VisualElement CreateExtensionDropdownList(string title, ResourcePackageDefinition package, bool isExclude)
        {
            ResourceCollectionService.SanitizeExclusiveExtensions(package);
            var selected = isExclude ? package.ExcludePatterns : package.SearchExtensions;
            var options = ResourceCollectionService.CollectAvailableExtensions(package)
                .Union(selected ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            return CreateMultiSelectDropdownField(title, options, selected, values =>
            {
                var normalized = values
                    .Where(static item => !string.IsNullOrWhiteSpace(item))
                    .Select(static item => item.Trim().ToLowerInvariant())
                    .Distinct()
                    .OrderBy(static item => item, StringComparer.Ordinal)
                    .ToList();

                if (isExclude)
                {
                    package.ExcludePatterns = normalized;
                    package.SearchExtensions = (package.SearchExtensions ?? new List<string>())
                        .Where(item => !normalized.Contains(item, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                }
                else
                {
                    package.SearchExtensions = normalized;
                    package.ExcludePatterns = (package.ExcludePatterns ?? new List<string>())
                        .Where(item => !normalized.Contains(item, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                }

                ResourceCollectionService.SanitizeExclusiveExtensions(package);
                _onCollectorChanged();
            }, includeEverything: true);
        }

        private VisualElement CreateLabelDropdownList(string title, ResourcePackageDefinition package)
        {
            package.Labels ??= new List<string>();

            var allLabels = new HashSet<string>();
            var allAssetGuids = AssetDatabase.FindAssets("l:", new[] { "Assets" });
            for (var i = 0; i < allAssetGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(allAssetGuids[i]);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                {
                    continue;
                }

                var labels = AssetDatabase.GetLabels(asset);
                for (var j = 0; j < labels.Length; j++)
                {
                    allLabels.Add(labels[j]);
                }
            }

            var options = allLabels
                .Union(package.Labels, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return CreateMultiSelectDropdownField(title, options, package.Labels, values =>
            {
                package.Labels = values;
                _onCollectorChanged();
            });
        }

        private VisualElement CreateTypeDropdown(string title, ResourcePackageDefinition package)
        {
            var typeNames = new List<string>
            {
                "Texture2D", "Sprite", "Material", "Prefab", "GameObject",
                "AudioClip", "TextAsset", "SceneAsset", "Shader",
                "AnimationClip", "AnimatorController", "Font",
                "ScriptableObject", "ComputeShader", "VideoClip",
                "Mesh", "TerrainData", "LightingSettings"
            };

            if (!string.IsNullOrWhiteSpace(package.TypeName) && !typeNames.Contains(package.TypeName))
            {
                typeNames.Add(package.TypeName);
            }

            return CreateSingleSelectDropdownField(title, typeNames, package.TypeName, value =>
            {
                package.TypeName = value;
                _onCollectorChanged();
            });
        }

        private static VisualElement CreateAssetPathSelector(string title, ResourcePackageDefinition package, Action<string> onChanged)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var label = new Label(title);
            label.style.width = 120;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            container.Add(label);

            var pathField = new TextField { value = package.RootAssetPath ?? string.Empty, isDelayed = true };
            pathField.style.flexGrow = 1;
            pathField.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            container.Add(pathField);

            var objectField = new ObjectField { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
            objectField.style.width = 120;
            objectField.style.marginLeft = 4;
            objectField.RegisterValueChangedCallback(evt =>
            {
                var obj = evt.newValue;
                if (obj == null)
                {
                    return;
                }

                var assetPath = AssetDatabase.GetAssetPath(obj);
                pathField.value = assetPath;
                onChanged(assetPath);
            });
            container.Add(objectField);

            return container;
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("btn");
            if (text.Contains("Add", StringComparison.Ordinal) || text.Contains("Collect", StringComparison.Ordinal))
            {
                button.AddToClassList("btn-success");
            }
            else if (text.Contains("Export", StringComparison.Ordinal))
            {
                button.AddToClassList("btn-primary");
            }
            else if (text.Contains("Remove", StringComparison.Ordinal))
            {
                button.AddToClassList("btn-danger");
            }
            else
            {
                button.AddToClassList("btn-secondary");
            }

            button.AddToClassList("btn-sm");
            return button;
        }

        private static Button CreateCompactButton(string text, string tooltip, Action action)
        {
            var button = CreateButton(text, action);
            button.tooltip = tooltip;
            button.AddToClassList("resource-compact-button");
            if (string.Equals(text, "...", StringComparison.Ordinal) || string.Equals(text, "×", StringComparison.Ordinal))
            {
                button.AddToClassList("resource-compact-button--icon");
            }

            return button;
        }

        private interface ICollectorEditor
        {
            VisualElement Build(ResourcePackageDefinition package);
        }

        private sealed class DirectoryCollectorEditor : ICollectorEditor
        {
            private readonly ResourcePackageDetailViewBuilder _builder;

            public DirectoryCollectorEditor(ResourcePackageDetailViewBuilder builder)
            {
                _builder = builder;
            }

            public VisualElement Build(ResourcePackageDefinition package)
            {
                var container = new VisualElement();
                container.AddToClassList("resource-collector");
                container.Add(_builder.CreateCollectRootsEditor(package, "Directory Roots"));
                var includeSub = new Toggle("Include Sub Directories") { value = package.IncludeSubDirectories };
                includeSub.RegisterValueChangedCallback(evt =>
                {
                    package.IncludeSubDirectories = evt.newValue;
                    _builder._onCollectorChanged();
                });
                container.Add(includeSub);
                container.Add(_builder.CreateExtensionDropdownList("Search Extensions", package, isExclude: false));
                container.Add(_builder.CreateExtensionDropdownList("Exclude Extensions", package, isExclude: true));
                return container;
            }
        }

        private sealed class LabelCollectorEditor : ICollectorEditor
        {
            private readonly ResourcePackageDetailViewBuilder _builder;

            public LabelCollectorEditor(ResourcePackageDetailViewBuilder builder)
            {
                _builder = builder;
            }

            public VisualElement Build(ResourcePackageDefinition package)
            {
                var container = new VisualElement();
                container.AddToClassList("resource-collector");
                container.Add(_builder.CreateCollectRootsEditor(package, "Search Paths"));
                container.Add(_builder.CreateLabelDropdownList("Labels", package));
                return container;
            }
        }

        private sealed class TypeCollectorEditor : ICollectorEditor
        {
            private readonly ResourcePackageDetailViewBuilder _builder;

            public TypeCollectorEditor(ResourcePackageDetailViewBuilder builder)
            {
                _builder = builder;
            }

            public VisualElement Build(ResourcePackageDefinition package)
            {
                var container = new VisualElement();
                container.AddToClassList("resource-collector");
                container.Add(_builder.CreateCollectRootsEditor(package, "Search Paths"));
                container.Add(_builder.CreateTypeDropdown("Type", package));
                return container;
            }
        }

        private sealed class DependencyCollectorEditor : ICollectorEditor
        {
            private readonly ResourcePackageDetailViewBuilder _builder;

            public DependencyCollectorEditor(ResourcePackageDetailViewBuilder builder)
            {
                _builder = builder;
            }

            public VisualElement Build(ResourcePackageDefinition package)
            {
                var container = new VisualElement();
                container.AddToClassList("resource-collector");
                container.Add(CreateAssetPathSelector("Root Asset Path", package, value =>
                {
                    package.RootAssetPath = value;
                    _builder._onCollectorChanged();
                }));
                container.Add(CreateCsvTextField("Exclude Prefixes", package.ExcludePatterns, values =>
                {
                    package.ExcludePatterns = values;
                    _builder._onCollectorChanged();
                }));
                return container;
            }
        }

        private sealed class QueryCollectorEditor : ICollectorEditor
        {
            private readonly ResourcePackageDetailViewBuilder _builder;

            public QueryCollectorEditor(ResourcePackageDetailViewBuilder builder)
            {
                _builder = builder;
            }

            public VisualElement Build(ResourcePackageDefinition package)
            {
                var container = new VisualElement();
                container.AddToClassList("resource-collector");
                container.Add(_builder.CreateCollectRootsEditor(package, "Search Paths"));
                container.Add(CreateDelayedTextField("AssetDatabase Query", package.Query, value =>
                {
                    package.Query = value;
                    _builder._onCollectorChanged();
                }));
                return container;
            }
        }
    }
}
