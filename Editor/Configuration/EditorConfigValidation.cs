using System;
using System.Collections.Generic;
using System.IO;

namespace GameDeveloperKit.EditorConfiguration
{
    internal static class EditorConfigValidation
    {
        private static readonly HashSet<string> s_CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };

        public static bool TryNormalize(EditorGlobalConfig config, out string error)
        {
            error = null;
            var luban = config.Luban;
            if (TryNormalizePath(luban.TableDirectory, "配置表目录", out var tableDirectory, out error) is false ||
                TryNormalizePath(luban.GeneratedCodeDirectory, "生成代码目录", out var codeDirectory, out error) is false ||
                TryNormalizePath(luban.GeneratedDataDirectory, "导出数据目录", out var dataDirectory, out error) is false)
            {
                return false;
            }

            var codeNamespace = luban.CodeNamespace?.Trim() ?? string.Empty;
            if (IsValidNamespace(codeNamespace) is false)
            {
                error = $"代码命名空间无效：{luban.CodeNamespace}";
                return false;
            }

            var localization = config.Localization;
            localization.CatalogAssetGuid = localization.CatalogAssetGuid?.Trim() ?? string.Empty;
            localization.PreviewLocale = localization.PreviewLocale?.Trim() ?? string.Empty;

            var storyMedia = config.StoryMedia;
            if (TryNormalizeOptionalHttpsUrl(
                    storyMedia.CatalogApiUrl,
                    "Catalog API URL",
                    out var catalogApiUrl,
                    out error) is false ||
                TryNormalizeOptionalHttpsUrl(
                    storyMedia.CdnBaseUrl,
                    "CDN Base URL",
                    out var cdnBaseUrl,
                    out error) is false)
            {
                return false;
            }

            var cloud = config.Cloud;
            if (TryNormalizeCloud(cloud, out error) is false)
            {
                return false;
            }

            luban.TableDirectory = tableDirectory;
            luban.GeneratedCodeDirectory = codeDirectory;
            luban.GeneratedDataDirectory = dataDirectory;
            luban.CodeNamespace = codeNamespace;
            storyMedia.CatalogApiUrl = catalogApiUrl;
            storyMedia.CdnBaseUrl = cdnBaseUrl;
            return true;
        }

        private static bool TryNormalizeCloud(CloudProjectConfig cloud, out string error)
        {
            error = null;
            cloud.ProviderId = cloud.ProviderId?.Trim() ?? string.Empty;
            cloud.CredentialProfileName = cloud.CredentialProfileName?.Trim() ?? string.Empty;
            cloud.Bucket = cloud.Bucket?.Trim() ?? string.Empty;
            cloud.Region = cloud.Region?.Trim() ?? string.Empty;
            cloud.Endpoint = cloud.Endpoint?.Trim() ?? string.Empty;
            cloud.RootPrefix = cloud.RootPrefix?.Trim().Trim('/') ?? string.Empty;

            var hasAnyValue = cloud.ProviderId.Length > 0 ||
                              cloud.CredentialProfileName.Length > 0 ||
                              cloud.Bucket.Length > 0 ||
                              cloud.Region.Length > 0 ||
                              cloud.Endpoint.Length > 0 ||
                              cloud.RootPrefix.Length > 0;
            if (hasAnyValue is false)
            {
                return true;
            }

            if (cloud.ProviderId.Length == 0 ||
                cloud.CredentialProfileName.Length == 0 ||
                cloud.Bucket.Length == 0 ||
                cloud.Region.Length == 0)
            {
                error = "云配置必须填写 Provider、凭证 Profile、Bucket 和 Region。";
                return false;
            }

            if (TryNormalizeCloudEndpoint(
                    cloud.Endpoint,
                    out var endpoint,
                    out error) is false)
            {
                return false;
            }

            if (IsValidObjectPrefix(cloud.RootPrefix) is false)
            {
                error = "云根前缀必须是相对对象路径，不能包含反斜杠、空段、点段或控制字符。";
                return false;
            }

            cloud.Endpoint = endpoint;
            return true;
        }

        private static bool TryNormalizeCloudEndpoint(
            string value,
            out string normalized,
            out string error)
        {
            normalized = value?.Trim().TrimEnd('/') ?? string.Empty;
            error = null;
            if (normalized.Length == 0)
            {
                return true;
            }

            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(uri.Host) is false &&
                string.IsNullOrWhiteSpace(uri.UserInfo) &&
                (uri.AbsolutePath.Length == 0 || uri.AbsolutePath == "/") &&
                string.IsNullOrEmpty(uri.Query) &&
                string.IsNullOrEmpty(uri.Fragment))
            {
                return true;
            }

            error = "Cloud Endpoint 必须是无路径、查询和片段的 HTTPS origin。";
            return false;
        }

        private static bool IsValidObjectPrefix(string prefix)
        {
            if (prefix.Length == 0)
            {
                return true;
            }

            if (prefix.IndexOf('\\') >= 0 || Uri.TryCreate(prefix, UriKind.Absolute, out _))
            {
                return false;
            }

            var segments = prefix.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 ||
                    string.Equals(segments[i], ".", StringComparison.Ordinal) ||
                    string.Equals(segments[i], "..", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (var i = 0; i < prefix.Length; i++)
            {
                if (char.IsControl(prefix[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryNormalizeOptionalHttpsUrl(
            string value,
            string label,
            out string normalized,
            out string error)
        {
            normalized = value?.Trim() ?? string.Empty;
            error = null;
            if (normalized.Length == 0)
            {
                return true;
            }

            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(uri.Host) is false &&
                string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                return true;
            }

            error = $"{label} 必须是绝对 HTTPS URL。";
            return false;
        }

        private static bool TryNormalizePath(
            string value,
            string label,
            out string normalized,
            out string error)
        {
            normalized = (value ?? string.Empty).Trim().Replace('\\', '/');
            error = null;
            if (normalized.Length == 0)
            {
                error = $"{label}不能为空。";
                return false;
            }

            if (Path.IsPathRooted(normalized))
            {
                try
                {
                    normalized = Path.GetFullPath(normalized).Replace('\\', '/');
                    return true;
                }
                catch (Exception exception)
                {
                    error = $"{label}无效：{exception.Message}";
                    return false;
                }
            }

            var result = new List<string>();
            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                if (string.Equals(segments[i], ".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(segments[i], "..", StringComparison.Ordinal))
                {
                    if (result.Count > 0 && string.Equals(result[result.Count - 1], "..", StringComparison.Ordinal) is false)
                    {
                        result.RemoveAt(result.Count - 1);
                    }
                    else
                    {
                        result.Add(segments[i]);
                    }

                    continue;
                }

                result.Add(segments[i]);
            }

            normalized = string.Join("/", result);
            if (normalized.Length == 0)
            {
                error = $"{label}不能为空。";
                return false;
            }

            return true;
        }

        private static bool IsValidNamespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var segments = value.Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                if (IsValidIdentifier(segments[i]) is false)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || s_CSharpKeywords.Contains(value) ||
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

    }
}
