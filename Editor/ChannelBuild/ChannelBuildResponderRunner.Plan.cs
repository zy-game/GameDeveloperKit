using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ChannelBuild
{
    public static partial class ChannelBuildResponderRunner
    {
        public static IReadOnlyList<IChannelBuildResponder> CreatePlan(
            IReadOnlyList<IChannelBuildResponder> responders)
        {
            var nodes = CreatePlanNodes(responders);
            var plan = new List<IChannelBuildResponder>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                plan.Add(nodes[i].Responder);
            }
            return plan.AsReadOnly();
        }

        private static IReadOnlyList<ResponderNode> CreatePlanNodes(
            IReadOnlyList<IChannelBuildResponder> responders)
        {
            if (responders == null)
            {
                throw new ArgumentNullException(nameof(responders));
            }

            var nodesById = new Dictionary<string, ResponderNode>(StringComparer.Ordinal);
            for (var i = 0; i < responders.Count; i++)
            {
                var responder = responders[i];
                if (responder == null)
                {
                    throw new ArgumentException("Responder cannot be null.", nameof(responders));
                }

                var id = ChannelBuildContext.RequireSafeSegment(responder.Id, nameof(responders));
                if (nodesById.ContainsKey(id))
                {
                    throw new ArgumentException($"Duplicate responder id '{id}'.", nameof(responders));
                }

                var dependencies = responder.DependsOn;
                if (dependencies == null)
                {
                    throw new ArgumentException("Responder dependencies cannot be null.", nameof(responders));
                }

                var dependencyCopy = new List<string>(dependencies.Count);
                var dependencyIds = new HashSet<string>(StringComparer.Ordinal);
                for (var dependencyIndex = 0; dependencyIndex < dependencies.Count; dependencyIndex++)
                {
                    var dependencyId = ChannelBuildContext.RequireSafeSegment(
                        dependencies[dependencyIndex],
                        nameof(responders));
                    if (dependencyId == id)
                    {
                        throw new ArgumentException("Responder cannot depend on itself.", nameof(responders));
                    }
                    if (dependencyIds.Add(dependencyId) is false)
                    {
                        throw new ArgumentException("Responder dependency is duplicated.", nameof(responders));
                    }
                    dependencyCopy.Add(dependencyId);
                }

                nodesById.Add(
                    id,
                    new ResponderNode(responder, id, responder.Order, dependencyCopy.AsReadOnly()));
            }

            foreach (var node in nodesById.Values)
            {
                for (var i = 0; i < node.DependsOn.Count; i++)
                {
                    if (nodesById.ContainsKey(node.DependsOn[i]) is false)
                    {
                        throw new ArgumentException(
                            $"Responder '{node.Id}' dependency '{node.DependsOn[i]}' is not registered.",
                            nameof(responders));
                    }
                }
            }

            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var dependents = new Dictionary<string, List<ResponderNode>>(StringComparer.Ordinal);
            var ready = new List<ResponderNode>();
            foreach (var node in nodesById.Values)
            {
                indegree.Add(node.Id, node.DependsOn.Count);
                dependents.Add(node.Id, new List<ResponderNode>());
                if (node.DependsOn.Count == 0)
                {
                    ready.Add(node);
                }
            }
            foreach (var node in nodesById.Values)
            {
                for (var i = 0; i < node.DependsOn.Count; i++)
                {
                    dependents[node.DependsOn[i]].Add(node);
                }
            }

            var ordered = new List<ResponderNode>(nodesById.Count);
            while (ready.Count > 0)
            {
                ready.Sort(CompareNodes);
                var current = ready[0];
                ready.RemoveAt(0);
                ordered.Add(current);
                foreach (var dependent in dependents[current.Id])
                {
                    indegree[dependent.Id]--;
                    if (indegree[dependent.Id] == 0)
                    {
                        ready.Add(dependent);
                    }
                }
            }

            if (ordered.Count != nodesById.Count)
            {
                throw new GameException("Channel build responder dependency cycle detected.");
            }

            return ordered.AsReadOnly();
        }

        private static int CompareNodes(ResponderNode left, ResponderNode right)
        {
            var orderComparison = left.Order.CompareTo(right.Order);
            return orderComparison != 0
                ? orderComparison
                : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        }

        private sealed class ResponderNode
        {
            internal ResponderNode(
                IChannelBuildResponder responder,
                string id,
                int order,
                IReadOnlyList<string> dependsOn)
            {
                Responder = responder;
                Id = id;
                Order = order;
                DependsOn = dependsOn;
            }

            internal IChannelBuildResponder Responder { get; }

            internal string Id { get; }

            internal int Order { get; }

            internal IReadOnlyList<string> DependsOn { get; }
        }
    }
}
