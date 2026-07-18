using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit;
using GameDeveloperKit.Resource;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Build.Pipeline;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Resource Build Executor 类型。
    /// </summary>
    internal static class ResourceBuildExecutor
    {
        /// <summary>
        /// 构建 member。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="plan">plan 参数。</param>
        /// <returns>执行结果。</returns>
        internal static ResourceBuildResult Build(ResourceBuildContext context, ResourceBuildPlan plan)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            try
            {
                ValidatePlan(plan);

                var channels = ResolveBuildChannels(context.BuildSettings);
                if (channels.Count == 0)
                {
                    return ResourceBuildResult.Failure("Build channel cannot be empty.");
                }

                var channel = channels[0];
                var version = ResourceManifestBuildWriter.ResolveVersion(context);
                if (string.IsNullOrWhiteSpace(version))
                {
                    return ResourceBuildResult.Failure("Build version cannot be empty.");
                }

                var target = context.Target;
                var outputRoot = ResourceBuildUtilities.ProjectRelativeOrAbsolutePath(context.BuildSettings.OutputRoot);
                if (string.IsNullOrWhiteSpace(outputRoot))
                {
                    return ResourceBuildResult.Failure("Build output root cannot be empty.");
                }

                var platform = target.ToString();
                var finalVersionRoot = ResolveVersionOutputRoot(outputRoot, channel, platform, version);
                var versionRoot = ResourceBuildOutputTransaction.GetDirectoryStagingPath(finalVersionRoot);
                var sbpPlan = ResourceManifestPartitioner.CreateSbpPlan(plan);
                AssetBundleBuild[] builds = null;
                BundleBuildParameters buildParameters = null;
                if (sbpPlan.Bundles.Count > 0)
                {
                    ValidateSbpPlan(sbpPlan);
                    builds = CreateBuildMap(sbpPlan);
                    buildParameters = CreateBuildParameters(versionRoot, context.BuildSettings, target, out var parameterError);
                    if (buildParameters == null)
                    {
                        return ResourceBuildResult.Failure(parameterError);
                    }
                }

                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() is false)
                {
                    return ResourceBuildResult.Failure("Build canceled because open scenes have unsaved changes.");
                }

                using (var transaction = ResourceBuildOutputTransaction.Begin())
                {
                    versionRoot = transaction.StageDirectory(finalVersionRoot, context.BuildSettings.CleanOutput is false);
                    var result = new ResourceBuildResult
                    {
                        Succeeded = true,
                        OutputRoot = versionRoot,
                        BuildTime = new DateTimeOffset(context.BuildTime).ToUnixTimeSeconds()
                    };

                    if (sbpPlan.Bundles.Count > 0)
                    {
                        var exitCode = ContentPipeline.BuildAssetBundles(buildParameters, new BundleBuildContent(builds), out IBundleBuildResults sbpResults);
                        if (exitCode < ReturnCode.Success || sbpResults == null)
                        {
                            return ResourceBuildResult.Failure($"SBP build failed: {exitCode}.");
                        }

                        AddBuildArtifacts(result, context, sbpPlan, sbpResults.BundleInfos, versionRoot, channel, platform, version);
                        StageLocalBaseBundles(context, result, transaction);
                    }

                    CleanupSbpSidecars(versionRoot);
                    WriteManifests(context, plan, result, channel, platform, version, transaction);
                    StageAdditionalChannels(context, plan, result, channels, platform, version, transaction);
                    ValidateBuildOutputs(context, result);
                    transaction.Commit();
                    RewriteCommittedPaths(result, transaction, finalVersionRoot);
                    return result;
                }
            }
            catch (Exception exception)
            {
                return ResourceBuildResult.Failure($"{exception.GetType().Name}: {exception.Message}");
            }
        }

        /// <summary>
        /// 校验 Plan。
        /// </summary>
        /// <param name="plan">plan 参数。</param>
        private static void ValidatePlan(ResourceBuildPlan plan)
        {
            if (plan.Bundles.Count == 0)
            {
                throw new InvalidOperationException("Build plan has no bundles.");
            }

            foreach (var bundle in plan.Bundles)
            {
                if (bundle == null)
                {
                    throw new InvalidOperationException("Build plan contains null bundle.");
                }

                if (string.IsNullOrWhiteSpace(bundle.BundleName))
                {
                    throw new InvalidOperationException("Build plan contains empty bundle name.");
                }

                if (ResourceProviderIds.IsAssetBundle(bundle.Bundle.ProviderId) &&
                    (bundle.Resources == null || bundle.Resources.Count == 0))
                {
                    throw new InvalidOperationException($"Bundle has no resources: {bundle.BundleName}");
                }
            }
        }

        private static void ValidateSbpPlan(ResourceBuildPlan plan)
        {
            foreach (var bundle in plan.Bundles)
            {
                if (bundle.Resources == null || bundle.Resources.Count == 0)
                {
                    throw new InvalidOperationException($"Bundle has no resources: {bundle.BundleName}");
                }
            }
        }

        /// <summary>
        /// 创建 Build Map。
        /// </summary>
        /// <param name="plan">plan 参数。</param>
        /// <returns>执行结果。</returns>
        private static AssetBundleBuild[] CreateBuildMap(ResourceBuildPlan plan)
        {
            var bundleNames = new HashSet<string>(StringComparer.Ordinal);
            var assetPaths = new HashSet<string>(StringComparer.Ordinal);
            var builds = new List<AssetBundleBuild>();

            foreach (var bundle in plan.Bundles)
            {
                if (bundleNames.Add(bundle.BundleName) is false)
                {
                    throw new InvalidOperationException($"Duplicate bundle name: {bundle.BundleName}");
                }

                var assetNames = new List<string>();
                var addressableNames = new List<string>();
                foreach (var resource in bundle.Resources)
                {
                    if (resource == null || string.IsNullOrWhiteSpace(resource.AssetPath))
                    {
                        throw new InvalidOperationException($"Bundle contains empty resource path: {bundle.BundleName}");
                    }

                    var assetPath = resource.AssetPath.Replace('\\', '/');
                    if (AssetDatabase.IsValidFolder(assetPath))
                    {
                        throw new InvalidOperationException($"Bundle resource is a folder, not an asset: {assetPath}");
                    }

                    if (string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(assetPath)))
                    {
                        throw new InvalidOperationException($"Bundle resource does not exist: {assetPath}");
                    }

                    if (assetPaths.Add(assetPath) is false)
                    {
                        throw new InvalidOperationException($"Resource is assigned to multiple bundles: {assetPath}");
                    }

                    assetNames.Add(assetPath);
                    addressableNames.Add(string.IsNullOrWhiteSpace(resource.Location) ? assetPath : resource.Location);
                }

                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = bundle.BundleName,
                    assetNames = assetNames.ToArray(),
                    addressableNames = addressableNames.ToArray()
                });
            }

            return builds.ToArray();
        }

        /// <summary>
        /// 创建 Build Parameters。
        /// </summary>
        /// <param name="outputRoot">output Root 参数。</param>
        /// <param name="settings">settings 参数。</param>
        /// <param name="target">target 参数。</param>
        /// <param name="error">error 参数。</param>
        /// <returns>执行结果。</returns>
        private static BundleBuildParameters CreateBuildParameters(string outputRoot, ResourceBuildSettings settings, BuildTarget target, out string error)
        {
            error = null;
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (group == BuildTargetGroup.Unknown || BuildPipeline.IsBuildTargetSupported(group, target) is false)
            {
                error = $"Build target is not supported by the current Unity installation: {target}";
                return null;
            }

            var parameters = new BundleBuildParameters(target, group, outputRoot)
            {
                UseCache = false,
                BundleCompression = ToBuildCompression(settings.Compression)
            };

            return parameters;
        }

        /// <summary>
        /// 执行 To Build Compression。
        /// </summary>
        /// <param name="compression">compression 参数。</param>
        /// <returns>执行结果。</returns>
        internal static UnityEngine.BuildCompression ToBuildCompression(ResourceBuildCompression compression)
        {
            switch (compression)
            {
                case ResourceBuildCompression.Lz4:
                    return UnityEngine.BuildCompression.LZ4;
                case ResourceBuildCompression.Uncompressed:
                    return UnityEngine.BuildCompression.Uncompressed;
                case ResourceBuildCompression.Default:
                    return UnityEngine.BuildCompression.LZMA;
                default:
                    throw new ArgumentOutOfRangeException(nameof(compression), compression, "Unsupported resource build compression.");
            }
        }

        /// <summary>
        /// 构建 Result。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="plan">plan 参数。</param>
        /// <param name="bundleInfos">bundle Infos 参数。</param>
        /// <param name="versionRoot">version Root 参数。</param>
        /// <param name="channel">channel 参数。</param>
        /// <param name="platform">platform 参数。</param>
        /// <param name="version">version 参数。</param>
        /// <returns>执行结果。</returns>
        private static void AddBuildArtifacts(
            ResourceBuildResult result,
            ResourceBuildContext context,
            ResourceBuildPlan plan,
            IReadOnlyDictionary<string, BundleDetails> bundleInfos,
            string versionRoot,
            string channel,
            string platform,
            string version)
        {
            foreach (var planBundle in plan.Bundles)
            {
                var bundleName = planBundle.BundleName;
                if (bundleInfos.TryGetValue(bundleName, out var details) is false)
                {
                    throw new InvalidOperationException($"SBP result missing bundle info: {bundleName}");
                }

                var builtPath = ResolveBundlePath(versionRoot, bundleName, details);
                var fileName = bundleName;
                var remoteKey = ResourceBuildUtilities.CombineRemoteKey(
                    ResourceBuildUtilities.SanitizeSegment(channel, ResourceSettings.DEFAULT_CHANNEL_NAME),
                    ResourceBuildUtilities.SanitizeSegment(platform, "platform"),
                    ResourceBuildUtilities.SanitizeSegment(version, "version"),
                    fileName);
                var localPath = Path.Combine(versionRoot, fileName).Replace('\\', '/');
                MoveFile(builtPath, localPath);
                var size = System.IO.File.Exists(localPath) ? new FileInfo(localPath).Length : 0L;
                var hash = System.IO.File.Exists(localPath)
                    ? ResourceBuildUtilities.ComputeHash(localPath)
                    : string.Empty;

                result.Artifacts.Add(new ResourceBuildArtifact
                {
                    PackageName = planBundle.Package.Name,
                    BundleName = bundleName,
                    LocalPath = localPath,
                    RemoteKey = remoteKey,
                    Hash = hash,
                    Size = size,
                    Crc = details.Crc,
                    Dependencies = (details.Dependencies ?? Array.Empty<string>())
                        .Where(x => string.IsNullOrWhiteSpace(x) is false)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToList()
                });
            }
        }

        /// <summary>
        /// 解析 Version Output Root。
        /// </summary>
        /// <param name="outputRoot">output Root 参数。</param>
        /// <param name="channel">channel 参数。</param>
        /// <param name="platform">platform 参数。</param>
        /// <param name="version">version 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ResolveVersionOutputRoot(string outputRoot, string channel, string platform, string version)
        {
            return Path.Combine(
                    outputRoot,
                    ResourceBuildUtilities.SanitizeSegment(channel, ResourceSettings.DEFAULT_CHANNEL_NAME),
                    ResourceBuildUtilities.SanitizeSegment(platform, "platform"),
                    ResourceBuildUtilities.SanitizeSegment(version, "version"))
                .Replace('\\', '/');
        }

        /// <summary>
        /// 执行 Move File。
        /// </summary>
        /// <param name="source">source 参数。</param>
        /// <param name="destination">destination 参数。</param>
        private static void MoveFile(string source, string destination)
        {
            if (string.IsNullOrWhiteSpace(source) || System.IO.File.Exists(source) is false)
            {
                throw new FileNotFoundException($"Built bundle file not found: {source}", source);
            }

            source = source.Replace('\\', '/');
            destination = destination.Replace('\\', '/');
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");
            if (System.IO.File.Exists(destination))
            {
                System.IO.File.Delete(destination);
            }

            System.IO.File.Move(source, destination);
        }

        /// <summary>
        /// 解析 Bundle Path。
        /// </summary>
        /// <param name="outputRoot">output Root 参数。</param>
        /// <param name="bundleName">bundle Name 参数。</param>
        /// <param name="details">details 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ResolveBundlePath(string outputRoot, string bundleName, BundleDetails details)
        {
            if (string.IsNullOrWhiteSpace(details.FileName) is false)
            {
                var fileName = details.FileName.Replace('\\', '/');
                if (System.IO.File.Exists(fileName))
                {
                    return fileName;
                }

                var combinedFileName = Path.Combine(outputRoot, fileName).Replace('\\', '/');
                if (System.IO.File.Exists(combinedFileName))
                {
                    return combinedFileName;
                }
            }

            var directPath = Path.Combine(outputRoot, bundleName).Replace('\\', '/');
            return directPath;
        }

        /// <summary>
        /// 执行 Cleanup Sbp Sidecars。
        /// </summary>
        /// <param name="versionRoot">version Root 参数。</param>
        private static void CleanupSbpSidecars(string versionRoot)
        {
            if (Directory.Exists(versionRoot) is false)
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(versionRoot, "*.manifest", SearchOption.TopDirectoryOnly)
                         .Concat(Directory.EnumerateFiles(versionRoot, "buildlogtep.json", SearchOption.TopDirectoryOnly)))
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// 写入 Manifest。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="plan">plan 参数。</param>
        /// <param name="result">result 参数。</param>
        /// <param name="channel">channel 参数。</param>
        /// <param name="platform">platform 参数。</param>
        /// <param name="version">version 参数。</param>
        private static void WriteManifests(
            ResourceBuildContext context,
            ResourceBuildPlan plan,
            ResourceBuildResult result,
            string channel,
            string platform,
            string version,
            ResourceBuildOutputTransaction transaction)
        {
            var manifestName = context.BuildSettings.ManifestFileName;
            if (context.BuildSettings.Scope != ResourceBuildScope.HotUpdatePackages)
            {
                var localManifest = ResourceManifestPartitioner.BuildLocalBaseManifest(context, plan, result);
                var localManifestPath = ResourceManifestPartitioner.ResolveLocalManifestPath(context.Settings);
                var localManifestStagingPath = transaction.StageFile(localManifestPath);
                ResourceManifestPartitioner.WriteManifest(localManifestStagingPath, localManifest);
                result.Artifacts.Add(CreateManifestArtifact(localManifestStagingPath, "local-base-manifest", string.Empty));
            }

            var hotManifest = ResourceManifestPartitioner.BuildHotUpdateManifest(context, plan, result);
            var hotManifestPath = Path.Combine(result.OutputRoot, manifestName).Replace('\\', '/');
            ResourceManifestPartitioner.WriteManifest(hotManifestPath, hotManifest);
            result.ManifestPath = hotManifestPath;
            result.Artifacts.Add(new ResourceBuildArtifact
            {
                PackageName = "manifest",
                BundleName = manifestName,
                LocalPath = hotManifestPath,
                RemoteKey = ResourceBuildUtilities.CombineRemoteKey(
                    ResourceBuildUtilities.SanitizeSegment(channel, ResourceSettings.DEFAULT_CHANNEL_NAME),
                    ResourceBuildUtilities.SanitizeSegment(platform, "platform"),
                    ResourceBuildUtilities.SanitizeSegment(version, "version"),
                    manifestName),
                Hash = ResourceBuildUtilities.ComputeHash(hotManifestPath),
                Size = new FileInfo(hotManifestPath).Length,
                Crc = Crc32Utility.Compute(System.IO.File.ReadAllBytes(hotManifestPath)),
                Dependencies = new List<string>()
            });
        }

        private static void StageAdditionalChannels(
            ResourceBuildContext context,
            ResourceBuildPlan plan,
            ResourceBuildResult primaryResult,
            IReadOnlyList<string> channels,
            string platform,
            string version,
            ResourceBuildOutputTransaction transaction)
        {
            if (channels == null || channels.Count <= 1)
            {
                return;
            }

            var outputRoot = ResourceBuildUtilities.ProjectRelativeOrAbsolutePath(context.BuildSettings.OutputRoot);
            var primaryVersionRoot = primaryResult.OutputRoot;
            var primaryArtifacts = primaryResult.Artifacts
                .Where(artifact => artifact != null && string.IsNullOrWhiteSpace(artifact.RemoteKey) is false)
                .ToList();

            for (var i = 1; i < channels.Count; i++)
            {
                var channel = channels[i];
                var finalVersionRoot = ResolveVersionOutputRoot(outputRoot, channel, platform, version);
                var targetVersionRoot = transaction.StageDirectory(finalVersionRoot, context.BuildSettings.CleanOutput is false);
                var channelResult = CloneResultForChannel(primaryResult, primaryArtifacts, channel, platform, version, targetVersionRoot);
                CopyChannelArtifacts(primaryVersionRoot, channelResult.Artifacts);
                RewriteChannelManifest(context, plan, channelResult, channel, platform, version);
                primaryResult.Artifacts.AddRange(channelResult.Artifacts);
            }
        }

        private static ResourceBuildResult CloneResultForChannel(
            ResourceBuildResult primaryResult,
            IReadOnlyList<ResourceBuildArtifact> primaryArtifacts,
            string targetChannel,
            string platform,
            string version,
            string targetVersionRoot)
        {
            var result = new ResourceBuildResult
            {
                Succeeded = true,
                OutputRoot = targetVersionRoot,
                BuildTime = primaryResult.BuildTime
            };

            foreach (var artifact in primaryArtifacts.Where(artifact => string.Equals(artifact.PackageName, "manifest", StringComparison.Ordinal) is false))
            {
                var fileName = Path.GetFileName(artifact.LocalPath);
                var targetPath = Path.Combine(targetVersionRoot, fileName).Replace('\\', '/');
                result.Artifacts.Add(new ResourceBuildArtifact
                {
                    PackageName = artifact.PackageName,
                    BundleName = artifact.BundleName,
                    LocalPath = targetPath,
                    RemoteKey = RewriteRemoteKey(artifact.RemoteKey, targetChannel, platform, version, fileName),
                    Hash = artifact.Hash,
                    Size = artifact.Size,
                    Crc = artifact.Crc,
                    Dependencies = artifact.Dependencies?.ToList() ?? new List<string>()
                });
            }

            return result;
        }

        private static void RewriteChannelManifest(
            ResourceBuildContext context,
            ResourceBuildPlan plan,
            ResourceBuildResult channelResult,
            string channel,
            string platform,
            string version)
        {
            var manifestName = context.BuildSettings.ManifestFileName;
            var hotManifest = ResourceManifestPartitioner.BuildHotUpdateManifest(context, plan, channelResult);
            var hotManifestPath = Path.Combine(channelResult.OutputRoot, manifestName).Replace('\\', '/');
            ResourceManifestPartitioner.WriteManifest(hotManifestPath, hotManifest);
            channelResult.ManifestPath = hotManifestPath;
            channelResult.Artifacts.Add(new ResourceBuildArtifact
            {
                PackageName = "manifest",
                BundleName = manifestName,
                LocalPath = hotManifestPath,
                RemoteKey = ResourceBuildUtilities.CombineRemoteKey(
                    ResourceBuildUtilities.SanitizeSegment(channel, ResourceSettings.DEFAULT_CHANNEL_NAME),
                    ResourceBuildUtilities.SanitizeSegment(platform, "platform"),
                    ResourceBuildUtilities.SanitizeSegment(version, "version"),
                    manifestName),
                Hash = ResourceBuildUtilities.ComputeHash(hotManifestPath),
                Size = new FileInfo(hotManifestPath).Length,
                Crc = Crc32Utility.Compute(System.IO.File.ReadAllBytes(hotManifestPath)),
                Dependencies = new List<string>()
            });
        }

        private static string RewriteRemoteKey(string remoteKey, string targetChannel, string platform, string version, string fileName)
        {
            return ResourceBuildUtilities.CombineRemoteKey(
                ResourceBuildUtilities.SanitizeSegment(targetChannel, ResourceSettings.DEFAULT_CHANNEL_NAME),
                ResourceBuildUtilities.SanitizeSegment(platform, "platform"),
                ResourceBuildUtilities.SanitizeSegment(version, "version"),
                string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(remoteKey) : fileName);
        }

        private static void CopyChannelArtifacts(
            string primaryVersionRoot,
            IReadOnlyList<ResourceBuildArtifact> artifacts)
        {
            if (Directory.Exists(primaryVersionRoot) is false)
            {
                throw new DirectoryNotFoundException(primaryVersionRoot);
            }

            foreach (var artifact in artifacts ?? Array.Empty<ResourceBuildArtifact>())
            {
                if (artifact == null || string.IsNullOrWhiteSpace(artifact.LocalPath))
                {
                    continue;
                }

                var source = Path.Combine(primaryVersionRoot, Path.GetFileName(artifact.LocalPath));
                Directory.CreateDirectory(Path.GetDirectoryName(artifact.LocalPath) ?? ".");
                System.IO.File.Copy(source, artifact.LocalPath, true);
            }
        }

        private static IReadOnlyList<string> ResolveBuildChannels(ResourceBuildSettings settings)
        {
            return (settings?.Channels ?? Array.Empty<string>())
                .Where(channel => string.IsNullOrWhiteSpace(channel) is false)
                .Select(channel => channel.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static ResourceBuildArtifact CreateManifestArtifact(string path, string packageName, string remoteKey)
        {
            return new ResourceBuildArtifact
            {
                PackageName = packageName,
                BundleName = Path.GetFileName(path),
                LocalPath = path,
                RemoteKey = remoteKey,
                Hash = ResourceBuildUtilities.ComputeHash(path),
                Size = new FileInfo(path).Length,
                Crc = Crc32Utility.Compute(System.IO.File.ReadAllBytes(path)),
                Dependencies = new List<string>()
            };
        }

        private static void ValidateBuildOutputs(ResourceBuildContext context, ResourceBuildResult result)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var artifact in result.Artifacts)
            {
                if (artifact == null || string.IsNullOrWhiteSpace(artifact.LocalPath))
                {
                    throw new InvalidOperationException("Resource build result contains an artifact without a path.");
                }

                var path = Path.GetFullPath(artifact.LocalPath).Replace('\\', '/');
                if (paths.Add(path) is false)
                {
                    throw new InvalidOperationException($"Resource build result contains a duplicate artifact path: {path}");
                }

                if (System.IO.File.Exists(path) is false)
                {
                    throw new FileNotFoundException($"Resource build artifact is missing: {path}", path);
                }

                var size = new FileInfo(path).Length;
                if (size != artifact.Size)
                {
                    throw new InvalidDataException($"Resource build artifact size mismatch: {path}. Expected={artifact.Size}, Actual={size}");
                }

                if (IsManifestArtifact(artifact))
                {
                    ValidateManifestArtifact(path, artifact, context.BuildSettings.ManifestVersion);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(artifact.Hash))
                {
                    throw new InvalidDataException($"Resource build bundle hash is empty: {path}");
                }

                var hash = ResourceBuildUtilities.ComputeHash(path);
                if (string.Equals(hash, artifact.Hash, StringComparison.Ordinal) is false)
                {
                    throw new InvalidDataException($"Resource build bundle hash mismatch: {path}");
                }
            }

            if (string.IsNullOrWhiteSpace(result.ManifestPath) || System.IO.File.Exists(result.ManifestPath) is false)
            {
                throw new FileNotFoundException($"Primary resource build manifest is missing: {result.ManifestPath}", result.ManifestPath);
            }
        }

        private static void ValidateManifestArtifact(
            string path,
            ResourceBuildArtifact artifact,
            string expectedVersion)
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            var hash = ResourceBuildUtilities.ComputeHash(path);
            if (string.Equals(hash, artifact.Hash, StringComparison.Ordinal) is false)
            {
                throw new InvalidDataException($"Resource build manifest hash mismatch: {path}");
            }

            var crc = Crc32Utility.Compute(bytes);
            if (crc != artifact.Crc)
            {
                throw new InvalidDataException($"Resource build manifest CRC mismatch: {path}. Expected={artifact.Crc}, Actual={crc}");
            }

            ManifestInfo manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<ManifestInfo>(System.IO.File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                throw new InvalidDataException($"Resource build manifest JSON is invalid: {path}", exception);
            }

            if (manifest == null || manifest.Packages == null)
            {
                throw new InvalidDataException($"Resource build manifest is empty: {path}");
            }

            if (string.Equals(manifest.Version, expectedVersion, StringComparison.Ordinal) is false)
            {
                throw new InvalidDataException(
                    $"Resource build manifest version mismatch: {path}. Expected={expectedVersion}, Actual={manifest.Version}");
            }

            var packageNames = new HashSet<string>(StringComparer.Ordinal);
            var bundleNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var package in manifest.Packages)
            {
                if (package == null || string.IsNullOrWhiteSpace(package.Name) || packageNames.Add(package.Name) is false)
                {
                    throw new InvalidDataException($"Resource build manifest contains an invalid or duplicate package: {path}");
                }

                foreach (var bundle in package.Bundles ?? new List<BundleInfo>())
                {
                    if (bundle == null || string.IsNullOrWhiteSpace(bundle.Name) || bundleNames.Add(bundle.Name) is false)
                    {
                        throw new InvalidDataException($"Resource build manifest contains an invalid or duplicate bundle: {path}");
                    }

                    if (ResourceProviderIds.IsAssetBundle(bundle.ProviderId) &&
                        (string.IsNullOrWhiteSpace(bundle.Hash) || bundle.Size < 0))
                    {
                        throw new InvalidDataException($"Resource build manifest contains invalid bundle metadata: {bundle.Name}");
                    }
                }
            }
        }

        private static bool IsManifestArtifact(ResourceBuildArtifact artifact)
        {
            return string.Equals(artifact?.PackageName, "manifest", StringComparison.Ordinal) ||
                   string.Equals(artifact?.PackageName, "local-base-manifest", StringComparison.Ordinal);
        }

        private static void RewriteCommittedPaths(
            ResourceBuildResult result,
            ResourceBuildOutputTransaction transaction,
            string finalVersionRoot)
        {
            foreach (var artifact in result.Artifacts)
            {
                artifact.LocalPath = transaction.ResolveTargetPath(artifact.LocalPath);
            }

            result.OutputRoot = finalVersionRoot;
            result.ManifestPath = transaction.ResolveTargetPath(result.ManifestPath);
        }

        private static void StageLocalBaseBundles(
            ResourceBuildContext context,
            ResourceBuildResult result,
            ResourceBuildOutputTransaction transaction)
        {
            foreach (var artifact in result.Artifacts.Where(artifact => IsNonHotUpdateBundle(context, artifact)).ToArray())
            {
                var destination = ResourceManifestPartitioner.ResolveLocalBundlePath(artifact);
                if (string.IsNullOrWhiteSpace(destination))
                {
                    continue;
                }

                var stagingPath = transaction.StageFile(destination);
                System.IO.File.Copy(artifact.LocalPath, stagingPath, true);
                if (System.IO.File.Exists(artifact.LocalPath))
                {
                    System.IO.File.Delete(artifact.LocalPath);
                }

                artifact.LocalPath = stagingPath;
                artifact.RemoteKey = string.Empty;
            }
        }

        private static bool IsNonHotUpdateBundle(ResourceBuildContext context, ResourceBuildArtifact artifact)
        {
            if (context == null || artifact == null || string.IsNullOrWhiteSpace(artifact.PackageName))
            {
                return false;
            }

            if (string.Equals(artifact.PackageName, "manifest", StringComparison.Ordinal))
            {
                return false;
            }

            var package = context.Settings.Packages.FirstOrDefault(package => package != null && package.Name == artifact.PackageName);
            return package != null && package.IsHotUpdate is false;
        }
    }
}
