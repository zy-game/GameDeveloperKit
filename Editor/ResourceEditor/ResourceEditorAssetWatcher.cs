using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 监听 Resources 目录下资源的移动、删除、导入（新增），
    /// 自动同步 BUILTIN/Resources 分组条目并重写本地 StreamingAssets 清单。
    /// 不监听资源内容修改。
    /// </summary>
    internal sealed class ResourceEditorAssetWatcher : AssetPostprocessor
    {
        private static bool s_Writing;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (s_Writing || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (IsRelevant(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths) is false)
            {
                return;
            }

            Sync();
        }

        /// <summary>
        /// 判断本次资源变更是否涉及 Resources 目录或已跟踪的内置资源条目。
        /// </summary>
        private static bool IsRelevant(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // 新增（导入）、移入、移出 Resources 目录的路径按字符串即可判定。
            if (AnyRuntimeResourceAsset(importedAssets)
                || AnyRuntimeResourceAsset(movedAssets)
                || AnyRuntimeResourceAsset(movedFromAssetPaths))
            {
                return true;
            }

            // 删除或移出的路径若命中已跟踪条目，也需要同步（用于清理陈旧条目）。
            var tracked = TrackedResourcePaths();
            if (tracked.Count == 0)
            {
                return false;
            }

            return deletedAssets.Any(tracked.Contains)
                || movedFromAssetPaths.Any(tracked.Contains);
        }

        private static bool AnyRuntimeResourceAsset(IEnumerable<string> paths)
        {
            return paths != null && paths.Any(UnityResourcesCollector.IsRuntimeResourceAsset);
        }

        /// <summary>
        /// 当前 BUILTIN/Resources 分组已跟踪的资源路径集合。
        /// </summary>
        private static HashSet<string> TrackedResourcePaths()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var settings = ResourceEditorSettings.LoadOrCreate();
            var bundle = ResolveResourcesBundle(settings);
            if (bundle == null)
            {
                return result;
            }

            foreach (var entry in bundle.Entries.Where(entry => entry != null && string.IsNullOrWhiteSpace(entry.AssetPath) is false))
            {
                result.Add(entry.AssetPath);
            }

            return result;
        }

        /// <summary>
        /// 同步 BUILTIN/Resources 分组并重写本地清单。
        /// </summary>
        private static void Sync()
        {
            try
            {
                var settings = ResourceEditorSettings.LoadOrCreate();
                if (settings == null)
                {
                    return;
                }

                settings.EnsureDefaults();
                var package = settings.Packages.FirstOrDefault(ResourceEditorBuiltinConstants.IsBuiltinPackage);
                var bundle = package?.Bundles.FirstOrDefault(ResourceEditorBuiltinConstants.IsResourcesGroup);
                if (package == null || bundle == null)
                {
                    return;
                }

                var changed = Reconcile(package, bundle);
                if (changed)
                {
                    settings.SaveSettings();
                }

                WriteManifest(settings);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"[ResourceEditor] Resources 自动同步失败：{exception}");
            }
        }

        /// <summary>
        /// 使用磁盘上的 Resources 资源与已跟踪条目对账：新增缺失、清理陈旧。
        /// </summary>
        private static bool Reconcile(ResourceEditorPackage package, ResourceEditorBundle bundle)
        {
            var current = new UnityResourcesCollector().Collect(package, bundle);
            var currentPaths = new HashSet<string>(
                current.Select(preview => preview.AssetPath).Where(path => string.IsNullOrWhiteSpace(path) is false),
                StringComparer.Ordinal);

            var changed = false;

            // 清理已移出 Resources 目录或已删除的条目。
            var stale = bundle.Entries
                .Where(entry => entry != null && (string.IsNullOrWhiteSpace(entry.AssetPath) || currentPaths.Contains(entry.AssetPath) is false))
                .ToList();
            foreach (var entry in stale)
            {
                bundle.Entries.Remove(entry);
                changed = true;
            }

            // 新增磁盘上存在但尚未跟踪的资源。
            foreach (var path in currentPaths)
            {
                changed |= ResourceEditorEntryTable.AddEntry(bundle, path);
            }

            return changed;
        }

        private static void WriteManifest(ResourceEditorSettings settings)
        {
            s_Writing = true;
            try
            {
                ResourceEditorPlayModeManifestProvider.WriteLocalBaseManifest(settings);
            }
            finally
            {
                s_Writing = false;
            }
        }

        private static ResourceEditorBundle ResolveResourcesBundle(ResourceEditorSettings settings)
        {
            var package = settings?.Packages?.FirstOrDefault(ResourceEditorBuiltinConstants.IsBuiltinPackage);
            return package?.Bundles.FirstOrDefault(ResourceEditorBuiltinConstants.IsResourcesGroup);
        }
    }
}
