using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using GameDeveloperKit.ChannelBuild;
using GameDeveloperKit.ResourceEditor;
using NUnit.Framework;
using UnityEditor;
using IOFile = System.IO.File;
using ResourceBuildArtifact = GameDeveloperKit.ResourceEditor.Build.Artifact;
using ResourceBuildCompression = GameDeveloperKit.ResourceEditor.Build.Compression;
using ResourceBuildContext = GameDeveloperKit.ResourceEditor.Build.Context;
using ResourceBuildPlan = GameDeveloperKit.ResourceEditor.Build.Plan;
using ResourceBuildPreparedRequest = GameDeveloperKit.ResourceEditor.Build.PreparedRequest;
using ResourceBuildResult = GameDeveloperKit.ResourceEditor.Build.Result;
using ResourceBuildScope = GameDeveloperKit.ResourceEditor.Build.Scope;
using ResourceBuildWorkflow = GameDeveloperKit.ResourceEditor.Build.Workflow;

namespace GameDeveloperKit.Tests
{
    public sealed class ChannelBuildResourceResponderTests
    {
        private string m_TempRoot;

        [SetUp]
        public void SetUp()
        {
            m_TempRoot = Path.GetFullPath(Path.Combine(
                "Temp/ChannelBuildResourceResponderTests",
                Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(m_TempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_TempRoot))
            {
                Directory.Delete(m_TempRoot, true);
            }
        }

        [Test]
        public void BuildSettings_DefaultsUseFixedContextMapping()
        {
            var context = CreateContext();

            var success = ChannelBuildResourceResponder.TryCreateBuildSettings(
                context,
                out var settings,
                out var error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(Path.Combine(context.OutputRoot, "resources").Replace('\\', '/'), settings.OutputRoot);
            Assert.AreEqual(BuildTarget.Android.ToString(), settings.Target);
            Assert.AreEqual("dev", settings.Channel);
            Assert.IsTrue(settings.CleanOutput);
            Assert.AreEqual(ResourceBuildCompression.Lz4, settings.Compression);
            Assert.AreEqual("manifest.json", settings.ManifestFileName);
            Assert.AreEqual("1.2.3", settings.ManifestVersion);
            Assert.AreEqual(ResourceBuildScope.AllPackages, settings.Scope);
        }

        [TestCase("default", ResourceBuildCompression.Default)]
        [TestCase("lz4", ResourceBuildCompression.Lz4)]
        [TestCase("uncompressed", ResourceBuildCompression.Uncompressed)]
        public void BuildSettings_AcceptsCompressionAllowlist(
            string value,
            ResourceBuildCompression expected)
        {
            var profile = CreateProfile(new Dictionary<string, string> { ["compression"] = value });

            var success = ChannelBuildResourceResponder.TryCreateBuildSettings(
                CreateContext(profile),
                out var settings,
                out var error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(expected, settings.Compression);
        }

        [Test]
        public void BuildSettings_RejectsUnknownKeyAndDoesNotEchoInvalidValue()
        {
            const string sensitiveValue = "must-not-appear";
            var unknownProfile = CreateProfile(
                new Dictionary<string, string> { ["scope"] = sensitiveValue });
            var invalidProfile = CreateProfile(
                new Dictionary<string, string> { ["compression"] = sensitiveValue });

            Assert.IsFalse(ChannelBuildResourceResponder.TryCreateBuildSettings(
                CreateContext(unknownProfile),
                out _,
                out var unknownError));
            Assert.IsFalse(ChannelBuildResourceResponder.TryCreateBuildSettings(
                CreateContext(invalidProfile),
                out _,
                out var invalidError));

            StringAssert.Contains("scope", unknownError);
            StringAssert.DoesNotContain(sensitiveValue, unknownError);
            StringAssert.DoesNotContain(sensitiveValue, invalidError);
        }

        [Test]
        public void Prepare_WhenSettingsAreMissing_ReturnsFailureWithoutCreatingBackup()
        {
            var responder = new ChannelBuildResourceResponder();
            var backupRoot = Path.GetFullPath(
                "Library/GameDeveloperKit/ChannelBuild/FileMutationState");
            var before = Directory.Exists(backupRoot)
                ? Directory.GetFileSystemEntries(backupRoot).Length
                : 0;

            var result = responder.Prepare(CreateContext());

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ChannelBuildResponderPhase.Prepare, result.Phase);
            StringAssert.Contains("settings are missing", result.Message);
            Assert.AreEqual(
                before,
                Directory.Exists(backupRoot)
                    ? Directory.GetFileSystemEntries(backupRoot).Length
                    : 0);
        }

        [Test]
        public void PackagedState_RestoresExistingAndDeletesNewFilesAndMeta()
        {
            var existing = Path.Combine(m_TempRoot, "existing.bin");
            var created = Path.Combine(m_TempRoot, "created.bin");
            IOFile.WriteAllText(existing, "before");
            IOFile.WriteAllText(existing + ".meta", "meta-before");
            var state = new ChannelBuildFileMutationState(new[] { existing, created });

            state.Capture();
            IOFile.WriteAllText(existing, "after");
            IOFile.WriteAllText(existing + ".meta", "meta-after");
            IOFile.WriteAllText(created, "created");
            IOFile.WriteAllText(created + ".meta", "created-meta");

            Assert.AreEqual(0, state.Restore());
            Assert.AreEqual("before", IOFile.ReadAllText(existing));
            Assert.AreEqual("meta-before", IOFile.ReadAllText(existing + ".meta"));
            Assert.IsFalse(IOFile.Exists(created));
            Assert.IsFalse(IOFile.Exists(created + ".meta"));
            Assert.AreEqual(0, state.Restore());
        }

        [Test]
        public void PackagedState_RejectsDuplicateTargetsAndDirectories()
        {
            var file = Path.Combine(m_TempRoot, "same.bin");
            Assert.Throws<ArgumentException>(
                () => new ChannelBuildFileMutationState(new[] { file, file }));

            var directory = Path.Combine(m_TempRoot, "directory");
            Directory.CreateDirectory(directory);
            var state = new ChannelBuildFileMutationState(new[] { directory });
            Assert.Throws<InvalidOperationException>(() => state.Capture());
            Assert.AreEqual(0, state.Restore());
        }

        [Test]
        public void PreparedRequest_RejectsAnotherOwnerAndSecondConsumption()
        {
            var owner = Uninitialized<ResourceBuildWorkflow>();
            var otherOwner = Uninitialized<ResourceBuildWorkflow>();
            var request = new ResourceBuildPreparedRequest(
                owner,
                Uninitialized<ResourceBuildContext>(),
                new ResourceBuildPlan());

            Assert.Throws<ArgumentException>(() => request.Consume(otherOwner));
            request.Consume(owner);
            Assert.Throws<InvalidOperationException>(() => request.Consume(owner));
        }

        [Test]
        public void Runner_OperationObservesPackagedFileAndFailureStillRestoresIt()
        {
            var path = Path.Combine(m_TempRoot, "packaged.bin");
            IOFile.WriteAllText(path, "before");
            var state = new ChannelBuildFileMutationState(new[] { path });
            var responder = new FileMutationResponder(path, state);

            var execution = ChannelBuildResponderRunner.Execute(
                CreateContext(),
                new[] { responder },
                context =>
                {
                    Assert.AreEqual("during", IOFile.ReadAllText(path));
                    return new ChannelBuildStepResult(
                        "operation",
                        ChannelBuildResponderPhase.Operation,
                        false,
                        "operation failed");
                });

            Assert.IsFalse(execution.Success);
            Assert.AreEqual("before", IOFile.ReadAllText(path));
        }

        [Test]
        public void ArtifactOutputs_AreRelativeStableAndExcludePackagedTargets()
        {
            var context = CreateContext();
            var resultRoot = Path.Combine(context.OutputRoot, "resources/dev/Android/1.2.3");
            Directory.CreateDirectory(resultRoot);
            var bundlePath = Path.Combine(resultRoot, "bundle.bin");
            var manifestPath = Path.Combine(resultRoot, "manifest.json");
            var packagedPath = Path.Combine(m_TempRoot, "packaged.bin");
            IOFile.WriteAllText(bundlePath, "bundle");
            IOFile.WriteAllText(manifestPath, "manifest");
            IOFile.WriteAllText(packagedPath, "packaged");
            var result = new ResourceBuildResult
            {
                Succeeded = true,
                OutputRoot = resultRoot,
                ManifestPath = manifestPath,
                Artifacts = new List<ResourceBuildArtifact>
                {
                    Artifact("manifest", "manifest.json", manifestPath),
                    Artifact("main", "bundle.bin", bundlePath),
                    Artifact("local", "packaged.bin", packagedPath)
                }
            };

            var outputs = ChannelBuildResourceResponder.CreateArtifactOutputs(
                context,
                result,
                new[] { packagedPath });

            Assert.AreEqual("2", outputs["resource.artifactCount"]);
            Assert.AreEqual("resource-artifact", outputs["resource.artifact.0000.kind"]);
            Assert.AreEqual("resources/dev/Android/1.2.3/bundle.bin", outputs["resource.artifact.0000.path"]);
            Assert.AreEqual("resource-manifest", outputs["resource.artifact.0001.kind"]);
            Assert.AreEqual("resources/dev/Android/1.2.3/manifest.json", outputs["resource.artifact.0001.path"]);
            Assert.AreEqual(
                "resources/dev/Android/1.2.3",
                outputs["resource.outputRoot"]);
            Assert.AreEqual(
                "resources/dev/Android/1.2.3/manifest.json",
                outputs["resource.manifestPath"]);
        }

        [Test]
        public void ArtifactOutputs_RejectMissingEscapeDuplicateAndUnlistedManifest()
        {
            var context = CreateContext();
            var resultRoot = Path.Combine(context.OutputRoot, "resources/dev/Android/1.2.3");
            Directory.CreateDirectory(resultRoot);
            var manifestPath = Path.Combine(resultRoot, "manifest.json");
            var escapePath = Path.Combine(m_TempRoot, "escape.bin");
            IOFile.WriteAllText(manifestPath, "manifest");
            IOFile.WriteAllText(escapePath, "escape");

            Assert.Throws<InvalidDataException>(
                () => ChannelBuildResourceResponder.CreateArtifactOutputs(
                    context,
                    Result(resultRoot, manifestPath, Artifact("outside", "escape", escapePath)),
                    Array.Empty<string>()));
            Assert.Throws<FileNotFoundException>(
                () => ChannelBuildResourceResponder.CreateArtifactOutputs(
                    context,
                    Result(resultRoot, manifestPath, Artifact("main", "missing", Path.Combine(resultRoot, "missing.bin"))),
                    Array.Empty<string>()));
            Assert.Throws<InvalidDataException>(
                () => ChannelBuildResourceResponder.CreateArtifactOutputs(
                    context,
                    Result(
                        resultRoot,
                        manifestPath,
                        Artifact("manifest", "manifest.json", manifestPath),
                        Artifact("duplicate", "duplicate", manifestPath)),
                    Array.Empty<string>()));
            Assert.Throws<InvalidDataException>(
                () => ChannelBuildResourceResponder.CreateArtifactOutputs(
                    context,
                    Result(resultRoot, manifestPath),
                    Array.Empty<string>()));
        }

        private ChannelBuildContext CreateContext(ChannelProfile profile = null)
        {
            return new ChannelBuildContext(
                "dev",
                ChannelBuildEnvironment.Dev,
                BuildTarget.Android,
                "1.2.3",
                1,
                Path.Combine(m_TempRoot, "output"),
                profile: profile);
        }

        private static ChannelProfile CreateProfile(IReadOnlyDictionary<string, string> overrides)
        {
            return new ChannelProfile("dev-profile", "dev", resourceOverrides: overrides);
        }

        private static ResourceBuildArtifact Artifact(
            string package,
            string bundle,
            string path)
        {
            return new ResourceBuildArtifact
            {
                PackageName = package,
                BundleName = bundle,
                LocalPath = path
            };
        }

        private static ResourceBuildResult Result(
            string outputRoot,
            string manifestPath,
            params ResourceBuildArtifact[] artifacts)
        {
            return new ResourceBuildResult
            {
                Succeeded = true,
                OutputRoot = outputRoot,
                ManifestPath = manifestPath,
                Artifacts = new List<ResourceBuildArtifact>(artifacts)
            };
        }

        private static T Uninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        private sealed class FileMutationResponder : IChannelBuildResponder
        {
            private readonly string m_Path;
            private readonly ChannelBuildFileMutationState m_State;

            internal FileMutationResponder(string path, ChannelBuildFileMutationState state)
            {
                m_Path = path;
                m_State = state;
            }

            public string Id => "file-mutation";

            public int Order => 0;

            public IReadOnlyList<string> DependsOn => Array.Empty<string>();

            public ChannelBuildStepResult Prepare(ChannelBuildContext context)
            {
                return Step(ChannelBuildResponderPhase.Prepare);
            }

            public ChannelBuildStepResult Apply(ChannelBuildContext context)
            {
                m_State.Capture();
                IOFile.WriteAllText(m_Path, "during");
                return Step(ChannelBuildResponderPhase.Apply);
            }

            public ChannelBuildStepResult Restore(ChannelBuildContext context)
            {
                var failures = m_State.Restore();
                return new ChannelBuildStepResult(
                    Id,
                    ChannelBuildResponderPhase.Restore,
                    failures == 0,
                    failures == 0 ? null : "restore failed");
            }

            private ChannelBuildStepResult Step(ChannelBuildResponderPhase phase)
            {
                return new ChannelBuildStepResult(Id, phase, true);
            }
        }
    }
}
