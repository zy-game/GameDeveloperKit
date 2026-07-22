using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Localization;
using GameDeveloperKit.LocalizationEditor;
using GameDeveloperKit.LubanConfigEditor;
using GameDeveloperKit.StoryEditor.Media;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class EditorConfigurationTests
    {
        private const string LocalizationTestFolder = "Assets/GameDeveloperKit/Tests/Editor/TempLocalizationAuthoring";

        private byte[] m_ProjectConfigBackup;
        private byte[] m_UserConfigBackup;
        private byte[] m_LegacyLubanBackup;
        private byte[] m_LegacyLocalizationBackup;
        private byte[] m_LegacyStoryMediaBackup;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(LocalizationTestFolder);
            m_ProjectConfigBackup = ReadIfExists(EditorGlobalConfig.SettingsPath);
            m_UserConfigBackup = ReadIfExists(EditorUserConfig.SettingsPath);
            m_LegacyLubanBackup = ReadIfExists(LubanEditorSettings.SettingsPath);
            m_LegacyLocalizationBackup = ReadIfExists(LocalizationEditorSettings.SettingsPath);
            m_LegacyStoryMediaBackup = ReadIfExists(CatalogSettings.SettingsPath);
            DeleteSettingsFiles();
            ResetInstances();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(LocalizationTestFolder);
            ResetInstances();
            DeleteSettingsFiles();
            Restore(EditorGlobalConfig.SettingsPath, m_ProjectConfigBackup);
            Restore(EditorUserConfig.SettingsPath, m_UserConfigBackup);
            Restore(LubanEditorSettings.SettingsPath, m_LegacyLubanBackup);
            Restore(LocalizationEditorSettings.SettingsPath, m_LegacyLocalizationBackup);
            Restore(CatalogSettings.SettingsPath, m_LegacyStoryMediaBackup);
        }

        [Test]
        public void LoadOrCreate_WhenFilesAreMissing_CreatesDeterministicDefaults()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            var user = EditorUserConfig.LoadOrCreate();

            Assert.AreEqual(EditorGlobalConfig.CurrentVersion, project.Version);
            Assert.AreEqual(LubanProjectConfig.DefaultTableDirectory, project.Luban.TableDirectory);
            Assert.AreEqual(LubanProjectConfig.DefaultGeneratedCodeDirectory, project.Luban.GeneratedCodeDirectory);
            Assert.AreEqual(LubanProjectConfig.DefaultGeneratedDataDirectory, project.Luban.GeneratedDataDirectory);
            Assert.AreEqual(LubanProjectConfig.DefaultCodeNamespace, project.Luban.CodeNamespace);
            Assert.AreEqual(string.Empty, project.Localization.CatalogAssetGuid);
            Assert.AreEqual(string.Empty, project.Localization.PreviewLocale);
            Assert.AreEqual(EditorUserConfig.DefaultLubanDllPath, user.LubanDllPath);
            Assert.IsTrue(IOFile.Exists(EditorGlobalConfig.SettingsPath));
            Assert.IsTrue(IOFile.Exists(EditorUserConfig.SettingsPath));
        }

        [Test]
        public void LoadOrCreate_WhenLegacySettingsExist_MigratesLubanWithoutInferringCatalogBinding()
        {
            SaveLegacyLocalization(" ja-JP ", "legacy-preview-guid");
            SaveLegacyStoryMedia("ko-KR");
            SaveLegacyLuban(@"E:\Tools\Luban\Luban.dll");
            var localizationBytes = IOFile.ReadAllBytes(LocalizationEditorSettings.SettingsPath);
            var storyBytes = IOFile.ReadAllBytes(CatalogSettings.SettingsPath);
            var lubanBytes = IOFile.ReadAllBytes(LubanEditorSettings.SettingsPath);

            var project = EditorGlobalConfig.LoadOrCreate();
            var user = EditorUserConfig.LoadOrCreate();

            Assert.AreEqual(EditorGlobalConfig.CurrentVersion, project.Version);
            Assert.AreEqual(EditorUserConfig.CurrentVersion, user.Version);
            Assert.AreEqual(string.Empty, project.Localization.CatalogAssetGuid);
            Assert.AreEqual(string.Empty, project.Localization.PreviewLocale);
            Assert.AreEqual("E:/Tools/Luban/Luban.dll", user.LubanDllPath);
            Assert.IsNull(new SerializedObject(project).FindProperty("m_PreviewPackGuid"));
            CollectionAssert.AreEqual(localizationBytes, IOFile.ReadAllBytes(LocalizationEditorSettings.SettingsPath));
            CollectionAssert.AreEqual(storyBytes, IOFile.ReadAllBytes(CatalogSettings.SettingsPath));
            CollectionAssert.AreEqual(lubanBytes, IOFile.ReadAllBytes(LubanEditorSettings.SettingsPath));
        }

        [Test]
        public void LoadOrCreate_WhenOnlyStoryLocaleExists_DoesNotUseItAsPreviewLocale()
        {
            SaveLegacyStoryMedia("ko-KR");

            var project = EditorGlobalConfig.LoadOrCreate();

            Assert.AreEqual(string.Empty, project.Localization.PreviewLocale);
        }

        [Test]
        public void LoadOrCreate_WhenMigrationAlreadyCompleted_DoesNotReadChangedLegacyValuesAgain()
        {
            SaveLegacyLocalization("ja-JP", string.Empty);
            SaveLegacyLuban(@"E:\Tools\Luban\First.dll");
            Assert.AreEqual(string.Empty, EditorGlobalConfig.LoadOrCreate().Localization.PreviewLocale);
            Assert.AreEqual("E:/Tools/Luban/First.dll", EditorUserConfig.LoadOrCreate().LubanDllPath);

            SaveLegacyLocalization("ko-KR", string.Empty);
            SaveLegacyLuban(@"E:\Tools\Luban\Second.dll");
            ResetInstances();

            Assert.AreEqual(string.Empty, EditorGlobalConfig.LoadOrCreate().Localization.PreviewLocale);
            Assert.AreEqual("E:/Tools/Luban/First.dll", EditorUserConfig.LoadOrCreate().LubanDllPath);
        }

        [Test]
        public void LoadOrCreate_WhenNewValuesAreNonDefault_DoesNotOverwriteThemDuringMigration()
        {
            SaveVersionedProjectConfig(EditorGlobalConfig.CurrentVersion - 1, "zh-CN");
            SaveVersionedUserConfig(EditorUserConfig.CurrentVersion - 1, @"E:\Current\Luban.dll");
            SaveLegacyLocalization("ja-JP", string.Empty);
            SaveLegacyLuban(@"E:\Legacy\Luban.dll");

            var project = EditorGlobalConfig.LoadOrCreate();
            var user = EditorUserConfig.LoadOrCreate();

            Assert.AreEqual("zh-CN", project.Localization.PreviewLocale);
            Assert.AreEqual("E:/Current/Luban.dll", user.LubanDllPath);
            Assert.AreEqual(EditorGlobalConfig.CurrentVersion, project.Version);
            Assert.AreEqual(EditorUserConfig.CurrentVersion, user.Version);
        }

        [Test]
        public void LoadOrCreate_WhenLegacyFilesAreDamaged_KeepsDefaultsAndCompletesMigration()
        {
            Directory.CreateDirectory("ProjectSettings");
            IOFile.WriteAllText(LubanEditorSettings.SettingsPath, "not a serialized Unity object");
            LogAssert.Expect(LogType.Warning, new Regex("读取旧 Editor 配置失败.*GameDeveloperKitLubanEditorSettings"));

            var project = EditorGlobalConfig.LoadOrCreate();
            var user = EditorUserConfig.LoadOrCreate();

            Assert.AreEqual(string.Empty, project.Localization.PreviewLocale);
            Assert.AreEqual(EditorUserConfig.DefaultLubanDllPath, user.LubanDllPath);
            Assert.AreEqual(EditorGlobalConfig.CurrentVersion, project.Version);
            Assert.AreEqual(EditorUserConfig.CurrentVersion, user.Version);
        }

        [Test]
        public void LegacySettings_AreReadOnlyMigrationTypesWithoutProductionProviders()
        {
            var assembly = typeof(LocalizationEditorSettings).Assembly;

            Assert.IsNull(typeof(LocalizationEditorSettings).GetMethod("LoadOrCreate"));
            Assert.IsNull(typeof(LubanEditorSettings).GetMethod("LoadOrCreate"));
            Assert.IsFalse(assembly.GetTypes().Any(type => type.Name == "LocalizationEditorSettingsProvider"));
        }

        [Test]
        public void StoryMediaPreviewLocale_UsesItsOwnLocaleInsteadOfGlobalPreviewLocale()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Localization.PreviewLocale = "en-US";
            project.Save();
            var settings = ScriptableObject.CreateInstance<CatalogSettings>();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_PreviewLocale").stringValue = "ko-KR";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("ko-KR", settings.PreviewLocale);

            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void Save_WhenValuesAreValid_NormalizesAndReloadsProjectConfig()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Luban.TableDirectory = @" Tables\Excel ";
            project.Luban.GeneratedCodeDirectory = @"Assets\Generated\Code";
            project.Luban.GeneratedDataDirectory = "Assets/Generated/./Data";
            project.Luban.CodeNamespace = " Game.Config ";
            project.Localization.CatalogAssetGuid = " catalog-guid ";
            project.Localization.PreviewLocale = " zh-CN ";
            project.Save();

            EditorGlobalConfig.ResetInstance();
            var reloaded = EditorGlobalConfig.LoadOrCreate();

            Assert.AreEqual("Tables/Excel", reloaded.Luban.TableDirectory);
            Assert.AreEqual("Assets/Generated/Code", reloaded.Luban.GeneratedCodeDirectory);
            Assert.AreEqual("Assets/Generated/Data", reloaded.Luban.GeneratedDataDirectory);
            Assert.AreEqual("Game.Config", reloaded.Luban.CodeNamespace);
            Assert.AreEqual("catalog-guid", reloaded.Localization.CatalogAssetGuid);
            Assert.AreEqual("zh-CN", reloaded.Localization.PreviewLocale);
        }

        [Test]
        public void Save_WhenDirectoriesAreOutsideProject_PersistsAbsoluteAndParentPaths()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            var tableDirectory = Path.Combine(Path.GetTempPath(), "gdk-external-tables");
            var dataDirectory = Path.Combine(Path.GetTempPath(), "gdk-external-data");
            project.Luban.TableDirectory = tableDirectory;
            project.Luban.GeneratedCodeDirectory = "../SharedGeneratedCode";
            project.Luban.GeneratedDataDirectory = dataDirectory;
            project.Save();

            EditorGlobalConfig.ResetInstance();
            var reloaded = EditorGlobalConfig.LoadOrCreate();
            Assert.AreEqual(Path.GetFullPath(tableDirectory).Replace('\\', '/'), reloaded.Luban.TableDirectory);
            Assert.AreEqual("../SharedGeneratedCode", reloaded.Luban.GeneratedCodeDirectory);
            Assert.AreEqual(Path.GetFullPath(dataDirectory).Replace('\\', '/'), reloaded.Luban.GeneratedDataDirectory);
        }

        [Test]
        public void TryValidate_WhenNamespaceIsInvalid_ReturnsChineseError()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Luban.CodeNamespace = "Game.class";

            Assert.IsFalse(project.TryValidate(out var error));
            StringAssert.Contains("代码命名空间无效", error);
        }

        [Test]
        public void LocalizationConfig_OnlyExposesCatalogGuidAndPreviewLocale()
        {
            Assert.NotNull(typeof(LocalizationProjectConfig).GetProperty("CatalogAssetGuid"));
            Assert.NotNull(typeof(LocalizationProjectConfig).GetProperty("PreviewLocale"));
            Assert.IsNull(typeof(LocalizationProjectConfig).GetProperty("TableId"));
            Assert.IsNull(typeof(LocalizationProjectConfig).GetProperty("KeyField"));
            Assert.IsNull(typeof(LocalizationProjectConfig).GetProperty("PreviewField"));
            Assert.IsNull(typeof(LocalizationProjectConfig).GetProperty("LocaleFields"));
            Assert.IsNull(typeof(LocalizationProjectConfig).Assembly.GetType(
                "GameDeveloperKit.EditorConfiguration.LocalizationLocaleField"));
        }

        [Test]
        public void Save_WhenPreviewLocaleHasWhitespace_NormalizesIt()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Localization.PreviewLocale = " zh-CN ";
            project.Save();

            EditorGlobalConfig.ResetInstance();
            Assert.AreEqual("zh-CN", EditorGlobalConfig.LoadOrCreate().Localization.PreviewLocale);
        }

        [Test]
        public void SaveUserConfig_WhenPathIsAbsolute_PersistsOnlyInUserSettings()
        {
            EditorGlobalConfig.LoadOrCreate().Save();
            var projectBytes = IOFile.ReadAllBytes(EditorGlobalConfig.SettingsPath);
            var user = EditorUserConfig.LoadOrCreate();
            user.LubanDllPath = @"E:\Tools\Luban\Luban.dll";
            user.Save();

            EditorUserConfig.ResetInstance();
            Assert.AreEqual("E:/Tools/Luban/Luban.dll", EditorUserConfig.LoadOrCreate().LubanDllPath);
            CollectionAssert.AreEqual(projectBytes, IOFile.ReadAllBytes(EditorGlobalConfig.SettingsPath));
        }

        [Test]
        public void LoadOrCreate_AfterFilesAreDeleted_RecreatesDefaults()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Luban.TableDirectory = "CustomTables";
            project.Save();
            var user = EditorUserConfig.LoadOrCreate();
            user.LubanDllPath = @"E:\Custom\Luban.dll";
            user.Save();

            DeleteSettingsFiles();
            ResetInstances();

            Assert.AreEqual(
                LubanProjectConfig.DefaultTableDirectory,
                EditorGlobalConfig.LoadOrCreate().Luban.TableDirectory);
            Assert.AreEqual(
                EditorUserConfig.DefaultLubanDllPath,
                EditorUserConfig.LoadOrCreate().LubanDllPath);
        }

        [Test]
        public void ConfigurationContracts_ExposeGlobalPanelAndCacheRootWithoutSettingsProvider()
        {
            var cacheRootExisted = Directory.Exists(EditorGlobalConfig.CacheRoot);
            var assembly = typeof(EditorGlobalConfig).Assembly;
            var panel = new EditorConfigurationPanel();

            Assert.IsFalse(assembly.GetTypes().Any(type =>
                type.Name == "EditorConfigurationSettingsProvider" &&
                typeof(SettingsProvider).IsAssignableFrom(type)));
            Assert.NotNull(panel.Q<TextField>("table-directory-field"));
            Assert.NotNull(panel.Q<TextField>("luban-dll-path-field"));
            Assert.IsNull(panel.Q<VisualElement>("localization-config-content"));
            Assert.IsNull(panel.Q<VisualElement>("localization-asset-workbench"));
            Assert.NotNull(panel.Q<VisualElement>("global-settings-form"));
            Assert.NotNull(panel.Q<Button>("table-directory-browse-button"));
            Assert.NotNull(panel.Q<Button>("generated-code-directory-browse-button"));
            Assert.NotNull(panel.Q<Button>("generated-data-directory-browse-button"));
            Assert.NotNull(panel.Q<Button>("luban-dll-browse-button"));
            Assert.AreEqual("Library/GameDeveloperKit/EditorConfig", EditorGlobalConfig.CacheRoot);
            Assert.AreEqual(cacheRootExisted, Directory.Exists(EditorGlobalConfig.CacheRoot));
        }

        [Test]
        public void InlineConfigurationPanel_DoesNotEmbedLocalizationAuthoring()
        {
            var panel = new EditorConfigurationPanel();

            Assert.IsNull(panel.Q<DropdownField>("localization-table-field"));
            Assert.IsNull(panel.Q<DropdownField>("localization-key-field"));
            Assert.IsNull(panel.Q<DropdownField>("localization-preview-field"));
            Assert.IsNull(panel.Q<TextField>("localization-catalog-name"));
            Assert.IsNull(panel.Q<TextField>("localization-catalog-locale"));
        }

        [Test]
        public void InlineConfigurationPanel_WhenDraftChanges_PersistsGlobalConfig()
        {
            var panel = new EditorConfigurationPanel();
            var namespaceField = panel.Q<TextField>("code-namespace-field");
            var project = EditorGlobalConfig.LoadOrCreate();

            Assert.AreEqual(project.Luban.CodeNamespace, namespaceField.value);
            project.Luban.CodeNamespace = "Game.InlineConfig";
            typeof(EditorConfigurationPanel)
                .GetMethod("SaveConfigs", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(panel, null);
            EditorGlobalConfig.ResetInstance();

            Assert.AreEqual(
                "Game.InlineConfig",
                EditorGlobalConfig.LoadOrCreate().Luban.CodeNamespace);
        }

        [Test]
        public void LubanWorkbench_GlobalSettingsToggleSwitchesContentAndDefaultsToSourceHierarchy()
        {
            var window = ScriptableObject.CreateInstance<GameDeveloperKit.LubanConfigEditor.UI.MainWindow>();
            try
            {
                var windowType = typeof(GameDeveloperKit.LubanConfigEditor.UI.MainWindow);
                windowType.GetMethod("BuildLayout", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(window, null);

                var toolbar = window.rootVisualElement.Q<VisualElement>("configuration-toolbar");
                Assert.NotNull(toolbar);
                Assert.AreEqual(30f, toolbar.style.minHeight.value.value);
                Assert.AreEqual(30f, toolbar.style.maxHeight.value.value);
                Assert.NotNull(window.rootVisualElement.Q<Button>("global-settings-toggle"));
                Assert.NotNull(window.rootVisualElement.Q<Button>("localization-toggle"));
                var sourceTable = window.rootVisualElement.Q<VisualElement>("configuration-source-table");
                Assert.NotNull(sourceTable);
                Assert.AreEqual(0f, sourceTable.style.marginLeft.value.value);
                Assert.AreEqual(0f, sourceTable.style.marginRight.value.value);
                Assert.AreEqual(0f, sourceTable.style.marginTop.value.value);
                Assert.AreEqual(0f, sourceTable.style.marginBottom.value.value);
                var statusHeader = window.rootVisualElement.Q<VisualElement>("luban-status-header");
                Assert.NotNull(statusHeader);
                Assert.AreEqual(26f, statusHeader.style.minHeight.value.value);
                Assert.AreEqual(26f, statusHeader.style.maxHeight.value.value);
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("global-settings-view"));
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("localization-asset-workbench"));
                Assert.NotNull(window.rootVisualElement.Q<VisualElement>("configuration-source-table-body"));
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("global-config-row"));
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("global-config-details"));
                Assert.IsEmpty(window.rootVisualElement.Query<ListView>().ToList());
                Assert.IsFalse(window.rootVisualElement.Query<Button>().ToList().Any(button => button.text == "配置"));

                windowType.GetMethod("ToggleGlobalSettingsMode", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(window, null);
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("configuration-source-table"));
                Assert.NotNull(window.rootVisualElement.Q<VisualElement>("global-settings-view"));

                windowType.GetMethod("ToggleGlobalSettingsMode", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(window, null);
                Assert.NotNull(window.rootVisualElement.Q<VisualElement>("configuration-source-table"));
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("global-settings-view"));

                windowType.GetMethod("ToggleLocalizationMode", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(window, null);
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("configuration-source-table"));
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("global-settings-view"));
                Assert.NotNull(window.rootVisualElement.Q<VisualElement>("localization-asset-workbench"));
                Assert.IsNull(window.rootVisualElement.Q<UnityEditor.UIElements.ObjectField>("localization-catalog-field"));
                Assert.IsNull(window.rootVisualElement.Q<TextField>("localization-catalog-name"));

                windowType.GetMethod("ToggleLocalizationMode", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(window, null);
                Assert.NotNull(window.rootVisualElement.Q<VisualElement>("configuration-source-table"));
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("localization-asset-workbench"));

                var statusDetails = window.rootVisualElement.Q<VisualElement>("luban-status-details");
                Assert.AreEqual(DisplayStyle.None, statusDetails.style.display.value);
                windowType.GetMethod("ToggleStatusDetails", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(window, null);
                Assert.AreEqual(DisplayStyle.Flex, statusDetails.style.display.value);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void LocalizationAssetWorkbench_BuildsDynamicLocaleColumnsWithReadOnlyLabels()
        {
            var catalog = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            var zhCn = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            var enUs = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            try
            {
                var descriptors = new[]
                {
                    new LocalizationLocaleDescriptor("zh-CN", "Assets/Localization.zh-CN.asset"),
                    new LocalizationLocaleDescriptor("en-US", "Assets/Localization.en-US.asset", "zh-CN")
                };
                catalog.Replace(
                    "test-catalog",
                    "zh-CN",
                    new[]
                    {
                        new LocalizationKeyEntry(1, "ui.start"),
                        new LocalizationKeyEntry(2, "ui.empty")
                    },
                    descriptors);
                zhCn.Replace(
                    "zh-CN",
                    new[]
                    {
                        new LocalizationValueEntry(1, "开始"),
                        new LocalizationValueEntry(2, string.Empty)
                    },
                    1);
                enUs.Replace("en-US", new[] { new LocalizationValueEntry(1, "Start") }, 1);
                var snapshot = new LocalizationAuthoringSnapshot(
                    1,
                    catalog,
                    "Assets/Localization.asset",
                    "zh-CN",
                    new[]
                    {
                        new LocalizationAuthoringLocale(descriptors[0], zhCn, descriptors[0].ResourceLocation),
                        new LocalizationAuthoringLocale(descriptors[1], enUs, descriptors[1].ResourceLocation)
                    },
                    Array.Empty<LocalizationAuthoringDiagnostic>());
                var workbench = new LocalizationAssetWorkbench(new StubLocalizationAuthoringService(snapshot));

                Assert.NotNull(workbench.Q<VisualElement>("localization-table-header"));
                Assert.NotNull(workbench.Q<VisualElement>("localization-locale-column-zh-CN"));
                Assert.NotNull(workbench.Q<VisualElement>("localization-locale-column-en-US"));
                Assert.AreEqual("(空文本)", workbench.Q<Label>("localization-text-label-2-zh-CN").text);
                Assert.AreEqual("缺翻译", workbench.Q<Label>("localization-text-label-2-en-US").text);
                Assert.IsNull(workbench.Q<TextField>("localization-text-editor-1-zh-CN"));
                Assert.IsNull(workbench.Q<TextField>("localization-key-editor-1"));

                workbench.SetSearchQuery("Start");
                Assert.AreEqual("显示 1 / 2 个 Key · 2 种语言", workbench.Q<Label>("localization-key-count").text);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(zhCn);
                UnityEngine.Object.DestroyImmediate(enUs);
            }
        }

        [Test]
        public void LocalizationAssetWorkbench_WhenCatalogIsUnbound_ExposesBindingMenuOnce()
        {
            var snapshot = new LocalizationAuthoringSnapshot(
                1,
                null,
                string.Empty,
                "zh-CN",
                Array.Empty<LocalizationAuthoringLocale>(),
                new[]
                {
                    new LocalizationAuthoringDiagnostic(
                        LocalizationAuthoringDiagnosticSeverity.Error,
                        "catalog_not_bound",
                        "尚未绑定全局本地化 Catalog。")
                });
            var workbench = new LocalizationAssetWorkbench(new StubLocalizationAuthoringService(snapshot));

            var catalogMenu = workbench.Q<UnityEditor.UIElements.ToolbarMenu>("localization-catalog-menu");
            Assert.NotNull(catalogMenu);
            Assert.AreEqual("绑定 Catalog", catalogMenu.text);
            Assert.NotNull(workbench.Q<Label>("localization-unavailable-state"));
            Assert.IsNull(workbench.Q<VisualElement>("localization-diagnostics"));
            Assert.IsNull(workbench.Q<UnityEditor.UIElements.ObjectField>("localization-catalog-field"));
        }

        [Test]
        public void LocalizationTableContract_WhenKeysAreUnique_AllowsEmptyTranslationCells()
        {
            var fixture = CreateLocalizationFixture(
                new LubanTableRow(5, Values("ui.start", "开始", "Start")),
                new LubanTableRow(6, Values("ui.empty", string.Empty, string.Empty)));

            var result = LocalizationTableContractValidator.Validate(
                fixture.Snapshot,
                fixture.Catalog,
                CreateLocalizationConfig());

            Assert.IsTrue(result.IsValid, string.Join("|", result.Diagnostics.Select(item => item.Message)));
            Assert.AreEqual(2, result.Data.Rows.Count);
        }

        [Test]
        public void LocalizationTableContract_WhenTrimmedKeyIsDuplicated_ReturnsSourceRows()
        {
            var fixture = CreateLocalizationFixture(
                new LubanTableRow(5, Values("ui.start", "开始", "Start")),
                new LubanTableRow(8, Values(" ui.start ", "继续", "Continue")));

            var result = LocalizationTableContractValidator.Validate(
                fixture.Snapshot,
                fixture.Catalog,
                CreateLocalizationConfig());

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("本地化 Key 重复：ui.start", string.Join("|", result.Diagnostics.Select(item => item.Message)));
            StringAssert.Contains("5、8", string.Join("|", result.Diagnostics.Select(item => item.Message)));
        }

        [Test]
        public void LocalizationTableContract_WhenPreviewFieldIsMissing_ReturnsError()
        {
            var fixture = CreateLocalizationFixture(new LubanTableRow(5, Values("ui.start", "开始", "Start")));
            var config = CreateLocalizationConfig();
            config.PreviewField = "missingField";

            var result = LocalizationTableContractValidator.Validate(fixture.Snapshot, fixture.Catalog, config);

            Assert.IsFalse(result.IsValid);
            var messages = string.Join("|", result.Diagnostics.Select(item => item.Message));
            StringAssert.Contains("本地化预览字段不存在：missingField", messages);
        }

        [Test]
        public void LocalizationImportMergeRule_ClassifiesMissingEmptyAndChangedValues()
        {
            var missing = LocalizationImportValue.Missing;
            var empty = LocalizationImportValue.From(string.Empty);
            var original = LocalizationImportValue.From("base");
            var asset = LocalizationImportValue.From("asset");
            var source = LocalizationImportValue.From("source");

            Assert.AreEqual(LocalizationMergeKind.Unchanged,
                LocalizationImportService.Classify(original, source, source));
            Assert.AreEqual(LocalizationMergeKind.UpdateFromSource,
                LocalizationImportService.Classify(original, original, source));
            Assert.AreEqual(LocalizationMergeKind.KeepAsset,
                LocalizationImportService.Classify(original, asset, original));
            Assert.AreEqual(LocalizationMergeKind.Conflict,
                LocalizationImportService.Classify(original, asset, source));
            Assert.AreEqual(LocalizationMergeKind.UpdateFromSource,
                LocalizationImportService.Classify(missing, missing, empty));
            Assert.AreEqual(LocalizationMergeKind.KeepAsset,
                LocalizationImportService.Classify(missing, empty, missing));
        }

        [Test]
        public void LocalizationImportService_UnresolvedStaleAndAppliedPlansDoNotDoubleCommit()
        {
            var fixture = CreateLocalizationFixture(
                new LubanTableRow(5, Values("ui.start", "配置表", "Source")));
            var catalog = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            var locale = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            try
            {
                var descriptor = new LocalizationLocaleDescriptor("zh-CN", "Assets/Test.zh-CN.asset");
                catalog.Replace(
                    "test-catalog",
                    "zh-CN",
                    new[] { new LocalizationKeyEntry(1, "ui.start") },
                    new[] { descriptor });
                locale.Replace("zh-CN", new[] { new LocalizationValueEntry(1, "资产") }, 1);
                var original = CreateAuthoringSnapshot(catalog, descriptor, locale);
                var authoring = new StubLocalizationAuthoringService(original);
                var baselines = new RecordingBaselineStore();
                var service = new LocalizationImportService(
                    fixture.Catalog,
                    () => new LubanProjectConfig(),
                    authoring,
                    baselines);
                var request = new LocalizationImportRequest(
                    "test-catalog",
                    CreateLocalizationConfig().TableId,
                    "key",
                    fixture.Snapshot.Revision,
                    new[] { new LocalizationImportColumn("zh-CN", "textZhCn") });

                var plan = service.CreatePlan(request);
                Assert.AreEqual(1, plan.UnresolvedCount);
                Assert.IsFalse(service.Apply(plan).Succeeded);
                Assert.AreEqual(0, authoring.ApplyImportCallCount);

                plan.Resolve(1, "zh-CN", LocalizationConflictResolution.UseSource);
                var sourceCatalog = (StubLubanSourceCatalog)fixture.Catalog;
                sourceCatalog.Snapshot = new LubanSourceSnapshot(
                    fixture.Snapshot.Revision + 1,
                    fixture.Snapshot.Sources,
                    fixture.Snapshot.Diagnostics);
                Assert.IsFalse(service.Apply(plan).Succeeded);
                Assert.AreEqual(0, authoring.ApplyImportCallCount);
                sourceCatalog.Snapshot = fixture.Snapshot;
                authoring.Snapshot = CreateAuthoringSnapshot(catalog, descriptor, locale, 8);
                Assert.IsFalse(service.Apply(plan).Succeeded);
                Assert.AreEqual(0, authoring.ApplyImportCallCount);

                authoring.Snapshot = original;
                Assert.IsTrue(service.Apply(plan).Succeeded);
                Assert.AreEqual(1, authoring.ApplyImportCallCount);
                Assert.AreEqual("配置表", authoring.LastImportMutation.LocaleValues["zh-CN"].Single().Value);
                Assert.IsFalse(service.Apply(plan).Succeeded);
                Assert.AreEqual(1, authoring.ApplyImportCallCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(locale);
            }
        }

        [Test]
        public void LocalizationAuthoringService_WhenBaselineWriteFails_RollsBackThenCanCommit()
        {
            EnsureLocalizationTestFolder();
            var baselines = new RecordingBaselineStore { ThrowOnWrite = true, StoredContent = "old baseline" };
            var service = new LocalizationAuthoringService(EditorGlobalConfig.LoadOrCreate, baselines);
            Assert.IsTrue(service.CreateCatalog(
                LocalizationTestFolder,
                "ImportRollbackCatalog",
                "zh-CN").Succeeded);
            var created = service.CreateKey("ui.start", "zh-CN", "资产");
            Assert.IsTrue(created.Succeeded, created.Message);
            var before = service.Refresh();
            var mutation = new LocalizationImportAssetMutation(
                before.Catalog.CatalogId,
                before.Revision,
                before.Catalog.Keys,
                new Dictionary<string, IReadOnlyList<LocalizationValueEntry>>
                {
                    ["zh-CN"] = new[] { new LocalizationValueEntry(created.KeyId, "配置表") }
                },
                baselines.GetPath(before.Catalog.CatalogId),
                "new baseline");

            var failed = service.ApplyImport(mutation);
            var rolledBack = service.Refresh();
            Assert.IsFalse(failed.Succeeded);
            Assert.IsTrue(rolledBack.TryGetText(created.KeyId, "zh-CN", out var rolledBackText));
            Assert.AreEqual("资产", rolledBackText);
            Assert.AreEqual("old baseline", baselines.StoredContent);
            Assert.AreEqual(1, baselines.RestoreCallCount);

            baselines.ThrowOnWrite = false;
            var succeeded = service.ApplyImport(mutation);
            Assert.IsTrue(succeeded.Succeeded, succeeded.Message);
            Assert.IsTrue(succeeded.Snapshot.TryGetText(created.KeyId, "zh-CN", out var committedText));
            Assert.AreEqual("配置表", committedText);
            Assert.AreEqual("new baseline", baselines.StoredContent);
        }

        [Test]
        public void LocalizationImportWorkbench_UsesCompactTableAndMultiLanguageMappingWithConditionalConflicts()
        {
            var fixture = CreateLocalizationFixture(
                new LubanTableRow(5, Values("ui.start", "开始", "Start")));
            var catalog = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            var locale = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            try
            {
                var descriptor = new LocalizationLocaleDescriptor("zh-CN", "Assets/Test.zh-CN.asset");
                catalog.Replace(
                    "test-catalog",
                    "zh-CN",
                    new[] { new LocalizationKeyEntry(1, "ui.start") },
                    new[] { descriptor });
                locale.Replace("zh-CN", new[] { new LocalizationValueEntry(1, "开始") }, 1);
                var authoring = new StubLocalizationAuthoringService(
                    CreateAuthoringSnapshot(catalog, descriptor, locale));
                var importer = new StubLocalizationImportService(fixture.Snapshot);
                var workbench = new LocalizationImportWorkbench(
                    authoring,
                    service: importer);

                var table = workbench.Q<DropdownField>("localization-import-table");
                Assert.IsNotNull(table);
                Assert.Less(table.value.Length, fixture.Data.TableId.Length);
                Assert.AreEqual(fixture.Data.TableId, table.tooltip);
                Assert.IsNotNull(workbench.Q<DropdownField>("localization-import-key-field"));
                var languageFields = workbench.Q<Button>("localization-import-language-fields");
                Assert.IsNotNull(languageFields);
                Assert.AreEqual("语言字段 (0) ▾", languageFields.text);
                InvokeNonPublic(workbench, "ToggleLanguageField", "textZhCn");
                var zhMapping = workbench.Q<TextField>("localization-import-target-textZhCn");
                Assert.IsNotNull(zhMapping);
                zhMapping.value = "zh-CN";
                InvokeNonPublic(workbench, "ToggleLanguageField", "textEnUs");
                var enMapping = workbench.Q<TextField>("localization-import-target-textEnUs");
                Assert.IsNotNull(enMapping);
                enMapping.value = "en-US";
                Assert.AreEqual("语言字段 (2) ▾", languageFields.text);
                Assert.IsNotNull(workbench.Q<Button>("localization-import-preview-button"));
                Assert.IsNotNull(workbench.Q<Button>("localization-import-use-asset"));
                Assert.IsNotNull(workbench.Q<Button>("localization-import-use-source"));
                var apply = workbench.Q<Button>("localization-import-apply-button");
                Assert.IsNotNull(apply);
                var conflicts = workbench.Q<VisualElement>("localization-import-conflict-actions");
                Assert.AreEqual(DisplayStyle.None, conflicts.style.display.value);

                var request = new LocalizationImportRequest(
                    catalog.CatalogId,
                    fixture.Data.TableId,
                    "key",
                    fixture.Snapshot.Revision,
                    new[]
                    {
                        new LocalizationImportColumn("zh-CN", "textZhCn"),
                        new LocalizationImportColumn("en-US", "textEnUs")
                    });
                importer.Plan = new LocalizationImportPlan(
                    request,
                    "localization.xlsx",
                    1,
                    "fingerprint",
                    new[]
                    {
                        new LocalizationImportMergeEntry(
                            1,
                            "ui.start",
                            "ui.start",
                            "ui.start",
                            "zh-CN",
                            "textZhCn",
                            LocalizationImportValue.Missing,
                            LocalizationImportValue.From("资产"),
                            LocalizationImportValue.From("配置表"),
                            LocalizationMergeKind.Conflict,
                            LocalizationMergeKind.Unchanged,
                            LocalizationMergeKind.Conflict,
                            5)
                    },
                    Array.Empty<LocalizationImportDiagnostic>(),
                    new LocalizationImportBaselineDocument());

                InvokeNonPublic(workbench, "CreatePlan");

                Assert.AreEqual(2, importer.LastRequest.Columns.Count);
                CollectionAssert.AreEquivalent(
                    new[] { "zh-CN:textZhCn", "en-US:textEnUs" },
                    importer.LastRequest.Columns.Select(column =>
                        $"{column.TargetLocale}:{column.SourceField}").ToArray());
                Assert.AreEqual(DisplayStyle.Flex, conflicts.style.display.value);
                Assert.IsTrue(apply.enabledSelf);

                InvokeNonPublic(workbench, "Apply");

                Assert.AreEqual(0, importer.ApplyCallCount);
                StringAssert.Contains(
                    "仍有 1 个冲突或删除候选未解决",
                    workbench.Q<Label>("localization-import-status").text);

                importer.Plan.ResolveAll(LocalizationConflictResolution.UseSource);
                importer.ApplyResult = LocalizationMutationResult.Failure("模拟导入提交失败");
                InvokeNonPublic(workbench, "Apply");

                Assert.AreEqual(1, importer.ApplyCallCount);
                Assert.AreEqual(
                    "模拟导入提交失败",
                    workbench.Q<Label>("localization-import-status").text);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(locale);
            }
        }

        [Test]
        public void LocalizationImportService_WhenTargetLocaleIsMissing_CreatesReadOnlyPendingLocalePlan()
        {
            var fixture = CreateLocalizationFixture(
                new LubanTableRow(5, Values("ui.start", "开始", "Start")));
            var catalog = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            var locale = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            try
            {
                var descriptor = new LocalizationLocaleDescriptor("zh-CN", "Assets/Test.zh-CN.asset");
                catalog.Replace(
                    "test-catalog",
                    "zh-CN",
                    Array.Empty<LocalizationKeyEntry>(),
                    new[] { descriptor });
                locale.Replace("zh-CN", Array.Empty<LocalizationValueEntry>(), 1);
                var authoring = new StubLocalizationAuthoringService(
                    CreateAuthoringSnapshot(catalog, descriptor, locale));
                var service = new LocalizationImportService(
                    fixture.Catalog,
                    () => new LubanProjectConfig(),
                    authoring,
                    new RecordingBaselineStore());
                service.RefreshSource();

                var plan = service.CreatePlan(new LocalizationImportRequest(
                    catalog.CatalogId,
                    fixture.Data.TableId,
                    "key",
                    fixture.Snapshot.Revision,
                    new[] { new LocalizationImportColumn("en-US", "textEnUs") }));

                CollectionAssert.AreEqual(new[] { "en-US" }, plan.PendingLocales);
                Assert.IsFalse(plan.Diagnostics.Any(diagnostic =>
                    diagnostic.Severity == LocalizationImportDiagnosticSeverity.Error));
                Assert.AreEqual(1, plan.Entries.Count);
                Assert.AreEqual(LocalizationMergeKind.Add, plan.Entries[0].Kind);
                Assert.AreEqual(0, authoring.ApplyImportCallCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(locale);
            }
        }

        [Test]
        public void LocalizationAuthoringService_ImportCreatesMissingLocaleAndRollsBackWhenBaselineFails()
        {
            EnsureLocalizationTestFolder();
            var baselines = new RecordingBaselineStore { StoredContent = "old baseline" };
            var service = new LocalizationAuthoringService(EditorGlobalConfig.LoadOrCreate, baselines);
            var created = service.CreateCatalog(LocalizationTestFolder, "ImportedLocaleCatalog", "zh-CN");
            Assert.IsTrue(created.Succeeded, created.Message);
            var before = service.Refresh();
            var localePath = $"{LocalizationTestFolder}/ImportedLocaleCatalog.en-US.asset";
            var mutation = new LocalizationImportAssetMutation(
                before.Catalog.CatalogId,
                before.Revision,
                new[] { new LocalizationKeyEntry(1, "ui.start") },
                new Dictionary<string, IReadOnlyList<LocalizationValueEntry>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["zh-CN"] = Array.Empty<LocalizationValueEntry>(),
                    ["en-US"] = new[] { new LocalizationValueEntry(1, "Start") }
                },
                baselines.GetPath(before.Catalog.CatalogId),
                "new baseline",
                new[] { new LocalizationLocaleDraft("en-US", localePath, localePath) });

            baselines.ThrowOnWrite = true;
            var failed = service.ApplyImport(mutation);

            Assert.IsFalse(failed.Succeeded);
            Assert.IsFalse(service.Refresh().Catalog.TryGetLocale("en-US", out _));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<LocalizationLocaleAsset>(localePath));
            Assert.AreEqual("old baseline", baselines.StoredContent);

            baselines.ThrowOnWrite = false;
            var succeeded = service.ApplyImport(mutation);

            Assert.IsTrue(succeeded.Succeeded, succeeded.Message);
            Assert.IsTrue(succeeded.Snapshot.TryGetLocale("en-US", out var importedLocale));
            Assert.AreEqual(localePath, importedLocale.AssetPath);
            Assert.IsTrue(succeeded.Snapshot.TryGetText(1, "en-US", out var text));
            Assert.AreEqual("Start", text);
            Assert.AreEqual("new baseline", baselines.StoredContent);
        }

        [Test]
        public void LocalizationImportService_AddDeleteAndKeyResolutionRespectStableIdsAndLocaleScope()
        {
            var fixture = CreateLocalizationFixture(
                new LubanTableRow(5, Values("base.key", "来源", "Source")),
                new LubanTableRow(6, Values("ui.new", "新增", "New")));
            var catalog = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            var zhAsset = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            var enAsset = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            try
            {
                var zhDescriptor = new LocalizationLocaleDescriptor("zh-CN", "Assets/Test.zh-CN.asset");
                var enDescriptor = new LocalizationLocaleDescriptor("en-US", "Assets/Test.en-US.asset");
                catalog.Replace(
                    "test-catalog",
                    "zh-CN",
                    new[] { new LocalizationKeyEntry(1, "asset.key") },
                    new[] { zhDescriptor, enDescriptor });
                zhAsset.Replace("zh-CN", new[] { new LocalizationValueEntry(1, "资产") }, 1);
                enAsset.Replace("en-US", new[] { new LocalizationValueEntry(1, "Asset") }, 1);
                var authoring = new StubLocalizationAuthoringService(new LocalizationAuthoringSnapshot(
                    7,
                    catalog,
                    "Assets/Test.asset",
                    "zh-CN",
                    new[]
                    {
                        new LocalizationAuthoringLocale(zhDescriptor, zhAsset, zhDescriptor.ResourceLocation),
                        new LocalizationAuthoringLocale(enDescriptor, enAsset, enDescriptor.ResourceLocation)
                    },
                    Array.Empty<LocalizationAuthoringDiagnostic>()));
                var baselines = new RecordingBaselineStore
                {
                    Baseline = new LocalizationImportBaselineDocument
                    {
                        CatalogId = "test-catalog",
                        Entries = new List<LocalizationImportBaselineEntry>
                        {
                            new LocalizationImportBaselineEntry
                            {
                                SourceId = fixture.Snapshot.Sources.Single().SourceId,
                                TableId = fixture.Data.TableId,
                                SourceField = "textZhCn",
                                TargetLocale = "zh-CN",
                                KeyId = 1,
                                Key = "base.key",
                                BaseValue = "原始",
                                SourceRevision = fixture.Snapshot.Revision
                            }
                        }
                    }
                };
                var service = new LocalizationImportService(
                    fixture.Catalog,
                    () => new LubanProjectConfig(),
                    authoring,
                    baselines);
                var plan = service.CreatePlan(new LocalizationImportRequest(
                    "test-catalog",
                    fixture.Data.TableId,
                    "key",
                    fixture.Snapshot.Revision,
                    new[]
                    {
                        new LocalizationImportColumn("zh-CN", "textZhCn"),
                        new LocalizationImportColumn("en-US", "textEnUs")
                    }));

                Assert.IsTrue(plan.Entries.Any(entry => entry.KeyId == 1 &&
                    entry.KeyKind == LocalizationMergeKind.Conflict));
                Assert.IsTrue(plan.Entries.Any(entry => entry.Kind == LocalizationMergeKind.Add));
                Assert.Greater(plan.UnresolvedCount, 0);
                plan.ResolveAll(LocalizationConflictResolution.UseSource);
                Assert.IsTrue(service.Apply(plan).Succeeded);
                var mutation = authoring.LastImportMutation;
                Assert.AreEqual("base.key", mutation.Keys.Single(entry => entry.Id == 1).Key);
                Assert.IsTrue(mutation.LocaleValues["zh-CN"].Any(entry => entry.KeyId == 1));
                Assert.IsTrue(mutation.LocaleValues["en-US"].Any(entry => entry.KeyId == 1));

                baselines.Baseline = new LocalizationImportBaselineDocument
                {
                    CatalogId = "test-catalog",
                    Entries = new List<LocalizationImportBaselineEntry>
                    {
                        new LocalizationImportBaselineEntry
                        {
                            SourceId = fixture.Snapshot.Sources.Single().SourceId,
                            TableId = fixture.Data.TableId,
                            SourceField = "textZhCn",
                            TargetLocale = "zh-CN",
                            KeyId = 1,
                            Key = "asset.key",
                            BaseValue = "来源",
                            SourceRevision = fixture.Snapshot.Revision
                        }
                    }
                };
                var deleteFixture = CreateLocalizationFixture();
                var deleteService = new LocalizationImportService(
                    deleteFixture.Catalog,
                    () => new LubanProjectConfig(),
                    authoring,
                    baselines);
                var deletePlan = deleteService.CreatePlan(new LocalizationImportRequest(
                    "test-catalog",
                    deleteFixture.Data.TableId,
                    "key",
                    deleteFixture.Snapshot.Revision,
                    new[] { new LocalizationImportColumn("zh-CN", "textZhCn") }));
                Assert.IsTrue(deletePlan.Entries.Any(entry =>
                    entry.ValueKind == LocalizationMergeKind.DeleteCandidate));
                deletePlan.ResolveAll(LocalizationConflictResolution.UseSource);
                Assert.IsTrue(deleteService.Apply(deletePlan).Succeeded);
                Assert.IsFalse(authoring.LastImportMutation.LocaleValues["zh-CN"]
                    .Any(entry => entry.KeyId == 1));
                Assert.IsFalse(authoring.LastImportMutation.LocaleValues.ContainsKey("en-US"));
                Assert.IsTrue(authoring.LastImportMutation.Keys.Any(entry => entry.Id == 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(zhAsset);
                UnityEngine.Object.DestroyImmediate(enAsset);
            }
        }

        [Test]
        public void LocalizationEditorCatalog_WhenAssetSnapshotIsValid_ExposesPreviewLocaleAndEmptyText()
        {
            var catalogAsset = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            var localeAsset = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            try
            {
                var descriptor = new LocalizationLocaleDescriptor("zh-CN", "Assets/Test.zh-CN.asset");
                catalogAsset.Replace(
                    "test-catalog",
                    "zh-CN",
                    new[]
                    {
                        new LocalizationKeyEntry(1, "ui.start"),
                        new LocalizationKeyEntry(2, "ui.empty")
                    },
                    new[] { descriptor });
                localeAsset.Replace(
                    "zh-CN",
                    new[]
                    {
                        new LocalizationValueEntry(1, "开始"),
                        new LocalizationValueEntry(2, string.Empty)
                    },
                    1);
                var authoring = CreateAuthoringSnapshot(catalogAsset, descriptor, localeAsset);
                var catalog = new LocalizationEditorCatalog(new StubLocalizationAuthoringService(authoring));

                var snapshot = catalog.Refresh();

                Assert.AreEqual(authoring.Revision, snapshot.SourceRevision);
                Assert.AreEqual("zh-CN", snapshot.PreviewLocale);
                Assert.IsTrue(snapshot.Entries["ui.empty"].IsEmpty);
                Assert.IsFalse(snapshot.Entries["ui.empty"].IsMissing);
                Assert.IsTrue(catalog.TryGetText("ui.empty", out var text));
                Assert.AreEqual(string.Empty, text);
                Assert.IsFalse(catalog.TryGetText("missing", out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(localeAsset);
                UnityEngine.Object.DestroyImmediate(catalogAsset);
            }
        }

        [Test]
        public void LocalizationEditorCatalog_Search_UsesFixedKeyThenTextRanking()
        {
            var catalogAsset = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            var localeAsset = ScriptableObject.CreateInstance<LocalizationLocaleAsset>();
            try
            {
                var descriptor = new LocalizationLocaleDescriptor("zh-CN", "Assets/Test.zh-CN.asset");
                catalogAsset.Replace(
                    "test-catalog",
                    "zh-CN",
                    new[]
                    {
                        new LocalizationKeyEntry(1, "rain"),
                        new LocalizationKeyEntry(2, "rain.chapter"),
                        new LocalizationKeyEntry(3, "story.rain.middle"),
                        new LocalizationKeyEntry(4, "story.text.prefix"),
                        new LocalizationKeyEntry(5, "story.text.contains")
                    },
                    new[] { descriptor });
                localeAsset.Replace(
                    "zh-CN",
                    new[]
                    {
                        new LocalizationValueEntry(1, "精确"),
                        new LocalizationValueEntry(2, "前缀"),
                        new LocalizationValueEntry(3, "包含"),
                        new LocalizationValueEntry(4, "rain 开始"),
                        new LocalizationValueEntry(5, "夜里的 rain")
                    },
                    1);
                var catalog = new LocalizationEditorCatalog(new StubLocalizationAuthoringService(
                    CreateAuthoringSnapshot(catalogAsset, descriptor, localeAsset)));
                catalog.Refresh();

                var results = catalog.Search("rain");

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "rain",
                        "rain.chapter",
                        "story.rain.middle",
                        "story.text.prefix",
                        "story.text.contains"
                    },
                    results.Select(result => result.Key).ToArray());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(localeAsset);
                UnityEngine.Object.DestroyImmediate(catalogAsset);
            }
        }

        [Test]
        public void LocalizationEditorCatalog_WhenAssetSnapshotIsInvalid_ReturnsDiagnosticsWithoutEntries()
        {
            var authoring = new LocalizationAuthoringSnapshot(
                3,
                null,
                string.Empty,
                "zh-CN",
                null,
                new[]
                {
                    new LocalizationAuthoringDiagnostic(
                        LocalizationAuthoringDiagnosticSeverity.Error,
                        "catalog_not_bound",
                        "尚未绑定全局本地化 Catalog。")
                });
            var catalog = new LocalizationEditorCatalog(new StubLocalizationAuthoringService(authoring));

            var snapshot = catalog.Refresh();

            Assert.AreEqual(0, snapshot.Entries.Count);
            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == LocalizationCatalogDiagnosticSeverity.Error &&
                diagnostic.Message.Contains("尚未绑定")));
        }

        [Test]
        public void LocalizationAuthoringService_CreateCatalogAndKey_WritesOnlyPreviewLocale()
        {
            EnsureLocalizationTestFolder();
            var service = new LocalizationAuthoringService(EditorGlobalConfig.LoadOrCreate);

            var created = service.CreateCatalog(LocalizationTestFolder, "TestLocalization", "zh-CN");

            Assert.IsTrue(created.Succeeded, created.Message);
            Assert.IsTrue(created.Snapshot.IsValid);
            Assert.IsNotEmpty(EditorGlobalConfig.LoadOrCreate().Localization.CatalogAssetGuid);
            Assert.AreEqual("zh-CN", EditorGlobalConfig.LoadOrCreate().Localization.PreviewLocale);

            var keyResult = service.CreateKey("story.start", "zh-CN", "开始");
            Assert.IsTrue(keyResult.Succeeded, keyResult.Message);
            Assert.Greater(keyResult.KeyId, 0);
            Assert.IsTrue(keyResult.Snapshot.TryGetText(keyResult.KeyId, "zh-CN", out var text));
            Assert.AreEqual("开始", text);

            var localePath = $"{LocalizationTestFolder}/TestLocalization.en-US.asset";
            var localeResult = service.AddLocale(new LocalizationLocaleDraft(
                "en-US",
                localePath,
                localePath,
                "zh-CN"));
            Assert.IsTrue(localeResult.Succeeded, localeResult.Message);
            Assert.IsFalse(localeResult.Snapshot.TryGetText(keyResult.KeyId, "en-US", out _));
        }

        [Test]
        public void LocalizationAuthoringService_AssetsAreDirectlyReadableByRuntimeModule()
        {
            EnsureLocalizationTestFolder();
            var service = new LocalizationAuthoringService(EditorGlobalConfig.LoadOrCreate);
            var created = service.CreateCatalog(LocalizationTestFolder, "RuntimeCatalog", "zh-CN");
            var key = service.CreateKey("story.start", "zh-CN", "开始剧情");
            Assert.IsTrue(created.Succeeded && key.Succeeded, created.Message + key.Message);

            var snapshot = service.Refresh();
            var loader = new EditorLocalizationAssetLoader();
            loader.Add(snapshot.CatalogPath, snapshot.Catalog);
            foreach (var locale in snapshot.Locales.Values)
            {
                loader.Add(locale.Descriptor.ResourceLocation, locale.Asset);
            }

            var module = new LocalizationModule(loader);
            module.Startup();
            try
            {
                module.InitializeAsync(snapshot.CatalogPath, "zh-CN").GetAwaiter().GetResult();
                Assert.AreEqual("开始剧情", module.GetText("story.start"));
            }
            finally
            {
                module.Shutdown();
            }
        }

        [Test]
        public void LocalizationAuthoringService_InvalidMutations_DoNotWriteAssets()
        {
            EnsureLocalizationTestFolder();
            var service = new LocalizationAuthoringService(EditorGlobalConfig.LoadOrCreate);
            Assert.IsTrue(service.CreateCatalog(
                LocalizationTestFolder,
                "InvalidMutationCatalog",
                "zh-CN").Succeeded);
            var key = service.CreateKey("story.start", "zh-CN", "开始");
            var localePath = $"{LocalizationTestFolder}/InvalidMutationCatalog.en-US.asset";
            Assert.IsTrue(service.AddLocale(new LocalizationLocaleDraft(
                "en-US",
                localePath,
                localePath,
                "zh-CN")).Succeeded);
            var before = service.Refresh();

            var duplicate = service.CreateKey("story.start", "zh-CN", "重复");
            var cycle = service.SetLocaleDescriptor(
                "zh-CN",
                before.Locales["zh-CN"].Descriptor.ResourceLocation,
                "en-US");
            var removeDefault = service.RemoveLocale("zh-CN");
            var after = service.Refresh();

            Assert.IsFalse(duplicate.Succeeded);
            Assert.IsFalse(cycle.Succeeded);
            Assert.IsFalse(removeDefault.Succeeded);
            Assert.AreEqual(before.Entries.Count, after.Entries.Count);
            Assert.AreEqual(string.Empty, after.Locales["zh-CN"].Descriptor.FallbackLocale);
            Assert.AreEqual(key.KeyId, after.Entries.Single().KeyId);
        }

        [Test]
        public void LocalizationAuthoringService_CatalogGuidSurvivesMoveAndTextSupportsUndo()
        {
            EnsureLocalizationTestFolder();
            var service = new LocalizationAuthoringService(EditorGlobalConfig.LoadOrCreate);
            var created = service.CreateCatalog(LocalizationTestFolder, "MovableCatalog", "zh-CN");
            var key = service.CreateKey("story.start", "zh-CN", "开始");
            Assert.IsTrue(created.Succeeded && key.Succeeded);

            var movedPath = $"{LocalizationTestFolder}/MovedCatalog.asset";
            Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(created.Snapshot.CatalogPath, movedPath));
            var moved = service.Refresh();
            Assert.AreEqual(movedPath, moved.CatalogPath);

            var changed = service.SetText(key.KeyId, "zh-CN", "继续");
            Assert.IsTrue(changed.Succeeded, changed.Message);
            Assert.IsTrue(changed.Snapshot.TryGetText(key.KeyId, "zh-CN", out var changedText));
            Assert.AreEqual("继续", changedText);

            Undo.PerformUndo();
            var undone = service.Refresh();
            Assert.IsTrue(undone.TryGetText(key.KeyId, "zh-CN", out var undoneText));
            Assert.AreEqual("开始", undoneText);
        }

        private static byte[] ReadIfExists(string path)
        {
            return IOFile.Exists(path) ? IOFile.ReadAllBytes(path) : null;
        }

        private static void EnsureLocalizationTestFolder()
        {
            const string parent = "Assets/GameDeveloperKit/Tests/Editor";
            if (AssetDatabase.IsValidFolder(LocalizationTestFolder) is false)
            {
                AssetDatabase.CreateFolder(parent, "TempLocalizationAuthoring");
            }
        }

        private static object InvokeNonPublic(object target, string methodName, params object[] arguments)
        {
            Assert.IsNotNull(target);
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            return method.Invoke(target, arguments ?? Array.Empty<object>());
        }

        private static void Restore(string path, byte[] contents)
        {
            if (contents == null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            IOFile.WriteAllBytes(path, contents);
        }

        private static void DeleteSettingsFiles()
        {
            IOFile.Delete(EditorGlobalConfig.SettingsPath);
            IOFile.Delete(EditorUserConfig.SettingsPath);
            IOFile.Delete(LubanEditorSettings.SettingsPath);
            IOFile.Delete(LocalizationEditorSettings.SettingsPath);
            IOFile.Delete(CatalogSettings.SettingsPath);
        }

        private static void ResetInstances()
        {
            EditorGlobalConfig.ResetInstance();
            EditorUserConfig.ResetInstance();
        }

        private static void SaveLegacyLocalization(string previewLocale, string previewPackGuid)
        {
            SaveSerializedSettings<LocalizationEditorSettings>(
                LocalizationEditorSettings.SettingsPath,
                serialized =>
                {
                    serialized.FindProperty("m_PreviewLocale").stringValue = previewLocale;
                    serialized.FindProperty("m_PreviewPackGuid").stringValue = previewPackGuid;
                });
        }

        private static void SaveLegacyStoryMedia(string previewLocale)
        {
            SaveSerializedSettings<CatalogSettings>(
                CatalogSettings.SettingsPath,
                serialized => serialized.FindProperty("m_PreviewLocale").stringValue = previewLocale);
        }

        private static void SaveLegacyLuban(string releasePath)
        {
            SaveSerializedSettings<LubanEditorSettings>(
                LubanEditorSettings.SettingsPath,
                serialized => serialized.FindProperty("m_ReleasePath").stringValue = releasePath);
        }

        private static void SaveVersionedProjectConfig(int version, string previewLocale)
        {
            var config = ScriptableObject.CreateInstance<EditorGlobalConfig>();
            config.EnsureDefaults();
            config.Localization.PreviewLocale = previewLocale;
            SaveSerializedSettings(
                config,
                EditorGlobalConfig.SettingsPath,
                serialized => serialized.FindProperty("m_Version").intValue = version);
        }

        private static void SaveVersionedUserConfig(int version, string lubanDllPath)
        {
            var config = ScriptableObject.CreateInstance<EditorUserConfig>();
            config.LubanDllPath = lubanDllPath;
            SaveSerializedSettings(
                config,
                EditorUserConfig.SettingsPath,
                serialized => serialized.FindProperty("m_Version").intValue = version);
        }

        private static void SaveSerializedSettings<T>(string path, Action<SerializedObject> configure)
            where T : ScriptableObject
        {
            SaveSerializedSettings(ScriptableObject.CreateInstance<T>(), path, configure);
        }

        private static void SaveSerializedSettings(
            ScriptableObject settings,
            string path,
            Action<SerializedObject> configure)
        {
            var serialized = new SerializedObject(settings);
            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            InternalEditorUtility.SaveToSerializedFileAndForget(
                new UnityEngine.Object[] { settings },
                path,
                true);
            UnityEngine.Object.DestroyImmediate(settings);
        }

        private static LocalizationTableImportConfig CreateLocalizationConfig()
        {
            return new LocalizationTableImportConfig
            {
                TableId = "DataTables/Datas/localization.xlsx#Localization#TbLocalization",
                KeyField = "key",
                PreviewField = "textZhCn"
            };
        }

        private static LocalizationAuthoringSnapshot CreateAuthoringSnapshot(
            LocalizationCatalogAsset catalog,
            LocalizationLocaleDescriptor descriptor,
            LocalizationLocaleAsset locale,
            long revision = 7)
        {
            return new LocalizationAuthoringSnapshot(
                revision,
                catalog,
                "Assets/TestCatalog.asset",
                descriptor.Locale,
                new[]
                {
                    new LocalizationAuthoringLocale(descriptor, locale, descriptor.ResourceLocation)
                },
                Array.Empty<LocalizationAuthoringDiagnostic>());
        }

        private static Dictionary<string, string> Values(string key, string zhCn, string enUs)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["key"] = key,
                ["textZhCn"] = zhCn,
                ["textEnUs"] = enUs
            };
        }

        private static LocalizationFixture CreateLocalizationFixture(params LubanTableRow[] rows)
        {
            const string tableId = "DataTables/Datas/localization.xlsx#Localization#TbLocalization";
            var table = new LubanTableDescriptor(
                tableId,
                "DataTables/Datas/localization.xlsx",
                "Localization",
                "TbLocalization",
                new[]
                {
                    new LubanFieldDescriptor("key", "string", string.Empty, 2),
                    new LubanFieldDescriptor("textZhCn", "string", string.Empty, 3),
                    new LubanFieldDescriptor("textEnUs", "string", string.Empty, 4)
                });
            var data = new LubanTableData(tableId, rows);
            var snapshot = new LubanSourceSnapshot(
                1,
                new[]
                {
                    new LubanSourceDescriptor(
                        "DataTables/Datas/localization.xlsx",
                        "localization.xlsx",
                        1,
                        new[] { table })
                },
                Array.Empty<LubanDiagnostic>());
            return new LocalizationFixture(snapshot, new StubLubanSourceCatalog(snapshot, data));
        }

        private readonly struct LocalizationFixture
        {
            public LocalizationFixture(LubanSourceSnapshot snapshot, ILubanSourceCatalog catalog)
            {
                Snapshot = snapshot;
                Catalog = catalog;
            }

            public LubanSourceSnapshot Snapshot { get; }

            public ILubanSourceCatalog Catalog { get; }

            public LubanTableData Data => ((StubLubanSourceCatalog)Catalog).Data;
        }

        private sealed class StubLubanSourceCatalog : ILubanSourceCatalog
        {
            private readonly LubanTableData m_Data;

            public StubLubanSourceCatalog(LubanSourceSnapshot snapshot, LubanTableData data)
            {
                Snapshot = snapshot;
                m_Data = data;
            }

            public LubanSourceSnapshot Snapshot { get; set; }

            public LubanTableData Data => m_Data;

            public LubanSourceSnapshot Refresh(LubanProjectConfig config)
            {
                return Snapshot;
            }

            public bool TryReadTable(string tableId, out LubanTableData data, out LubanDiagnostic diagnostic)
            {
                diagnostic = null;
                data = string.Equals(tableId, m_Data.TableId, StringComparison.Ordinal) ? m_Data : null;
                if (data != null)
                {
                    return true;
                }

                diagnostic = new LubanDiagnostic(LubanDiagnosticSeverity.Error, "missing table", tableId: tableId);
                return false;
            }
        }

        private sealed class StubLocalizationAuthoringService : ILocalizationAuthoringService
        {
            public StubLocalizationAuthoringService(LocalizationAuthoringSnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public LocalizationAuthoringSnapshot Snapshot { get; set; }

            public int ApplyImportCallCount { get; private set; }

            public LocalizationImportAssetMutation LastImportMutation { get; private set; }

            public LocalizationAuthoringSnapshot Refresh() => Snapshot;

            public LocalizationMutationResult CreateCatalog(string folderPath, string catalogName, string initialLocale) =>
                throw new NotSupportedException();

            public LocalizationMutationResult BindCatalog(LocalizationCatalogAsset catalog) =>
                throw new NotSupportedException();

            public LocalizationMutationResult CreateKey(string key, string locale, string value) =>
                throw new NotSupportedException();

            public LocalizationMutationResult RenameKey(long keyId, string newKey) =>
                throw new NotSupportedException();

            public LocalizationMutationResult RemoveKey(long keyId) =>
                throw new NotSupportedException();

            public LocalizationMutationResult SetText(long keyId, string locale, string value) =>
                throw new NotSupportedException();

            public LocalizationMutationResult RemoveText(long keyId, string locale) =>
                throw new NotSupportedException();

            public LocalizationMutationResult AddLocale(LocalizationLocaleDraft draft) =>
                throw new NotSupportedException();

            public LocalizationMutationResult RemoveLocale(string locale) =>
                throw new NotSupportedException();

            public LocalizationMutationResult SetDefaultLocale(string locale) =>
                throw new NotSupportedException();

            public LocalizationMutationResult SetLocaleDescriptor(
                string locale,
                string resourceLocation,
                string fallbackLocale) => throw new NotSupportedException();

            public LocalizationMutationResult ApplyImport(LocalizationImportAssetMutation mutation)
            {
                ApplyImportCallCount++;
                LastImportMutation = mutation;
                return LocalizationMutationResult.Success(Snapshot);
            }

            public IReadOnlyList<string> FindKeyUsages(string key) => Array.Empty<string>();
        }

        private sealed class RecordingBaselineStore : ILocalizationImportBaselineStore
        {
            public bool ThrowOnWrite { get; set; }

            public string StoredContent { get; set; }

            public int RestoreCallCount { get; private set; }

            public LocalizationImportBaselineDocument Baseline { get; set; }

            public string GetPath(string catalogId) =>
                $"ProjectSettings/GameDeveloperKit/LocalizationImports/{catalogId}.json";

            public LocalizationImportBaselineLoadResult Load(string catalogId)
            {
                return new LocalizationImportBaselineLoadResult(
                    Baseline ?? new LocalizationImportBaselineDocument { CatalogId = catalogId },
                    Array.Empty<LocalizationImportDiagnostic>());
            }

            public string Serialize(LocalizationImportBaselineDocument document) => document.CatalogId;

            public LocalizationImportBaselineFileBackup Capture(string path)
            {
                return StoredContent == null
                    ? new LocalizationImportBaselineFileBackup(false, null)
                    : new LocalizationImportBaselineFileBackup(true, Encoding.UTF8.GetBytes(StoredContent));
            }

            public void Write(string path, string content)
            {
                if (ThrowOnWrite)
                {
                    throw new IOException("baseline write failed");
                }

                StoredContent = content;
            }

            public void Restore(string path, LocalizationImportBaselineFileBackup backup)
            {
                RestoreCallCount++;
                StoredContent = backup.Existed
                    ? Encoding.UTF8.GetString(backup.Bytes ?? Array.Empty<byte>())
                    : null;
            }
        }

        private sealed class StubLocalizationImportService : ILocalizationImportService
        {
            private readonly LubanSourceSnapshot m_Snapshot;

            public StubLocalizationImportService(LubanSourceSnapshot snapshot)
            {
                m_Snapshot = snapshot;
            }

            public LubanSourceSnapshot RefreshSource() => m_Snapshot;

            public LocalizationImportPlan Plan { get; set; }

            public LocalizationImportRequest LastRequest { get; private set; }

            public LocalizationMutationResult ApplyResult { get; set; } =
                LocalizationMutationResult.Failure("not used");

            public int ApplyCallCount { get; private set; }

            public LocalizationImportPlan CreatePlan(LocalizationImportRequest request)
            {
                LastRequest = request;
                return Plan;
            }

            public LocalizationMutationResult Apply(LocalizationImportPlan plan)
            {
                ApplyCallCount++;
                return ApplyResult;
            }
        }

        private sealed class EditorLocalizationAssetLoader : ILocalizationAssetLoader
        {
            private readonly Dictionary<string, UnityEngine.Object> m_Assets =
                new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

            public void Add(string location, UnityEngine.Object asset)
            {
                m_Assets.Add(location, asset);
            }

            public UniTask<LocalizationAssetLease> LoadAsync(string location)
            {
                if (m_Assets.TryGetValue(location, out var asset) is false)
                {
                    throw new InvalidOperationException("Missing test localization asset: " + location);
                }

                return UniTask.FromResult(new LocalizationAssetLease(
                    location,
                    asset,
                    () => UniTask.CompletedTask));
            }
        }

    }
}
