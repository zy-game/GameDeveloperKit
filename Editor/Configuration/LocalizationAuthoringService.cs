using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Localization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.LocalizationEditor
{
    public interface ILocalizationAuthoringService
    {
        LocalizationAuthoringSnapshot Refresh();

        LocalizationMutationResult CreateCatalog(string folderPath, string catalogName, string initialLocale);

        LocalizationMutationResult BindCatalog(LocalizationCatalogAsset catalog);

        LocalizationMutationResult CreateKey(string key, string locale, string value);

        LocalizationMutationResult RenameKey(long keyId, string newKey);

        LocalizationMutationResult RemoveKey(long keyId);

        LocalizationMutationResult SetText(long keyId, string locale, string value);

        LocalizationMutationResult RemoveText(long keyId, string locale);

        LocalizationMutationResult AddLocale(LocalizationLocaleDraft draft);

        LocalizationMutationResult RemoveLocale(string locale);

        LocalizationMutationResult SetDefaultLocale(string locale);

        LocalizationMutationResult SetLocaleDescriptor(string locale, string resourceLocation, string fallbackLocale);

        LocalizationMutationResult ApplyImport(LocalizationImportAssetMutation mutation);

        IReadOnlyList<string> FindKeyUsages(string key);
    }

    public sealed class LocalizationAuthoringService : ILocalizationAuthoringService
    {
        private readonly Func<EditorGlobalConfig> m_ConfigProvider;
        private readonly ILocalizationImportBaselineStore m_ImportBaselines;
        private long m_Revision = 1;

        public LocalizationAuthoringService(Func<EditorGlobalConfig> configProvider)
            : this(configProvider, LocalizationImportBaselineStore.Shared)
        {
        }

        internal LocalizationAuthoringService(
            Func<EditorGlobalConfig> configProvider,
            ILocalizationImportBaselineStore importBaselines)
        {
            m_ConfigProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            m_ImportBaselines = importBaselines ?? throw new ArgumentNullException(nameof(importBaselines));
        }

        public static LocalizationAuthoringService Shared { get; } = new LocalizationAuthoringService(
            EditorGlobalConfig.LoadOrCreate);

        public LocalizationAuthoringSnapshot Refresh()
        {
            var config = m_ConfigProvider();
            var diagnostics = new List<LocalizationAuthoringDiagnostic>();
            var guid = config.Localization.CatalogAssetGuid?.Trim() ?? string.Empty;
            if (guid.Length == 0)
            {
                diagnostics.Add(Error("catalog_not_bound", "尚未绑定全局本地化 Catalog。"));
                return new LocalizationAuthoringSnapshot(
                    m_Revision,
                    null,
                    string.Empty,
                    config.Localization.PreviewLocale,
                    null,
                    diagnostics);
            }

            var catalogPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(catalogPath))
            {
                diagnostics.Add(Error("catalog_guid_invalid", $"本地化 Catalog GUID 已失效：{guid}"));
                return new LocalizationAuthoringSnapshot(
                    m_Revision,
                    null,
                    string.Empty,
                    config.Localization.PreviewLocale,
                    null,
                    diagnostics);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalogAsset>(catalogPath);
            if (catalog == null)
            {
                diagnostics.Add(Error("catalog_type_invalid", $"绑定资产不是本地化 Catalog：{catalogPath}"));
                return new LocalizationAuthoringSnapshot(
                    m_Revision,
                    null,
                    catalogPath,
                    config.Localization.PreviewLocale,
                    null,
                    diagnostics);
            }

            var locales = new List<LocalizationAuthoringLocale>();
            foreach (var descriptor in catalog.Locales.Where(item => item != null))
            {
                var localeAsset = ResolveLocaleAsset(catalogPath, descriptor, out var assetPath);
                if (localeAsset == null)
                {
                    diagnostics.Add(Error(
                        "locale_asset_missing",
                        $"找不到语言 {descriptor.Locale} 的文本资产，资源位置：{descriptor.ResourceLocation}",
                        descriptor.Locale));
                    continue;
                }

                locales.Add(new LocalizationAuthoringLocale(descriptor, localeAsset, assetPath));
            }

            AppendValidationDiagnostics(
                LocalizationAssetValidator.Validate(catalog, locales.Select(locale => locale.Asset)),
                diagnostics);
            var previewLocale = config.Localization.PreviewLocale?.Trim() ?? string.Empty;
            if (previewLocale.Length == 0 && catalog.TryGetLocale(catalog.DefaultLocale, out _))
            {
                previewLocale = catalog.DefaultLocale;
            }

            return new LocalizationAuthoringSnapshot(
                m_Revision,
                catalog,
                catalogPath,
                previewLocale,
                locales,
                diagnostics);
        }

        public LocalizationMutationResult CreateCatalog(string folderPath, string catalogName, string initialLocale)
        {
            folderPath = NormalizeAssetPath(folderPath);
            catalogName = SanitizeFileName(catalogName);
            initialLocale = NormalizeLocale(initialLocale);
            if (IsAssetFolder(folderPath) is false)
            {
                return LocalizationMutationResult.Failure("本地化资产只能创建在项目 Assets 目录中。");
            }

            if (catalogName.Length == 0 || initialLocale.Length == 0)
            {
                return LocalizationMutationResult.Failure("Catalog 名称和初始语言不能为空。");
            }

            var catalogPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{catalogName}.asset");
            var localePath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{catalogName}.{initialLocale}.asset");
            var catalog = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            var localeAsset = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            localeAsset.Replace(initialLocale, Array.Empty<LocalizationValueEntry>(), 1);
            catalog.Replace(
                Guid.NewGuid().ToString("N"),
                initialLocale,
                Array.Empty<LocalizationKeyEntry>(),
                new[] { new LocalizationLocaleDescriptor(initialLocale, localePath) });

            var validation = LocalizationAssetValidator.Validate(catalog, new[] { localeAsset });
            if (validation.IsValid is false)
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(localeAsset);
                return LocalizationMutationResult.Failure(FirstValidationMessage(validation));
            }

            var config = m_ConfigProvider();
            var previousGuid = config.Localization.CatalogAssetGuid;
            var previousPreviewLocale = config.Localization.PreviewLocale;
            try
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("创建本地化 Catalog");
                AssetDatabase.CreateAsset(catalog, catalogPath);
                AssetDatabase.CreateAsset(localeAsset, localePath);
                Undo.RegisterCreatedObjectUndo(catalog, "创建本地化 Catalog");
                Undo.RegisterCreatedObjectUndo(localeAsset, "创建本地化语言资产");
                AssetDatabase.SaveAssets();

                config.Localization.CatalogAssetGuid = AssetDatabase.AssetPathToGUID(catalogPath);
                config.Localization.PreviewLocale = initialLocale;
                config.Save();
                m_Revision++;
                return LocalizationMutationResult.Success(Refresh(), "已创建并绑定本地化 Catalog。");
            }
            catch (Exception exception)
            {
                config.Localization.CatalogAssetGuid = previousGuid;
                config.Localization.PreviewLocale = previousPreviewLocale;
                if (AssetDatabase.LoadAssetAtPath<Object>(localePath) != null)
                {
                    AssetDatabase.DeleteAsset(localePath);
                }

                if (AssetDatabase.LoadAssetAtPath<Object>(catalogPath) != null)
                {
                    AssetDatabase.DeleteAsset(catalogPath);
                }

                return LocalizationMutationResult.Failure($"创建本地化资产失败：{exception.Message}");
            }
        }

        public LocalizationMutationResult BindCatalog(LocalizationCatalogAsset catalog)
        {
            if (catalog == null)
            {
                return LocalizationMutationResult.Failure("请选择有效的本地化 Catalog。");
            }

            var path = AssetDatabase.GetAssetPath(catalog);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid))
            {
                return LocalizationMutationResult.Failure("所选 Catalog 不是项目资产。");
            }

            var validation = LocalizationAssetValidator.ValidateCatalog(catalog);
            if (validation.IsValid is false)
            {
                return LocalizationMutationResult.Failure(FirstValidationMessage(validation));
            }

            var config = m_ConfigProvider();
            var previousGuid = config.Localization.CatalogAssetGuid;
            var previousPreviewLocale = config.Localization.PreviewLocale;
            config.Localization.CatalogAssetGuid = guid;
            if (catalog.TryGetLocale(config.Localization.PreviewLocale, out _) is false)
            {
                config.Localization.PreviewLocale = catalog.DefaultLocale;
            }

            try
            {
                config.Save();
            }
            catch (Exception exception)
            {
                config.Localization.CatalogAssetGuid = previousGuid;
                config.Localization.PreviewLocale = previousPreviewLocale;
                return LocalizationMutationResult.Failure($"绑定本地化 Catalog 失败：{exception.Message}");
            }
            m_Revision++;
            return LocalizationMutationResult.Success(Refresh(), "已绑定本地化 Catalog。");
        }

        public LocalizationMutationResult CreateKey(string key, string locale, string value)
        {
            var snapshot = RequireValidSnapshot(out var failure);
            if (snapshot == null)
            {
                return failure;
            }

            key = key?.Trim() ?? string.Empty;
            locale = NormalizeLocale(locale);
            if (key.Length == 0)
            {
                return LocalizationMutationResult.Failure("本地化 Key 不能为空。", snapshot);
            }

            if (snapshot.Catalog.TryGetKey(key, out _))
            {
                return LocalizationMutationResult.Failure($"本地化 Key 已存在：{key}", snapshot);
            }

            if (locale.Length > 0 && snapshot.TryGetLocale(locale, out _) is false)
            {
                return LocalizationMutationResult.Failure($"预览语言未注册：{locale}", snapshot);
            }

            var keyId = CreateKeyId(snapshot.Catalog.Keys);
            var keys = snapshot.Catalog.Keys
                .Where(entry => entry != null)
                .Select(entry => new LocalizationKeyEntry(entry.Id, entry.Key))
                .Append(new LocalizationKeyEntry(keyId, key))
                .ToArray();
            var overrides = new Dictionary<string, IReadOnlyList<LocalizationValueEntry>>(StringComparer.OrdinalIgnoreCase);
            if (locale.Length > 0)
            {
                var entries = CopyValues(snapshot.Locales[locale].Asset.Entries);
                entries.Add(new LocalizationValueEntry(keyId, value ?? string.Empty));
                overrides.Add(locale, entries);
            }

            return Commit(snapshot, keys, null, null, overrides, "新增本地化 Key", keyId);
        }

        public LocalizationMutationResult RenameKey(long keyId, string newKey)
        {
            var snapshot = RequireValidSnapshot(out var failure);
            if (snapshot == null)
            {
                return failure;
            }

            newKey = newKey?.Trim() ?? string.Empty;
            if (newKey.Length == 0)
            {
                return LocalizationMutationResult.Failure("本地化 Key 不能为空。", snapshot);
            }

            if (snapshot.Catalog.TryGetKey(keyId, out _) is false)
            {
                return LocalizationMutationResult.Failure($"找不到 KeyId：{keyId}", snapshot);
            }

            if (snapshot.Catalog.Keys.Any(entry => entry != null && entry.Id != keyId &&
                    string.Equals(entry.Key, newKey, StringComparison.Ordinal)))
            {
                return LocalizationMutationResult.Failure($"本地化 Key 已存在：{newKey}", snapshot);
            }

            var keys = snapshot.Catalog.Keys
                .Where(entry => entry != null)
                .Select(entry => new LocalizationKeyEntry(entry.Id, entry.Id == keyId ? newKey : entry.Key))
                .ToArray();
            return Commit(snapshot, keys, null, null, null, "重命名本地化 Key", keyId);
        }

        public LocalizationMutationResult RemoveKey(long keyId)
        {
            var snapshot = RequireValidSnapshot(out var failure);
            if (snapshot == null)
            {
                return failure;
            }

            if (snapshot.Catalog.TryGetKey(keyId, out _) is false)
            {
                return LocalizationMutationResult.Failure($"找不到 KeyId：{keyId}", snapshot);
            }

            var keys = snapshot.Catalog.Keys
                .Where(entry => entry != null && entry.Id != keyId)
                .Select(entry => new LocalizationKeyEntry(entry.Id, entry.Key))
                .ToArray();
            var overrides = snapshot.Locales.Values.ToDictionary(
                locale => locale.Descriptor.Locale,
                locale => (IReadOnlyList<LocalizationValueEntry>)CopyValues(locale.Asset.Entries)
                    .Where(entry => entry.KeyId != keyId)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
            return Commit(snapshot, keys, null, null, overrides, "删除本地化 Key", keyId);
        }

        public LocalizationMutationResult SetText(long keyId, string locale, string value)
        {
            var snapshot = RequireValidSnapshot(out var failure);
            if (snapshot == null)
            {
                return failure;
            }

            locale = NormalizeLocale(locale);
            if (snapshot.Catalog.TryGetKey(keyId, out _) is false)
            {
                return LocalizationMutationResult.Failure($"找不到 KeyId：{keyId}", snapshot);
            }

            if (snapshot.TryGetLocale(locale, out var authoringLocale) is false)
            {
                return LocalizationMutationResult.Failure($"语言未注册：{locale}", snapshot);
            }

            var entries = CopyValues(authoringLocale.Asset.Entries);
            var index = entries.FindIndex(entry => entry.KeyId == keyId);
            var replacement = new LocalizationValueEntry(keyId, value ?? string.Empty);
            if (index >= 0)
            {
                entries[index] = replacement;
            }
            else
            {
                entries.Add(replacement);
            }

            return Commit(
                snapshot,
                null,
                null,
                null,
                new Dictionary<string, IReadOnlyList<LocalizationValueEntry>>(StringComparer.OrdinalIgnoreCase)
                {
                    [locale] = entries
                },
                "设置本地化文本",
                keyId);
        }

        public LocalizationMutationResult RemoveText(long keyId, string locale)
        {
            var snapshot = RequireValidSnapshot(out var failure);
            if (snapshot == null)
            {
                return failure;
            }

            locale = NormalizeLocale(locale);
            if (snapshot.Catalog.TryGetKey(keyId, out _) is false)
            {
                return LocalizationMutationResult.Failure($"找不到 KeyId：{keyId}", snapshot);
            }

            if (snapshot.TryGetLocale(locale, out var authoringLocale) is false)
            {
                return LocalizationMutationResult.Failure($"语言未注册：{locale}", snapshot);
            }

            var entries = CopyValues(authoringLocale.Asset.Entries)
                .Where(entry => entry.KeyId != keyId)
                .ToArray();
            return Commit(
                snapshot,
                null,
                null,
                null,
                new Dictionary<string, IReadOnlyList<LocalizationValueEntry>>(StringComparer.OrdinalIgnoreCase)
                {
                    [locale] = entries
                },
                "删除本地化文本",
                keyId);
        }

        public LocalizationMutationResult AddLocale(LocalizationLocaleDraft draft)
        {
            if (draft == null)
            {
                return LocalizationMutationResult.Failure("语言配置不能为空。");
            }

            var snapshot = RequireValidSnapshot(out var failure);
            if (snapshot == null)
            {
                return failure;
            }

            var locale = NormalizeLocale(draft.Locale);
            var fallback = NormalizeLocale(draft.FallbackLocale);
            var assetPath = NormalizeAssetPath(draft.AssetPath);
            var resourceLocation = NormalizeAssetPath(draft.ResourceLocation);
            if (locale.Length == 0 || IsAssetPath(assetPath) is false || resourceLocation.Length == 0)
            {
                return LocalizationMutationResult.Failure("语言、资产路径和资源位置不能为空，资产必须位于 Assets 目录。", snapshot);
            }

            if (snapshot.Catalog.TryGetLocale(locale, out _))
            {
                return LocalizationMutationResult.Failure($"语言已存在：{locale}", snapshot);
            }

            if (fallback.Length > 0 && snapshot.Catalog.TryGetLocale(fallback, out _) is false)
            {
                return LocalizationMutationResult.Failure($"回退语言未注册：{fallback}", snapshot);
            }

            var requestedAssetPath = assetPath;
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            if (string.Equals(resourceLocation, requestedAssetPath, StringComparison.Ordinal))
            {
                resourceLocation = assetPath;
            }
            var descriptors = CopyDescriptors(snapshot.Catalog.Locales);
            descriptors.Add(new LocalizationLocaleDescriptor(locale, resourceLocation, fallback));
            var localeAsset = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            localeAsset.Replace(locale, Array.Empty<LocalizationValueEntry>(), 1);
            var tempCatalog = CreateCatalogClone(snapshot.Catalog, null, descriptors, null);
            var tempLocales = snapshot.Locales.Values.Select(item => CreateLocaleClone(item.Asset, null)).ToList();
            tempLocales.Add(localeAsset);
            var validation = LocalizationAssetValidator.Validate(tempCatalog, tempLocales);
            Object.DestroyImmediate(tempCatalog);
            foreach (var temp in tempLocales.Where(item => item != localeAsset))
            {
                Object.DestroyImmediate(temp);
            }

            if (validation.IsValid is false)
            {
                Object.DestroyImmediate(localeAsset);
                return LocalizationMutationResult.Failure(FirstValidationMessage(validation), snapshot);
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            try
            {
                Undo.SetCurrentGroupName("添加本地化语言");
                AssetDatabase.CreateAsset(localeAsset, assetPath);
                Undo.RegisterCreatedObjectUndo(localeAsset, "添加本地化语言");
                Undo.RecordObject(snapshot.Catalog, "添加本地化语言");
                snapshot.Catalog.Replace(
                    snapshot.Catalog.CatalogId,
                    snapshot.Catalog.DefaultLocale,
                    snapshot.Catalog.Keys,
                    descriptors);
                EditorUtility.SetDirty(snapshot.Catalog);
                AssetDatabase.SaveAssets();
                m_Revision++;
                Undo.CollapseUndoOperations(undoGroup);
                return LocalizationMutationResult.Success(Refresh(), $"已添加语言 {locale}。");
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                return LocalizationMutationResult.Failure($"添加语言失败：{exception.Message}", snapshot);
            }
        }

        public LocalizationMutationResult RemoveLocale(string locale)
        {
            var snapshot = RequireValidSnapshot(out var failure);
            if (snapshot == null)
            {
                return failure;
            }

            locale = NormalizeLocale(locale);
            if (string.Equals(locale, snapshot.Catalog.DefaultLocale, StringComparison.OrdinalIgnoreCase))
            {
                return LocalizationMutationResult.Failure("默认语言不能移除。", snapshot);
            }

            if (snapshot.Catalog.Locales.Any(item => item != null &&
                    string.Equals(item.FallbackLocale, locale, StringComparison.OrdinalIgnoreCase)))
            {
                return LocalizationMutationResult.Failure($"语言 {locale} 正被其他语言用作回退，不能移除。", snapshot);
            }

            if (snapshot.Catalog.TryGetLocale(locale, out _) is false)
            {
                return LocalizationMutationResult.Failure($"语言未注册：{locale}", snapshot);
            }

            var descriptors = CopyDescriptors(snapshot.Catalog.Locales)
                .Where(item => string.Equals(item.Locale, locale, StringComparison.OrdinalIgnoreCase) is false)
                .ToArray();
            var result = Commit(snapshot, null, descriptors, null, null, "移除本地化语言");
            if (result.Succeeded && string.Equals(
                    m_ConfigProvider().Localization.PreviewLocale,
                    locale,
                    StringComparison.OrdinalIgnoreCase))
            {
                var config = m_ConfigProvider();
                config.Localization.PreviewLocale = snapshot.Catalog.DefaultLocale;
                config.Save();
                return LocalizationMutationResult.Success(Refresh(), result.Message, result.KeyId);
            }

            return result;
        }

        public LocalizationMutationResult SetDefaultLocale(string locale)
        {
            var snapshot = RequireValidSnapshot(out var failure);
            if (snapshot == null)
            {
                return failure;
            }

            locale = NormalizeLocale(locale);
            if (snapshot.Catalog.TryGetLocale(locale, out _) is false)
            {
                return LocalizationMutationResult.Failure($"语言未注册：{locale}", snapshot);
            }

            return Commit(snapshot, null, null, locale, null, "设置默认语言");
        }

        public LocalizationMutationResult SetLocaleDescriptor(
            string locale,
            string resourceLocation,
            string fallbackLocale)
        {
            var snapshot = RequireValidSnapshot(out var failure);
            if (snapshot == null)
            {
                return failure;
            }

            locale = NormalizeLocale(locale);
            resourceLocation = NormalizeAssetPath(resourceLocation);
            fallbackLocale = NormalizeLocale(fallbackLocale);
            if (snapshot.Catalog.TryGetLocale(locale, out _) is false)
            {
                return LocalizationMutationResult.Failure($"语言未注册：{locale}", snapshot);
            }

            if (resourceLocation.Length == 0)
            {
                return LocalizationMutationResult.Failure("资源位置不能为空。", snapshot);
            }

            var descriptors = snapshot.Catalog.Locales
                .Where(item => item != null)
                .Select(item => string.Equals(item.Locale, locale, StringComparison.OrdinalIgnoreCase)
                    ? new LocalizationLocaleDescriptor(item.Locale, resourceLocation, fallbackLocale)
                    : new LocalizationLocaleDescriptor(item.Locale, item.ResourceLocation, item.FallbackLocale))
                .ToArray();
            return Commit(snapshot, null, descriptors, null, null, "更新语言配置");
        }

        public LocalizationMutationResult ApplyImport(LocalizationImportAssetMutation mutation)
        {
            if (mutation == null)
            {
                throw new ArgumentNullException(nameof(mutation));
            }

            var snapshot = RequireValidSnapshot(out var failure);
            if (snapshot == null)
            {
                return failure;
            }

            if (snapshot.Revision != mutation.ExpectedAuthoringRevision ||
                string.Equals(snapshot.Catalog.CatalogId, mutation.ExpectedCatalogId, StringComparison.Ordinal) is false)
            {
                return LocalizationMutationResult.Failure("本地化资产已变化，请重新生成导入预览。", snapshot);
            }

            var newLocaleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newLocales = new List<LocalizationLocaleDraft>();
            foreach (var draft in mutation.NewLocales)
            {
                var locale = NormalizeLocale(draft.Locale);
                var requestedAssetPath = NormalizeAssetPath(draft.AssetPath);
                var resourceLocation = NormalizeAssetPath(draft.ResourceLocation);
                var fallbackLocale = NormalizeLocale(draft.FallbackLocale);
                if (locale.Length == 0 || IsAssetPath(requestedAssetPath) is false || resourceLocation.Length == 0)
                {
                    return LocalizationMutationResult.Failure(
                        "待创建语言、资产路径和资源位置不能为空，资产必须位于 Assets 目录。",
                        snapshot);
                }

                if (snapshot.TryGetLocale(locale, out _) || newLocaleNames.Add(locale) is false)
                {
                    return LocalizationMutationResult.Failure($"待创建语言重复或已注册：{locale}", snapshot);
                }

                var assetPath = AssetDatabase.GenerateUniqueAssetPath(requestedAssetPath);
                if (string.Equals(resourceLocation, requestedAssetPath, StringComparison.Ordinal))
                {
                    resourceLocation = assetPath;
                }

                newLocales.Add(new LocalizationLocaleDraft(
                    locale,
                    assetPath,
                    resourceLocation,
                    fallbackLocale));
            }

            var availableLocales = new HashSet<string>(
                snapshot.Locales.Keys.Concat(newLocaleNames),
                StringComparer.OrdinalIgnoreCase);
            foreach (var draft in newLocales)
            {
                if (draft.FallbackLocale.Length > 0 && availableLocales.Contains(draft.FallbackLocale) is false)
                {
                    return LocalizationMutationResult.Failure(
                        $"待创建语言 {draft.Locale} 的回退语言未注册：{draft.FallbackLocale}",
                        snapshot);
                }

                if (mutation.LocaleValues.ContainsKey(draft.Locale) is false)
                {
                    return LocalizationMutationResult.Failure($"待创建语言缺少导入文本：{draft.Locale}", snapshot);
                }
            }

            foreach (var locale in mutation.LocaleValues.Keys)
            {
                if (snapshot.TryGetLocale(locale, out _) is false && newLocaleNames.Contains(locale) is false)
                {
                    return LocalizationMutationResult.Failure($"导入目标语言未注册：{locale}", snapshot);
                }
            }

            LocalizationImportBaselineFileBackup baselineBackup;
            try
            {
                baselineBackup = m_ImportBaselines.Capture(mutation.BaselinePath);
            }
            catch (Exception exception)
            {
                return LocalizationMutationResult.Failure($"准备导入 Baseline 失败：{exception.Message}", snapshot);
            }

            var descriptors = CopyDescriptors(snapshot.Catalog.Locales);
            descriptors.AddRange(newLocales.Select(draft => new LocalizationLocaleDescriptor(
                draft.Locale,
                draft.ResourceLocation,
                draft.FallbackLocale)));
            var tempCatalog = CreateCatalogClone(
                snapshot.Catalog,
                mutation.Keys,
                descriptors,
                snapshot.Catalog.DefaultLocale);
            var tempLocales = snapshot.Locales.Values.Select(locale => CreateLocaleClone(
                locale.Asset,
                mutation.LocaleValues.TryGetValue(locale.Descriptor.Locale, out var values) ? values : null)).ToList();
            foreach (var draft in newLocales)
            {
                var tempLocale = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
                tempLocale.Replace(draft.Locale, mutation.LocaleValues[draft.Locale], 1);
                tempLocales.Add(tempLocale);
            }

            var validation = LocalizationAssetValidator.Validate(tempCatalog, tempLocales);
            Object.DestroyImmediate(tempCatalog);
            foreach (var tempLocale in tempLocales)
            {
                Object.DestroyImmediate(tempLocale);
            }

            if (validation.IsValid is false)
            {
                return LocalizationMutationResult.Failure(FirstValidationMessage(validation), snapshot);
            }

            var affectedLocales = snapshot.Locales.Values
                .Where(locale => mutation.LocaleValues.ContainsKey(locale.Descriptor.Locale))
                .ToArray();
            var originalKeys = snapshot.Catalog.Keys
                .Where(entry => entry != null)
                .Select(entry => new LocalizationKeyEntry(entry.Id, entry.Key))
                .ToArray();
            var originalDescriptors = CopyDescriptors(snapshot.Catalog.Locales).ToArray();
            var originalLocaleValues = affectedLocales.ToDictionary(
                locale => locale.Descriptor.Locale,
                locale => (IReadOnlyList<LocalizationValueEntry>)CopyValues(locale.Asset.Entries),
                StringComparer.OrdinalIgnoreCase);
            var originalLocaleRevisions = affectedLocales.ToDictionary(
                locale => locale.Descriptor.Locale,
                locale => locale.Asset.Revision,
                StringComparer.OrdinalIgnoreCase);
            var affectedObjects = new List<Object> { snapshot.Catalog };
            affectedObjects.AddRange(affectedLocales.Select(locale => (Object)locale.Asset));
            var createdLocalePaths = new List<string>();
            var createdLocaleObjects = new List<LocalizationLocaleAsset>();
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            const string undoName = "导入本地化配置表";
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObjects(affectedObjects.ToArray(), undoName);
            try
            {
                foreach (var draft in newLocales)
                {
                    var localeAsset = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
                    localeAsset.Replace(draft.Locale, mutation.LocaleValues[draft.Locale], 1);
                    createdLocaleObjects.Add(localeAsset);
                    AssetDatabase.CreateAsset(localeAsset, draft.AssetPath);
                    createdLocalePaths.Add(draft.AssetPath);
                    Undo.RegisterCreatedObjectUndo(localeAsset, undoName);
                }

                snapshot.Catalog.Replace(
                    snapshot.Catalog.CatalogId,
                    snapshot.Catalog.DefaultLocale,
                    mutation.Keys,
                    descriptors);
                EditorUtility.SetDirty(snapshot.Catalog);
                foreach (var locale in affectedLocales)
                {
                    locale.Asset.Replace(
                        locale.Descriptor.Locale,
                        mutation.LocaleValues[locale.Descriptor.Locale],
                        locale.Asset.Revision + 1);
                    EditorUtility.SetDirty(locale.Asset);
                }

                AssetDatabase.SaveAssets();
                m_ImportBaselines.Write(mutation.BaselinePath, mutation.BaselineJson);
                Undo.CollapseUndoOperations(undoGroup);
                m_Revision++;
                return LocalizationMutationResult.Success(Refresh(), "本地化配置表导入完成。");
            }
            catch (Exception exception)
            {
                Exception rollbackError = null;
                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    snapshot.Catalog.Replace(
                        snapshot.Catalog.CatalogId,
                        snapshot.Catalog.DefaultLocale,
                        originalKeys,
                        originalDescriptors);
                    EditorUtility.SetDirty(snapshot.Catalog);
                    foreach (var locale in affectedLocales)
                    {
                        locale.Asset.Replace(
                            locale.Descriptor.Locale,
                            originalLocaleValues[locale.Descriptor.Locale],
                            originalLocaleRevisions[locale.Descriptor.Locale]);
                        EditorUtility.SetDirty(locale.Asset);
                    }

                    foreach (var createdPath in createdLocalePaths)
                    {
                        if (AssetDatabase.LoadAssetAtPath<LocalizationLocaleAsset>(createdPath) != null)
                        {
                            AssetDatabase.DeleteAsset(createdPath);
                        }
                    }

                    foreach (var localeAsset in createdLocaleObjects.Where(localeAsset =>
                                 localeAsset != null && AssetDatabase.Contains(localeAsset) is false))
                    {
                        Object.DestroyImmediate(localeAsset);
                    }

                    AssetDatabase.SaveAssets();
                    m_ImportBaselines.Restore(mutation.BaselinePath, baselineBackup);
                }
                catch (Exception restoreException)
                {
                    rollbackError = restoreException;
                }

                var rollbackMessage = rollbackError == null
                    ? string.Empty
                    : $"；回滚失败：{rollbackError.Message}";
                return LocalizationMutationResult.Failure(
                    $"提交本地化配置表导入失败：{exception.Message}{rollbackMessage}",
                    Refresh());
            }
        }

        public IReadOnlyList<string> FindKeyUsages(string key)
        {
            return LocalizationKeyUsageScanner.Find(key);
        }

        private LocalizationMutationResult Commit(
            LocalizationAuthoringSnapshot snapshot,
            IReadOnlyList<LocalizationKeyEntry> keys,
            IReadOnlyList<LocalizationLocaleDescriptor> descriptors,
            string defaultLocale,
            IReadOnlyDictionary<string, IReadOnlyList<LocalizationValueEntry>> localeOverrides,
            string undoName,
            long keyId = 0)
        {
            var catalogChanged = keys != null || descriptors != null || defaultLocale != null;
            keys ??= snapshot.Catalog.Keys.ToArray();
            descriptors ??= snapshot.Catalog.Locales.ToArray();
            defaultLocale ??= snapshot.Catalog.DefaultLocale;
            localeOverrides ??= new Dictionary<string, IReadOnlyList<LocalizationValueEntry>>(StringComparer.OrdinalIgnoreCase);

            var tempCatalog = CreateCatalogClone(snapshot.Catalog, keys, descriptors, defaultLocale);
            var tempLocales = snapshot.Locales.Values
                .Where(locale => descriptors.Any(descriptor => descriptor != null &&
                    string.Equals(descriptor.Locale, locale.Descriptor.Locale, StringComparison.OrdinalIgnoreCase)))
                .Select(locale => CreateLocaleClone(
                    locale.Asset,
                    localeOverrides.TryGetValue(locale.Descriptor.Locale, out var values) ? values : null))
                .ToArray();
            var validation = LocalizationAssetValidator.Validate(tempCatalog, tempLocales);
            Object.DestroyImmediate(tempCatalog);
            foreach (var tempLocale in tempLocales)
            {
                Object.DestroyImmediate(tempLocale);
            }

            if (validation.IsValid is false)
            {
                return LocalizationMutationResult.Failure(FirstValidationMessage(validation), snapshot);
            }

            var affectedLocales = snapshot.Locales.Values
                .Where(locale => localeOverrides.ContainsKey(locale.Descriptor.Locale))
                .ToArray();
            var affectedObjects = new List<Object>();
            if (catalogChanged)
            {
                affectedObjects.Add(snapshot.Catalog);
            }

            affectedObjects.AddRange(affectedLocales.Select(locale => (Object)locale.Asset));
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObjects(affectedObjects.ToArray(), undoName);
            try
            {
                if (catalogChanged)
                {
                    snapshot.Catalog.Replace(snapshot.Catalog.CatalogId, defaultLocale, keys, descriptors);
                    EditorUtility.SetDirty(snapshot.Catalog);
                }
                foreach (var locale in affectedLocales)
                {
                    locale.Asset.Replace(
                        locale.Descriptor.Locale,
                        localeOverrides[locale.Descriptor.Locale],
                        locale.Asset.Revision + 1);
                    EditorUtility.SetDirty(locale.Asset);
                }

                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                m_Revision++;
                return LocalizationMutationResult.Success(Refresh(), null, keyId);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return LocalizationMutationResult.Failure($"保存本地化资产失败：{exception.Message}", Refresh());
            }
        }

        private LocalizationAuthoringSnapshot RequireValidSnapshot(out LocalizationMutationResult failure)
        {
            var snapshot = Refresh();
            if (snapshot.IsValid)
            {
                failure = null;
                return snapshot;
            }

            var message = snapshot.Diagnostics.FirstOrDefault(item =>
                item.Severity == LocalizationAuthoringDiagnosticSeverity.Error)?.Message ?? "本地化资产不可用。";
            failure = LocalizationMutationResult.Failure(message, snapshot);
            return null;
        }

        private static LocalizationCatalogAsset CreateCatalogClone(
            LocalizationCatalogAsset source,
            IEnumerable<LocalizationKeyEntry> keys,
            IEnumerable<LocalizationLocaleDescriptor> descriptors,
            string defaultLocale)
        {
            var clone = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            clone.Replace(
                source.CatalogId,
                defaultLocale ?? source.DefaultLocale,
                keys ?? source.Keys,
                descriptors ?? source.Locales,
                source.SchemaVersion);
            return clone;
        }

        private static LocalizationLocaleAsset CreateLocaleClone(
            LocalizationLocaleAsset source,
            IEnumerable<LocalizationValueEntry> entries)
        {
            var clone = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            clone.Replace(source.Locale, entries ?? source.Entries, source.Revision, source.SchemaVersion);
            return clone;
        }

        private static LocalizationLocaleAsset ResolveLocaleAsset(
            string catalogPath,
            LocalizationLocaleDescriptor descriptor,
            out string assetPath)
        {
            assetPath = NormalizeAssetPath(descriptor.ResourceLocation);
            var direct = AssetDatabase.LoadAssetAtPath<LocalizationLocaleAsset>(assetPath);
            if (direct != null && string.Equals(direct.Locale, descriptor.Locale, StringComparison.OrdinalIgnoreCase))
            {
                return direct;
            }

            var catalogFolder = Path.GetDirectoryName(catalogPath)?.Replace('\\', '/');
            var searchFolders = IsAssetFolder(catalogFolder) ? new[] { catalogFolder } : new[] { "Assets" };
            var matches = AssetDatabase.FindAssets("t:LocalizationLocaleAsset", searchFolders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => new { Path = path, Asset = AssetDatabase.LoadAssetAtPath<LocalizationLocaleAsset>(path) })
                .Where(candidate => candidate.Asset != null &&
                    string.Equals(candidate.Asset.Locale, descriptor.Locale, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            if (matches.Length != 1)
            {
                assetPath = string.Empty;
                return null;
            }

            assetPath = matches[0].Path;
            return matches[0].Asset;
        }

        private static List<LocalizationValueEntry> CopyValues(IEnumerable<LocalizationValueEntry> entries)
        {
            return entries.Where(entry => entry != null)
                .Select(entry => new LocalizationValueEntry(entry.KeyId, entry.Value))
                .ToList();
        }

        private static List<LocalizationLocaleDescriptor> CopyDescriptors(
            IEnumerable<LocalizationLocaleDescriptor> descriptors)
        {
            return descriptors.Where(descriptor => descriptor != null)
                .Select(descriptor => new LocalizationLocaleDescriptor(
                    descriptor.Locale,
                    descriptor.ResourceLocation,
                    descriptor.FallbackLocale))
                .ToList();
        }

        private static long CreateKeyId(IEnumerable<LocalizationKeyEntry> keys)
        {
            var existing = new HashSet<long>(keys.Where(key => key != null).Select(key => key.Id));
            long candidate;
            do
            {
                candidate = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & long.MaxValue;
            } while (candidate == 0 || existing.Contains(candidate));

            return candidate;
        }

        internal static string NormalizeLocale(string locale)
        {
            var parts = (locale ?? string.Empty).Trim().Replace('_', '-').Split('-');
            if (parts.Length == 0 || parts[0].Length == 0)
            {
                return string.Empty;
            }

            parts[0] = parts[0].ToLowerInvariant();
            for (var i = 1; i < parts.Length; i++)
            {
                parts[i] = parts[i].Length == 2
                    ? parts[i].ToUpperInvariant()
                    : parts[i];
            }

            return string.Join("-", parts);
        }

        internal static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static bool IsAssetFolder(string path)
        {
            return string.Equals(path, "Assets", StringComparison.Ordinal) || IsAssetPath(path);
        }

        private static bool IsAssetPath(string path)
        {
            return path?.StartsWith("Assets/", StringComparison.Ordinal) == true;
        }

        private static string SanitizeFileName(string value)
        {
            var result = (value ?? string.Empty).Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }

            return result;
        }

        private static string FirstValidationMessage(LocalizationAssetValidationResult validation)
        {
            var diagnostic = validation.Diagnostics.FirstOrDefault(item =>
                item.Severity == LocalizationAssetDiagnosticSeverity.Error);
            return diagnostic == null ? "本地化资产校验失败。" : ToChineseMessage(diagnostic);
        }

        private static void AppendValidationDiagnostics(
            LocalizationAssetValidationResult validation,
            ICollection<LocalizationAuthoringDiagnostic> target)
        {
            foreach (var diagnostic in validation.Diagnostics)
            {
                target.Add(new LocalizationAuthoringDiagnostic(
                    diagnostic.Severity == LocalizationAssetDiagnosticSeverity.Error
                        ? LocalizationAuthoringDiagnosticSeverity.Error
                        : LocalizationAuthoringDiagnosticSeverity.Warning,
                    diagnostic.Code,
                    ToChineseMessage(diagnostic),
                    diagnostic.Locale,
                    diagnostic.KeyId));
            }
        }

        private static LocalizationAuthoringDiagnostic Error(
            string code,
            string message,
            string locale = null,
            long keyId = 0)
        {
            return new LocalizationAuthoringDiagnostic(
                LocalizationAuthoringDiagnosticSeverity.Error,
                code,
                message,
                locale,
                keyId);
        }

        private static string ToChineseMessage(LocalizationAssetDiagnostic diagnostic)
        {
            return diagnostic.Code switch
            {
                "catalog_null" => "本地化 Catalog 为空。",
                "catalog_schema_unsupported" => "Catalog 结构版本不受支持。",
                "catalog_id_empty" => "Catalog ID 不能为空。",
                "key_entry_null" => "Catalog 中存在空 Key 条目。",
                "key_id_invalid" => $"KeyId 必须为正数且不能重复：{diagnostic.KeyId}",
                "key_empty" => "本地化 Key 不能为空。",
                "key_duplicate" => "本地化 Key 不能重复。",
                "locale_descriptor_null" => "Catalog 中存在空语言描述。",
                "locale_empty" => "语言标识不能为空。",
                "locale_duplicate" => "语言标识不能重复。",
                "locale_location_empty" => $"语言 {diagnostic.Locale} 的资源位置不能为空。",
                "default_locale_invalid" => "默认语言必须是已注册语言。",
                "fallback_locale_missing" => $"语言 {diagnostic.Locale} 的回退语言不存在。",
                "fallback_cycle" => $"语言 {diagnostic.Locale} 的回退关系形成循环。",
                "locale_asset_missing" => $"缺少语言 {diagnostic.Locale} 的文本资产。",
                "locale_asset_duplicate" => $"语言 {diagnostic.Locale} 对应了多个文本资产。",
                "locale_asset_null" => "语言文本资产为空。",
                "locale_schema_unsupported" => $"语言 {diagnostic.Locale} 的资产结构版本不受支持。",
                "locale_asset_locale_empty" => "语言文本资产未设置语言标识。",
                "locale_asset_unregistered" => $"语言文本资产未在 Catalog 注册：{diagnostic.Locale}",
                "locale_asset_mismatch" => $"语言文本资产与 Catalog 描述不匹配：{diagnostic.Locale}",
                "locale_value_null" => $"语言 {diagnostic.Locale} 中存在空文本条目。",
                "locale_key_id_invalid" => $"语言 {diagnostic.Locale} 中的 KeyId 无效或重复：{diagnostic.KeyId}",
                "locale_key_id_unknown" => $"语言 {diagnostic.Locale} 引用了不存在的 KeyId：{diagnostic.KeyId}",
                _ => $"本地化资产校验失败（{diagnostic.Code}）。"
            };
        }
    }
}
