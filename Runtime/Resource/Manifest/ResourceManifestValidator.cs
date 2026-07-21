using System;
using System.Collections.Generic;
using System.Linq;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.Resource
{
    internal static class ResourceManifestValidator
    {
        public static ResourceManifestIndex ValidateAndIndex(
            ManifestInfo manifest,
            ResourceMode mode,
            IReadOnlyCollection<string> remoteBundleNames = null)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var errors = new List<string>();
            var remoteNames = new HashSet<string>(
                remoteBundleNames ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            if (Enum.IsDefined(typeof(ResourceMode), mode) is false)
            {
                errors.Add($"Mode has unsupported value: {(int)mode}.");
            }

            if (manifest.FormatVersion != ManifestInfo.CurrentFormatVersion)
            {
                errors.Add(
                    $"Manifest format '{manifest.FormatVersion}' is not supported; expected '{ManifestInfo.CurrentFormatVersion}'.");
            }

            if (manifest.Packages == null)
            {
                errors.Add("Packages cannot be null.");
                ThrowIfInvalid(errors);
            }

            var packageNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var bundleNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var locations = new Dictionary<string, string>(StringComparer.Ordinal);
            var bundleRecords = new List<BundleRecord>();
            if (remoteNames.Count > 0 && mode is not ResourceMode.Online and not ResourceMode.Web)
            {
                errors.Add($"Remote bundle names are not allowed in {mode} mode.");
            }

            var hasRemoteAssetBundle = false;
            for (var packageIndex = 0; packageIndex < manifest.Packages.Count; packageIndex++)
            {
                var packagePath = $"Packages[{packageIndex}]";
                var package = manifest.Packages[packageIndex];
                if (package == null)
                {
                    errors.Add($"{packagePath} cannot be null.");
                    continue;
                }

                ValidateIdentity(package.Name, $"{packagePath}.Name", packageNames, errors);
                var bundles = package.Bundles ?? new List<BundleInfo>();
                for (var bundleIndex = 0; bundleIndex < bundles.Count; bundleIndex++)
                {
                    var bundlePath = $"{packagePath}.Bundles[{bundleIndex}]";
                    var bundle = bundles[bundleIndex];
                    if (bundle == null)
                    {
                        errors.Add($"{bundlePath} cannot be null.");
                        continue;
                    }

                    ValidateIdentity(bundle.Name, $"{bundlePath}.Name", bundleNames, errors);
                    bundleRecords.Add(new BundleRecord(bundlePath, bundle));
                    if (bundle.Size < 0)
                    {
                        errors.Add($"{bundlePath}.Size cannot be negative: {bundle.Size}.");
                    }

                    var providerId = bundle.ProviderId?.Trim();
                    var isResources = string.Equals(providerId, ResourceProviderIds.Resources, StringComparison.Ordinal);
                    var isAssetBundle = string.Equals(providerId, ResourceProviderIds.AssetBundle, StringComparison.Ordinal);
                    var isRemote = string.IsNullOrWhiteSpace(bundle.Name) is false && remoteNames.Contains(bundle.Name);
                    if (isResources is false && isAssetBundle is false)
                    {
                        errors.Add($"{bundlePath}.ProviderId is required and must be '{ResourceProviderIds.Resources}' or '{ResourceProviderIds.AssetBundle}': {bundle.ProviderId ?? "<null>"}.");
                    }

                    if (isRemote)
                    {
                        if (isAssetBundle is false)
                        {
                            errors.Add($"{bundlePath}.ProviderId must be '{ResourceProviderIds.AssetBundle}' for a remote bundle.");
                        }
                        else
                        {
                            hasRemoteAssetBundle = true;
                        }

                        if (bundle.Size <= 0)
                        {
                            errors.Add($"{bundlePath}.Size must be greater than zero for a remote bundle: {bundle.Size}.");
                        }

                        if (IsSha1Hash(bundle.Hash) is false)
                        {
                            errors.Add($"{bundlePath}.Hash must be a 40-character hexadecimal SHA-1 for a remote bundle.");
                        }

                        if (mode == ResourceMode.Web && bundle.Crc == 0)
                        {
                            errors.Add($"{bundlePath}.Crc must be non-zero for a Web remote bundle.");
                        }
                    }

                    var assets = bundle.Assets ?? new List<AssetInfo>();
                    for (var assetIndex = 0; assetIndex < assets.Count; assetIndex++)
                    {
                        var assetPath = $"{bundlePath}.Assets[{assetIndex}]";
                        var asset = assets[assetIndex];
                        if (asset == null)
                        {
                            errors.Add($"{assetPath} cannot be null.");
                            continue;
                        }

                        ValidateIdentity(asset.Location, $"{assetPath}.Location", locations, errors);
                        if (isResources && IsValidResourcesLocation(asset.Location) is false)
                        {
                            errors.Add($"{assetPath}.Location must be an extensionless 'Resources/...' path: {asset.Location ?? "<null>"}.");
                        }

                    }
                }
            }

            foreach (var remoteName in remoteNames)
            {
                if (bundleNames.ContainsKey(remoteName) is false)
                {
                    errors.Add($"Remote bundle name does not exist in the merged manifest: {remoteName}.");
                }
            }

            if (hasRemoteAssetBundle && string.IsNullOrWhiteSpace(manifest.Version))
            {
                errors.Add($"Version is required when {mode} manifest contains asset-bundle resources.");
            }

            ValidateDependencies(bundleRecords, errors);
            ThrowIfInvalid(errors);
            return new ResourceManifestIndex(manifest, remoteNames);
        }

        private static bool IsSha1Hash(string value)
        {
            if (value == null || value.Length != 40)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < '0' || character > '9') &&
                    (character < 'a' || character > 'f') &&
                    (character < 'A' || character > 'F'))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateIdentity(
            string value,
            string path,
            IDictionary<string, string> known,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{path} cannot be empty.");
                return;
            }

            if (known.TryGetValue(value, out var firstPath))
            {
                errors.Add($"{path} duplicates {firstPath}: {value}.");
                return;
            }

            known.Add(value, path);
        }

        private static bool IsValidResourcesLocation(string location)
        {
            return string.IsNullOrWhiteSpace(location) is false &&
                   location.StartsWith("Resources/", StringComparison.Ordinal) &&
                   IOPath.HasExtension(location) is false;
        }

        private static void ValidateDependencies(
            IReadOnlyList<BundleRecord> records,
            ICollection<string> errors)
        {
            var bundles = new Dictionary<string, BundleRecord>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.Bundle.Name) is false &&
                    bundles.ContainsKey(record.Bundle.Name) is false)
                {
                    bundles.Add(record.Bundle.Name, record);
                }
            }

            foreach (var record in records)
            {
                var dependencies = record.Bundle.Dependencies ?? new List<string>();
                for (var dependencyIndex = 0; dependencyIndex < dependencies.Count; dependencyIndex++)
                {
                    var dependency = dependencies[dependencyIndex];
                    var dependencyPath = $"{record.Path}.Dependencies[{dependencyIndex}]";
                    if (string.IsNullOrWhiteSpace(dependency))
                    {
                        errors.Add($"{dependencyPath} cannot be empty.");
                    }
                    else if (string.Equals(dependency, record.Bundle.Name, StringComparison.Ordinal))
                    {
                        errors.Add($"{dependencyPath} cannot reference its own bundle: {dependency}.");
                    }
                    else if (bundles.ContainsKey(dependency) is false)
                    {
                        errors.Add($"{dependencyPath} references missing bundle: {dependency}.");
                    }
                }
            }

            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            var stack = new List<string>();
            foreach (var record in records)
            {
                var name = record.Bundle.Name;
                if (string.IsNullOrWhiteSpace(name) ||
                    bundles.TryGetValue(name, out var canonical) is false ||
                    ReferenceEquals(canonical, record) is false ||
                    states.ContainsKey(name))
                {
                    continue;
                }

                VisitDependencies(record, bundles, states, stack, errors);
            }
        }

        private static void VisitDependencies(
            BundleRecord record,
            IReadOnlyDictionary<string, BundleRecord> bundles,
            IDictionary<string, int> states,
            IList<string> stack,
            ICollection<string> errors)
        {
            states[record.Bundle.Name] = 1;
            stack.Add(record.Bundle.Name);
            var dependencies = record.Bundle.Dependencies ?? new List<string>();
            for (var dependencyIndex = 0; dependencyIndex < dependencies.Count; dependencyIndex++)
            {
                var dependency = dependencies[dependencyIndex];
                if (string.IsNullOrWhiteSpace(dependency) ||
                    string.Equals(dependency, record.Bundle.Name, StringComparison.Ordinal) ||
                    bundles.TryGetValue(dependency, out var dependencyRecord) is false)
                {
                    continue;
                }

                if (states.TryGetValue(dependency, out var state) is false)
                {
                    VisitDependencies(dependencyRecord, bundles, states, stack, errors);
                    continue;
                }

                if (state == 1)
                {
                    var cycleStart = stack.IndexOf(dependency);
                    var cycle = stack.Skip(cycleStart).Concat(new[] { dependency });
                    errors.Add(
                        $"{record.Path}.Dependencies[{dependencyIndex}] creates dependency cycle: " +
                        string.Join(" -> ", cycle) + ".");
                }
            }

            stack.RemoveAt(stack.Count - 1);
            states[record.Bundle.Name] = 2;
        }

        private static void ThrowIfInvalid(IReadOnlyCollection<string> errors)
        {
            if (errors.Count == 0)
            {
                return;
            }

            throw new GameException(
                "Resource manifest validation failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => $"- {error}")));
        }

        private sealed class BundleRecord
        {
            public BundleRecord(string path, BundleInfo bundle)
            {
                Path = path;
                Bundle = bundle;
            }

            public string Path { get; }

            public BundleInfo Bundle { get; }
        }
    }
}
