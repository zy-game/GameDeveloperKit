using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using Newtonsoft.Json;

namespace GameDeveloperKit.ResourceEditor.Build
{
    internal static class Planner
    {
        public static bool TryCreate(Context context, out Plan plan, out string error)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            plan = new Plan();
            foreach (var package in context.Packages
                         .Where(package => package != null)
                         .OrderBy(package => package.Name, StringComparer.Ordinal))
            {
                foreach (var group in package.Bundles
                             .Where(group => group != null)
                             .OrderBy(group => group.Name, StringComparer.Ordinal)
                             .ThenBy(group => group.Group, StringComparer.Ordinal))
                {
                    var packRule = context.Registry.GetPackRule(group.PackRuleId);
                    if (packRule == null)
                    {
                        error = $"Missing pack rule '{group.PackRuleId}' for package '{package.Name}', group '{group.Name}'.";
                        plan = null;
                        return false;
                    }

                    var resources = context.GetResources(group)
                        .Where(resource => resource != null)
                        .OrderBy(resource => resource.AssetPath, StringComparer.Ordinal)
                        .ToArray();
                    if (resources.Length == 0)
                    {
                        if (ResourceProviderIds.IsAssetBundle(group.ProviderId))
                        {
                            continue;
                        }

                        AddBundle(plan, package, group, packRule.Id, "empty", resources);
                        continue;
                    }

                    var buckets = new SortedDictionary<string, List<ResourceGroupPreview>>(StringComparer.Ordinal);
                    foreach (var resource in resources)
                    {
                        string packKey;
                        try
                        {
                            packKey = packRule.Instance.GetPackKey(package, group, resource)?.Trim();
                        }
                        catch (Exception exception)
                        {
                            error = $"Pack rule '{packRule.Id}' failed for package '{package.Name}', group '{group.Name}': {exception.Message}";
                            plan = null;
                            return false;
                        }

                        if (string.IsNullOrWhiteSpace(packKey))
                        {
                            error = $"Pack rule '{packRule.Id}' returned an empty key for package '{package.Name}', group '{group.Name}', resource '{resource.AssetPath}'.";
                            plan = null;
                            return false;
                        }

                        if (buckets.TryGetValue(packKey, out var bucket) is false)
                        {
                            bucket = new List<ResourceGroupPreview>();
                            buckets.Add(packKey, bucket);
                        }

                        bucket.Add(resource);
                    }

                    foreach (var bucket in buckets)
                    {
                        AddBundle(plan, package, group, packRule.Id, bucket.Key, bucket.Value);
                    }
                }
            }

            error = null;
            return true;
        }

        private static void AddBundle(
            Plan plan,
            GameDeveloperKit.ResourceEditor.Authoring.Package package,
            GameDeveloperKit.ResourceEditor.Authoring.Bundle group,
            string ruleId,
            string packKey,
            IReadOnlyList<ResourceGroupPreview> resources)
        {
            var bundleName = CreateBundleName(package, group, ruleId, packKey, resources);
            plan.AddBundle(new PlanBundle(package, group, bundleName, resources));
        }

        internal static string CreateBundleName(
            GameDeveloperKit.ResourceEditor.Authoring.Package package,
            GameDeveloperKit.ResourceEditor.Authoring.Bundle group,
            string ruleId,
            string packKey,
            IReadOnlyList<ResourceGroupPreview> resources)
        {
            var payload = JsonConvert.SerializeObject(new
            {
                Package = package?.Name,
                GroupName = group?.Name,
                Group = group?.Group,
                Rule = ruleId,
                Key = packKey,
                Resources = (resources ?? Array.Empty<ResourceGroupPreview>())
                    .Where(resource => resource != null)
                    .OrderBy(resource => resource.AssetPath, StringComparer.Ordinal)
                    .Select(resource => new
                    {
                        resource.AssetPath,
                        resource.Location,
                        resource.TypeName
                    })
            });
            return $"{Utilities.ComputeHashFromText(payload)}.bundle";
        }
    }
}
