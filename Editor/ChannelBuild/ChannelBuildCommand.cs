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
            try
            {
                var context = CreateContext(arguments, projectRoot);
                Debug.Log(
                    $"Channel build input accepted: channel={context.Channel}, " +
                    $"platform={context.Platform}, version={context.Version}, profile={context.Profile.Id}.");
                return ChannelBuildExitCode.Success;
            }
            catch (Exception exception) when (IsInvalidInput(exception))
            {
                Debug.LogError(exception.Message);
                return ChannelBuildExitCode.InvalidInput;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return ChannelBuildExitCode.PipelineFailed;
            }
        }

        private static bool IsInvalidInput(Exception exception)
        {
            return exception is ArgumentException ||
                exception is FileNotFoundException ||
                exception is KeyNotFoundException ||
                exception is GameException;
        }
    }
}
