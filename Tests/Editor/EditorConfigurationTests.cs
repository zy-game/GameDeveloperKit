using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.LocalizationEditor;
using GameDeveloperKit.Localization;
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
        private byte[] m_ProjectConfigBackup;
        private byte[] m_UserConfigBackup;
        private byte[] m_LegacyLubanBackup;
        private byte[] m_LegacyLocalizationBackup;
        private byte[] m_LegacyStoryMediaBackup;

        [SetUp]
        public void SetUp()
        {
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
            Assert.AreEqual(LocalizationProjectConfig.DefaultKeyField, project.Localization.KeyField);
            Assert.AreEqual(LocalizationProjectConfig.DefaultPreviewLocale, project.Localization.PreviewLocale);
            Assert.AreEqual(EditorUserConfig.DefaultLubanDllPath, user.LubanDllPath);
            Assert.IsTrue(IOFile.Exists(EditorGlobalConfig.SettingsPath));
            Assert.IsTrue(IOFile.Exists(EditorUserConfig.SettingsPath));
        }

        [Test]
        public void LoadOrCreate_WhenLegacySettingsExist_MigratesPreferredValuesWithoutChangingLegacyFiles()
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
            Assert.AreEqual("ja-JP", project.Localization.PreviewLocale);
            Assert.AreEqual("E:/Tools/Luban/Luban.dll", user.LubanDllPath);
            Assert.IsNull(new SerializedObject(project).FindProperty("m_PreviewPackGuid"));
            CollectionAssert.AreEqual(localizationBytes, IOFile.ReadAllBytes(LocalizationEditorSettings.SettingsPath));
            CollectionAssert.AreEqual(storyBytes, IOFile.ReadAllBytes(CatalogSettings.SettingsPath));
            CollectionAssert.AreEqual(lubanBytes, IOFile.ReadAllBytes(LubanEditorSettings.SettingsPath));
        }

        [Test]
        public void LoadOrCreate_WhenOnlyStoryLocaleExists_UsesStoryAsLegacyFallback()
        {
            SaveLegacyStoryMedia("ko-KR");

            var project = EditorGlobalConfig.LoadOrCreate();

            Assert.AreEqual("ko-KR", project.Localization.PreviewLocale);
        }

        [Test]
        public void LoadOrCreate_WhenMigrationAlreadyCompleted_DoesNotReadChangedLegacyValuesAgain()
        {
            SaveLegacyLocalization("ja-JP", string.Empty);
            SaveLegacyLuban(@"E:\Tools\Luban\First.dll");
            Assert.AreEqual("ja-JP", EditorGlobalConfig.LoadOrCreate().Localization.PreviewLocale);
            Assert.AreEqual("E:/Tools/Luban/First.dll", EditorUserConfig.LoadOrCreate().LubanDllPath);

            SaveLegacyLocalization("ko-KR", string.Empty);
            SaveLegacyLuban(@"E:\Tools\Luban\Second.dll");
            ResetInstances();

            Assert.AreEqual("ja-JP", EditorGlobalConfig.LoadOrCreate().Localization.PreviewLocale);
            Assert.AreEqual("E:/Tools/Luban/First.dll", EditorUserConfig.LoadOrCreate().LubanDllPath);
        }

        [Test]
        public void LoadOrCreate_WhenNewValuesAreNonDefault_DoesNotOverwriteThemDuringMigration()
        {
            SaveVersionedProjectConfig(EditorGlobalConfig.CurrentVersion - 1, "fr-FR");
            SaveVersionedUserConfig(EditorUserConfig.CurrentVersion - 1, @"E:\Current\Luban.dll");
            SaveLegacyLocalization("ja-JP", string.Empty);
            SaveLegacyLuban(@"E:\Legacy\Luban.dll");

            var project = EditorGlobalConfig.LoadOrCreate();
            var user = EditorUserConfig.LoadOrCreate();

            Assert.AreEqual("fr-FR", project.Localization.PreviewLocale);
            Assert.AreEqual("E:/Current/Luban.dll", user.LubanDllPath);
            Assert.AreEqual(EditorGlobalConfig.CurrentVersion, project.Version);
            Assert.AreEqual(EditorUserConfig.CurrentVersion, user.Version);
        }

        [Test]
        public void LoadOrCreate_WhenLegacyFilesAreDamaged_KeepsDefaultsAndCompletesMigration()
        {
            Directory.CreateDirectory("ProjectSettings");
            IOFile.WriteAllText(LocalizationEditorSettings.SettingsPath, "not a serialized Unity object");
            IOFile.WriteAllText(LubanEditorSettings.SettingsPath, "not a serialized Unity object");
            LogAssert.Expect(LogType.Warning, new Regex("读取旧 Editor 配置失败.*GameDeveloperKitLocalizationSettings"));
            LogAssert.Expect(LogType.Warning, new Regex("读取旧 Editor 配置失败.*GameDeveloperKitLubanEditorSettings"));

            var project = EditorGlobalConfig.LoadOrCreate();
            var user = EditorUserConfig.LoadOrCreate();

            Assert.AreEqual(LocalizationProjectConfig.DefaultPreviewLocale, project.Localization.PreviewLocale);
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
        public void StoryMediaPreviewLocale_UsesGlobalConfigAndIgnoresLegacyField()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Localization.PreviewLocale = "ja-JP";
            project.Save();
            var settings = ScriptableObject.CreateInstance<CatalogSettings>();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_PreviewLocale").stringValue = "ko-KR";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("ja-JP", settings.PreviewLocale);

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
            project.Localization.TableId = " localization ";
            project.Localization.KeyField = " id ";
            project.Localization.PreviewLocale = " zh-CN ";
            project.Save();

            EditorGlobalConfig.ResetInstance();
            var reloaded = EditorGlobalConfig.LoadOrCreate();

            Assert.AreEqual("Tables/Excel", reloaded.Luban.TableDirectory);
            Assert.AreEqual("Assets/Generated/Code", reloaded.Luban.GeneratedCodeDirectory);
            Assert.AreEqual("Assets/Generated/Data", reloaded.Luban.GeneratedDataDirectory);
            Assert.AreEqual("Game.Config", reloaded.Luban.CodeNamespace);
            Assert.AreEqual("localization", reloaded.Localization.TableId);
            Assert.AreEqual("id", reloaded.Localization.KeyField);
            Assert.AreEqual("zh-CN", reloaded.Localization.PreviewLocale);
        }

        [TestCase(@"C:\Tables")]
        [TestCase("../outside")]
        public void Save_WhenProjectPathIsUnsafe_RejectsWithoutOverwritingFile(string unsafePath)
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Luban.TableDirectory = "Tables/Baseline";
            project.Save();
            project.Luban.TableDirectory = unsafePath;

            Assert.Throws<ArgumentException>(() => project.Save());

            EditorGlobalConfig.ResetInstance();
            Assert.AreEqual("Tables/Baseline", EditorGlobalConfig.LoadOrCreate().Luban.TableDirectory);
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
        public void TryValidate_WhenLocaleMappingsContainEmptyEntries_RemovesOnlyEmptyEntries()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Localization.LocaleFields.Add(new LocalizationLocaleField());
            project.Localization.LocaleFields.Add(new LocalizationLocaleField
            {
                Locale = " zh-CN ",
                FieldName = " text_cn "
            });
            project.Localization.LocaleFields.Add(null);

            Assert.IsTrue(project.TryValidate(out var error), error);
            Assert.AreEqual(1, project.Localization.LocaleFields.Count);
            Assert.AreEqual("zh-CN", project.Localization.LocaleFields[0].Locale);
            Assert.AreEqual("text_cn", project.Localization.LocaleFields[0].FieldName);
        }

        [Test]
        public void Save_WhenLocaleIsDuplicated_RejectsWithoutOverwritingFile()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Localization.TableId = "baseline";
            project.Save();
            project.Localization.TableId = "invalid";
            project.Localization.LocaleFields.Add(new LocalizationLocaleField
            {
                Locale = "zh-CN",
                FieldName = "text_cn"
            });
            project.Localization.LocaleFields.Add(new LocalizationLocaleField
            {
                Locale = "zh-CN",
                FieldName = "text_cn_duplicate"
            });

            var exception = Assert.Throws<ArgumentException>(() => project.Save());
            StringAssert.Contains("本地化语言重复", exception.Message);

            EditorGlobalConfig.ResetInstance();
            Assert.AreEqual("baseline", EditorGlobalConfig.LoadOrCreate().Localization.TableId);
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
        public void ConfigurationContracts_ExposeInlinePanelAndCacheRootWithoutSettingsProvider()
        {
            var cacheRootExisted = Directory.Exists(EditorGlobalConfig.CacheRoot);
            var assembly = typeof(EditorGlobalConfig).Assembly;
            var panel = new EditorConfigurationPanel();

            Assert.IsFalse(assembly.GetTypes().Any(type =>
                type.Name == "EditorConfigurationSettingsProvider" &&
                typeof(SettingsProvider).IsAssignableFrom(type)));
            Assert.NotNull(panel.Q<TextField>("table-directory-field"));
            Assert.NotNull(panel.Q<TextField>("luban-dll-path-field"));
            Assert.NotNull(panel.Q<VisualElement>("localization-config-content"));
            Assert.AreSame(
                panel.Q<TextField>("table-directory-field").parent,
                panel.Q<TextField>("luban-dll-path-field").parent);
            Assert.AreSame(
                panel.Q<DropdownField>("localization-table-field").parent,
                panel.Q<DropdownField>("localization-preview-locale-field").parent);
            Assert.AreEqual("Library/GameDeveloperKit/EditorConfig", EditorGlobalConfig.CacheRoot);
            Assert.AreEqual(cacheRootExisted, Directory.Exists(EditorGlobalConfig.CacheRoot));
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

                Assert.NotNull(window.rootVisualElement.Q<VisualElement>("configuration-toolbar"));
                Assert.NotNull(window.rootVisualElement.Q<Button>("global-settings-toggle"));
                Assert.NotNull(window.rootVisualElement.Q<VisualElement>("configuration-source-table"));
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>("global-settings-view"));
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
        public void LocalizationTableContract_WhenFieldOrPreviewMappingIsMissing_ReturnsErrors()
        {
            var fixture = CreateLocalizationFixture(new LubanTableRow(5, Values("ui.start", "开始", "Start")));
            var config = CreateLocalizationConfig();
            config.LocaleFields[0].FieldName = "missingField";
            config.PreviewLocale = "ja-JP";

            var result = LocalizationTableContractValidator.Validate(fixture.Snapshot, fixture.Catalog, config);

            Assert.IsFalse(result.IsValid);
            var messages = string.Join("|", result.Diagnostics.Select(item => item.Message));
            StringAssert.Contains("对应字段不存在", messages);
            StringAssert.Contains("预览语言尚未配置字段映射：ja-JP", messages);
        }

        [Test]
        public void LocalizationEditorCatalog_WhenContractIsValid_ExposesLocalesAndEmptyTranslation()
        {
            var fixture = CreateLocalizationFixture(
                new LubanTableRow(5, Values("ui.start", "开始", "Start")),
                new LubanTableRow(6, Values("ui.empty", string.Empty, string.Empty)));
            var config = CreateLocalizationConfig();
            var catalog = new LocalizationEditorCatalog(
                fixture.Catalog,
                () => new LubanProjectConfig(),
                () => config);

            var snapshot = catalog.Refresh();

            Assert.AreEqual(fixture.Snapshot.Revision, snapshot.SourceRevision);
            CollectionAssert.AreEquivalent(new[] { "zh-CN", "en-US" }, snapshot.Locales);
            Assert.IsTrue(snapshot.Entries["ui.empty"].IsEmpty("zh-CN"));
            Assert.IsTrue(catalog.TryGetText("ui.empty", "zh-CN", out var text));
            Assert.AreEqual(string.Empty, text);
            Assert.IsFalse(catalog.TryGetText("missing", "zh-CN", out _));
        }

        [Test]
        public void LocalizationEditorCatalog_Search_UsesFixedKeyThenTextRanking()
        {
            var fixture = CreateLocalizationFixture(
                new LubanTableRow(5, Values("rain", "精确", "exact")),
                new LubanTableRow(6, Values("rain.chapter", "前缀", "prefix")),
                new LubanTableRow(7, Values("story.rain.middle", "包含", "contains")),
                new LubanTableRow(8, Values("story.text.prefix", "rain 开始", "text prefix")),
                new LubanTableRow(9, Values("story.text.contains", "夜里的 rain", "text contains")));
            var config = CreateLocalizationConfig();
            var catalog = new LocalizationEditorCatalog(
                fixture.Catalog,
                () => new LubanProjectConfig(),
                () => config);
            catalog.Refresh();

            var results = catalog.Search("rain", "zh-CN");

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

        [Test]
        public void LocalizationEditorCatalog_WhenContractIsInvalid_ReturnsDiagnosticsWithoutEntries()
        {
            var fixture = CreateLocalizationFixture(new LubanTableRow(5, Values("ui.start", "开始", "Start")));
            var config = CreateLocalizationConfig();
            config.TableId = "missing-table";
            var catalog = new LocalizationEditorCatalog(
                fixture.Catalog,
                () => new LubanProjectConfig(),
                () => config);

            var snapshot = catalog.Refresh();

            Assert.AreEqual(0, snapshot.Entries.Count);
            Assert.IsTrue(snapshot.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == LocalizationCatalogDiagnosticSeverity.Error &&
                diagnostic.Message.Contains("本地化表不存在")));
        }

        [Test]
        public void LocalizationPackExporter_WhenTableIsValid_WritesRuntimePacksForEveryLocale()
        {
            var fixture = CreateLocalizationFixture(
                new LubanTableRow(5, Values("ui.start", "开始", "Start")),
                new LubanTableRow(6, Values("ui.empty", string.Empty, string.Empty)));
            var output = Path.Combine(Path.GetTempPath(), "gdk-localization-export", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            try
            {
                var result = LocalizationPackExporter.Shared.Export(
                    fixture.Data,
                    CreateLocalizationConfig(),
                    output);

                Assert.IsTrue(result.Success, string.Join("|", result.Diagnostics.Select(item => item.Message)));
                Assert.AreEqual(2, result.Files.Count);
                var zhPath = Path.Combine(output, "Localization", "zh-CN.json");
                var enPath = Path.Combine(output, "Localization", "en-US.json");
                Assert.IsTrue(IOFile.Exists(zhPath));
                Assert.IsTrue(IOFile.Exists(enPath));
                var pack = LocalizationPack.Parse("zh-CN", IOFile.ReadAllText(zhPath), zhPath);
                Assert.AreEqual("开始", pack.Entries["ui.start"]);
                Assert.AreEqual(string.Empty, pack.Entries["ui.empty"]);
                pack.Release();
            }
            finally
            {
                Directory.Delete(output, true);
            }
        }

        [Test]
        public void LocalizationPackExporter_WhenKeyIsDuplicated_FailsWithoutPublishingLocalizationDirectory()
        {
            var fixture = CreateLocalizationFixture(
                new LubanTableRow(5, Values("ui.start", "开始", "Start")),
                new LubanTableRow(8, Values(" ui.start ", "继续", "Continue")));
            var output = Path.Combine(Path.GetTempPath(), "gdk-localization-export", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            try
            {
                var result = LocalizationPackExporter.Shared.Export(
                    fixture.Data,
                    CreateLocalizationConfig(),
                    output);

                Assert.IsFalse(result.Success);
                Assert.IsFalse(Directory.Exists(Path.Combine(output, "Localization")));
                StringAssert.Contains(
                    "本地化 Key 重复",
                    string.Join("|", result.Diagnostics.Select(item => item.Message)));
            }
            finally
            {
                Directory.Delete(output, true);
            }
        }

        private static byte[] ReadIfExists(string path)
        {
            return IOFile.Exists(path) ? IOFile.ReadAllBytes(path) : null;
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

        private static LocalizationProjectConfig CreateLocalizationConfig()
        {
            var config = new LocalizationProjectConfig
            {
                TableId = "DataTables/Datas/localization.xlsx#Localization#TbLocalization",
                KeyField = "key",
                PreviewLocale = "zh-CN"
            };
            config.EnsureDefaults();
            config.LocaleFields.Add(new LocalizationLocaleField
            {
                Locale = "zh-CN",
                FieldName = "textZhCn"
            });
            config.LocaleFields.Add(new LocalizationLocaleField
            {
                Locale = "en-US",
                FieldName = "textEnUs"
            });
            return config;
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
            private readonly LubanSourceSnapshot m_Snapshot;
            private readonly LubanTableData m_Data;

            public StubLubanSourceCatalog(LubanSourceSnapshot snapshot, LubanTableData data)
            {
                m_Snapshot = snapshot;
                m_Data = data;
            }

            public LubanTableData Data => m_Data;

            public LubanSourceSnapshot Refresh(LubanProjectConfig config)
            {
                return m_Snapshot;
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

    }
}
