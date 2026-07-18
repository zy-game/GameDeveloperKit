using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.ChannelBuild;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class ChannelBuildDefinesConfigResponderTests
    {
        [Test]
        public void Defines_CreateStableUnionWithGeneratedAndProfileSymbols()
        {
            var profile = new ChannelProfile(
                "dev-profile",
                "dev",
                defines: new[] { "FEATURE_X", "TRACE" });
            var context = CreateContext(profile, "official.flavor");

            var success = ChannelBuildDefinesResponder.TryCreateSymbols(
                context,
                "TRACE;DEBUG;TRACE",
                out var symbols,
                out var error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(
                "DEBUG;FEATURE_X;GDK_CHANNEL_DEV;GDK_ENV_DEV;GDK_FLAVOR_OFFICIAL_FLAVOR;TRACE",
                symbols);
        }

        [Test]
        public void Defines_NoFlavorOmitsFlavorSymbolAndRejectsInvalidProfileSymbol()
        {
            Assert.IsTrue(ChannelBuildDefinesResponder.TryCreateSymbols(
                CreateContext(),
                string.Empty,
                out var symbols,
                out var error), error);
            StringAssert.DoesNotContain("GDK_FLAVOR_", symbols);

            var invalid = new ChannelProfile(
                "dev-profile",
                "dev",
                defines: new[] { "INVALID-SYMBOL" });
            Assert.IsFalse(ChannelBuildDefinesResponder.TryCreateSymbols(
                CreateContext(invalid),
                string.Empty,
                out _,
                out var invalidError));
            StringAssert.DoesNotContain("INVALID-SYMBOL", invalidError);
        }

        [Test]
        public void ConfigJson_UsesFixedSchemaStableValuesAndNoBom()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zeta"] = "last",
                ["alpha"] = "first"
            };
            var context = CreateContext(
                new ChannelProfile("dev-profile", "dev", configOverrides: values),
                "official");

            var json = ChannelBuildConfigResponder.CreateJson(context);
            var root = JObject.Parse(json);

            Assert.AreEqual(1, (int)root["schemaVersion"]);
            Assert.AreEqual("dev", (string)root["channel"]);
            Assert.AreEqual("dev", (string)root["environment"]);
            Assert.AreEqual("official", (string)root["flavor"]);
            CollectionAssert.AreEqual(
                new[] { "alpha", "zeta" },
                ((JObject)root["values"]).Properties().Select(property => property.Name));

            var path = Path.Combine("Temp", Guid.NewGuid().ToString("N"), "config.json");
            try
            {
                ChannelBuildConfigResponder.WriteAtomic(path, json);
                var bytes = IOFile.ReadAllBytes(path);
                Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf);
                Assert.AreEqual(json, IOFile.ReadAllText(path));
            }
            finally
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void ConfigJson_DoesNotSerializeProfileOrCiFields()
        {
            var json = ChannelBuildConfigResponder.CreateJson(CreateContext());

            StringAssert.DoesNotContain("profile", json.ToLowerInvariant());
            StringAssert.DoesNotContain("arguments", json.ToLowerInvariant());
            StringAssert.DoesNotContain("ci", json.ToLowerInvariant());
            StringAssert.DoesNotContain("defines", json.ToLowerInvariant());
        }

        [Test]
        public void Runner_AppliesDefinesAndConfigDuringOperationAndRestoresExactState()
        {
            var symbols = "BASE;TRACE";
            var previousSymbols = symbols;
            var configPath = Path.GetFullPath(Path.Combine(
                "Temp/ChannelBuildDefinesConfigResponderTests",
                Guid.NewGuid().ToString("N"),
                "channel-config.json"));
            var configMetaPath = configPath + ".meta";
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            IOFile.WriteAllText(configPath, "before");
            IOFile.WriteAllText(configMetaPath, "meta-before");
            var previousConfig = IOFile.ReadAllBytes(configPath);
            var previousMeta = IOFile.ReadAllBytes(configMetaPath);
            var profile = new ChannelProfile(
                "dev-profile",
                "dev",
                defines: new[] { "CHANNEL_TEST_FEATURE" },
                configOverrides: new Dictionary<string, string> { ["apiUrl"] = "https://api.example.com" });
            var context = CreateContext(profile, "official");

            try
            {
                var execution = ChannelBuildResponderRunner.Execute(
                    context,
                    new IChannelBuildResponder[]
                    {
                        new ChannelBuildConfigResponder(configPath, path => { }, () => { }),
                        new ChannelBuildDefinesResponder(
                            target => target == BuildTarget.Android,
                            target => symbols,
                            (target, value) => symbols = value)
                    },
                    operationContext =>
                    {
                        var activeSymbols = symbols;
                        StringAssert.Contains("CHANNEL_TEST_FEATURE", activeSymbols);
                        StringAssert.Contains("GDK_CHANNEL_DEV", activeSymbols);
                        StringAssert.Contains("GDK_ENV_DEV", activeSymbols);
                        StringAssert.Contains("GDK_FLAVOR_OFFICIAL", activeSymbols);
                        Assert.IsTrue(IOFile.Exists(configPath));
                        var root = JObject.Parse(IOFile.ReadAllText(configPath));
                        Assert.AreEqual("https://api.example.com", (string)root["values"]["apiUrl"]);
                        return new ChannelBuildStepResult(
                            "operation",
                            ChannelBuildResponderPhase.Operation,
                            false,
                            "expected operation failure");
                    });

                Assert.IsFalse(execution.Success);
            }
            finally
            {
                Assert.AreEqual(previousSymbols, symbols);
                AssertFileState(configPath, true, previousConfig);
                AssertFileState(configMetaPath, true, previousMeta);
                var directory = Path.GetDirectoryName(configPath);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void ConfigResponder_NewFileIsRemovedAfterOperation()
        {
            var configPath = Path.GetFullPath(Path.Combine(
                "Temp/ChannelBuildDefinesConfigResponderTests",
                Guid.NewGuid().ToString("N"),
                "channel-config.json"));
            var context = CreateContext();
            var responder = new ChannelBuildConfigResponder(configPath, path => { }, () => { });

            var execution = ChannelBuildResponderRunner.Execute(
                context,
                new IChannelBuildResponder[]
                {
                    new ChannelBuildDefinesResponder(
                        target => true,
                        target => string.Empty,
                        (target, value) => { }),
                    responder
                },
                operationContext =>
                {
                    Assert.IsTrue(IOFile.Exists(configPath));
                    return new ChannelBuildStepResult(
                        "operation",
                        ChannelBuildResponderPhase.Operation,
                        true);
                });

            Assert.IsTrue(execution.Success);
            Assert.IsFalse(IOFile.Exists(configPath));
            Assert.IsFalse(IOFile.Exists(configPath + ".meta"));
        }

        [Test]
        public void Responders_RejectReuseAndContextMismatch()
        {
            var context = CreateContext();
            var otherContext = CreateContext();
            var defines = new ChannelBuildDefinesResponder(
                target => true,
                target => string.Empty,
                (target, value) => { });

            Assert.IsTrue(defines.Prepare(context).Success);
            Assert.Throws<GameException>(() => defines.Prepare(context));
            Assert.Throws<GameException>(() => defines.Apply(otherContext));

            var configPath = Path.GetFullPath(Path.Combine(
                "Temp/ChannelBuildDefinesConfigResponderTests",
                Guid.NewGuid().ToString("N"),
                "channel-config.json"));
            var config = new ChannelBuildConfigResponder(configPath, path => { }, () => { });
            Assert.IsTrue(config.Prepare(context).Success);
            Assert.Throws<GameException>(() => config.Prepare(context));
            Assert.Throws<GameException>(() => config.Apply(otherContext));
        }

        private static ChannelBuildContext CreateContext(
            ChannelProfile profile = null,
            string flavor = null)
        {
            return new ChannelBuildContext(
                "dev",
                ChannelBuildEnvironment.Dev,
                BuildTarget.Android,
                "1.2.3",
                1,
                "Build/Channel",
                flavor,
                profile: profile);
        }

        private static void AssertFileState(string path, bool existed, byte[] expected)
        {
            Assert.AreEqual(existed, IOFile.Exists(path));
            if (existed)
            {
                CollectionAssert.AreEqual(expected, IOFile.ReadAllBytes(path));
            }
        }
    }
}
