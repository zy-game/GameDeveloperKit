using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ChannelBuild
{
    internal sealed class ChannelBuildArguments
    {
        internal const string Channel = "-gdkChannel";
        internal const string Environment = "-gdkEnvironment";
        internal const string BuildTarget = "-gdkBuildTarget";
        internal const string Version = "-gdkVersion";
        internal const string PlayerBuildNumber = "-gdkPlayerBuildNumber";
        internal const string Profile = "-gdkProfile";
        internal const string ProfileCatalog = "-gdkProfileCatalog";
        internal const string OutputRoot = "-gdkOutputRoot";
        internal const string Flavor = "-gdkFlavor";
        internal const string RemoteRoot = "-gdkRemoteRoot";
        internal const string MinimumClientBuild = "-gdkMinimumClientBuild";
        internal const string MaximumClientBuild = "-gdkMaximumClientBuild";
        internal const string CiProvider = "-gdkCiProvider";
        internal const string CiJobName = "-gdkCiJobName";
        internal const string CiBuildId = "-gdkCiBuildId";
        internal const string CiBuildUrl = "-gdkCiBuildUrl";
        internal const string CiRevision = "-gdkCiRevision";

        private static readonly HashSet<string> AllowedNames = new HashSet<string>(
            new[]
            {
                Channel,
                Environment,
                BuildTarget,
                Version,
                PlayerBuildNumber,
                Profile,
                ProfileCatalog,
                OutputRoot,
                Flavor,
                RemoteRoot,
                MinimumClientBuild,
                MaximumClientBuild,
                CiProvider,
                CiJobName,
                CiBuildId,
                CiBuildUrl,
                CiRevision
            },
            StringComparer.Ordinal);

        private readonly Dictionary<string, string> m_Values;

        private ChannelBuildArguments(Dictionary<string, string> values)
        {
            m_Values = values;
        }

        internal static ChannelBuildArguments Parse(IReadOnlyList<string> arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument == null)
                {
                    throw new ArgumentException("Command argument cannot be null.", nameof(arguments));
                }

                if (argument.StartsWith("-gdk", StringComparison.Ordinal) is false)
                {
                    continue;
                }

                if (AllowedNames.Contains(argument) is false)
                {
                    throw new ArgumentException(
                        $"Unknown channel build argument '{argument}'.",
                        nameof(arguments));
                }

                if (values.ContainsKey(argument))
                {
                    throw new ArgumentException(
                        $"Duplicate channel build argument '{argument}'.",
                        nameof(arguments));
                }

                if (i + 1 >= arguments.Count ||
                    arguments[i + 1] == null ||
                    arguments[i + 1].StartsWith("-gdk", StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Channel build argument '{argument}' requires a value.",
                        nameof(arguments));
                }

                values.Add(argument, arguments[++i]);
            }

            return new ChannelBuildArguments(values);
        }

        internal string GetRequired(string name)
        {
            var value = GetOptional(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    $"Required channel build argument '{name}' is missing.",
                    "arguments");
            }

            return value;
        }

        internal string GetOptional(string name)
        {
            return m_Values.TryGetValue(name, out var value) ? value : null;
        }
    }
}
