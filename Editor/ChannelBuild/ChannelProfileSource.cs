using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.ChannelBuild
{
    public static class ChannelProfileSource
    {
        public const int CurrentSchemaVersion = 1;
        public const string DefaultRelativePath =
            "ProjectSettings/GameDeveloperKit/channel-build-profiles.json";

        private static readonly HashSet<string> CatalogMembers = new HashSet<string>(
            new[] { "schemaVersion", "profiles" },
            StringComparer.Ordinal);

        private static readonly HashSet<string> ProfileMembers = new HashSet<string>(
            new[]
            {
                "id",
                "channel",
                "displayName",
                "productName",
                "applicationIdentifier",
                "iconPath",
                "splashPath",
                "defines",
                "configOverrides",
                "resourceOverrides",
                "platformOptions"
            },
            StringComparer.Ordinal);

        public static ChannelProfileCatalog Load(
            string projectRoot,
            string relativePath = DefaultRelativePath)
        {
            var catalogPath = ResolveCatalogPath(projectRoot, relativePath);
            string json;
            try
            {
                json = System.IO.File.ReadAllText(catalogPath, new UTF8Encoding(false, true));
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (DirectoryNotFoundException exception)
            {
                throw new FileNotFoundException(
                    "Channel profile catalog was not found.",
                    catalogPath,
                    exception);
            }
            catch (DecoderFallbackException exception)
            {
                throw InvalidCatalog(catalogPath, exception);
            }

            JObject root;
            try
            {
                RejectComments(json);
                var settings = new JsonLoadSettings
                {
                    CommentHandling = CommentHandling.Ignore,
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    LineInfoHandling = LineInfoHandling.Load
                };
                var token = JToken.Parse(json, settings);
                root = token as JObject;
                if (root == null)
                {
                    throw InvalidCatalog(catalogPath);
                }
            }
            catch (JsonReaderException exception)
            {
                throw InvalidCatalog(
                    catalogPath,
                    exception.LineNumber,
                    exception.LinePosition,
                    exception);
            }

            try
            {
                ValidateMembers(root, CatalogMembers, catalogPath);
                ValidateSchema(root["schemaVersion"], catalogPath);
                var profilesToken = root["profiles"] as JArray;
                if (profilesToken == null || profilesToken.Count == 0)
                {
                    throw InvalidCatalog(catalogPath);
                }

                var profiles = new List<ChannelProfile>(profilesToken.Count);
                for (var i = 0; i < profilesToken.Count; i++)
                {
                    var profileObject = profilesToken[i] as JObject;
                    if (profileObject == null)
                    {
                        throw InvalidCatalog(catalogPath, profilesToken[i]);
                    }

                    profiles.Add(ReadProfile(profileObject, catalogPath));
                }

                return new ChannelProfileCatalog(CurrentSchemaVersion, profiles);
            }
            catch (GameException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is OverflowException ||
                exception is FormatException)
            {
                throw InvalidCatalog(catalogPath, exception);
            }
        }

        public static ChannelProfile Merge(
            ChannelProfile profile,
            ChannelProfileOverrides overrides)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (overrides == null)
            {
                throw new ArgumentNullException(nameof(overrides));
            }

            return new ChannelProfile(
                profile.Id,
                overrides.Channel ?? profile.Channel,
                overrides.DisplayName ?? profile.DisplayName,
                overrides.ProductName ?? profile.ProductName,
                overrides.ApplicationIdentifier ?? profile.ApplicationIdentifier,
                overrides.IconPath ?? profile.IconPath,
                overrides.SplashPath ?? profile.SplashPath,
                overrides.Defines ?? profile.Defines,
                Overlay(profile.ConfigOverrides, overrides.ConfigOverrides),
                Overlay(profile.ResourceOverrides, overrides.ResourceOverrides),
                Overlay(profile.PlatformOptions, overrides.PlatformOptions));
        }

        private static string ResolveCatalogPath(string projectRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root cannot be empty.", nameof(projectRoot));
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Catalog path cannot be empty.", nameof(relativePath));
            }

            if (Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("Catalog path must be project-relative.", nameof(relativePath));
            }

            string rootPath;
            string catalogPath;
            try
            {
                rootPath = Path.GetFullPath(projectRoot);
                catalogPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                throw new ArgumentException("Catalog path is invalid.", nameof(relativePath), exception);
            }

            if (string.Equals(Path.GetExtension(catalogPath), ".json", StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new ArgumentException("Catalog path must have a .json extension.", nameof(relativePath));
            }

            var rootPrefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (catalogPath.StartsWith(rootPrefix, comparison) is false)
            {
                throw new ArgumentException("Catalog path must remain inside the project root.", nameof(relativePath));
            }

            return catalogPath;
        }

        private static IReadOnlyDictionary<string, string> Overlay(
            IReadOnlyDictionary<string, string> baseValues,
            IReadOnlyDictionary<string, string> overrideValues)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (baseValues != null)
            {
                foreach (var pair in baseValues)
                {
                    result.Add(pair.Key, pair.Value);
                }
            }

            if (overrideValues != null)
            {
                foreach (var pair in overrideValues)
                {
                    result[pair.Key] = pair.Value;
                }
            }

            return result;
        }

        private static ChannelProfile ReadProfile(JObject profile, string catalogPath)
        {
            ValidateMembers(profile, ProfileMembers, catalogPath);
            return new ChannelProfile(
                ReadString(profile, "id", false, catalogPath),
                ReadString(profile, "channel", false, catalogPath),
                ReadString(profile, "displayName", true, catalogPath),
                ReadString(profile, "productName", true, catalogPath),
                ReadString(profile, "applicationIdentifier", true, catalogPath),
                ReadString(profile, "iconPath", true, catalogPath),
                ReadString(profile, "splashPath", true, catalogPath),
                ReadStringList(profile, "defines", catalogPath),
                ReadStringDictionary(profile, "configOverrides", catalogPath),
                ReadStringDictionary(profile, "resourceOverrides", catalogPath),
                ReadStringDictionary(profile, "platformOptions", catalogPath));
        }

        private static string ReadString(
            JObject value,
            string name,
            bool optional,
            string catalogPath)
        {
            var token = value[name];
            if (token == null)
            {
                if (optional)
                {
                    return null;
                }

                throw InvalidCatalog(catalogPath, value);
            }

            if (token.Type == JTokenType.Null && optional)
            {
                return null;
            }

            if (token.Type != JTokenType.String)
            {
                throw InvalidCatalog(catalogPath, token);
            }

            return token.Value<string>();
        }

        private static IReadOnlyList<string> ReadStringList(
            JObject value,
            string name,
            string catalogPath)
        {
            var token = value[name];
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            var array = token as JArray;
            if (array == null)
            {
                throw InvalidCatalog(catalogPath, token);
            }

            var result = new List<string>(array.Count);
            for (var i = 0; i < array.Count; i++)
            {
                if (array[i].Type != JTokenType.String)
                {
                    throw InvalidCatalog(catalogPath, array[i]);
                }

                result.Add(array[i].Value<string>());
            }

            return result;
        }

        private static IReadOnlyDictionary<string, string> ReadStringDictionary(
            JObject value,
            string name,
            string catalogPath)
        {
            var token = value[name];
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            var dictionaryObject = token as JObject;
            if (dictionaryObject == null)
            {
                throw InvalidCatalog(catalogPath, token);
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in dictionaryObject.Properties())
            {
                if (property.Value.Type != JTokenType.String)
                {
                    throw InvalidCatalog(catalogPath, property.Value);
                }

                result.Add(property.Name, property.Value.Value<string>());
            }

            return result;
        }

        private static void ValidateMembers(
            JObject value,
            HashSet<string> allowedMembers,
            string catalogPath)
        {
            foreach (var property in value.Properties())
            {
                if (allowedMembers.Contains(property.Name) is false)
                {
                    throw InvalidCatalog(catalogPath, property);
                }
            }
        }

        private static void ValidateSchema(JToken schemaToken, string catalogPath)
        {
            if (schemaToken == null ||
                schemaToken.Type != JTokenType.Integer ||
                schemaToken.Value<long>() != CurrentSchemaVersion)
            {
                throw InvalidCatalog(catalogPath, schemaToken);
            }
        }

        private static void RejectComments(string json)
        {
            using (var textReader = new StringReader(json))
            using (var reader = new JsonTextReader(textReader))
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.Comment)
                    {
                        throw new JsonReaderException(
                            "Comments are not supported.",
                            reader.Path,
                            reader.LineNumber,
                            reader.LinePosition,
                            null);
                    }
                }
            }
        }

        private static GameException InvalidCatalog(string catalogPath, Exception exception = null)
        {
            return new GameException($"Channel profile catalog is invalid: {catalogPath}", exception);
        }

        private static GameException InvalidCatalog(string catalogPath, JToken token)
        {
            if (token is IJsonLineInfo lineInfo && lineInfo.HasLineInfo())
            {
                return InvalidCatalog(catalogPath, lineInfo.LineNumber, lineInfo.LinePosition, null);
            }

            return InvalidCatalog(catalogPath);
        }

        private static GameException InvalidCatalog(
            string catalogPath,
            int lineNumber,
            int linePosition,
            Exception exception)
        {
            return new GameException(
                $"Channel profile catalog is invalid at line {lineNumber}, position {linePosition}: {catalogPath}",
                exception);
        }
    }
}
