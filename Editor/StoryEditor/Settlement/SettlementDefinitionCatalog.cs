using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Settlement;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor.Settlement
{
    internal sealed class SettlementDefinitionCatalog
    {
        private static SettlementDefinitionCatalog s_Shared;
        private readonly Dictionary<string, SettlementDefinition> m_ByKind;

        private SettlementDefinitionCatalog(
            IReadOnlyList<SettlementDefinition> definitions,
            IReadOnlyList<string> errors)
        {
            Definitions = definitions;
            Errors = errors;
            m_ByKind = definitions.ToDictionary(x => x.Kind, StringComparer.Ordinal);
        }

        public static SettlementDefinitionCatalog Shared => s_Shared ??= Discover();

        public IReadOnlyList<SettlementDefinition> Definitions { get; }

        public IReadOnlyList<string> Errors { get; }

        public bool TryGet(string kind, out SettlementDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                definition = null;
                return false;
            }

            return m_ByKind.TryGetValue(kind, out definition);
        }

        public bool TryValidate(SettlementPlan plan, out string error)
        {
            error = null;
            if (Errors.Count != 0)
            {
                error = Errors[0];
                return false;
            }

            if (plan == null)
            {
                error = "Settlement plan cannot be null.";
                return false;
            }

            for (var i = 0; i < plan.Operations.Count; i++)
            {
                var operation = plan.Operations[i];
                if (!TryGet(operation.Kind, out var definition))
                {
                    error = $"Settlement operation kind is not registered. operation:{operation.OperationId} kind:{operation.Kind}";
                    return false;
                }

                var declared = definition.Arguments.ToDictionary(x => x.Key, StringComparer.Ordinal);
                foreach (var pair in operation.Arguments.Values)
                {
                    if (!declared.TryGetValue(pair.Key, out var argument))
                    {
                        error = $"Settlement operation argument is not declared. operation:{operation.OperationId} argument:{pair.Key}";
                        return false;
                    }

                    if (!Matches(argument, pair.Value))
                    {
                        error = $"Settlement operation argument has an invalid type or value. operation:{operation.OperationId} argument:{pair.Key}";
                        return false;
                    }

                    if (argument.Required &&
                        pair.Value.Kind == ValueKind.String &&
                        string.IsNullOrWhiteSpace(pair.Value.StringValue))
                    {
                        error = $"Settlement operation argument cannot be empty. operation:{operation.OperationId} argument:{pair.Key}";
                        return false;
                    }
                }

                for (var argumentIndex = 0; argumentIndex < definition.Arguments.Count; argumentIndex++)
                {
                    var argument = definition.Arguments[argumentIndex];
                    if (argument.Required && !operation.Arguments.Values.ContainsKey(argument.Key))
                    {
                        error = $"Settlement operation argument is required. operation:{operation.OperationId} argument:{argument.Key}";
                        return false;
                    }
                }
            }

            return true;
        }

        internal static SettlementDefinitionCatalog Create(
            IReadOnlyList<ISettlementDefinitionProvider> providers)
        {
            var definitions = new List<SettlementDefinition>();
            var errors = new List<string>();
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            if (providers == null)
            {
                return new SettlementDefinitionCatalog(definitions, errors);
            }

            for (var providerIndex = 0; providerIndex < providers.Count; providerIndex++)
            {
                var provider = providers[providerIndex];
                if (provider == null)
                {
                    errors.Add($"Settlement definition provider is null. index:{providerIndex}");
                    continue;
                }

                IReadOnlyList<SettlementDefinition> provided;
                try
                {
                    provided = provider.GetDefinitions();
                }
                catch (Exception exception)
                {
                    errors.Add($"Settlement definition provider failed. provider:{provider.GetType().FullName} reason:{exception.Message}");
                    continue;
                }

                if (provided == null)
                {
                    errors.Add($"Settlement definition provider returned null. provider:{provider.GetType().FullName}");
                    continue;
                }

                for (var definitionIndex = 0; definitionIndex < provided.Count; definitionIndex++)
                {
                    var definition = provided[definitionIndex];
                    if (definition == null)
                    {
                        errors.Add($"Settlement definition is null. provider:{provider.GetType().FullName} index:{definitionIndex}");
                        continue;
                    }

                    if (!kinds.Add(definition.Kind))
                    {
                        errors.Add($"Settlement definition kind is duplicated. kind:{definition.Kind}");
                        continue;
                    }

                    definitions.Add(definition);
                }
            }

            definitions.Sort((left, right) => string.Compare(left.Kind, right.Kind, StringComparison.Ordinal));
            return new SettlementDefinitionCatalog(definitions, errors);
        }

        private static bool Matches(SettlementArgumentDefinition definition, Value value)
        {
            switch (definition.ValueType)
            {
                case ParameterValueType.Boolean:
                    return value.Kind == ValueKind.Boolean;
                case ParameterValueType.Number:
                    return value.Kind == ValueKind.Number &&
                           !double.IsNaN(value.NumberValue) &&
                           !double.IsInfinity(value.NumberValue);
                case ParameterValueType.Option:
                    return value.Kind == ValueKind.String &&
                           definition.Options.Contains(value.StringValue, StringComparer.Ordinal);
                default:
                    return value.Kind == ValueKind.String;
            }
        }

        private static SettlementDefinitionCatalog Discover()
        {
            var providers = new List<ISettlementDefinitionProvider>();
            var discoveryErrors = new List<string>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ISettlementDefinitionProvider>()
                         .Where(x => x != null && !x.IsAbstract && !x.IsInterface)
                         .OrderBy(x => x.FullName, StringComparer.Ordinal))
            {
                try
                {
                    providers.Add((ISettlementDefinitionProvider)Activator.CreateInstance(type, true));
                }
                catch (Exception exception)
                {
                    discoveryErrors.Add($"Settlement definition provider cannot be created. provider:{type.FullName} reason:{exception.Message}");
                }
            }

            var catalog = Create(providers);
            if (discoveryErrors.Count == 0)
            {
                return catalog;
            }

            discoveryErrors.AddRange(catalog.Errors);
            return new SettlementDefinitionCatalog(catalog.Definitions, discoveryErrors);
        }
    }
}
