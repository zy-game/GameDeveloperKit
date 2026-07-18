using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using GameDeveloperKit.ChannelBuild;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class ChannelBuildBrandingResponderTests
    {
        [Test]
        public void Runner_AppliesCompleteBrandingAndRestoresPreviousState()
        {
            var icon = NewTexture();
            var splash = NewSprite();
            var previousIcon = NewTexture();
            var previousSplash = NewSprite();
            var gateway = new FakeGateway(new ChannelBuildBrandingResponder.BrandingState(
                "Original",
                "0.9.0",
                "com.example.original",
                new[] { previousIcon, previousIcon },
                previousSplash))
            {
                Icon = icon,
                Splash = splash
            };
            var profile = new ChannelProfile(
                "dev-profile",
                "dev",
                productName: "Dev Product",
                applicationIdentifier: "com.example.dev",
                iconPath: "Assets/icon.png",
                splashPath: "Assets/splash.png");
            var context = CreateContext(profile);

            var execution = ChannelBuildResponderRunner.Execute(
                context,
                new IChannelBuildResponder[]
                {
                    new SucceededDependency(ChannelBuildConfigResponder.ResponderId),
                    new ChannelBuildBrandingResponder(gateway)
                },
                operationContext =>
                {
                    AssertState(
                        gateway.Current,
                        "Dev Product",
                        "1.2.3",
                        "com.example.dev",
                        icon,
                        splash);
                    return Result(ChannelBuildResponderPhase.Operation, false, "expected failure");
                });

            Assert.IsFalse(execution.Success);
            AssertState(
                gateway.Current,
                "Original",
                "0.9.0",
                "com.example.original",
                previousIcon,
                previousSplash);
        }

        [Test]
        public void NullProfile_OnlyChangesBundleVersion()
        {
            var icon = NewTexture();
            var splash = NewSprite();
            var gateway = new FakeGateway(new ChannelBuildBrandingResponder.BrandingState(
                "Original",
                "0.9.0",
                "com.example.original",
                new[] { icon },
                splash));
            var context = CreateContext();
            var responder = new ChannelBuildBrandingResponder(gateway);

            Assert.IsTrue(responder.Prepare(context).Success);
            Assert.IsTrue(responder.Apply(context).Success);
            AssertState(
                gateway.Current,
                "Original",
                "1.2.3",
                "com.example.original",
                icon,
                splash);
            Assert.IsTrue(responder.Restore(context).Success);
            Assert.AreEqual("0.9.0", gateway.Current.BundleVersion);
        }

        [Test]
        public void Prepare_RejectsUnsupportedTargetInvalidAssetsAndNoIconSlots()
        {
            var profile = new ChannelProfile(
                "dev-profile",
                "dev",
                iconPath: "Assets/icon.png",
                splashPath: "Assets/splash.png");
            var context = CreateContext(profile);

            var unsupported = new FakeGateway(s_EmptyState) { Supported = false };
            Assert.IsFalse(new ChannelBuildBrandingResponder(unsupported).Prepare(context).Success);

            var invalidIcon = new FakeGateway(s_EmptyState) { Splash = NewSprite() };
            Assert.IsFalse(new ChannelBuildBrandingResponder(invalidIcon).Prepare(context).Success);

            var invalidSplash = new FakeGateway(s_EmptyState) { Icon = NewTexture() };
            Assert.IsFalse(new ChannelBuildBrandingResponder(invalidSplash).Prepare(context).Success);

            var noSlots = new FakeGateway(s_EmptyState) { Icon = NewTexture(), Splash = NewSprite() };
            Assert.IsFalse(new ChannelBuildBrandingResponder(noSlots).Prepare(context).Success);
            Assert.AreEqual(0, noSlots.ApplyCount);
        }

        [Test]
        public void Runner_ApplyExceptionStillRestoresPreviousSnapshot()
        {
            var gateway = new FakeGateway(new ChannelBuildBrandingResponder.BrandingState(
                "Original", "0.9.0", "com.original", new[] { NewTexture() }, null))
            {
                ThrowAfterApply = true
            };
            var context = CreateContext(new ChannelProfile("dev-profile", "dev", productName: "Changed"));

            Assert.Throws<GameException>(() => ChannelBuildResponderRunner.Execute(
                context,
                new IChannelBuildResponder[]
                {
                    new SucceededDependency(ChannelBuildConfigResponder.ResponderId),
                    new ChannelBuildBrandingResponder(gateway)
                },
                operationContext => Result(ChannelBuildResponderPhase.Operation)));

            Assert.AreEqual("Original", gateway.Current.ProductName);
            Assert.AreEqual(2, gateway.ApplyCount);
        }

        [Test]
        public void Responder_RejectsReuseAndContextMismatch()
        {
            var gateway = new FakeGateway(new ChannelBuildBrandingResponder.BrandingState(
                "Original", "0.9.0", "com.original", new[] { NewTexture() }, null));
            var context = CreateContext();
            var responder = new ChannelBuildBrandingResponder(gateway);

            Assert.IsTrue(responder.Prepare(context).Success);
            Assert.Throws<GameException>(() => responder.Prepare(context));
            Assert.Throws<GameException>(() => responder.Apply(CreateContext()));
        }

        private static readonly ChannelBuildBrandingResponder.BrandingState s_EmptyState =
            new ChannelBuildBrandingResponder.BrandingState(
                "Original", "0.9.0", "com.original", Array.Empty<Texture2D>(), null);

        private static ChannelBuildContext CreateContext(ChannelProfile profile = null)
        {
            return new ChannelBuildContext(
                "dev",
                ChannelBuildEnvironment.Dev,
                BuildTarget.Android,
                "1.2.3",
                1,
                "Build/Channel",
                profile: profile);
        }

        private static ChannelBuildStepResult Result(
            ChannelBuildResponderPhase phase,
            bool success = true,
            string message = null)
        {
            return new ChannelBuildStepResult("operation", phase, success, message);
        }

        private static Texture2D NewTexture()
        {
            return (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
        }

        private static Sprite NewSprite()
        {
            return (Sprite)FormatterServices.GetUninitializedObject(typeof(Sprite));
        }

        private static void AssertState(
            ChannelBuildBrandingResponder.BrandingState state,
            string product,
            string version,
            string identifier,
            Texture2D icon,
            Sprite splash)
        {
            Assert.AreEqual(product, state.ProductName);
            Assert.AreEqual(version, state.BundleVersion);
            Assert.AreEqual(identifier, state.ApplicationIdentifier);
            Assert.IsTrue(state.Icons.Length > 0);
            Assert.AreSame(icon, state.Icons[0]);
            Assert.AreSame(splash, state.Splash);
        }

        private sealed class FakeGateway : ChannelBuildBrandingResponder.IBrandingGateway
        {
            internal FakeGateway(ChannelBuildBrandingResponder.BrandingState state)
            {
                Current = state;
            }

            internal bool Supported { get; set; } = true;
            internal Texture2D Icon { get; set; }
            internal Sprite Splash { get; set; }
            internal bool ThrowAfterApply { get; set; }
            internal int ApplyCount { get; private set; }
            internal ChannelBuildBrandingResponder.BrandingState Current { get; private set; }

            public bool IsTargetSupported(BuildTarget target) => Supported;
            public ChannelBuildBrandingResponder.BrandingState Capture(BuildTarget target) => Current;
            public Texture2D LoadIcon(string path) => Icon;
            public Sprite LoadSplash(string path) => Splash;

            public void Apply(BuildTarget target, ChannelBuildBrandingResponder.BrandingState state)
            {
                ApplyCount++;
                Current = state;
                if (ThrowAfterApply && ApplyCount == 1)
                {
                    throw new InvalidOperationException("apply failed");
                }
            }
        }

        private sealed class SucceededDependency : IChannelBuildResponder
        {
            internal SucceededDependency(string id) => Id = id;
            public string Id { get; }
            public int Order => 0;
            public IReadOnlyList<string> DependsOn => Array.Empty<string>();
            public ChannelBuildStepResult Prepare(ChannelBuildContext context) => Step(ChannelBuildResponderPhase.Prepare);
            public ChannelBuildStepResult Apply(ChannelBuildContext context) => Step(ChannelBuildResponderPhase.Apply);
            public ChannelBuildStepResult Restore(ChannelBuildContext context) => Step(ChannelBuildResponderPhase.Restore);
            private ChannelBuildStepResult Step(ChannelBuildResponderPhase phase) =>
                new ChannelBuildStepResult(Id, phase, true);
        }
    }
}
