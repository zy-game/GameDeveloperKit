using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace GameDeveloperKit.Story.Logic
{
    public static class LogicNodeRegistry
    {
        private static readonly object s_Lock = new object();
        private static readonly Dictionary<string, Registration> s_Registrations =
            new Dictionary<string, Registration>(StringComparer.Ordinal);

        public static void Register<TLogicNode>(string logicId, Func<TLogicNode> factory)
            where TLogicNode : class, ILogicNode
        {
            if (string.IsNullOrWhiteSpace(logicId))
            {
                throw new ArgumentException("Logic ID cannot be empty.", nameof(logicId));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            var normalizedId = logicId.Trim();
            var nodeType = typeof(TLogicNode);
            lock (s_Lock)
            {
                if (s_Registrations.TryGetValue(normalizedId, out var existing))
                {
                    if (existing.NodeType == nodeType)
                    {
                        return;
                    }

                    throw new GameException(
                        $"Logic node ID is already registered. logic:{normalizedId} " +
                        $"existing:{existing.NodeType.FullName} incoming:{nodeType.FullName}");
                }

                s_Registrations.Add(
                    normalizedId,
                    new Registration(nodeType, () => factory()));
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterGenerated<TLogicNode>(string logicId)
            where TLogicNode : class, ILogicNode, new()
        {
            Register(logicId, static () => new TLogicNode());
        }

        public static ILogicNode Create(string logicId)
        {
            if (string.IsNullOrWhiteSpace(logicId))
            {
                throw new ArgumentException("Logic ID cannot be empty.", nameof(logicId));
            }

            Registration registration;
            var normalizedId = logicId.Trim();
            lock (s_Lock)
            {
                if (!s_Registrations.TryGetValue(normalizedId, out registration))
                {
                    throw new GameException($"Logic node is not registered. logic:{normalizedId}");
                }
            }

            var node = registration.Factory();
            if (node == null)
            {
                throw new GameException(
                    $"Logic node factory returned null. logic:{normalizedId} type:{registration.NodeType.FullName}");
            }

            return node;
        }

        private readonly struct Registration
        {
            public Registration(Type nodeType, Func<ILogicNode> factory)
            {
                NodeType = nodeType;
                Factory = factory;
            }

            public Type NodeType { get; }

            public Func<ILogicNode> Factory { get; }
        }
    }
}
