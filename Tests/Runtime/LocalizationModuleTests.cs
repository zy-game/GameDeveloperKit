using System;
using System.Collections.Generic;
using GameDeveloperKit.Localization;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class LocalizationModuleTests : RuntimeTestBase
    {
        private static string FixturePath => FrameworkAssetPath("Tests/Runtime/LocalizationPackFixture.json");

        [TearDown]
        public void TearDown()
        {
            TryUnregister<LocalizationModule>();
            TryUnregister<ResourceModule>();
            TryUnregister<OperationModule>();
        }

        [SetUp]
        public void SetUp()
        {
            App.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void Startup_WhenModuleStarts_HasEmptySnapshot()
        {
            var module = new LocalizationModule();

            module.Startup();
            var snapshot = module.Snapshot();

            Assert.IsNull(snapshot.CurrentLocale);
            Assert.IsNull(snapshot.FallbackLocale);
            Assert.AreEqual(0, snapshot.LoadedLocales.Count);
            Assert.AreEqual(0, snapshot.MissingEntries.Count);
        }

        [Test]
        public void RegisterPack_WhenCurrentLocaleHasKey_ReturnsCurrentText()
        {
            var module = CreateStartedModule();
            module.RegisterPack(LocalizationPack.FromDictionary("zh-CN", new Dictionary<string, string>
            {
                ["ui.start"] = "Start CN",
            }));

            module.SetLocale("zh-CN");

            Assert.IsTrue(module.HasText("ui.start"));
            Assert.AreEqual("Start CN", module.GetText("ui.start"));
        }

        [Test]
        public void GetText_WhenCurrentMissing_UsesFallbackAndRecordsMissing()
        {
            var module = CreateStartedModule();
            module.RegisterPack(LocalizationPack.FromDictionary("zh-CN", new Dictionary<string, string>
            {
                ["ui.start"] = "Start CN",
            }));
            module.RegisterPack(LocalizationPack.FromDictionary("en-US", new Dictionary<string, string>()));

            module.SetFallbackLocale("zh-CN");
            module.SetLocale("en-US");

            Assert.AreEqual("Start CN", module.GetText("ui.start"));
            var snapshot = module.Snapshot();
            Assert.AreEqual(1, snapshot.MissingEntries.Count);
            AssertMissing(snapshot.MissingEntries, "en-US", "ui.start");
        }

        [Test]
        public void GetText_WhenCurrentAndFallbackMissing_ReturnsKeyAndRecordsMissing()
        {
            var module = CreateStartedModule();

            module.SetFallbackLocale("zh-CN");
            module.SetLocale("en-US");

            Assert.AreEqual("ui.missing", module.GetText("ui.missing"));
            var snapshot = module.Snapshot();
            Assert.AreEqual(2, snapshot.MissingEntries.Count);
            AssertMissing(snapshot.MissingEntries, "en-US", "ui.missing");
            AssertMissing(snapshot.MissingEntries, "zh-CN", "ui.missing");
        }

        [Test]
        public void Format_WhenArgumentsMatch_ReturnsFormattedText()
        {
            var module = CreateStartedModule();
            module.RegisterPack(LocalizationPack.FromDictionary("en-US", new Dictionary<string, string>
            {
                ["battle.damage"] = "Damage {0}",
            }));

            module.SetLocale("en-US");

            Assert.AreEqual("Damage 120", module.Format("battle.damage", 120));
        }

        [Test]
        public void Format_WhenArgumentsMismatch_ThrowsGameException()
        {
            var module = CreateStartedModule();
            module.RegisterPack(LocalizationPack.FromDictionary("en-US", new Dictionary<string, string>
            {
                ["battle.damage"] = "Damage {0}",
            }));

            module.SetLocale("en-US");

            Assert.Throws<GameException>(() => module.Format("battle.damage"));
        }

        [Test]
        public void SetLocale_WhenChanged_NotifiesOnceAndNoOpsForSameLocale()
        {
            var module = CreateStartedModule();
            var count = 0;
            var previous = string.Empty;
            var current = string.Empty;
            module.SetLocale("zh-CN");
            module.LocaleChanged += args =>
            {
                count++;
                previous = args.PreviousLocale;
                current = args.CurrentLocale;
            };

            module.SetLocale("en-US");
            module.SetLocale("en-US");

            Assert.AreEqual(1, count);
            Assert.AreEqual("zh-CN", previous);
            Assert.AreEqual("en-US", current);
        }

        [Test]
        public void RegisterPack_WhenSameLocale_ReplacesOldPack()
        {
            var module = CreateStartedModule();
            var oldPack = LocalizationPack.FromDictionary("en-US", new Dictionary<string, string>
            {
                ["ui.start"] = "Old",
            });
            var newPack = LocalizationPack.FromDictionary("en-US", new Dictionary<string, string>
            {
                ["ui.start"] = "New",
            });

            module.RegisterPack(oldPack);
            module.RegisterPack(newPack);
            module.SetLocale("en-US");

            Assert.AreEqual("New", module.GetText("ui.start"));
            Assert.IsNull(oldPack.Locale);
        }

        [Test]
        public void Shutdown_WhenCalled_ClearsPacksLocaleMissingAndEvents()
        {
            var module = CreateStartedModule();
            var count = 0;
            module.LocaleChanged += _ => count++;
            module.RegisterPack(LocalizationPack.FromDictionary("en-US", new Dictionary<string, string>
            {
                ["ui.start"] = "Start",
            }));
            module.SetLocale("en-US");
            module.GetText("ui.missing");

            module.Shutdown();
            module.SetLocale("zh-CN");
            var snapshot = module.Snapshot();

            Assert.AreEqual(0, snapshot.LoadedLocales.Count);
            Assert.AreEqual(0, snapshot.MissingEntries.Count);
            Assert.AreEqual("zh-CN", snapshot.CurrentLocale);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Arguments_WhenInvalid_ThrowExpectedExceptions()
        {
            var module = CreateStartedModule();

            Assert.Throws<ArgumentNullException>(() => module.SetLocale(null));
            Assert.Throws<ArgumentException>(() => module.SetLocale(" "));
            Assert.Throws<ArgumentNullException>(() => module.SetFallbackLocale(null));
            Assert.Throws<ArgumentException>(() => module.SetFallbackLocale(" "));
            Assert.Throws<ArgumentNullException>(() => module.RegisterPack(null));
            Assert.Throws<ArgumentNullException>(() => module.GetText(null));
            Assert.Throws<ArgumentException>(() => module.GetText(" "));
            Assert.Throws<ArgumentNullException>(() => LocalizationPack.FromDictionary(null, new Dictionary<string, string>()));
            Assert.Throws<ArgumentException>(() => LocalizationPack.FromDictionary(" ", new Dictionary<string, string>()));
            Assert.Throws<ArgumentNullException>(() => LocalizationPack.FromDictionary("en-US", null));
            Assert.Throws<ArgumentException>(() => LocalizationPack.FromDictionary("en-US", new Dictionary<string, string> { [" "] = "bad" }));
            Assert.Throws<ArgumentNullException>(() => module.LoadPackAsync(null, "path").GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => module.LoadPackAsync("en-US", " ").GetAwaiter().GetResult());
        }

        [Test]
        public void LoadPackAsync_WhenJsonResourceIsValid_RegistersPack()
        {
            var module = CreateStartedModule();
            var resource = App.Resource;
            resource.InitializeAsync(CreateSettings()).GetAwaiter().GetResult();
            resource.PreloadDefaultPackagesAsync().GetAwaiter().GetResult();

            var pack = module.LoadPackAsync("en-US", FixturePath).GetAwaiter().GetResult();
            module.SetLocale("en-US");

            Assert.IsNotNull(pack);
            Assert.AreEqual("en-US", pack.Locale);
            Assert.AreEqual("Start", module.GetText("ui.start"));
        }

        [Test]
        public void LoadPackAsync_WhenResourceMissing_DoesNotReplaceExistingPack()
        {
            var module = CreateStartedModule();
            var resource = App.Resource;
            resource.InitializeAsync(CreateSettings()).GetAwaiter().GetResult();
            resource.PreloadDefaultPackagesAsync().GetAwaiter().GetResult();
            module.RegisterPack(LocalizationPack.FromDictionary("en-US", new Dictionary<string, string>
            {
                ["ui.start"] = "Old",
            }));
            module.SetLocale("en-US");

            Assert.Throws<GameException>(() => module.LoadPackAsync("en-US", "missing-localization").GetAwaiter().GetResult());

            Assert.AreEqual("Old", module.GetText("ui.start"));
        }

        [Test]
        public void AppLocalization_WhenAccessed_ReturnsStartedModule()
        {
            var module = App.Localization;
            var snapshot = module.Snapshot();

            Assert.IsNotNull(module);
            Assert.AreEqual(0, snapshot.LoadedLocales.Count);
        }

        private static LocalizationModule CreateStartedModule()
        {
            var module = new LocalizationModule();
            module.Startup();
            return module;
        }

        private static ResourceSettings CreateSettings()
        {
            var settings = new ResourceSettings();
            settings.Mode = ResourceMode.EditorSimulator;
            settings.DefaultPackages = new[] { "Package1" };
            return settings;
        }

        private static void AssertMissing(IReadOnlyList<MissingLocalizationEntry> entries, string locale, string key)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Locale == locale && entries[i].Key == key)
                {
                    return;
                }
            }

            Assert.Fail($"Missing entry was not found: {locale}/{key}");
        }

        private static void TryUnregister<T>() where T : IGameModule
        {
            try
            {
                App.Unregister<T>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
        }
    }
}
