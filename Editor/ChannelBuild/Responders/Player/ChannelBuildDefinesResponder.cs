using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelBuildDefinesResponder : IChannelBuildResponder
    {
        public const string ResponderId = "defines";

        private readonly Func<BuildTarget, bool> m_IsTargetSupported;
        private readonly Func<BuildTarget, string> m_GetSymbols;
        private readonly Action<BuildTarget, string> m_SetSymbols;
        private ChannelBuildContext m_Context;
        private BuildTarget m_BuildTarget;
        private string m_PreviousSymbols;
        private string m_AppliedSymbols;
        private bool m_Prepared;
        private bool m_Applied;

        public ChannelBuildDefinesResponder()
            : this(IsTargetSupported, GetSymbols, SetSymbols)
        {
        }

        internal ChannelBuildDefinesResponder(
            Func<BuildTarget, bool> isTargetSupported,
            Func<BuildTarget, string> getSymbols,
            Action<BuildTarget, string> setSymbols)
        {
            m_IsTargetSupported = isTargetSupported ?? throw new ArgumentNullException(nameof(isTargetSupported));
            m_GetSymbols = getSymbols ?? throw new ArgumentNullException(nameof(getSymbols));
            m_SetSymbols = setSymbols ?? throw new ArgumentNullException(nameof(setSymbols));
        }

        public string Id => ResponderId;

        public int Order => 0;

        public IReadOnlyList<string> DependsOn => Array.Empty<string>();

        public ChannelBuildStepResult Prepare(ChannelBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (m_Prepared)
            {
                throw new GameException("Channel defines responder can only be prepared once.");
            }

            if (m_IsTargetSupported(context.BuildTarget) is false)
            {
                return Failure(ChannelBuildResponderPhase.Prepare, "Channel build target is not supported.");
            }

            try
            {
                var previousSymbols = m_GetSymbols(context.BuildTarget);
                if (TryCreateSymbols(context, previousSymbols, out var appliedSymbols, out var error) is false)
                {
                    return Failure(ChannelBuildResponderPhase.Prepare, error);
                }

                m_Context = context;
                m_BuildTarget = context.BuildTarget;
                m_PreviousSymbols = previousSymbols;
                m_AppliedSymbols = appliedSymbols;
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
            if (m_Applied)
            {
                throw new GameException("Channel defines responder can only be applied once.");
            }

            m_Applied = true;
            m_SetSymbols(m_BuildTarget, m_AppliedSymbols);
            return new ChannelBuildStepResult(
                ResponderId,
                ChannelBuildResponderPhase.Apply,
                true,
                outputs: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["defines.count"] = SplitSymbols(m_AppliedSymbols).Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                });
        }

        public ChannelBuildStepResult Restore(ChannelBuildContext context)
        {
            ValidateContext(context);
            if (m_Applied)
            {
                m_SetSymbols(m_BuildTarget, m_PreviousSymbols);
                m_Applied = false;
            }

            return new ChannelBuildStepResult(ResponderId, ChannelBuildResponderPhase.Restore, true);
        }

        internal static bool TryCreateSymbols(
            ChannelBuildContext context,
            string previousSymbols,
            out string appliedSymbols,
            out string error)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var symbols = new SortedSet<string>(SplitSymbols(previousSymbols), StringComparer.Ordinal)
            {
                "GDK_CHANNEL_" + ToSymbolToken(context.Channel),
                "GDK_ENV_" + context.Environment.ToString().ToUpperInvariant()
            };
            if (string.IsNullOrEmpty(context.Flavor) is false)
            {
                symbols.Add("GDK_FLAVOR_" + ToSymbolToken(context.Flavor));
            }

            var profileSymbols = context.Profile?.Defines ?? Array.Empty<string>();
            for (var i = 0; i < profileSymbols.Count; i++)
            {
                var symbol = profileSymbols[i]?.Trim();
                if (IsValidSymbol(symbol) is false)
                {
                    appliedSymbols = null;
                    error = "Channel profile contains an invalid scripting define symbol.";
                    return false;
                }

                symbols.Add(symbol);
            }

            appliedSymbols = string.Join(";", symbols);
            error = null;
            return true;
        }

        private static IReadOnlyList<string> SplitSymbols(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(symbol => symbol.Trim())
                .Where(symbol => symbol.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string ToSymbolToken(string value)
        {
            return value.Replace('-', '_').Replace('.', '_').ToUpperInvariant();
        }

        private static bool IsValidSymbol(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                (char.IsLetter(value[0]) is false && value[0] != '_'))
            {
                return false;
            }

            for (var i = 1; i < value.Length; i++)
            {
                if (char.IsLetterOrDigit(value[i]) is false && value[i] != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsTargetSupported(BuildTarget target)
        {
            return BuildPipeline.GetBuildTargetGroup(target) != BuildTargetGroup.Unknown;
        }

        private static string GetSymbols(BuildTarget target)
        {
            return PlayerSettings.GetScriptingDefineSymbols(
                NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target)));
        }

        private static void SetSymbols(BuildTarget target, string symbols)
        {
            PlayerSettings.SetScriptingDefineSymbols(
                NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target)),
                symbols);
        }

        private static ChannelBuildStepResult Failure(
            ChannelBuildResponderPhase phase,
            string message)
        {
            var normalized = string.IsNullOrWhiteSpace(message)
                ? "Channel defines responder failed."
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
                throw new GameException("Channel defines responder is not prepared.");
            }
            if (ReferenceEquals(context, m_Context) is false)
            {
                throw new GameException("Channel defines responder context does not match Prepare.");
            }
        }
    }
}
