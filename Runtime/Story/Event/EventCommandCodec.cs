using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Event
{
    public static class EventCommandCodec
    {
        public const string EventIdParameter = "eventId";
        public const string ModeParameter = "mode";
        public const string ModeArgument = "__eventMode";

        public const string NotifyMode = "notify";
        public const string RequestMode = "request";

        public static global::GameDeveloperKit.Story.Model.Command Create(
            EventRequest request,
            IReadOnlyDictionary<string, Target> outcomeTargets = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var values = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var pair in request.Arguments.Values)
            {
                if (string.Equals(pair.Key, ModeArgument, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Event argument key is reserved. key:{ModeArgument}", nameof(request));
                }

                values.Add(pair.Key, pair.Value);
            }

            values.Add(ModeArgument, Value.FromString(SerializeMode(request.Mode)));
            return new global::GameDeveloperKit.Story.Model.Command(
                request.RequestId,
                request.EventId,
                new ArgumentBag(values),
                request.Mode == EventMode.Request,
                request.Outcomes,
                outcomeTargets);
        }

        public static bool HasEventMarker(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command?.Arguments != null && command.Arguments.Values.ContainsKey(ModeArgument);
        }

        public static bool TryDecode(
            global::GameDeveloperKit.Story.Model.Command command,
            out EventRequest request,
            out string error)
        {
            request = null;
            error = null;
            if (command == null)
            {
                error = "Event command is missing.";
                return false;
            }

            if (!command.Arguments.TryGetValue(ModeArgument, out var modeValue) ||
                !modeValue.TryGetString(out var modeText) ||
                !TryParseMode(modeText, out var mode))
            {
                error = "Event mode is missing or invalid.";
                return false;
            }

            var arguments = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var pair in command.Arguments.Values)
            {
                if (!string.Equals(pair.Key, ModeArgument, StringComparison.Ordinal))
                {
                    arguments.Add(pair.Key, pair.Value);
                }
            }

            try
            {
                request = new EventRequest(
                    command.CommandId,
                    command.Name,
                    new ArgumentBag(arguments),
                    mode,
                    command.OutcomePorts);
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }

            if (mode == EventMode.Notify && command.WaitForCompletion)
            {
                request = null;
                error = "Notify event cannot wait for completion.";
                return false;
            }

            if (mode == EventMode.Request && !command.WaitForCompletion)
            {
                request = null;
                error = "Request event must wait for completion.";
                return false;
            }

            return true;
        }

        public static string SerializeMode(EventMode mode)
        {
            switch (mode)
            {
                case EventMode.Notify:
                    return NotifyMode;
                case EventMode.Request:
                    return RequestMode;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public static bool TryParseMode(string value, out EventMode mode)
        {
            switch (value)
            {
                case NotifyMode:
                    mode = EventMode.Notify;
                    return true;
                case RequestMode:
                    mode = EventMode.Request;
                    return true;
                default:
                    mode = default;
                    return false;
            }
        }
    }
}
