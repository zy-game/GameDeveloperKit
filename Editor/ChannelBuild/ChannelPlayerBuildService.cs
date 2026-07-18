using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed partial class ChannelPlayerBuildService
    {
        public const string OperationId = "player";

        private readonly Func<ChannelBuildSigningInput, IReadOnlyList<IChannelBuildResponder>> m_CreateResponders;
        private readonly IPlayerBuildGateway m_Gateway;

        internal ChannelPlayerBuildService(
            Func<ChannelBuildSigningInput, IReadOnlyList<IChannelBuildResponder>> createResponders,
            IPlayerBuildGateway gateway)
        {
            m_CreateResponders = createResponders ?? throw new ArgumentNullException(nameof(createResponders));
            m_Gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public ChannelPlayerBuildResult Build(
            ChannelBuildContext context,
            ChannelBuildSigningInput signingInput = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (TryCreateRequest(context, out var request, out var error) is false)
            {
                return Failure(ChannelBuildExitCode.PipelineFailed, error);
            }

            ChannelBuildResponderExecution execution;
            try
            {
                m_Gateway.RecreateDirectory(request.PlayerOutputRoot);
                execution = ChannelBuildResponderRunner.Execute(
                    context,
                    m_CreateResponders(signingInput),
                    operationContext => BuildPlayer(request));
            }
            catch
            {
                return Failure(
                    ChannelBuildExitCode.PipelineFailed,
                    "Channel player responder pipeline failed.",
                    request.PlayerOutputRoot);
            }

            var exitCode = Classify(execution);
            IReadOnlyList<ChannelBuildArtifact> artifacts = null;
            if (exitCode == ChannelBuildExitCode.Success)
            {
                try
                {
                    artifacts = CreateArtifacts(context, execution, request.PlayerOutputRoot);
                }
                catch
                {
                    return new ChannelPlayerBuildResult(
                        ChannelBuildExitCode.PipelineFailed,
                        request.PlayerOutputRoot,
                        execution,
                        steps: CreateStepReports(execution.Results),
                        warnings: CollectWarnings(execution.Results));
                }
            }
            return new ChannelPlayerBuildResult(
                exitCode,
                request.PlayerOutputRoot,
                execution,
                artifacts,
                steps: CreateStepReports(execution.Results),
                warnings: CollectWarnings(execution.Results));
        }

        internal bool TryCreateRequest(
            ChannelBuildContext context,
            out PlayerBuildRequest request,
            out string error)
        {
            if (context.BuildTarget != BuildTarget.Android && context.BuildTarget != BuildTarget.iOS)
            {
                request = null;
                error = "Channel player build target is not supported.";
                return false;
            }

            var scenes = m_Gateway.GetEnabledScenes();
            if (scenes == null || scenes.Count == 0 || scenes.Any(string.IsNullOrWhiteSpace))
            {
                request = null;
                error = "Channel player build requires at least one enabled scene.";
                return false;
            }

            var outputRoot = Path.GetFullPath(context.OutputRoot);
            var playerRoot = Path.GetFullPath(Path.Combine(
                outputRoot,
                "player",
                context.Channel,
                context.Platform,
                context.Version));
            if (IsInside(outputRoot, playerRoot) is false)
            {
                request = null;
                error = "Channel player output root escaped the channel output root.";
                return false;
            }

            request = new PlayerBuildRequest(
                context.BuildTarget,
                scenes,
                playerRoot.Replace('\\', '/'));
            error = null;
            return true;
        }

        private ChannelBuildStepResult BuildPlayer(PlayerBuildRequest request)
        {
            var location = request.Target == BuildTarget.Android
                ? Path.Combine(
                    request.PlayerOutputRoot,
                    "player" + (m_Gateway.GetAndroidBuildAppBundle() ? ".aab" : ".apk"))
                : request.PlayerOutputRoot;
            var summary = m_Gateway.Build(new PlayerBuildInvocation(
                request.Target,
                request.Scenes,
                location.Replace('\\', '/'),
                BuildOptions.CleanBuildCache | BuildOptions.StrictMode));
            if (summary == null)
            {
                throw new GameException("Unity player build returned a null summary.");
            }
            if (summary.Succeeded is false)
            {
                return new ChannelBuildStepResult(
                    OperationId,
                    ChannelBuildResponderPhase.Operation,
                    false,
                    "Unity player build failed with " +
                    summary.ErrorCount.ToString(CultureInfo.InvariantCulture) +
                    " error(s).");
            }

            return new ChannelBuildStepResult(
                OperationId,
                ChannelBuildResponderPhase.Operation,
                true,
                outputs: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["player.outputRoot"] = request.PlayerOutputRoot
                });
        }

        private static ChannelBuildExitCode Classify(ChannelBuildResponderExecution execution)
        {
            if (execution.Success)
            {
                return ChannelBuildExitCode.Success;
            }

            var failure = execution.PrimaryFailure;
            if (failure == null)
            {
                return ChannelBuildExitCode.PipelineFailed;
            }
            if (failure.ResponderId == OperationId &&
                failure.Phase == ChannelBuildResponderPhase.Operation)
            {
                return ChannelBuildExitCode.PlayerBuildFailed;
            }
            if (failure.ResponderId == "resources" &&
                failure.Phase != ChannelBuildResponderPhase.Restore)
            {
                return ChannelBuildExitCode.ResourceBuildFailed;
            }

            return ChannelBuildExitCode.PipelineFailed;
        }

        private static IReadOnlyList<ChannelBuildArtifact> CreateArtifacts(
            ChannelBuildContext context,
            ChannelBuildResponderExecution execution,
            string playerOutputRoot)
        {
            var artifacts = new List<ChannelBuildArtifact>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var resultIndex = 0; resultIndex < execution.Results.Count; resultIndex++)
            {
                var result = execution.Results[resultIndex];
                if (result.ResponderId != "resources" ||
                    result.Phase != ChannelBuildResponderPhase.Apply ||
                    result.Success is false)
                {
                    continue;
                }

                if (result.Outputs.TryGetValue("resource.artifactCount", out var countText) is false ||
                    int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var count) is false ||
                    count < 0)
                {
                    throw new GameException("Resource artifact output count is invalid.");
                }

                for (var artifactIndex = 0; artifactIndex < count; artifactIndex++)
                {
                    var prefix = "resource.artifact." + artifactIndex.ToString("D4", CultureInfo.InvariantCulture);
                    if (result.Outputs.TryGetValue(prefix + ".kind", out var kind) is false ||
                        result.Outputs.TryGetValue(prefix + ".path", out var relativePath) is false)
                    {
                        throw new GameException("Resource artifact output is incomplete.");
                    }

                    AddArtifact(
                        artifacts,
                        paths,
                        kind,
                        context.OutputRoot,
                        Path.Combine(context.OutputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                }
            }

            var playerFiles = Directory.GetFiles(playerOutputRoot, "*", SearchOption.AllDirectories);
            Array.Sort(playerFiles, StringComparer.Ordinal);
            if (playerFiles.Length == 0)
            {
                throw new GameException("Successful Unity player build produced no files.");
            }
            for (var i = 0; i < playerFiles.Length; i++)
            {
                AddArtifact(
                    artifacts,
                    paths,
                    "player-artifact",
                    context.OutputRoot,
                    playerFiles[i]);
            }

            artifacts.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));
            return artifacts.AsReadOnly();
        }

        private static void AddArtifact(
            ICollection<ChannelBuildArtifact> artifacts,
            ISet<string> paths,
            string kind,
            string outputRoot,
            string path)
        {
            var artifact = CaptureArtifact(kind, outputRoot, path);
            if (paths.Add(artifact.Path) is false)
            {
                throw new GameException("Channel player build artifact is duplicated.");
            }
            artifacts.Add(artifact);
        }

        private static ChannelBuildArtifact CaptureArtifact(
            string kind,
            string outputRoot,
            string path)
        {
            var root = Path.GetFullPath(outputRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var file = Path.GetFullPath(path);
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (file.StartsWith(root, comparison) is false || System.IO.File.Exists(file) is false)
            {
                throw new FileNotFoundException("Channel player artifact is missing or outside output root.");
            }

            string hash;
            using (var stream = System.IO.File.OpenRead(file))
            using (var algorithm = SHA256.Create())
            {
                hash = string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2")));
            }

            return new ChannelBuildArtifact(
                kind,
                file.Substring(root.Length)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/'),
                hash,
                new FileInfo(file).Length);
        }

        private static IReadOnlyList<ChannelBuildStepReport> CreateStepReports(
            IReadOnlyList<ChannelBuildStepResult> results)
        {
            var steps = new List<ChannelBuildStepReport>(results.Count);
            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                steps.Add(new ChannelBuildStepReport(
                    result.ResponderId + "-" + result.Phase.ToString().ToLowerInvariant(),
                    result.Success ? "succeeded" : "failed",
                    result.Message));
            }
            return steps.AsReadOnly();
        }

        private static IReadOnlyList<string> CollectWarnings(
            IReadOnlyList<ChannelBuildStepResult> results)
        {
            var warnings = new List<string>();
            for (var i = 0; i < results.Count; i++)
            {
                for (var warningIndex = 0; warningIndex < results[i].Warnings.Count; warningIndex++)
                {
                    warnings.Add(results[i].Warnings[warningIndex]);
                }
            }
            return warnings.AsReadOnly();
        }

        private static ChannelPlayerBuildResult Failure(
            ChannelBuildExitCode exitCode,
            string message,
            string playerOutputRoot = null)
        {
            return new ChannelPlayerBuildResult(
                exitCode,
                playerOutputRoot,
                steps: new[] { new ChannelBuildStepReport(OperationId, "failed", message) });
        }

        private static bool IsInside(string rootPath, string candidatePath)
        {
            var root = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(candidatePath);
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return candidate.StartsWith(root, comparison);
        }

        internal interface IPlayerBuildGateway
        {
            IReadOnlyList<string> GetEnabledScenes();
            bool GetAndroidBuildAppBundle();
            void RecreateDirectory(string path);
            PlayerBuildSummary Build(PlayerBuildInvocation invocation);
        }

        internal sealed class PlayerBuildRequest
        {
            internal PlayerBuildRequest(
                BuildTarget target,
                IReadOnlyList<string> scenes,
                string playerOutputRoot)
            {
                Target = target;
                Scenes = ChannelBuildContext.CopyList(scenes);
                PlayerOutputRoot = playerOutputRoot;
            }

            internal BuildTarget Target { get; }
            internal IReadOnlyList<string> Scenes { get; }
            internal string PlayerOutputRoot { get; }
        }

        internal sealed class PlayerBuildInvocation
        {
            internal PlayerBuildInvocation(
                BuildTarget target,
                IReadOnlyList<string> scenes,
                string locationPathName,
                BuildOptions options)
            {
                Target = target;
                Scenes = ChannelBuildContext.CopyList(scenes);
                LocationPathName = locationPathName;
                Options = options;
            }

            internal BuildTarget Target { get; }
            internal IReadOnlyList<string> Scenes { get; }
            internal string LocationPathName { get; }
            internal BuildOptions Options { get; }
        }

        internal sealed class PlayerBuildSummary
        {
            internal PlayerBuildSummary(bool succeeded, int errorCount)
            {
                if (errorCount < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(errorCount));
                }
                Succeeded = succeeded;
                ErrorCount = errorCount;
            }

            internal bool Succeeded { get; }
            internal int ErrorCount { get; }
        }

        private sealed class UnityPlayerBuildGateway : IPlayerBuildGateway
        {
            public IReadOnlyList<string> GetEnabledScenes()
            {
                return EditorBuildSettings.scenes
                    .Where(scene => scene.enabled && string.IsNullOrWhiteSpace(scene.path) is false)
                    .Select(scene => scene.path)
                    .ToArray();
            }

            public bool GetAndroidBuildAppBundle()
            {
                return EditorUserBuildSettings.buildAppBundle;
            }

            public void RecreateDirectory(string path)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                Directory.CreateDirectory(path);
            }

            public PlayerBuildSummary Build(PlayerBuildInvocation invocation)
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = invocation.Scenes.ToArray(),
                    locationPathName = invocation.LocationPathName,
                    target = invocation.Target,
                    options = invocation.Options
                });
                return new PlayerBuildSummary(
                    report.summary.result == BuildResult.Succeeded,
                    report.summary.totalErrors);
            }
        }
    }
}
