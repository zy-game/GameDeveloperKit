using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Story.Event;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor.Event
{
    internal sealed class EventDefinitionCatalog
    {
        private static EventDefinitionCatalog s_Shared;

        private readonly Dictionary<string, EventDefinition> m_ById;

        private EventDefinitionCatalog(
            IReadOnlyList<EventDefinition> definitions,
            IReadOnlyList<string> errors)
        {
            Definitions = definitions;
            Errors = errors;
            m_ById = definitions.ToDictionary(x => x.EventId, StringComparer.Ordinal);
        }

        public static EventDefinitionCatalog Shared => s_Shared ??= Discover();

        public IReadOnlyList<EventDefinition> Definitions { get; }

        public IReadOnlyList<string> Errors { get; }

        public bool TryGet(string eventId, out EventDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                definition = null;
                return false;
            }

            return m_ById.TryGetValue(eventId, out definition);
        }

        internal static EventDefinitionCatalog Create(
            IReadOnlyList<IEventDefinitionProvider> providers)
        {
            var definitions = new List<EventDefinition>();
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (providers == null)
            {
                return new EventDefinitionCatalog(definitions, errors);
            }

            for (var providerIndex = 0; providerIndex < providers.Count; providerIndex++)
            {
                var provider = providers[providerIndex];
                if (provider == null)
                {
                    errors.Add($"Event definition provider is null. index:{providerIndex}");
                    continue;
                }

                IReadOnlyList<EventDefinition> provided;
                try
                {
                    provided = provider.GetDefinitions();
                }
                catch (Exception exception)
                {
                    errors.Add($"Event definition provider failed. provider:{provider.GetType().FullName} reason:{exception.Message}");
                    continue;
                }

                if (provided == null)
                {
                    errors.Add($"Event definition provider returned null. provider:{provider.GetType().FullName}");
                    continue;
                }

                for (var definitionIndex = 0; definitionIndex < provided.Count; definitionIndex++)
                {
                    var definition = provided[definitionIndex];
                    if (definition == null)
                    {
                        errors.Add($"Event definition is null. provider:{provider.GetType().FullName} index:{definitionIndex}");
                        continue;
                    }

                    if (!ids.Add(definition.EventId))
                    {
                        errors.Add($"Event definition ID is duplicated. event:{definition.EventId}");
                        continue;
                    }

                    if (definition.DefaultMode == EventMode.Notify && definition.Outcomes.Count != 0)
                    {
                        errors.Add($"Notify event definition cannot declare outcomes. event:{definition.EventId}");
                        continue;
                    }

                    if (definition.DefaultMode == EventMode.Request && definition.Outcomes.Count == 0)
                    {
                        errors.Add($"Request event definition requires outcomes. event:{definition.EventId}");
                        continue;
                    }

                    definitions.Add(definition);
                }
            }

            definitions.Sort((left, right) => string.Compare(left.EventId, right.EventId, StringComparison.Ordinal));
            return new EventDefinitionCatalog(definitions, errors);
        }

        private static EventDefinitionCatalog Discover()
        {
            var providers = new List<IEventDefinitionProvider>();
            var errors = new List<string>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IEventDefinitionProvider>()
                         .Where(x => x != null && !x.IsAbstract && !x.IsInterface)
                         .OrderBy(x => x.FullName, StringComparer.Ordinal))
            {
                try
                {
                    providers.Add((IEventDefinitionProvider)Activator.CreateInstance(type, true));
                }
                catch (Exception exception)
                {
                    errors.Add($"Event definition provider cannot be created. provider:{type.FullName} reason:{exception.Message}");
                }
            }

            var catalog = Create(providers);
            if (errors.Count == 0)
            {
                return catalog;
            }

            errors.AddRange(catalog.Errors);
            return new EventDefinitionCatalog(catalog.Definitions, errors);
        }
    }
}
