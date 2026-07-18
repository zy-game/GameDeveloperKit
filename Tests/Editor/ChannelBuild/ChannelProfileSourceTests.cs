using System;
using System.Collections.Generic;
using System.IO;
using GameDeveloperKit.ChannelBuild;
using GameDeveloperKit.ResourceEditor;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    [TestFixture]
    public sealed class ChannelProfileSourceTests
    {
        private string m_ProjectRoot;

        [SetUp]
        public void SetUp()
        {
            m_ProjectRoot = Path.Combine(
                Path.GetTempPath(),
                "gdk-channel-profile-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_ProjectRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_ProjectRoot))
            {
                Directory.Delete(m_ProjectRoot, true);
            }
        }

        [Test]
        public void Load_DefaultPath_PreservesOrderAndSupportsOrdinalLookup()
        {
            WriteCatalog(
                "{\"schemaVersion\":1,\"profiles\":[" +
                "{\"id\":\"android-dev\",\"channel\":\"dev\"}," +
                "{\"id\":\"Android-dev\",\"channel\":\"case\"}]}");

            var catalog = ChannelProfileSource.Load(m_ProjectRoot);

            Assert.AreEqual(1, catalog.SchemaVersion);
            Assert.AreEqual(2, catalog.Profiles.Count);
            Assert.AreEqual("android-dev", catalog.Profiles[0].Id);
            Assert.AreEqual("Android-dev", catalog.Profiles[1].Id);
            Assert.AreEqual("dev", catalog.GetRequired("android-dev").Channel);
            Assert.AreEqual("case", catalog.GetRequired("Android-dev").Channel);
        }

        [Test]
        public void GetConfiguredChannelNames_MergesDefaultBuildSelectionAndProfiles()
        {
            WriteCatalog(
                "{\"schemaVersion\":1,\"profiles\":[" +
                "{\"id\":\"android-dev\",\"channel\":\"dev\"}," +
                "{\"id\":\"android-release\",\"channel\":\"release\"}]}");
            var settings = new ResourceBuildSettings
            {
                Channel = "legacy,qa"
            };

            CollectionAssert.AreEqual(
                new[] { "dev", "developer", "legacy", "qa", "release" },
                ResourceBuildUtilities.GetConfiguredChannelNames(settings, m_ProjectRoot));
        }

        [Test]
        public void Load_ExplicitRelativePath_LoadsOnlyThatFile()
        {
            WriteCatalog(ValidCatalog("default", "default"));
            WriteFile("Config/channel.json", ValidCatalog("explicit", "explicit"));
            WriteFile("Config/ignored.json", ValidCatalog("ignored", "ignored"));

            var catalog = ChannelProfileSource.Load(m_ProjectRoot, "Config/channel.json");

            Assert.AreEqual(1, catalog.Profiles.Count);
            Assert.AreEqual("explicit", catalog.Profiles[0].Id);
        }

        [Test]
        public void Load_ParsesAllProfileFields()
        {
            WriteCatalog(
                "{\"schemaVersion\":1,\"profiles\":[{" +
                "\"id\":\"android-dev\",\"channel\":\"dev\"," +
                "\"displayName\":\"Display\",\"productName\":\"Product\"," +
                "\"applicationIdentifier\":\"com.company.game\"," +
                "\"iconPath\":\"Assets/icon.png\",\"splashPath\":\"Assets/splash.png\"," +
                "\"defines\":[\"A\",\"B\"]," +
                "\"configOverrides\":{\"api\":\"https://example.com\"}," +
                "\"resourceOverrides\":{\"group\":\"main\"}," +
                "\"platformOptions\":{\"architecture\":\"arm64\"}}]}");

            var profile = ChannelProfileSource.Load(m_ProjectRoot).Profiles[0];

            Assert.AreEqual("Display", profile.DisplayName);
            Assert.AreEqual("Product", profile.ProductName);
            Assert.AreEqual("com.company.game", profile.ApplicationIdentifier);
            Assert.AreEqual("Assets/icon.png", profile.IconPath);
            Assert.AreEqual("Assets/splash.png", profile.SplashPath);
            CollectionAssert.AreEqual(new[] { "A", "B" }, profile.Defines);
            Assert.AreEqual("https://example.com", profile.ConfigOverrides["api"]);
            Assert.AreEqual("main", profile.ResourceOverrides["group"]);
            Assert.AreEqual("arm64", profile.PlatformOptions["architecture"]);
        }

        [Test]
        public void Catalog_DefensivelyCopiesProfileList()
        {
            var profiles = new List<ChannelProfile> { new ChannelProfile("one", "dev") };
            var catalog = new ChannelProfileCatalog(1, profiles);

            profiles[0] = new ChannelProfile("two", "dev");

            Assert.AreEqual("one", catalog.Profiles[0].Id);
            Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired("two"));
        }

        [Test]
        public void Merge_AppliesScalarListAndDictionaryPrecedence()
        {
            var profile = CreateBaseProfile();
            var overrides = new ChannelProfileOverrides(
                channel: "release",
                displayName: "Override Display",
                productName: "Override Product",
                applicationIdentifier: "com.company.override",
                iconPath: "Override/icon.png",
                splashPath: "Override/splash.png",
                defines: new[] { "OVERRIDE" },
                configOverrides: new Dictionary<string, string>
                {
                    { "api", "override" },
                    { "new", "value" }
                },
                resourceOverrides: new Dictionary<string, string> { { "resource", "override" } },
                platformOptions: new Dictionary<string, string> { { "platform", "override" } });

            var merged = ChannelProfileSource.Merge(profile, overrides);

            Assert.AreEqual(profile.Id, merged.Id);
            Assert.AreEqual("release", merged.Channel);
            Assert.AreEqual("Override Display", merged.DisplayName);
            Assert.AreEqual("Override Product", merged.ProductName);
            Assert.AreEqual("com.company.override", merged.ApplicationIdentifier);
            Assert.AreEqual("Override/icon.png", merged.IconPath);
            Assert.AreEqual("Override/splash.png", merged.SplashPath);
            CollectionAssert.AreEqual(new[] { "OVERRIDE" }, merged.Defines);
            Assert.AreEqual("override", merged.ConfigOverrides["api"]);
            Assert.AreEqual("base-case", merged.ConfigOverrides["Api"]);
            Assert.AreEqual("value", merged.ConfigOverrides["new"]);
            Assert.AreEqual("override", merged.ResourceOverrides["resource"]);
            Assert.AreEqual("override", merged.PlatformOptions["platform"]);
        }

        [Test]
        public void Merge_EmptyOptionalScalarsAndDefines_ClearValues()
        {
            var profile = CreateBaseProfile();
            var overrides = new ChannelProfileOverrides(
                displayName: string.Empty,
                productName: string.Empty,
                applicationIdentifier: string.Empty,
                iconPath: string.Empty,
                splashPath: string.Empty,
                defines: Array.Empty<string>());

            var merged = ChannelProfileSource.Merge(profile, overrides);

            Assert.IsNull(merged.DisplayName);
            Assert.IsNull(merged.ProductName);
            Assert.IsNull(merged.ApplicationIdentifier);
            Assert.IsNull(merged.IconPath);
            Assert.IsNull(merged.SplashPath);
            Assert.IsEmpty(merged.Defines);
            Assert.AreEqual("Base Display", profile.DisplayName);
            CollectionAssert.AreEqual(new[] { "BASE" }, profile.Defines);
        }

        [Test]
        public void Merge_NullOverridesInheritAndEmptyDictionariesDoNotDelete()
        {
            var profile = CreateBaseProfile();

            var merged = ChannelProfileSource.Merge(
                profile,
                new ChannelProfileOverrides(
                    configOverrides: new Dictionary<string, string>(),
                    resourceOverrides: new Dictionary<string, string>(),
                    platformOptions: new Dictionary<string, string>()));

            Assert.AreEqual(profile.Channel, merged.Channel);
            Assert.AreEqual(profile.DisplayName, merged.DisplayName);
            CollectionAssert.AreEqual(profile.Defines, merged.Defines);
            Assert.AreEqual("base", merged.ConfigOverrides["api"]);
            Assert.AreEqual("base", merged.ResourceOverrides["resource"]);
            Assert.AreEqual("base", merged.PlatformOptions["platform"]);
        }

        [Test]
        public void Merge_DefensivelyCopiesCallerCollections()
        {
            var defines = new List<string> { "OVERRIDE" };
            var config = new Dictionary<string, string> { { "api", "override" } };
            var overrides = new ChannelProfileOverrides(defines: defines, configOverrides: config);
            var merged = ChannelProfileSource.Merge(CreateBaseProfile(), overrides);

            defines[0] = "MUTATED";
            config["api"] = "mutated";

            Assert.AreEqual("OVERRIDE", merged.Defines[0]);
            Assert.AreEqual("override", merged.ConfigOverrides["api"]);
        }

        [TestCase("../catalog.json")]
        [TestCase("Config/catalog.txt")]
        public void Load_EscapingOrNonJsonPath_ThrowsArgumentException(string relativePath)
        {
            Assert.Throws<ArgumentException>(() => ChannelProfileSource.Load(m_ProjectRoot, relativePath));
        }

        [Test]
        public void Load_RootedPath_ThrowsArgumentException()
        {
            var rootedPath = Path.Combine(Path.GetPathRoot(m_ProjectRoot), "catalog.json");

            Assert.Throws<ArgumentException>(() => ChannelProfileSource.Load(m_ProjectRoot, rootedPath));
        }

        [Test]
        public void Load_MissingFile_ThrowsFileNotFoundWithResolvedPath()
        {
            var exception = Assert.Throws<FileNotFoundException>(
                () => ChannelProfileSource.Load(m_ProjectRoot));

            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(m_ProjectRoot, ChannelProfileSource.DefaultRelativePath)),
                exception.FileName);
        }

        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("{not-json}")]
        [TestCase("{/*comment*/\"schemaVersion\":1,\"profiles\":[]}")]
        public void Load_MalformedOrNonObjectJson_ThrowsGameException(string json)
        {
            WriteCatalog(json);

            Assert.Throws<GameException>(() => ChannelProfileSource.Load(m_ProjectRoot));
        }

        [TestCase("{\"schemaVersion\":1,\"unknown\":true,\"profiles\":[{\"id\":\"one\",\"channel\":\"dev\"}]}")]
        [TestCase("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"one\",\"channel\":\"dev\",\"unknown\":true}]}")]
        [TestCase("{\"schemaVersion\":1,\"schemaVersion\":1,\"profiles\":[{\"id\":\"one\",\"channel\":\"dev\"}]}")]
        [TestCase("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"one\",\"channel\":\"dev\",\"defines\":[],\"defines\":[]}]}")]
        [TestCase("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"one\",\"channel\":\"dev\",\"configOverrides\":{\"key\":\"one\",\"key\":\"two\"}}]}")]
        public void Load_UnknownOrDuplicateMember_ThrowsGameException(string json)
        {
            WriteCatalog(json);

            Assert.Throws<GameException>(() => ChannelProfileSource.Load(m_ProjectRoot));
        }

        [TestCase("{\"schemaVersion\":2,\"profiles\":[{\"id\":\"one\",\"channel\":\"dev\"}]}")]
        [TestCase("{\"schemaVersion\":1,\"profiles\":null}")]
        [TestCase("{\"schemaVersion\":1,\"profiles\":[]}")]
        [TestCase("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"same\",\"channel\":\"dev\"},{\"id\":\"same\",\"channel\":\"release\"}]}")]
        public void Load_InvalidSchemaProfilesOrDuplicateId_ThrowsGameException(string json)
        {
            WriteCatalog(json);

            Assert.Throws<GameException>(() => ChannelProfileSource.Load(m_ProjectRoot));
        }

        [TestCase("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"one\",\"channel\":\"dev\",\"configOverrides\":{\"apiToken\":\"do-not-print\"}}]}")]
        [TestCase("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"one\",\"channel\":\"dev\",\"configOverrides\":{\"api\":null}}]}")]
        public void Load_UnsafeDictionary_RejectsWithoutPrintingValue(string json)
        {
            WriteCatalog(json);

            var exception = Assert.Throws<GameException>(() => ChannelProfileSource.Load(m_ProjectRoot));

            StringAssert.DoesNotContain("do-not-print", exception.ToString());
        }

        [Test]
        public void Overrides_UnsafeDictionary_RejectsWithoutPrintingValue()
        {
            var values = new Dictionary<string, string> { { "signingSecret", "do-not-print" } };

            var exception = Assert.Throws<ArgumentException>(
                () => new ChannelProfileOverrides(configOverrides: values));

            StringAssert.DoesNotContain("do-not-print", exception.ToString());
        }

        [Test]
        public void Catalog_GetRequired_RejectsInvalidOrMissingId()
        {
            var catalog = new ChannelProfileCatalog(
                1,
                new[] { new ChannelProfile("one", "dev") });

            Assert.Throws<ArgumentException>(() => catalog.GetRequired(null));
            Assert.Throws<ArgumentException>(() => catalog.GetRequired(string.Empty));
            Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired("missing"));
        }

        [Test]
        public void Merge_RejectsNullInputs()
        {
            Assert.Throws<ArgumentNullException>(
                () => ChannelProfileSource.Merge(null, new ChannelProfileOverrides()));
            Assert.Throws<ArgumentNullException>(
                () => ChannelProfileSource.Merge(CreateBaseProfile(), null));
        }

        private ChannelProfile CreateBaseProfile()
        {
            return new ChannelProfile(
                "android-dev",
                "dev",
                "Base Display",
                "Base Product",
                "com.company.base",
                "Base/icon.png",
                "Base/splash.png",
                new[] { "BASE" },
                new Dictionary<string, string> { { "api", "base" }, { "Api", "base-case" } },
                new Dictionary<string, string> { { "resource", "base" } },
                new Dictionary<string, string> { { "platform", "base" } });
        }

        private void WriteCatalog(string json)
        {
            WriteFile(ChannelProfileSource.DefaultRelativePath, json);
        }

        private void WriteFile(string relativePath, string contents)
        {
            var path = Path.Combine(m_ProjectRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, contents);
        }

        private static string ValidCatalog(string id, string channel)
        {
            return "{\"schemaVersion\":1,\"profiles\":[{\"id\":\"" + id +
                "\",\"channel\":\"" + channel + "\"}]}";
        }
    }
}
