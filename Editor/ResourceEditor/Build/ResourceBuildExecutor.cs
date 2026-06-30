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
    public static class ResourceBuildExecutor
    {
        /// <summary>
        /// 构建 member。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="plan">plan 参数。</param>
        /// <returns>执行结果。</returns>
        public static ResourceBuildResult Build(ResourceBuildContext context, ResourceBuildPlan plan)
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

                var channel = context.BuildSettings.Channel?.Trim();
                if (string.IsNullOrWhiteSpace(channel))
                {
                    return ResourceBuildResult.Failure("Build channel cannot be empty.");
                }

                var version = ResourceManifestBuildWriter.ResolveVersion(context);
                if (string.IsNullOrWhiteSpace(version))
                {
                    return ResourceBuildResult.Failure("Build version cannot be empty.");
                }

                var target = EditorUserBuildSettings.activeBuildTarget;
                var outputRoot = ResourceBuildUtilities.ProjectRelativeOrAbsolutePath(ResourceBuildSettings.OUTPUT_ROOT);
                if (string.IsNullOrWhiteSpace(outputRoot))
                {
                    return ResourceBuildResult.Failure("Build output root cannot be empty.");
                }

                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() is false)
                {
                    return ResourceBuildResult.Failure("Build canceled because open scenes have unsaved changes.");
                }

                var platform = target.ToString();
                var versionRoot = ResolveVersionOutputRoot(outputRoot, channel, platform, version);
                if (context.BuildSettings.CleanOutput && Directory.Exists(versionRoot))
                {
                    Directory.Delete(versionRoot, true);
                }

                Directory.CreateDirectory(versionRoot);
                var sbpPlan = ResourceManifestPartitioner.CreateSbpPlan(plan);
                var result = new ResourceBuildResult
                {
                    Succeeded = true,
                    OutputRoot = versionRoot,
                    BuildTime = new DateTimeOffset(context.BuildTime).ToUnixTimeSeconds()
                };

                if (sbpPlan.Bundles.Count > 0)
                {
                    ValidateSbpPlan(sbpPlan);
                    var builds = CreateBuildMap(sbpPlan);
                    var buildParameters = CreateBuildParameters(versionRoot, context.BuildSettings, target, out var parameterError);
                    if (buildParameters == null)
                    {
                        return ResourceBuildResult.Failure(parameterError);
                    }

                    var exitCode = ContentPipeline.BuildAssetBundles(buildParameters, new BundleBuildContent(builds), out IBundleBuildResults sbpResults);
                    if (exitCode < ReturnCode.Success || sbpResults == null)
                    {
                        return ResourceBuildResult.Failure($"SBP build failed: {exitCode}.");
                    }

                    AddBuildArtifacts(result, context, sbpPlan, sbpResults.BundleInfos, versionRoot, channel, platform, version);
                    CopyLocalBaseBundles(context, result);
                }

                CleanupSbpSidecars(versionRoot);
                WriteManifests(context, plan, result, channel, platform, version);
                return result;
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
        private static UnityEngine.BuildCompression ToBuildCompression(ResourceBuildCompression compression)
        {
            switch (compression)
            {
                case ResourceBuildCompression.Lz4:
                    return UnityEngine.BuildCompression.LZ4;
                case ResourceBuildCompression.Uncompressed:
                    return UnityEngine.BuildCompression.Uncompressed;
                default:
                    return UnityEngine.BuildCompression.LZMA;
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
                var hash = details.Hash.ToString();
                if (string.IsNullOrWhiteSpace(hash) || hash == default(Hash128).ToString())
                {
                    hash = System.IO.File.Exists(builtPath) ? ResourceBuildUtilities.ComputeHash(builtPath) : string.Empty;
                }

                var fileName = $"{ResourceBuildUtilities.SanitizeSegment(hash, Path.GetFileNameWithoutExtension(bundleName))}.bundle";
                var remoteKey = ResourceBuildUtilities.CombineRemoteKey(
                    ResourceBuildUtilities.SanitizeSegment(channel, "dev"),
                    ResourceBuildUtilities.SanitizeSegment(platform, "platform"),
                    ResourceBuildUtilities.SanitizeSegment(version, "version"),
                    fileName);
                var localPath = Path.Combine(versionRoot, fileName).Replace('\\', '/');
                MoveFile(builtPath, localPath);
                var size = System.IO.File.Exists(localPath) ? new FileInfo(localPath).Length : 0L;

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
                    ResourceBuildUtilities.SanitizeSegment(channel, "dev"),
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
        private static void WriteManifests(ResourceBuildContext context, ResourceBuildPlan plan, ResourceBuildResult result, string channel, string platform, string version)
        {
            var manifestName = ResourceSettings.MANIFEST_NAME;
            if (context.BuildSettings.Scope != ResourceBuildScope.HotUpdatePackages)
            {
                var localManifest = ResourceManifestPartitioner.BuildLocalBaseManifest(context, plan, result);
                var localManifestPath = ResourceManifestPartitioner.ResolveLocalManifestPath(context.Settings);
                ResourceManifestPartitioner.WriteManifest(localManifestPath, localManifest);
                result.Artifacts.Add(CreateManifestArtifact(localManifestPath, "local-base-manifest", string.Empty));
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
                    ResourceBuildUtilities.SanitizeSegment(channel, "dev"),
                    ResourceBuildUtilities.SanitizeSegment(platform, "platform"),
                    ResourceBuildUtilities.SanitizeSegment(version, "version"),
                    manifestName),
                Hash = ResourceBuildUtilities.ComputeHash(hotManifestPath),
                Size = new FileInfo(hotManifestPath).Length,
                Crc = Crc32Utility.Compute(System.IO.File.ReadAllBytes(hotManifestPath)),
                Dependencies = new List<string>()
            });
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

        private static void CopyLocalBaseBundles(ResourceBuildContext context, ResourceBuildResult result)
        {
            foreach (var artifact in result.Artifacts.Where(artifact => IsNonHotUpdateBundle(context, artifact)).ToArray())
            {
                var destination = ResourceManifestPartitioner.ResolveLocalBundlePath(artifact);
                if (string.IsNullOrWhiteSpace(destination))
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");
                System.IO.File.Copy(artifact.LocalPath, destination, true);
                if (System.IO.File.Exists(artifact.LocalPath))
                {
                    System.IO.File.Delete(artifact.LocalPath);
                }

                artifact.LocalPath = destination;
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
