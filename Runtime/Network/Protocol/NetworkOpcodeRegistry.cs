using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace GameDeveloperKit.Network
{
    public static class NetworkMessageRegistry
    {
        private static readonly object s_Lock = new object();
        private static readonly Dictionary<int, Type> s_TypesByOpcode = new Dictionary<int, Type>();
        private static readonly Dictionary<Type, int> s_OpcodesByType = new Dictionary<Type, int>();

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterGenerated<TMessage>(int opcode) where TMessage : Message
        {
            if (opcode <= 0)
            {
                throw new GameException($"Network message opcode must be greater than zero. Type: {typeof(TMessage).FullName}");
            }

            var messageType = typeof(TMessage);
            if (messageType.IsAbstract || messageType.ContainsGenericParameters)
            {
                throw new GameException($"Network message type '{messageType.FullName}' must be concrete.");
            }

            lock (s_Lock)
            {
                if (s_TypesByOpcode.TryGetValue(opcode, out var existingType) && existingType != messageType)
                {
                    throw new GameException(
                        $"Network message opcode '{opcode}' is duplicated by '{existingType.FullName}' and '{messageType.FullName}'.");
                }

                if (s_OpcodesByType.TryGetValue(messageType, out var existingOpcode) && existingOpcode != opcode)
                {
                    throw new GameException(
                        $"Network message type '{messageType.FullName}' is registered with both '{existingOpcode}' and '{opcode}'.");
                }

                s_TypesByOpcode[opcode] = messageType;
                s_OpcodesByType[messageType] = opcode;
            }
        }

        internal static int GetOpcode(Type messageType)
        {
            if (messageType == null)
            {
                throw new ArgumentNullException(nameof(messageType));
            }

            lock (s_Lock)
            {
                if (s_OpcodesByType.TryGetValue(messageType, out var opcode))
                {
                    return opcode;
                }
            }

            throw new NetworkException(
                $"Network message type '{messageType.FullName}' has no generated opcode registration.",
                NetworkFailureKind.InvalidResponse);
        }

        internal static bool TryGetType(int opcode, out Type messageType)
        {
            lock (s_Lock)
            {
                return s_TypesByOpcode.TryGetValue(opcode, out messageType);
            }
        }
    }
}
