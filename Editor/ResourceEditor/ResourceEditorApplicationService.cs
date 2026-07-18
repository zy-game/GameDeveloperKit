using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.ResourceEditor
{
    internal sealed class ResourceEditorApplicationService
    {
        private readonly ResourceEditorSettings m_Settings;
        private readonly ResourceEditorRegistry m_Registry;

        public ResourceEditorApplicationService(ResourceEditorSettings settings, ResourceEditorRegistry registry)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public ResourceEditorApplicationState Refresh()
        {
            var snapshot = ResourceAuthoringService.BuildSnapshot(m_Settings, m_Registry);
            return new ResourceEditorApplicationState(snapshot.Issues, snapshot.Previews);
        }

        public ResourceBuildResult Build(ResourceBuildScope scope)
        {
            m_Settings.SaveSettings();
            var buildSettings = m_Settings.BuildSettings.Copy();
            buildSettings.Scope = scope;
            return new ResourceBuildWorkflow(m_Settings, m_Registry, buildSettings).Build(out _);
        }
    }

    internal sealed class ResourceEditorApplicationState
    {
        private readonly IReadOnlyDictionary<ResourceEditorBundle, IReadOnlyList<ResourceGroupPreview>> m_Previews;

        public ResourceEditorApplicationState(
            IEnumerable<ResourceValidationIssue> issues,
            IReadOnlyDictionary<ResourceEditorBundle, IReadOnlyList<ResourceGroupPreview>> previews)
        {
            Issues = issues?.ToList() ?? throw new ArgumentNullException(nameof(issues));
            m_Previews = previews ?? throw new ArgumentNullException(nameof(previews));
        }

        public IReadOnlyList<ResourceValidationIssue> Issues { get; }

        public IReadOnlyList<ResourceGroupPreview> GetPreview(ResourceEditorBundle bundle)
        {
            return bundle != null && m_Previews.TryGetValue(bundle, out var preview)
                ? preview
                : Array.Empty<ResourceGroupPreview>();
        }
    }
}
