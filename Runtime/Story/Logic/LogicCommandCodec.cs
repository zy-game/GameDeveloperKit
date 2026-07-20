using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Logic
{
    public static class LogicCommandCodec
    {
        public const string LogicIdParameter = "logicId";
        public const string MarkerArgument = "__logicNode";

        public static global::GameDeveloperKit.Story.Model.Command Create(
            string commandId,
            string logicId,
            ArgumentBag arguments,
            IReadOnlyList<string> outputPorts,
            IReadOnlyDictionary<string, Target> outputTargets)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                throw new ArgumentException("Logic command ID cannot be empty.", nameof(commandId));
            }

            if (string.IsNullOrWhiteSpace(logicId))
            {
                throw new ArgumentException("Logic ID cannot be empty.", nameof(logicId));
            }

            ValidateOutputs(outputPorts, outputTargets);
            var values = new Dictionary<string, Value>(StringComparer.Ordinal);
            if (arguments != null)
            {
                foreach (var pair in arguments.Values)
                {
                    if (string.Equals(pair.Key, MarkerArgument, StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            $"Logic argument key is reserved. key:{MarkerArgument}",
                            nameof(arguments));
                    }

                    values.Add(pair.Key, pair.Value);
                }
            }

            values.Add(MarkerArgument, Value.FromBoolean(true));
            return new global::GameDeveloperKit.Story.Model.Command(
                commandId.Trim(),
                logicId.Trim(),
                new ArgumentBag(values),
                true,
                outputPorts,
                outputTargets);
        }

        public static bool IsLogicCommand(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command?.Arguments != null &&
                   command.Arguments.TryGetValue(MarkerArgument, out var marker) &&
                   marker.TryGetBoolean(out var enabled) &&
                   enabled;
        }

        public static bool TryDecode(
            global::GameDeveloperKit.Story.Model.Command command,
            out string logicId,
            out ArgumentBag arguments,
            out string error)
        {
            logicId = null;
            arguments = null;
            error = null;
            if (command == null)
            {
                error = "Logic command is missing.";
                return false;
            }

            if (!IsLogicCommand(command))
            {
                error = "Logic command marker is missing or invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(command.Name))
            {
                error = "Logic ID is missing.";
                return false;
            }

            if (!command.WaitForCompletion)
            {
                error = "Logic command must wait for completion.";
                return false;
            }

            try
            {
                ValidateOutputs(command.OutcomePorts, command.OutcomeTargets);
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }

            var values = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var pair in command.Arguments.Values)
            {
                if (!string.Equals(pair.Key, MarkerArgument, StringComparison.Ordinal))
                {
                    values.Add(pair.Key, pair.Value);
                }
            }

            logicId = command.Name;
            arguments = new ArgumentBag(values);
            return true;
        }

        private static void ValidateOutputs(
            IReadOnlyList<string> outputPorts,
            IReadOnlyDictionary<string, Target> outputTargets)
        {
            if (outputPorts == null || outputPorts.Count == 0)
            {
                throw new ArgumentException("Logic command requires at least one output port.", nameof(outputPorts));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < outputPorts.Count; i++)
            {
                var outputPort = outputPorts[i];
                if (string.IsNullOrWhiteSpace(outputPort) || !seen.Add(outputPort))
                {
                    throw new ArgumentException(
                        $"Logic output port is empty or duplicated. index:{i}",
                        nameof(outputPorts));
                }

                if (outputTargets == null ||
                    !outputTargets.TryGetValue(outputPort, out var target) ||
                    target == null)
                {
                    throw new ArgumentException(
                        $"Logic output target is missing. output:{outputPort}",
                        nameof(outputTargets));
                }
            }

            if (outputTargets.Count != seen.Count)
            {
                throw new ArgumentException(
                    "Logic output targets contain undeclared entries.",
                    nameof(outputTargets));
            }
        }
    }
}
