using System;
using System.Collections.Generic;
using System.IO;
using GameDeveloperKit.ChannelBuild;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit
{
    public static partial class ChannelBuildCommand
    {
        public static void Build()
        {
            EnsureBatchMode(Application.isBatchMode);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var exitCode = Run(Environment.GetCommandLineArgs(), projectRoot);
            EditorApplication.Exit((int)exitCode);
        }

        private static void EnsureBatchMode(bool isBatchMode)
        {
            if (isBatchMode is false)
            {
                throw new InvalidOperationException(
                    "Channel build command can only run in Unity batch mode.");
            }
        }

        internal static ChannelBuildExitCode Run(
            IReadOnlyList<string> arguments,
            string projectRoot)
        {
            return RunWithPlayerBuild(arguments, projectRoot, BuildPlayer);
        }

        internal static ChannelBuildExitCode RunWithPlayerBuild(
            IReadOnlyList<string> arguments,
            string projectRoot,
            Func<ChannelBuildContext, ChannelPlayerBuildResult> playerBuild)
        {
            if (playerBuild == null)
            {
                throw new ArgumentNullException(nameof(playerBuild));
            }

            var startedAtUtc = DateTime.UtcNow;
            string reportPath;
            try
            {
                reportPath = ChannelBuildArguments.GetRequiredReportPath(arguments);
            }
            catch (Exception exception) when (IsInvalidInput(exception))
            {
                Debug.LogError(exception.Message);
                return ChannelBuildExitCode.InvalidInput;
            }

            ChannelBuildContext context = null;
            ChannelPlayerBuildResult playerResult = null;
            ChannelBuildExitCode exitCode;
            try
            {
                context = CreateContext(arguments, projectRoot);
                var parsed = ChannelBuildArguments.Parse(arguments);
                var mode = parsed.GetOptional(ChannelBuildArguments.Mode) ?? "validate";
                if (mode == "player")
                {
                    playerResult = playerBuild(context) ??
                        throw new InvalidOperationException("Channel player build returned a null result.");
                    exitCode = playerResult.ExitCode;
                }
                else if (mode == "validate")
                {
                    exitCode = ChannelBuildExitCode.Success;
                }
                else
                {
                    throw new ArgumentException("Channel build mode is invalid.", nameof(arguments));
                }
                Debug.Log(
                    $"Channel build input accepted: channel={context.Channel}, " +
                    $"platform={context.Platform}, version={context.Version}, profile={context.Profile.Id}.");
            }
            catch (Exception exception) when (IsInvalidInput(exception))
            {
                Debug.LogError(exception.Message);
                exitCode = ChannelBuildExitCode.InvalidInput;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = ChannelBuildExitCode.PipelineFailed;
            }

            try
            {
                var report = new ChannelBuildReport(
                    exitCode == ChannelBuildExitCode.Success
                        ? ChannelBuildReport.SucceededStatus
                        : ChannelBuildReport.FailedStatus,
                    exitCode == ChannelBuildExitCode.Success
                        ? ChannelBuildReport.NoFailure
                        : ChannelBuildReport.FailureKindFor(exitCode),
                    exitCode,
                    ChannelBuildReportContext.FromContext(context),
                    context?.Ci,
                    playerResult?.Artifacts,
                    playerResult?.Steps,
                    playerResult?.Warnings,
                    startedAtUtc,
                    DateTime.UtcNow);
                ChannelBuildReportWriter.Write(reportPath, report);
                return exitCode;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Channel build report could not be written: {exception.Message}");
                return ChannelBuildExitCode.ReportFailed;
            }
        }

        private static bool IsInvalidInput(Exception exception)
        {
            return exception is ArgumentException ||
                exception is FileNotFoundException ||
                exception is KeyNotFoundException ||
                exception is GameException;
        }

        private static ChannelPlayerBuildResult BuildPlayer(ChannelBuildContext context)
        {
            return new ChannelPlayerBuildService().Build(context);
        }
    }
}
