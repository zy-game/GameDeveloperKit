using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameDeveloperKit.Network
{
    internal sealed class NetworkOpcodeRegistry
    {
        public static readonly NetworkOpcodeRegistry Shared = new NetworkOpcodeRegistry();

        private readonly object m_Lock = new object();

        private Dictionary<int, Type> m_TypesByOpcode;

        private Dictionary<Type, int> m_OpcodesByType;

        public int GetOpcode(Type messageType)
        {
            if (messageType == null)
            {
                throw new ArgumentNullException(nameof(messageType));
            }

            EnsureLoaded();
            if (m_OpcodesByType.TryGetValue(messageType, out var opcode))
            {
                return opcode;
            }

            throw new NetworkException(
                $"Network message type '{messageType.Name}' must declare OpcodeAttribute.",
                NetworkFailureKind.InvalidResponse);
        }

        public bool TryGetType(int opcode, out Type messageType)
        {
            EnsureLoaded();
            return m_TypesByOpcode.TryGetValue(opcode, out messageType);
        }

        private void EnsureLoaded()
        {
            if (m_TypesByOpcode != null)
            {
                return;
            }

            lock (m_Lock)
            {
                if (m_TypesByOpcode != null)
                {
                    return;
                }

                Load();
            }
        }

        private void Load()
        {
            var typesByOpcode = new Dictionary<int, Type>();
            var opcodesByType = new Dictionary<Type, int>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                foreach (var type in GetTypes(assemblies[i]))
                {
                    if (type == null || type.IsAbstract || !typeof(Message).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    var attribute = type.GetCustomAttribute<OpcodeAttribute>(false);
                    if (attribute == null)
                    {
                        continue;
                    }

                    if (attribute.Code <= 0)
                    {
                        throw new GameException($"Network message opcode must be greater than zero. Type: {type.FullName}");
                    }

                    if (typesByOpcode.TryGetValue(attribute.Code, out var existingType))
                    {
                        throw new GameException(
                            $"Network message opcode '{attribute.Code}' is duplicated by '{existingType.FullName}' and '{type.FullName}'.");
                    }

                    typesByOpcode.Add(attribute.Code, type);
                    opcodesByType.Add(type, attribute.Code);
                }
            }

            m_TypesByOpcode = typesByOpcode;
            m_OpcodesByType = opcodesByType;
        }

        private static IEnumerable<Type> GetTypes(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic)
            {
                return Array.Empty<Type>();
            }

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }
    }
}
