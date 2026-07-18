using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using IOFile = System.IO.File;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelBuildConfigResponder : IChannelBuildResponder
    {
        public const string ResponderId = "config";
        public const string RelativeConfigPath =
            "Assets/StreamingAssets/GameDeveloperKit/channel-config.json";

        private static readonly IReadOnlyList<string> Dependencies =
            Array.AsReadOnly(new[] { ChannelBuildDefinesResponder.ResponderId });

        private readonly string m_ConfigPath;
        private readonly Action<string> m_ImportAsset;
        private readonly Action m_RefreshAssets;
        private ChannelBuildContext m_Context;
        private string m_TargetPath;
        private string m_Json;
        private ChannelBuildFileMutationState m_FileState;
        private bool m_Prepared;
        private bool m_Applied;

        public ChannelBuildConfigResponder()
            : this(
                RelativeConfigPath,
                path => AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport),
                () => AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport))
        {
        }

        internal ChannelBuildConfigResponder(
            string configPath,
            Action<string> importAsset,
            Action refreshAssets)
        {
            m_ConfigPath = ChannelBuildContext.RequireText(configPath, nameof(configPath));
            m_ImportAsset = importAsset ?? throw new ArgumentNullException(nameof(importAsset));
            m_RefreshAssets = refreshAssets ?? throw new ArgumentNullException(nameof(refreshAssets));
        }

        public string Id => ResponderId;

        public int Order => 0;

        public IReadOnlyList<string> DependsOn => Dependencies;

        public ChannelBuildStepResult Prepare(ChannelBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (m_Prepared)
            {
                throw new GameException("Channel config responder can only be prepared once.");
            }

            try
            {
                m_TargetPath = ResolveTargetPath(m_ConfigPath);
                m_Json = CreateJson(context);
                m_Context = context;
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
                throw new GameException("Channel config responder can only be applied once.");
            }

            m_Applied = true;
            m_FileState = new ChannelBuildFileMutationState(new[] { m_TargetPath });
            m_FileState.Capture();
            WriteAtomic(m_TargetPath, m_Json);
            m_ImportAsset(m_ConfigPath);
            return new ChannelBuildStepResult(
                ResponderId,
                ChannelBuildResponderPhase.Apply,
                true,
                outputs: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["config.path"] = m_ConfigPath.Replace('\\', '/'),
                    ["config.valueCount"] = (context.Profile?.ConfigOverrides.Count ?? 0).ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                });
        }

        public ChannelBuildStepResult Restore(ChannelBuildContext context)
        {
            ValidateContext(context);
            if (m_FileState == null)
            {
                return new ChannelBuildStepResult(ResponderId, ChannelBuildResponderPhase.Restore, true);
            }

            var failureCount = m_FileState.Restore();
            if (failureCount > 0)
            {
                return Failure(
                    ChannelBuildResponderPhase.Restore,
                    $"Failed to restore {failureCount} channel config file(s).");
            }

            m_FileState = null;
            m_Applied = false;
            m_RefreshAssets();
            return new ChannelBuildStepResult(ResponderId, ChannelBuildResponderPhase.Restore, true);
        }

        internal static string CreateJson(ChannelBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var sourceValues = context.Profile?.ConfigOverrides;
            if (sourceValues != null)
            {
                foreach (var pair in sourceValues)
                {
                    values.Add(pair.Key, pair.Value);
                }
            }

            var payload = new ChannelConfigPayload(
                context.Channel,
                context.Environment.ToString().ToLowerInvariant(),
                context.Flavor,
                new ReadOnlyDictionary<string, string>(values));
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Culture = System.Globalization.CultureInfo.InvariantCulture,
                NullValueHandling = NullValueHandling.Include
            };
            return JsonConvert.SerializeObject(payload, Formatting.Indented, settings);
        }

        internal static void WriteAtomic(string targetPath, string content)
        {
            ChannelBuildContext.RequireText(targetPath, nameof(targetPath));
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var fullPath = Path.GetFullPath(targetPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Channel config directory is invalid.", nameof(targetPath));
            }

            Directory.CreateDirectory(directory);
            var tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                IOFile.WriteAllText(tempPath, content, new UTF8Encoding(false));
                if (IOFile.Exists(fullPath))
                {
                    IOFile.Replace(tempPath, fullPath, null);
                }
                else
                {
                    IOFile.Move(tempPath, fullPath);
                }
            }
            finally
            {
                if (IOFile.Exists(tempPath))
                {
                    IOFile.Delete(tempPath);
                }
            }
        }

        private static string ResolveTargetPath(string configPath)
        {
            var projectRoot = Path.GetFullPath(".");
            var targetPath = Path.GetFullPath(configPath);
            var prefix = projectRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (targetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new InvalidOperationException("Channel config path escaped the project root.");
            }

            return targetPath;
        }

        private static ChannelBuildStepResult Failure(
            ChannelBuildResponderPhase phase,
            string message)
        {
            var normalized = string.IsNullOrWhiteSpace(message)
                ? "Channel config responder failed."
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
                throw new GameException("Channel config responder is not prepared.");
            }
            if (ReferenceEquals(context, m_Context) is false)
            {
                throw new GameException("Channel config responder context does not match Prepare.");
            }
        }

        private sealed class ChannelConfigPayload
        {
            internal ChannelConfigPayload(
                string channel,
                string environment,
                string flavor,
                IReadOnlyDictionary<string, string> values)
            {
                Channel = channel;
                Environment = environment;
                Flavor = flavor;
                Values = values;
            }

            [JsonProperty(Order = 1)]
            public int SchemaVersion => 1;

            [JsonProperty(Order = 2)]
            public string Channel { get; }

            [JsonProperty(Order = 3)]
            public string Environment { get; }

            [JsonProperty(Order = 4)]
            public string Flavor { get; }

            [JsonProperty(Order = 5)]
            public IReadOnlyDictionary<string, string> Values { get; }
        }
    }
}
