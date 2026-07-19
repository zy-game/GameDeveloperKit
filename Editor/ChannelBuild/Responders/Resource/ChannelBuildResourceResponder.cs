using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using GameDeveloperKit.ResourceEditor;
using UnityEditor;
using ResourceBuildArtifact = GameDeveloperKit.ResourceEditor.Build.Artifact;
using ResourceBuildCompression = GameDeveloperKit.ResourceEditor.Build.Compression;
using ResourceBuildPreparedRequest = GameDeveloperKit.ResourceEditor.Build.PreparedRequest;
using ResourceBuildResult = GameDeveloperKit.ResourceEditor.Build.Result;
using ResourceBuildScope = GameDeveloperKit.ResourceEditor.Build.Scope;
using ResourceBuildSettings = GameDeveloperKit.ResourceEditor.Build.Settings;
using ResourceBuildWorkflow = GameDeveloperKit.ResourceEditor.Build.Workflow;
using ResourceEditorRegistry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry;
using ResourceEditorSettings = GameDeveloperKit.ResourceEditor.Authoring.Settings;
using ResourceManifestPartitioner = GameDeveloperKit.ResourceEditor.Build.ManifestPartitioner;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed partial class ChannelBuildResourceResponder : IChannelBuildResponder
    {
        public const string ResponderId = "resources";

        private ChannelBuildContext m_Context;
        private ResourceBuildWorkflow m_Workflow;
        private ResourceBuildPreparedRequest m_Request;
        private IReadOnlyList<string> m_PackagedTargets;
        private ChannelBuildFileMutationState m_PackagedState;

        public string Id => ResponderId;

        public int Order => 0;

        public IReadOnlyList<string> DependsOn => Array.Empty<string>();

        public ChannelBuildStepResult Prepare(ChannelBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (m_Request != null)
            {
                throw new GameException("Channel resource responder can only be prepared once.");
            }

            if (ResourceEditorSettings.TryLoadExisting(out var settings) is false)
            {
                return Failure(ChannelBuildResponderPhase.Prepare, "Resource editor settings are missing.");
            }
            if (TryCreateBuildSettings(context, out var buildSettings, out var error) is false)
            {
                return Failure(ChannelBuildResponderPhase.Prepare, error);
            }

            var workflow = new ResourceBuildWorkflow(
                settings,
                ResourceEditorRegistry.Scan(),
                buildSettings);
            if (workflow.TryPrepare(out var request, out error) is false)
            {
                return Failure(ChannelBuildResponderPhase.Prepare, error);
            }

            IReadOnlyList<string> packagedTargets;
            try
            {
                packagedTargets = CreatePackagedTargets(request);
                ValidatePackagedTargets(context.OutputRoot, packagedTargets);
            }
            catch (Exception exception)
            {
                return Failure(ChannelBuildResponderPhase.Prepare, exception.Message);
            }

            m_Context = context;
            m_Workflow = workflow;
            m_Request = request;
            m_PackagedTargets = packagedTargets;
            return new ChannelBuildStepResult(ResponderId, ChannelBuildResponderPhase.Prepare, true);
        }

        public ChannelBuildStepResult Apply(ChannelBuildContext context)
        {
            ValidateExecutionContext(context);
            if (m_PackagedState != null)
            {
                throw new GameException("Channel resource responder can only be applied once.");
            }

            m_PackagedState = new ChannelBuildFileMutationState(m_PackagedTargets);
            m_PackagedState.Capture();
            var buildResult = m_Workflow.Build(m_Request);
            if (buildResult == null)
            {
                throw new GameException("Resource build returned a null result.");
            }
            if (buildResult.Succeeded is false)
            {
                return Failure(ChannelBuildResponderPhase.Apply, buildResult.ErrorMessage);
            }

            var outputs = CreateArtifactOutputs(context, buildResult, m_PackagedTargets);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return new ChannelBuildStepResult(
                ResponderId,
                ChannelBuildResponderPhase.Apply,
                true,
                outputs: outputs);
        }

        public ChannelBuildStepResult Restore(ChannelBuildContext context)
        {
            ValidateExecutionContext(context);
            if (m_PackagedState == null)
            {
                return new ChannelBuildStepResult(ResponderId, ChannelBuildResponderPhase.Restore, true);
            }

            var failureCount = m_PackagedState.Restore();
            if (failureCount > 0)
            {
                return Failure(
                    ChannelBuildResponderPhase.Restore,
                    $"Failed to restore {failureCount} packaged resource file(s).");
            }

            m_PackagedState = null;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return new ChannelBuildStepResult(ResponderId, ChannelBuildResponderPhase.Restore, true);
        }

        internal static bool TryCreateBuildSettings(
            ChannelBuildContext context,
            out ResourceBuildSettings buildSettings,
            out string error)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var compression = ResourceBuildCompression.Lz4;
            var overrides = context.Profile?.ResourceOverrides;
            if (overrides != null)
            {
                foreach (var pair in overrides)
                {
                    if (string.Equals(pair.Key, "compression", StringComparison.Ordinal) is false)
                    {
                        buildSettings = null;
                        error = $"Unsupported resource override key '{pair.Key}'.";
                        return false;
                    }

                    switch (pair.Value)
                    {
                        case "default":
                            compression = ResourceBuildCompression.Default;
                            break;
                        case "lz4":
                            compression = ResourceBuildCompression.Lz4;
                            break;
                        case "uncompressed":
                            compression = ResourceBuildCompression.Uncompressed;
                            break;
                        default:
                            buildSettings = null;
                            error = "Resource compression override is invalid.";
                            return false;
                    }
                }
            }

            var outputRoot = Path.GetFullPath(context.OutputRoot);
            var resourceOutputRoot = Path.GetFullPath(Path.Combine(outputRoot, "resources"));
            if (IsPathInside(outputRoot, resourceOutputRoot) is false)
            {
                buildSettings = null;
                error = "Resource output root is outside the channel output root.";
                return false;
            }

            buildSettings = new ResourceBuildSettings
            {
                OutputRoot = resourceOutputRoot.Replace('\\', '/'),
                Target = context.BuildTarget.ToString(),
                Channel = context.Channel,
                CleanOutput = true,
                Compression = compression,
                ManifestFileName = ResourceSettings.MANIFEST_NAME,
                ManifestVersion = context.Version,
                Scope = ResourceBuildScope.AllPackages
            };
            error = null;
            return true;
        }

        private static IReadOnlyList<string> CreatePackagedTargets(ResourceBuildPreparedRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var projectRoot = Path.GetFullPath(".");
            var targets = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            AddPackagedTarget(
                targets,
                projectRoot,
                ResourceManifestPartitioner.ResolveLocalManifestPath(request.Context.Settings));
            for (var i = 0; i < request.Plan.Bundles.Count; i++)
            {
                var bundle = request.Plan.Bundles[i];
                if (bundle?.Package == null ||
                    bundle.Package.IsHotUpdate ||
                    bundle.Bundle == null ||
                    ResourceProviderIds.IsAssetBundle(bundle.Bundle.ProviderId) is false)
                {
                    continue;
                }

                AddPackagedTarget(
                    targets,
                    projectRoot,
                    ResourceManifestPartitioner.ResolveLocalBundlePath(
                        new ResourceBuildArtifact { BundleName = bundle.BundleName }));
            }

            return new List<string>(targets).AsReadOnly();
        }

        private static void AddPackagedTarget(
            ISet<string> targets,
            string projectRoot,
            string targetPath)
        {
            var fullPath = Path.GetFullPath(targetPath);
            if (IsPathInside(projectRoot, fullPath) is false)
            {
                throw new InvalidOperationException("Packaged resource target is outside the project root.");
            }
            if (targets.Add(fullPath.Replace('\\', '/')) is false)
            {
                throw new InvalidOperationException("Packaged resource target is duplicated.");
            }
        }

        private static void ValidatePackagedTargets(
            string outputRoot,
            IReadOnlyList<string> packagedTargets)
        {
            var fullOutputRoot = Path.GetFullPath(outputRoot);
            for (var i = 0; i < packagedTargets.Count; i++)
            {
                if (IsPathInside(fullOutputRoot, packagedTargets[i]))
                {
                    throw new InvalidOperationException(
                        "Packaged resource target overlaps the channel output root.");
                }
            }
        }

        private static bool IsPathInside(string rootPath, string candidatePath)
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

        private static ChannelBuildStepResult Failure(
            ChannelBuildResponderPhase phase,
            string message)
        {
            var normalizedMessage = string.IsNullOrWhiteSpace(message)
                ? "Resource responder failed."
                : message.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return new ChannelBuildStepResult(ResponderId, phase, false, normalizedMessage);
        }

        internal static IReadOnlyDictionary<string, string> CreateArtifactOutputs(
            ChannelBuildContext context,
            ResourceBuildResult result,
            IReadOnlyList<string> packagedTargets)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            if (result.Succeeded is false)
            {
                throw new ArgumentException("Resource build result must be successful.", nameof(result));
            }

            var outputRoot = Path.GetFullPath(context.OutputRoot);
            var resultRoot = Path.GetFullPath(result.OutputRoot ?? string.Empty);
            if (IsPathInside(outputRoot, resultRoot) is false)
            {
                throw new InvalidDataException("Resource build output root escaped the channel output root.");
            }

            var packagedPaths = new HashSet<string>(
                (packagedTargets ?? Array.Empty<string>()).Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            var artifacts = new List<ArtifactOutput>();
            var artifactPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceArtifacts = result.Artifacts ?? new List<ResourceBuildArtifact>();
            for (var i = 0; i < sourceArtifacts.Count; i++)
            {
                var artifact = sourceArtifacts[i];
                if (artifact == null || string.IsNullOrWhiteSpace(artifact.LocalPath))
                {
                    throw new InvalidDataException("Resource build result contains an invalid artifact.");
                }

                var artifactPath = Path.GetFullPath(artifact.LocalPath);
                if (IsPathInside(outputRoot, artifactPath) is false)
                {
                    if (packagedPaths.Contains(artifactPath))
                    {
                        continue;
                    }

                    throw new InvalidDataException("Resource build artifact escaped the channel output root.");
                }
                if (System.IO.File.Exists(artifactPath) is false)
                {
                    throw new FileNotFoundException("Resource build artifact is missing.", artifactPath);
                }
                if (artifactPaths.Add(artifactPath) is false)
                {
                    throw new InvalidDataException("Resource build artifact path is duplicated.");
                }

                var kind = string.Equals(artifact.PackageName, "manifest", StringComparison.Ordinal)
                    ? "resource-manifest"
                    : "resource-artifact";
                artifacts.Add(new ArtifactOutput(
                    kind,
                    GetRelativePath(outputRoot, artifactPath)));
            }

            var manifestPath = Path.GetFullPath(result.ManifestPath ?? string.Empty);
            if (IsPathInside(outputRoot, manifestPath) is false || System.IO.File.Exists(manifestPath) is false)
            {
                throw new FileNotFoundException("Primary resource build manifest is missing.", manifestPath);
            }
            if (artifacts.Any(artifact =>
                    string.Equals(artifact.Path, GetRelativePath(outputRoot, manifestPath), StringComparison.Ordinal)) is false)
            {
                throw new InvalidDataException("Primary resource build manifest is not an artifact.");
            }

            artifacts.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));
            var outputs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["resource.outputRoot"] = GetRelativePath(outputRoot, resultRoot),
                ["resource.manifestPath"] = GetRelativePath(outputRoot, manifestPath),
                ["resource.artifactCount"] = artifacts.Count.ToString(CultureInfo.InvariantCulture)
            };
            for (var i = 0; i < artifacts.Count; i++)
            {
                var prefix = $"resource.artifact.{i:D4}";
                outputs.Add(prefix + ".kind", artifacts[i].Kind);
                outputs.Add(prefix + ".path", artifacts[i].Path);
            }

            return outputs;
        }

        private static string GetRelativePath(string rootPath, string filePath)
        {
            var root = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var file = Path.GetFullPath(filePath);
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (file.StartsWith(root, comparison) is false)
            {
                throw new ArgumentException("Path must remain inside root.", nameof(filePath));
            }

            return file.Substring(root.Length)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private void ValidateExecutionContext(ChannelBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (m_Request == null || m_Workflow == null || m_PackagedTargets == null)
            {
                throw new GameException("Channel resource responder is not prepared.");
            }
            if (ReferenceEquals(context, m_Context) is false)
            {
                throw new GameException("Channel resource responder context does not match Prepare.");
            }
        }

        private sealed class ArtifactOutput
        {
            internal ArtifactOutput(string kind, string path)
            {
                Kind = kind;
                Path = path;
            }

            internal string Kind { get; }

            internal string Path { get; }
        }
    }
}
