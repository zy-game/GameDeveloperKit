using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace GameDeveloperKit.Quality
{
    public static class QualityGatePlayerBuilder
    {
        private const string OutputArgument = "-qualityGatePlayerPath";

        public static void BuildWindowsIl2Cpp()
        {
            var outputPath = GetArgumentValue(OutputArgument);
            if (string.IsNullOrWhiteSpace(outputPath) || !Path.IsPathRooted(outputPath))
            {
                throw new InvalidOperationException($"{OutputArgument} must specify an absolute player path.");
            }

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("Quality gate requires at least one enabled build scene.");
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new InvalidOperationException("Quality gate player output directory is invalid.");
            }

            Directory.CreateDirectory(outputDirectory);
            var namedBuildTarget = NamedBuildTarget.Standalone;
            var previousBackend = PlayerSettings.GetScriptingBackend(namedBuildTarget);
            try
            {
                PlayerSettings.SetScriptingBackend(namedBuildTarget, ScriptingImplementation.IL2CPP);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.CleanBuildCache | BuildOptions.StrictMode,
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Windows IL2CPP player build failed: {report.summary.result}, errors={report.summary.totalErrors}.");
                }
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(namedBuildTarget, previousBackend);
            }
        }

        private static string GetArgumentValue(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }
    }
}
