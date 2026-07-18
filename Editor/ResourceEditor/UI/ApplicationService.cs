using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.ResourceEditor.UI
{
    internal sealed class ApplicationService
    {
        private readonly GameDeveloperKit.ResourceEditor.Authoring.Settings m_Settings;
        private readonly GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry m_Registry;

        public ApplicationService(GameDeveloperKit.ResourceEditor.Authoring.Settings settings, GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public ApplicationState Refresh()
        {
            var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(m_Settings, m_Registry);
            return new ApplicationState(snapshot.Issues, snapshot.Previews);
        }

        public ApplicationState MutateAndCommit(Action mutation)
        {
            var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.MutateAndCommit(
                m_Settings,
                m_Registry,
                mutation);
            return new ApplicationState(snapshot.Issues, snapshot.Previews);
        }

        public GameDeveloperKit.ResourceEditor.Build.Result Build(GameDeveloperKit.ResourceEditor.Build.Scope scope)
        {
            m_Settings.SaveSettings();
            var buildSettings = m_Settings.BuildSettings.Copy();
            buildSettings.Scope = scope;
            return new GameDeveloperKit.ResourceEditor.Build.Workflow(m_Settings, m_Registry, buildSettings).Build(out _);
        }
    }

    internal sealed class ApplicationState
    {
        private readonly IReadOnlyDictionary<GameDeveloperKit.ResourceEditor.Authoring.Bundle, IReadOnlyList<ResourceGroupPreview>> m_Previews;

        public ApplicationState(
            IEnumerable<GameDeveloperKit.ResourceEditor.Validation.Issue> issues,
            IReadOnlyDictionary<GameDeveloperKit.ResourceEditor.Authoring.Bundle, IReadOnlyList<ResourceGroupPreview>> previews)
        {
            Issues = issues?.ToList() ?? throw new ArgumentNullException(nameof(issues));
            m_Previews = previews ?? throw new ArgumentNullException(nameof(previews));
        }

        public IReadOnlyList<GameDeveloperKit.ResourceEditor.Validation.Issue> Issues { get; }

        public IReadOnlyList<ResourceGroupPreview> GetPreview(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            return bundle != null && m_Previews.TryGetValue(bundle, out var preview)
                ? preview
                : Array.Empty<ResourceGroupPreview>();
        }
    }
}
