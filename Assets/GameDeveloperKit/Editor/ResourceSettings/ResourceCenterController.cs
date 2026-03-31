using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    internal sealed class ResourceCenterController
    {
        public ResourceProjectSettingsData Settings { get; private set; }

        public int SelectedPackageIndex { get; private set; } = -1;

        public bool ShowSettingsView { get; private set; }

        public bool HasUnsavedChanges { get; private set; }

        public void Initialize()
        {
            var configuration = GameFrameworkConfigurationBridge.ResolveSelectedOrFirstConfiguration();
            if (configuration != null)
            {
                Settings = GameFrameworkConfigurationBridge.BuildResourceSettingsData(configuration);
            }
            else
            {
                Settings = GameFrameworkConfigurationBridge.LoadResourceSettingsData();
            }

            if (NormalizeSettings())
            {
                SaveToDiskAndConfiguration();
            }

            HasUnsavedChanges = false;
        }

        public void SaveSettings(bool saveImmediately = true)
        {
            NormalizeSettings();

            if (saveImmediately)
            {
                SaveToDiskAndConfiguration();
                HasUnsavedChanges = false;
            }
            else
            {
                HasUnsavedChanges = true;
            }
        }

        public void SelectPackage(int index)
        {
            ShowSettingsView = false;
            SelectedPackageIndex = index;
        }

        public void ToggleSettingsView()
        {
            ShowSettingsView = !ShowSettingsView;
        }

        public void AddPackage()
        {
            Settings.Packages ??= new List<ResourcePackageDefinition>();
            Settings.Packages.Add(new ResourcePackageDefinition
            {
                PackageName = $"Package{Settings.Packages.Count + 1}",
                Role = ResourcePackageRole.Builtin,
                BuildStrategy = ResourcePackageBuildStrategy.OneFile,
                CollectionStrategy = ResourcePackageCollectionStrategy.ManualEntries
            });

            SelectedPackageIndex = Settings.Packages.Count - 1;
            ShowSettingsView = false;
            SaveSettings(false);
        }

        public bool RemoveSelectedPackage()
        {
            if (!TryGetSelectedPackage(out _))
            {
                return false;
            }

            Settings.Packages.RemoveAt(SelectedPackageIndex);
            SelectedPackageIndex = Settings.Packages.Count == 0
                ? -1
                : Mathf.Clamp(SelectedPackageIndex - 1, 0, Settings.Packages.Count - 1);
            SaveSettings(false);
            return true;
        }

        public bool TryGetSelectedPackage(out ResourcePackageDefinition package)
        {
            package = null;
            if (Settings?.Packages == null || SelectedPackageIndex < 0 || SelectedPackageIndex >= Settings.Packages.Count)
            {
                return false;
            }

            package = Settings.Packages[SelectedPackageIndex];
            if (package != null)
            {
                ResourceCollectionService.NormalizePackage(package);
            }

            return package != null;
        }

        public void EnsureSelectionIsValid()
        {
            if (Settings?.Packages == null)
            {
                SelectedPackageIndex = -1;
                return;
            }

            if (SelectedPackageIndex >= Settings.Packages.Count)
            {
                SelectedPackageIndex = -1;
            }
        }

        public int CollectSelectedPackage(bool saveImmediately = false)
        {
            if (!TryGetSelectedPackage(out var package))
            {
                return 0;
            }

            package.Entries = package.CollectionStrategy == ResourcePackageCollectionStrategy.ManualEntries
                ? new List<ResourceEntry>()
                : ResourceCollectionService.BuildCollectedEntries(package);
            SaveSettings(saveImmediately);
            return package.Entries.Count;
        }

        public string BuildResourceCheckSummary()
        {
            return ResourceValidationService.Validate(Settings).BuildSummary();
        }

        public string BuildSelectedPackageLogMessage()
        {
            return TryGetSelectedPackage(out var package)
                ? $"[Resource Center] Building package: {package.PackageName}..."
                : "[Resource Center] No package selected to build.";
        }

        public string BuildAllPackagesLogMessage()
        {
            return Settings?.Packages == null || Settings.Packages.Count == 0
                ? "[Resource Center] No packages to build."
                : $"[Resource Center] Building all {Settings.Packages.Count} packages...";
        }

        public (bool Success, string Message) ClearBuildCache()
        {
            var cachePath = Path.Combine("Library", "BuildCache");
            if (!Directory.Exists(cachePath))
            {
                return (true, "构建缓存目录不存在，无需清理。");
            }

            try
            {
                var di = new DirectoryInfo(cachePath);
                var sizeBefore = GetDirectorySize(di);
                di.Delete(true);
                Directory.CreateDirectory(cachePath);
                return (true, $"构建缓存已清理完成！\n释放空间：{FormatFileSize(sizeBefore)}");
            }
            catch (Exception ex)
            {
                return (false, $"清理缓存失败：{ex.Message}");
            }
        }

        private bool NormalizeSettings()
        {
            if (Settings?.Packages == null)
            {
                return false;
            }

            var changed = false;
            for (var i = 0; i < Settings.Packages.Count; i++)
            {
                changed |= ResourceCollectionService.NormalizePackage(Settings.Packages[i]);
            }

            return changed;
        }

        private void SaveToDiskAndConfiguration()
        {
            var configuration = GameFrameworkConfigurationBridge.ResolveSelectedOrFirstConfiguration();
            if (configuration != null)
            {
                GameFrameworkConfigurationBridge.ApplyResourceSettings(configuration, Settings);
                GameFrameworkConfigurationBridge.SaveConfiguration(configuration);
            }
            GameFrameworkConfigurationBridge.SaveResourceSettingsData(Settings);
        }

        private static long GetDirectorySize(DirectoryInfo dir)
        {
            long size = 0;
            try
            {
                var files = dir.GetFiles();
                for (var i = 0; i < files.Length; i++)
                {
                    size += files[i].Length;
                }

                var dirs = dir.GetDirectories();
                for (var i = 0; i < dirs.Length; i++)
                {
                    size += GetDirectorySize(dirs[i]);
                }
            }
            catch
            {
            }

            return size;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            if (bytes < 1024L * 1024 * 1024)
            {
                return $"{bytes / (1024.0 * 1024):F1} MB";
            }

            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
