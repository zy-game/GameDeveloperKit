using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelBuildBrandingResponder : IChannelBuildResponder
    {
        public const string ResponderId = "branding";

        private static readonly IReadOnlyList<string> s_Dependencies =
            Array.AsReadOnly(new[] { ChannelBuildConfigResponder.ResponderId });

        private readonly IBrandingGateway m_Gateway;
        private ChannelBuildContext m_Context;
        private BrandingState m_Previous;
        private BrandingState m_Applied;
        private bool m_Prepared;
        private bool m_Mutated;

        public ChannelBuildBrandingResponder()
            : this(new UnityBrandingGateway())
        {
        }

        internal ChannelBuildBrandingResponder(IBrandingGateway gateway)
        {
            m_Gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public string Id => ResponderId;

        public int Order => 0;

        public IReadOnlyList<string> DependsOn => s_Dependencies;

        public ChannelBuildStepResult Prepare(ChannelBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (m_Prepared)
            {
                throw new GameException("Channel branding responder can only be prepared once.");
            }

            try
            {
                if (m_Gateway.IsTargetSupported(context.BuildTarget) is false)
                {
                    return Failure(ChannelBuildResponderPhase.Prepare, "Channel branding target is not supported.");
                }

                var previous = m_Gateway.Capture(context.BuildTarget);
                var profile = context.Profile;
                var icon = string.IsNullOrEmpty(profile?.IconPath)
                    ? null
                    : m_Gateway.LoadIcon(profile.IconPath);
                var splash = string.IsNullOrEmpty(profile?.SplashPath)
                    ? null
                    : m_Gateway.LoadSplash(profile.SplashPath);
                if (string.IsNullOrEmpty(profile?.IconPath) is false && icon == null)
                {
                    return Failure(ChannelBuildResponderPhase.Prepare, "Channel branding icon asset is invalid.");
                }
                if (string.IsNullOrEmpty(profile?.SplashPath) is false && splash == null)
                {
                    return Failure(ChannelBuildResponderPhase.Prepare, "Channel branding splash asset is invalid.");
                }
                if (icon != null && previous.Icons.Length == 0)
                {
                    return Failure(ChannelBuildResponderPhase.Prepare, "Channel branding target has no icon slots.");
                }

                var icons = previous.Icons;
                if (icon != null)
                {
                    icons = new Texture2D[previous.Icons.Length];
                    for (var i = 0; i < icons.Length; i++)
                    {
                        icons[i] = icon;
                    }
                }

                m_Context = context;
                m_Previous = previous;
                m_Applied = new BrandingState(
                    profile?.ProductName ?? previous.ProductName,
                    context.Version,
                    profile?.ApplicationIdentifier ?? previous.ApplicationIdentifier,
                    icons,
                    splash ?? previous.Splash);
                m_Prepared = true;
                return new ChannelBuildStepResult(ResponderId, ChannelBuildResponderPhase.Prepare, true);
            }
            catch (Exception exception)
            {
                return Failure(ChannelBuildResponderPhase.Prepare, exception.Message);
            }
        }

        public ChannelBuildStepResult Apply(ChannelBuildContext context)
        {
            ValidateContext(context);
            if (m_Mutated)
            {
                throw new GameException("Channel branding responder can only be applied once.");
            }

            m_Mutated = true;
            m_Gateway.Apply(context.BuildTarget, m_Applied);
            return new ChannelBuildStepResult(
                ResponderId,
                ChannelBuildResponderPhase.Apply,
                true,
                outputs: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["branding.applied"] = "productName,bundleVersion,applicationIdentifier,icons,splash"
                });
        }

        public ChannelBuildStepResult Restore(ChannelBuildContext context)
        {
            ValidateContext(context);
            if (m_Mutated)
            {
                m_Gateway.Apply(context.BuildTarget, m_Previous);
                m_Mutated = false;
            }

            return new ChannelBuildStepResult(ResponderId, ChannelBuildResponderPhase.Restore, true);
        }

        private static ChannelBuildStepResult Failure(
            ChannelBuildResponderPhase phase,
            string message)
        {
            var normalized = string.IsNullOrWhiteSpace(message)
                ? "Channel branding responder failed."
                : message.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return new ChannelBuildStepResult(ResponderId, phase, false, normalized);
        }

        private void ValidateContext(ChannelBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (m_Prepared is false)
            {
                throw new GameException("Channel branding responder is not prepared.");
            }
            if (ReferenceEquals(context, m_Context) is false)
            {
                throw new GameException("Channel branding responder context does not match Prepare.");
            }
        }

        internal interface IBrandingGateway
        {
            bool IsTargetSupported(BuildTarget target);
            BrandingState Capture(BuildTarget target);
            Texture2D LoadIcon(string path);
            Sprite LoadSplash(string path);
            void Apply(BuildTarget target, BrandingState state);
        }

        internal sealed class BrandingState
        {
            internal BrandingState(
                string productName,
                string bundleVersion,
                string applicationIdentifier,
                Texture2D[] icons,
                Sprite splash)
            {
                ProductName = productName;
                BundleVersion = bundleVersion;
                ApplicationIdentifier = applicationIdentifier;
                Icons = icons == null ? Array.Empty<Texture2D>() : (Texture2D[])icons.Clone();
                Splash = splash;
            }

            internal string ProductName { get; }
            internal string BundleVersion { get; }
            internal string ApplicationIdentifier { get; }
            internal Texture2D[] Icons { get; }
            internal Sprite Splash { get; }
        }

        private sealed class UnityBrandingGateway : IBrandingGateway
        {
            public bool IsTargetSupported(BuildTarget target)
            {
                return BuildPipeline.GetBuildTargetGroup(target) != BuildTargetGroup.Unknown;
            }

            public BrandingState Capture(BuildTarget target)
            {
                var group = BuildPipeline.GetBuildTargetGroup(target);
                return new BrandingState(
                    PlayerSettings.productName,
                    PlayerSettings.bundleVersion,
                    PlayerSettings.GetApplicationIdentifier(group),
                    PlayerSettings.GetIconsForTargetGroup(group),
                    PlayerSettings.SplashScreen.background);
            }

            public Texture2D LoadIcon(string path)
            {
                return LoadAsset<Texture2D>(path);
            }

            public Sprite LoadSplash(string path)
            {
                return LoadAsset<Sprite>(path);
            }

            public void Apply(BuildTarget target, BrandingState state)
            {
                var group = BuildPipeline.GetBuildTargetGroup(target);
                var exceptions = new List<Exception>();
                TryApply(() => PlayerSettings.productName = state.ProductName, exceptions);
                TryApply(() => PlayerSettings.bundleVersion = state.BundleVersion, exceptions);
                TryApply(
                    () => PlayerSettings.SetApplicationIdentifier(group, state.ApplicationIdentifier),
                    exceptions);
                TryApply(() => PlayerSettings.SetIconsForTargetGroup(group, state.Icons), exceptions);
                TryApply(() => PlayerSettings.SplashScreen.background = state.Splash, exceptions);
                if (exceptions.Count == 1)
                {
                    throw exceptions[0];
                }
                if (exceptions.Count > 1)
                {
                    throw new AggregateException(exceptions);
                }
            }

            private static void TryApply(Action action, ICollection<Exception> exceptions)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            private static T LoadAsset<T>(string path) where T : UnityEngine.Object
            {
                var normalized = path?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(normalized) ||
                    normalized.StartsWith("Assets/", StringComparison.Ordinal) is false)
                {
                    return null;
                }

                return AssetDatabase.LoadAssetAtPath<T>(normalized);
            }
        }
    }
}
