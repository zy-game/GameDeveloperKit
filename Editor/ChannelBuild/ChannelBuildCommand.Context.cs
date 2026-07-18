using System;
using System.Collections.Generic;
using System.Globalization;
using GameDeveloperKit.ChannelBuild;
using UnityEditor;

namespace GameDeveloperKit
{
    public static partial class ChannelBuildCommand
    {
        public static ChannelBuildContext CreateContext(
            IReadOnlyList<string> arguments,
            string projectRoot)
        {
            var parsed = ChannelBuildArguments.Parse(arguments);
            var channel = parsed.GetRequired(ChannelBuildArguments.Channel);
            var environment = ParseEnvironment(parsed.GetRequired(ChannelBuildArguments.Environment));
            var buildTarget = ParseBuildTarget(parsed.GetRequired(ChannelBuildArguments.BuildTarget));
            var version = parsed.GetRequired(ChannelBuildArguments.Version);
            var playerBuildNumber = ParsePositiveInt(
                parsed.GetRequired(ChannelBuildArguments.PlayerBuildNumber),
                ChannelBuildArguments.PlayerBuildNumber);
            var profileId = parsed.GetRequired(ChannelBuildArguments.Profile);
            var outputRoot = parsed.GetRequired(ChannelBuildArguments.OutputRoot);
            var flavor = parsed.GetOptional(ChannelBuildArguments.Flavor);
            var remoteRoot = parsed.GetOptional(ChannelBuildArguments.RemoteRoot);
            var minimumClientBuild = ParseOptionalPositiveLong(
                parsed,
                ChannelBuildArguments.MinimumClientBuild);
            var maximumClientBuild = ParseOptionalPositiveLong(
                parsed,
                ChannelBuildArguments.MaximumClientBuild);
            var ci = CreateCiMetadata(parsed);
            var catalogPath = parsed.GetOptional(ChannelBuildArguments.ProfileCatalog) ??
                ChannelProfileSource.DefaultRelativePath;
            var profile = ChannelProfileSource.Load(projectRoot, catalogPath)
                .GetRequired(profileId);
            var mergedProfile = ChannelProfileSource.Merge(
                profile,
                new ChannelProfileOverrides(channel: channel));

            return new ChannelBuildContext(
                channel,
                environment,
                buildTarget,
                version,
                playerBuildNumber,
                outputRoot,
                flavor,
                remoteRoot,
                minimumClientBuild,
                maximumClientBuild,
                mergedProfile,
                ci: ci);
        }

        private static ChannelBuildEnvironment ParseEnvironment(string value)
        {
            switch (value)
            {
                case "dev":
                    return ChannelBuildEnvironment.Dev;
                case "test":
                    return ChannelBuildEnvironment.Test;
                case "staging":
                    return ChannelBuildEnvironment.Staging;
                case "prod":
                    return ChannelBuildEnvironment.Prod;
                default:
                    throw InvalidValue(ChannelBuildArguments.Environment);
            }
        }

        private static BuildTarget ParseBuildTarget(string value)
        {
            if (Enum.TryParse(value, false, out BuildTarget target) is false ||
                target == BuildTarget.NoTarget ||
                Enum.IsDefined(typeof(BuildTarget), target) is false ||
                string.Equals(target.ToString(), value, StringComparison.Ordinal) is false)
            {
                throw InvalidValue(ChannelBuildArguments.BuildTarget);
            }

            return target;
        }

        private static int ParsePositiveInt(string value, string argumentName)
        {
            if (int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var result) is false ||
                result <= 0)
            {
                throw InvalidValue(argumentName);
            }

            return result;
        }

        private static long? ParseOptionalPositiveLong(
            ChannelBuildArguments arguments,
            string argumentName)
        {
            var value = arguments.GetOptional(argumentName);
            if (value == null)
            {
                return null;
            }

            if (long.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var result) is false ||
                result <= 0)
            {
                throw InvalidValue(argumentName);
            }

            return result;
        }

        private static CiBuildMetadata CreateCiMetadata(ChannelBuildArguments arguments)
        {
            var provider = arguments.GetOptional(ChannelBuildArguments.CiProvider);
            var jobName = arguments.GetOptional(ChannelBuildArguments.CiJobName);
            var buildId = arguments.GetOptional(ChannelBuildArguments.CiBuildId);
            var buildUrl = arguments.GetOptional(ChannelBuildArguments.CiBuildUrl);
            var revision = arguments.GetOptional(ChannelBuildArguments.CiRevision);
            if (provider == null &&
                jobName == null &&
                buildId == null &&
                buildUrl == null &&
                revision == null)
            {
                return null;
            }

            return new CiBuildMetadata(
                arguments.GetRequired(ChannelBuildArguments.CiProvider),
                arguments.GetRequired(ChannelBuildArguments.CiJobName),
                arguments.GetRequired(ChannelBuildArguments.CiBuildId),
                buildUrl,
                arguments.GetRequired(ChannelBuildArguments.CiRevision));
        }

        private static ArgumentException InvalidValue(string argumentName)
        {
            return new ArgumentException(
                $"Channel build argument '{argumentName}' is invalid.",
                "arguments");
        }
    }
}
