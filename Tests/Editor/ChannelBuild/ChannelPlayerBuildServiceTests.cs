using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.ChannelBuild;
using NUnit.Framework;
using UnityEditor;

namespace GameDeveloperKit.Tests
{
    public sealed class ChannelPlayerBuildServiceTests
    {
        private string m_OutputRoot;

        [SetUp]
        public void SetUp()
        {
            m_OutputRoot = Path.Combine(Path.GetTempPath(), "gdk-player-service-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_OutputRoot))
            {
                Directory.Delete(m_OutputRoot, true);
            }
        }

        [TestCase(BuildTarget.Android, "player.apk")]
        [TestCase(BuildTarget.iOS, "")]
        public void Build_ValidTargetUsesStableInvocationAndReverseRestore(BuildTarget target, string suffix)
        {
            var events = new List<string>();
            var gateway = new FakeGateway(m_OutputRoot, events);
            var responders = CreateResponders(events);
            var service = Service(gateway, responders);

            var result = service.Build(Context(target));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(target, gateway.Invocation.Target);
            CollectionAssert.AreEqual(new[] { "Assets/Main.unity" }, gateway.Invocation.Scenes);
            Assert.AreEqual(BuildOptions.CleanBuildCache | BuildOptions.StrictMode, gateway.Invocation.Options);
            StringAssert.EndsWith(suffix, gateway.Invocation.LocationPathName);
            CollectionAssert.AreEqual(
                new[] { "defines:prepare", "config:prepare", "branding:prepare", "platform:prepare", "resources:prepare",
                    "defines:apply", "config:apply", "branding:apply", "platform:apply", "resources:apply", "build",
                    "resources:restore", "platform:restore", "branding:restore", "config:restore", "defines:restore" },
                events);
        }

        [Test]
        public void Build_RejectsUnsupportedTargetAndMissingScenesWithoutBuildOrCleanup()
        {
            var gateway = new FakeGateway(m_OutputRoot, new List<string>()) { Scenes = Array.Empty<string>() };
            var service = Service(gateway, CreateResponders(new List<string>()));

            Assert.AreEqual(ChannelBuildExitCode.PipelineFailed, service.Build(Context(BuildTarget.Android)).ExitCode);
            Assert.IsNull(gateway.Invocation);
            Assert.AreEqual(0, gateway.RecreateCount);

            gateway.Scenes = new[] { "Assets/Main.unity" };
            Assert.AreEqual(
                ChannelBuildExitCode.PipelineFailed,
                service.Build(Context(BuildTarget.StandaloneWindows64)).ExitCode);
            Assert.IsNull(gateway.Invocation);
            Assert.AreEqual(0, gateway.RecreateCount);
        }

        [Test]
        public void Build_ClassifiesResourcePlayerAndPipelineFailures()
        {
            Assert.AreEqual(
                ChannelBuildExitCode.ResourceBuildFailed,
                BuildWithFailure("resources", ChannelBuildResponderPhase.Apply).ExitCode);

            var gateway = new FakeGateway(m_OutputRoot, new List<string>())
            {
                Summary = new ChannelPlayerBuildService.PlayerBuildSummary(false, 2)
            };
            Assert.AreEqual(
                ChannelBuildExitCode.PlayerBuildFailed,
                Service(gateway, CreateResponders(new List<string>())).Build(Context(BuildTarget.Android)).ExitCode);

            Assert.AreEqual(
                ChannelBuildExitCode.PipelineFailed,
                BuildWithFailure("branding", ChannelBuildResponderPhase.Apply).ExitCode);
        }

        [Test]
        public void Build_SuccessCapturesResourceAndPlayerArtifactsStepsAndWarnings()
        {
            Directory.CreateDirectory(m_OutputRoot);
            var resourcePath = Path.Combine(m_OutputRoot, "resources", "manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(resourcePath));
            System.IO.File.WriteAllText(resourcePath, "manifest");
            var events = new List<string>();
            var responders = CreateResponders(events, resourcePath, warning: "resource warning");
            var result = Service(new FakeGateway(m_OutputRoot, events), responders)
                .Build(Context(BuildTarget.Android));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Artifacts.Count);
            CollectionAssert.AreEqual(
                new[] { "player-artifact", "resource-manifest" },
                result.Artifacts.Select(artifact => artifact.Kind).ToArray());
            Assert.IsTrue(result.Artifacts.All(artifact => artifact.Sha256.Length == 64));
            Assert.IsTrue(result.Artifacts.All(artifact => artifact.SizeBytes > 0));
            Assert.IsTrue(result.Steps.Any(step => step.Id == "player-operation" && step.Status == "succeeded"));
            CollectionAssert.Contains(result.Warnings, "resource warning");
        }

        [Test]
        public void Build_MissingPlayerArtifactConvertsSuccessToPipelineFailure()
        {
            var gateway = new FakeGateway(m_OutputRoot, new List<string>()) { WritePlayerFile = false };
            var result = Service(gateway, CreateResponders(new List<string>())).Build(Context(BuildTarget.Android));

            Assert.AreEqual(ChannelBuildExitCode.PipelineFailed, result.ExitCode);
            Assert.IsFalse(result.Success);
        }

        private ChannelPlayerBuildResult BuildWithFailure(string id, ChannelBuildResponderPhase phase)
        {
            var events = new List<string>();
            var responders = CreateResponders(events)
                .Select(responder => responder.Id == id
                    ? new FakeResponder(id, responder.DependsOn, events, m_OutputRoot, phase)
                    : responder)
                .ToArray();
            return Service(new FakeGateway(m_OutputRoot, events), responders).Build(Context(BuildTarget.Android));
        }

        private static ChannelPlayerBuildService Service(
            ChannelPlayerBuildService.IPlayerBuildGateway gateway,
            IReadOnlyList<IChannelBuildResponder> responders)
        {
            return new ChannelPlayerBuildService(signing => responders, gateway);
        }

        private IReadOnlyList<IChannelBuildResponder> CreateResponders(
            IList<string> events,
            string resourcePath = null,
            string warning = null)
        {
            return new IChannelBuildResponder[]
            {
                new FakeResponder("resources", Array.Empty<string>(), events, m_OutputRoot, resourcePath: resourcePath, warning: warning),
                new FakeResponder("defines", Array.Empty<string>(), events, m_OutputRoot),
                new FakeResponder("config", new[] { "defines" }, events, m_OutputRoot),
                new FakeResponder("branding", new[] { "config" }, events, m_OutputRoot),
                new FakeResponder("platform", new[] { "branding" }, events, m_OutputRoot)
            };
        }

        private ChannelBuildContext Context(BuildTarget target)
        {
            return new ChannelBuildContext(
                "dev", ChannelBuildEnvironment.Dev, target, "1.2.3", 42, m_OutputRoot);
        }

        private sealed class FakeGateway : ChannelPlayerBuildService.IPlayerBuildGateway
        {
            private readonly string m_Root;
            private readonly IList<string> m_Events;

            internal FakeGateway(string root, IList<string> events)
            {
                m_Root = root;
                m_Events = events;
            }

            internal IReadOnlyList<string> Scenes { get; set; } = new[] { "Assets/Main.unity" };
            internal ChannelPlayerBuildService.PlayerBuildSummary Summary { get; set; } =
                new ChannelPlayerBuildService.PlayerBuildSummary(true, 0);
            internal bool WritePlayerFile { get; set; } = true;
            internal int RecreateCount { get; private set; }
            internal ChannelPlayerBuildService.PlayerBuildInvocation Invocation { get; private set; }

            public IReadOnlyList<string> GetEnabledScenes() => Scenes;
            public bool GetAndroidBuildAppBundle() => false;

            public void RecreateDirectory(string path)
            {
                RecreateCount++;
                Directory.CreateDirectory(path);
            }

            public ChannelPlayerBuildService.PlayerBuildSummary Build(
                ChannelPlayerBuildService.PlayerBuildInvocation invocation)
            {
                Invocation = invocation;
                m_Events.Add("build");
                if (WritePlayerFile)
                {
                    var path = invocation.Target == BuildTarget.Android
                        ? invocation.LocationPathName
                        : Path.Combine(m_Root, "player", "dev", "iOS", "1.2.3", "project.pbxproj");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    System.IO.File.WriteAllText(path, "player");
                }
                return Summary;
            }
        }

        private sealed class FakeResponder : IChannelBuildResponder
        {
            private readonly IList<string> m_Events;
            private readonly ChannelBuildResponderPhase? m_FailurePhase;
            private readonly string m_OutputRoot;
            private readonly string m_ResourcePath;
            private readonly string m_Warning;

            internal FakeResponder(
                string id,
                IReadOnlyList<string> dependencies,
                IList<string> events,
                string outputRoot,
                ChannelBuildResponderPhase? failurePhase = null,
                string resourcePath = null,
                string warning = null)
            {
                Id = id;
                DependsOn = dependencies;
                m_Events = events;
                m_OutputRoot = outputRoot;
                m_FailurePhase = failurePhase;
                m_ResourcePath = resourcePath;
                m_Warning = warning;
            }

            public string Id { get; }
            public int Order => 0;
            public IReadOnlyList<string> DependsOn { get; }
            public ChannelBuildStepResult Prepare(ChannelBuildContext context) => Result(ChannelBuildResponderPhase.Prepare);
            public ChannelBuildStepResult Apply(ChannelBuildContext context) => Result(ChannelBuildResponderPhase.Apply);
            public ChannelBuildStepResult Restore(ChannelBuildContext context) => Result(ChannelBuildResponderPhase.Restore);

            private ChannelBuildStepResult Result(ChannelBuildResponderPhase phase)
            {
                m_Events.Add(Id + ":" + phase.ToString().ToLowerInvariant());
                var success = m_FailurePhase != phase;
                IReadOnlyDictionary<string, string> outputs = null;
                if (success && Id == "resources" && phase == ChannelBuildResponderPhase.Apply)
                {
                    outputs = m_ResourcePath == null
                        ? new Dictionary<string, string> { ["resource.artifactCount"] = "0" }
                        : new Dictionary<string, string>
                        {
                            ["resource.artifactCount"] = "1",
                            ["resource.artifact.0000.kind"] = "resource-manifest",
                            ["resource.artifact.0000.path"] = m_ResourcePath
                                .Substring(m_OutputRoot.TrimEnd(Path.DirectorySeparatorChar).Length + 1)
                                .Replace('\\', '/')
                        };
                }
                return new ChannelBuildStepResult(
                    Id,
                    phase,
                    success,
                    success ? null : "expected failure",
                    outputs,
                    m_Warning == null ? null : new[] { m_Warning });
            }
        }
    }
}
