using System.IO;
using System;
using System.Collections.Generic;
using GameDeveloperKit.Debugger;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;
using Cysharp.Threading.Tasks;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace GameDeveloperKit.Tests
{
    public abstract class RuntimeTestBase
    {
        private const string FrameworkAssetsRoot = "Assets/GameDeveloperKit";
        private const string FrameworkPackageRoot = "Packages/com.gamedeveloperkit.framework";

        [SetUp]
        public void RuntimeTestBaseSetUp()
        {
            var test = TestContext.CurrentContext.Test;
            LogTestMessage($"[TEST START] {test.ClassName}.{test.MethodName}", false);
        }

        [TearDown]
        public void RuntimeTestBaseTearDown()
        {
            var context = TestContext.CurrentContext;
            var test = context.Test;
            var result = context.Result;
            var status = result.Outcome.Status;
            var message = string.IsNullOrEmpty(result.Message) ? string.Empty : $" - {result.Message}";
            if (status == TestStatus.Passed)
            {
                LogTestMessage($"[TEST END] {test.ClassName}.{test.MethodName}: {result.Outcome}{message}", false);
                return;
            }

            LogTestMessage($"[TEST END] {test.ClassName}.{test.MethodName}: {result.Outcome}{message}", true);
        }

        private static void LogTestMessage(string message, bool warning)
        {
            if (!App.TryGetRegistered<DebugModule>(out var debug))
            {
                TestContext.Progress.WriteLine(message);
                return;
            }

            if (warning)
            {
                debug.Warning(message);
                return;
            }

            debug.Info(message);
        }

        protected static string FrameworkAssetPath(string relativePath)
        {
            return $"{ResolveFrameworkAssetRoot()}/{NormalizeRelativePath(relativePath)}";
        }

        protected static string ResolveFrameworkAssetPath(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return normalizedPath;
            }

            const string assetsRootWithSlash = FrameworkAssetsRoot + "/";
            if (normalizedPath.StartsWith(assetsRootWithSlash, System.StringComparison.Ordinal))
            {
                return FrameworkAssetPath(normalizedPath.Substring(assetsRootWithSlash.Length));
            }

            const string packageRootWithSlash = FrameworkPackageRoot + "/";
            if (normalizedPath.StartsWith(packageRootWithSlash, System.StringComparison.Ordinal))
            {
                return FrameworkAssetPath(normalizedPath.Substring(packageRootWithSlash.Length));
            }

            return normalizedPath;
        }

        protected static string FrameworkFilePath(string relativePath)
        {
            var normalizedRelativePath = NormalizeRelativePath(relativePath);
#if UNITY_EDITOR
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(App).Assembly);
            if (string.IsNullOrWhiteSpace(packageInfo?.resolvedPath) is false)
            {
                var packageFilePath = Path.Combine(packageInfo.resolvedPath, normalizedRelativePath);
                if (System.IO.File.Exists(packageFilePath) || Directory.Exists(packageFilePath))
                {
                    return NormalizePath(packageFilePath);
                }
            }
#endif

            var assetsFilePath = Path.Combine(FrameworkAssetsRoot, normalizedRelativePath);
            if (System.IO.File.Exists(assetsFilePath) || Directory.Exists(assetsFilePath))
            {
                return NormalizePath(assetsFilePath);
            }

            return NormalizePath(Path.Combine(FrameworkPackageRoot, normalizedRelativePath));
        }

        private static string ResolveFrameworkAssetRoot()
        {
#if UNITY_EDITOR
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(App).Assembly);
            if (string.IsNullOrWhiteSpace(packageInfo?.assetPath) is false)
            {
                return NormalizePath(packageInfo.assetPath);
            }

            if (UnityEditor.AssetDatabase.IsValidFolder(FrameworkPackageRoot))
            {
                return FrameworkPackageRoot;
            }
