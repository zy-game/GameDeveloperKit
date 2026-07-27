using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.UI;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UIEditor
{
    public enum UIBindingIssueCode
    {
        EmptyName,
        InvalidName,
        DuplicateName,
        MissingTarget,
        TargetOutsideDocument,
        MissingComponent,
        ComponentNotOnTarget,
        CanvasRendererSelected,
        PrefixTypeMismatch
    }

    public sealed class UIBindingIssue
    {
        public string PrefabPath { get; internal set; }

        public string HierarchyPath { get; internal set; }

        public string BindingName { get; internal set; }

        public UIBindingIssueCode Code { get; internal set; }

        public string Message { get; internal set; }
    }

    public sealed class UIBindingAuditReport
    {
        internal UIBindingAuditReport(int prefabCount, int documentCount, List<UIBindingIssue> issues)
        {
            PrefabCount = prefabCount;
            DocumentCount = documentCount;
            Issues = issues;
        }

        public int PrefabCount { get; }

        public int DocumentCount { get; }

        public IReadOnlyList<UIBindingIssue> Issues { get; }

        public bool IsValid => Issues.Count == 0;
    }

    public sealed class UIBindingMigrationReport
    {
        internal UIBindingMigrationReport(
            bool dryRun,
            int scannedPrefabCount,
            int changedPrefabCount,
            int removedComponentCount,
            int addedComponentCount,
            UIBindingAuditReport auditReport)
        {
            DryRun = dryRun;
            ScannedPrefabCount = scannedPrefabCount;
            ChangedPrefabCount = changedPrefabCount;
            RemovedComponentCount = removedComponentCount;
            AddedComponentCount = addedComponentCount;
            AuditReport = auditReport;
        }

        public bool DryRun { get; }

        public int ScannedPrefabCount { get; }

        public int ChangedPrefabCount { get; }

        public int RemovedComponentCount { get; }

        public int AddedComponentCount { get; }

        public UIBindingAuditReport AuditReport { get; }
    }

    public static class UIDocumentBindingAudit
    {
        public static UIBindingAuditReport ScanPrefabs(IReadOnlyList<string> prefabPaths)
        {
            if (prefabPaths == null)
            {
                throw new ArgumentNullException(nameof(prefabPaths));
            }

            var issues = new List<UIBindingIssue>();
            var documentCount = 0;
            var paths = NormalizePrefabPaths(prefabPaths);
            foreach (var prefabPath in paths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (root == null)
                {
                    issues.Add(CreateIssue(
                        prefabPath,
                        string.Empty,
                        string.Empty,
                        UIBindingIssueCode.MissingTarget,
                        $"UI prefab cannot be loaded: {prefabPath}"));
                    continue;
                }

                foreach (var document in root.GetComponentsInChildren<UIDocument>(true))
                {
                    documentCount++;
                    issues.AddRange(CollectIssues(document, prefabPath));
                }
            }

            return new UIBindingAuditReport(paths.Count, documentCount, issues);
        }

        public static UIBindingMigrationReport MigratePrefabs(IReadOnlyList<string> prefabPaths, bool dryRun)
        {
            if (prefabPaths == null)
            {
                throw new ArgumentNullException(nameof(prefabPaths));
            }

            var paths = NormalizePrefabPaths(prefabPaths);
            var changedPrefabCount = 0;
            var removedComponentCount = 0;
            var addedComponentCount = 0;
            foreach (var prefabPath in paths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (root == null)
                {
                    continue;
                }

                var prefabChanged = false;
                foreach (var document in root.GetComponentsInChildren<UIDocument>(true))
                {
                    var changes = BuildMigration(document);
                    if (changes.Changed is false)
                    {
                        continue;
                    }

                    prefabChanged = true;
                    removedComponentCount += changes.RemovedComponentCount;
                    addedComponentCount += changes.AddedComponentCount;
                    if (dryRun)
                    {
                        continue;
                    }

                    Undo.RecordObject(document, "Migrate UI Bindings");
                    for (var i = 0; i < changes.ComponentsByMapping.Count; i++)
                    {
                        changes.ComponentsByMapping[i].Mapping.Components =
                            changes.ComponentsByMapping[i].Components.ToArray();
                    }

                    EditorUtility.SetDirty(document);
                }

                if (prefabChanged is false)
                {
                    continue;
                }

                changedPrefabCount++;
                if (dryRun is false)
                {
                    PrefabUtility.SavePrefabAsset(root);
                }
            }

            if (dryRun is false && changedPrefabCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            return new UIBindingMigrationReport(
                dryRun,
                paths.Count,
                changedPrefabCount,
                removedComponentCount,
                addedComponentCount,
                ScanPrefabs(paths));
        }

        internal static List<UIBindingIssue> CollectIssues(UIDocument document, string prefabPath)
        {
            var issues = new List<UIBindingIssue>();
            if (document == null)
            {
                return issues;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < document.Mappings.Count; index++)
            {
                var mapping = document.Mappings[index];
                if (mapping == null)
                {
                    issues.Add(CreateIssue(
                        prefabPath,
                        GetHierarchyPath(document.transform),
                        $"#{index}",
                        UIBindingIssueCode.MissingTarget,
                        $"UI binding #{index} is missing."));
                    continue;
                }

                var issueName = string.IsNullOrWhiteSpace(mapping.Name) ? $"#{index}" : mapping.Name;
                var hierarchyPath = mapping.Target == null
                    ? GetHierarchyPath(document.transform)
                    : GetHierarchyPath(mapping.Target.transform);
                if (string.IsNullOrWhiteSpace(mapping.Name))
                {
                    issues.Add(CreateIssue(prefabPath, hierarchyPath, issueName, UIBindingIssueCode.EmptyName,
                        $"UI binding #{index} name cannot be empty."));
                }
                else
                {
                    if (UIDocumentBindingRules.IsBindingNameValid(mapping.Name) is false)
                    {
                        issues.Add(CreateIssue(prefabPath, hierarchyPath, mapping.Name, UIBindingIssueCode.InvalidName,
                            $"UI binding name '{mapping.Name}' is not a valid identifier."));
                    }

                    if (names.Add(mapping.Name) is false)
                    {
                        issues.Add(CreateIssue(prefabPath, hierarchyPath, mapping.Name, UIBindingIssueCode.DuplicateName,
                            $"Duplicate UI binding name: {mapping.Name}"));
                    }
                }

                if (mapping.Target == null)
                {
                    issues.Add(CreateIssue(prefabPath, hierarchyPath, issueName, UIBindingIssueCode.MissingTarget,
                        $"UI binding '{issueName}' target is missing."));
                }
                else if (mapping.Target != document.gameObject &&
                         mapping.Target.transform.IsChildOf(document.transform) is false)
                {
                    issues.Add(CreateIssue(prefabPath, hierarchyPath, issueName, UIBindingIssueCode.TargetOutsideDocument,
                        $"UI binding '{issueName}' target is outside its UIDocument hierarchy."));
                }

                var components = mapping.Components ?? Array.Empty<Component>();
                if (components.Length == 0)
                {
                    issues.Add(CreateIssue(prefabPath, hierarchyPath, issueName, UIBindingIssueCode.MissingComponent,
                        $"UI binding '{issueName}' must select at least one component."));
                }

                foreach (var component in components)
                {
                    if (component == null)
                    {
                        issues.Add(CreateIssue(prefabPath, hierarchyPath, issueName, UIBindingIssueCode.MissingComponent,
                            $"UI binding '{issueName}' contains a missing component reference."));
                        continue;
                    }

                    if (component is CanvasRenderer)
                    {
                        issues.Add(CreateIssue(prefabPath, hierarchyPath, issueName, UIBindingIssueCode.CanvasRendererSelected,
                            $"UI binding '{issueName}' cannot select CanvasRenderer."));
                    }

                    if (mapping.Target == null || component.gameObject != mapping.Target)
                    {
                        issues.Add(CreateIssue(prefabPath, hierarchyPath, issueName, UIBindingIssueCode.ComponentNotOnTarget,
                            $"UI binding '{issueName}' component '{component.GetType().Name}' does not belong to its target."));
                    }

                    if (string.IsNullOrWhiteSpace(mapping.Name) is false)
                    {
                        var fieldName = UIDocumentGenerator.CreateFieldName(mapping.Name, component.GetType());
                        if (fieldNames.Add(fieldName) is false)
                        {
                            issues.Add(CreateIssue(prefabPath, hierarchyPath, issueName, UIBindingIssueCode.DuplicateName,
                                $"Duplicate UI binding field name: {fieldName}"));
                        }
                    }
                }

                if (UIDocumentBindingRules.ContainsExpectedComponent(mapping.Name, components) is false)
                {
                    var expected = UIDocumentBindingRules.GetExpectedComponentName(mapping.Name);
                    issues.Add(CreateIssue(prefabPath, hierarchyPath, issueName, UIBindingIssueCode.PrefixTypeMismatch,
                        $"UI binding '{issueName}' must select {expected}."));
                }
            }

            return issues;
        }

        private static MigrationChanges BuildMigration(UIDocument document)
        {
            var componentsByMapping = new List<MappingComponents>();
            var removedComponentCount = 0;
            var addedComponentCount = 0;
            foreach (var mapping in document.Mappings)
            {
                if (mapping == null || mapping.Target == null)
                {
                    continue;
                }

                var desired = new List<Component>();
                var selected = new HashSet<Component>();
                foreach (var component in mapping.Components ?? Array.Empty<Component>())
                {
                    if (UIDocumentBindingRules.IsSelectableComponent(component) is false ||
                        component.gameObject != mapping.Target ||
                        selected.Add(component) is false)
                    {
                        removedComponentCount++;
                        continue;
                    }

                    desired.Add(component);
                }

                if (UIDocumentBindingRules.ContainsExpectedComponent(mapping.Name, desired) is false)
                {
                    var expected = UIDocumentBindingRules.SelectExpectedComponent(mapping.Name, mapping.Target);
                    if (expected != null && selected.Add(expected))
                    {
                        desired.Add(expected);
                        addedComponentCount++;
                    }
                }

                if (desired.Count == 0)
                {
                    var defaultComponent = UIDocumentBindingRules.SelectDefaultComponent(mapping.Name, mapping.Target);
                    if (defaultComponent != null)
                    {
                        desired.Add(defaultComponent);
                        addedComponentCount++;
                    }
                }

                if (ComponentsEqual(mapping.Components, desired) is false)
                {
                    componentsByMapping.Add(new MappingComponents(mapping, desired));
                }
            }

            return new MigrationChanges(componentsByMapping, removedComponentCount, addedComponentCount);
        }

        private static bool ComponentsEqual(Component[] current, IReadOnlyList<Component> desired)
        {
            current = current ?? Array.Empty<Component>();
            if (current.Length != desired.Count)
            {
                return false;
            }

            for (var i = 0; i < current.Length; i++)
            {
                if (current[i] != desired[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static List<string> NormalizePrefabPaths(IReadOnlyList<string> prefabPaths)
        {
            return prefabPaths
                .Where(path => string.IsNullOrWhiteSpace(path) is false)
                .Select(path => path.Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static UIBindingIssue CreateIssue(
            string prefabPath,
            string hierarchyPath,
            string bindingName,
            UIBindingIssueCode code,
            string message)
        {
            return new UIBindingIssue
            {
                PrefabPath = prefabPath,
                HierarchyPath = hierarchyPath,
                BindingName = bindingName,
                Code = code,
                Message = message
            };
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var names = new Stack<string>();
            var cursor = transform;
            while (cursor != null)
            {
                names.Push(cursor.name);
                cursor = cursor.parent;
            }

            return string.Join("/", names);
        }

        private readonly struct MappingComponents
        {
            public MappingComponents(UIBindMapping mapping, List<Component> components)
            {
                Mapping = mapping;
                Components = components;
            }

            public UIBindMapping Mapping { get; }

            public List<Component> Components { get; }
        }

        private readonly struct MigrationChanges
        {
            public MigrationChanges(
                List<MappingComponents> componentsByMapping,
                int removedComponentCount,
                int addedComponentCount)
            {
                ComponentsByMapping = componentsByMapping;
                RemovedComponentCount = removedComponentCount;
                AddedComponentCount = addedComponentCount;
            }

            public List<MappingComponents> ComponentsByMapping { get; }

            public int RemovedComponentCount { get; }

            public int AddedComponentCount { get; }

            public bool Changed => ComponentsByMapping.Count > 0;
        }
    }
}