#endif

            return FrameworkAssetsRoot;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return NormalizePath(relativePath).Trim('/');
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }

    internal static class StoryProgramTestFactory
    {
        public const string VolumeId = "volume_test";

        public static Program Program(
            string storyId,
            string version,
            string entryEpisodeId,
            IReadOnlyList<Episode> episodes,
            VariableSchema variableSchema = null,
            CommandSchema commandSchema = null)
        {
            var edges = new List<RouteEdge>();
            if (episodes != null)
            {
                for (var i = 0; i < episodes.Count; i++)
                {
                    if (episodes[i] != null)
                    {
                        edges.Add(RouteEdge.FromRoot("root_" + episodes[i].EpisodeId, episodes[i].EpisodeId));
                    }
                }
            }

            return new Program(
                storyId,
                version,
                new[] { new Volume(VolumeId, VolumeId, episodes, new Route(edges)) },
                variableSchema,
                commandSchema);
        }

        public static Episode Episode(
            string episodeId,
            string title,
            string entryStepId,
            IReadOnlyList<Step> steps,
            string previewImagePath = null,
            string description = null)
        {
            var normalized = new List<Step>();
            var exits = new List<EpisodeExit>();
            if (steps != null)
            {
                for (var i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    if (step?.Kind == StepKind.Choice)
                    {
                        normalized.Add(step);
                        for (var choiceIndex = 0; choiceIndex < step.Choices.Count; choiceIndex++)
                        {
                            var choice = step.Choices[choiceIndex];
                            if (choice != null && string.IsNullOrWhiteSpace(choice.ExitId) is false)
                            {
                                exits.Add(new EpisodeExit(choice.ExitId));
                            }
                        }

                        continue;
                    }

                    if (step?.Kind != StepKind.End || !string.IsNullOrWhiteSpace(step.Data.ExitId))
                    {
                        normalized.Add(step);
                        continue;
                    }

                    var exitId = step.StepId;
                    exits.Add(new EpisodeExit(exitId));
                    normalized.Add(new Step(step.StepId, step.Kind, CopyStepData(step.Data, exitId)));
                }
            }

            return new Episode(
                episodeId,
                title,
                entryStepId,
                exits,
                normalized,
                previewImagePath,
                description);
        }

        private static StepData CopyStepData(StepData data, string exitId)
        {
            return new StepData(
                data.TextKey,
                data.Speaker,
                data.Command,
                data.Choices,
                data.Condition,
                data.Target,
                data.WaitSeconds,
                data.Tags,
                data.Branches,
                exitId);
        }
    }

    internal static class StoryRouteTestExtensions
    {
        public static Runner StartProgram(this StoryModule module, string storyId, string episodeId = null)
        {
            if (!module.TryGetProgram(storyId, out var program))
            {
                throw new GameException($"Story program is not registered. story:{storyId}");
            }

            ResolveEpisode(program, episodeId, out var volume, out var episode);
            return module.StartEpisode(storyId, volume.VolumeId, episode.EpisodeId);
        }

        public static Frame Start(this Presenter presenter, Program program, string episodeId = null)
        {
            ResolveEpisode(program, episodeId, out var volume, out var episode);
            return presenter.Start(program, volume.VolumeId, episode.EpisodeId);
        }

        public static void Play(this PlayerView view, Program program, string episodeId)
        {
            ResolveEpisode(program, episodeId, out var volume, out var episode);
            view.Play(program, volume.VolumeId, episode.EpisodeId);
        }

        public static UniTask PlayAsync(
            this PlayerView view,
            Program program,
            CancellationToken cancellationToken = default)
        {
            ResolveEpisode(program, null, out var volume, out var episode);
            return view.PlayAsync(program, volume.VolumeId, episode.EpisodeId, cancellationToken);
        }

        public static UniTask PlayAsync(
            this PlayerView view,
            Program program,
            string episodeId,
            CancellationToken cancellationToken = default)
        {
            ResolveEpisode(program, episodeId, out var volume, out var episode);
            return view.PlayAsync(program, volume.VolumeId, episode.EpisodeId, cancellationToken);
        }

        public static void PlayRegistered(this PlayerView view, string storyId, string episodeId)
        {
            view.PlayRegistered(storyId, StoryProgramTestFactory.VolumeId, episodeId);
        }

        private static void ResolveEpisode(
            Program program,
            string episodeId,
            out Volume volume,
            out Episode episode)
        {
            for (var volumeIndex = 0; volumeIndex < program.Volumes.Count; volumeIndex++)
            {
                var candidateVolume = program.Volumes[volumeIndex];
                for (var episodeIndex = 0; episodeIndex < candidateVolume.Episodes.Count; episodeIndex++)
                {
                    var candidateEpisode = candidateVolume.Episodes[episodeIndex];
                    if (candidateEpisode != null &&
                        (string.IsNullOrWhiteSpace(episodeId) || string.Equals(candidateEpisode.EpisodeId, episodeId, StringComparison.Ordinal)))
                    {
                        volume = candidateVolume;
                        episode = candidateEpisode;
                        return;
                    }
                }
            }

            throw new GameException($"Story episode does not exist. story:{program.StoryId} episode:{episodeId}");
        }
    }
}
