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
            if (TryNormalizeProjectPath(luban.TableDirectory, "配置表目录", out var tableDirectory, out error) is false ||
                TryNormalizeProjectPath(luban.GeneratedCodeDirectory, "生成代码目录", out var codeDirectory, out error) is false ||
                TryNormalizeProjectPath(luban.GeneratedDataDirectory, "导出数据目录", out var dataDirectory, out error) is false)
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
            localization.TableId = localization.TableId?.Trim() ?? string.Empty;
            localization.KeyField = localization.KeyField?.Trim() ?? string.Empty;
            localization.PreviewLocale = localization.PreviewLocale?.Trim() ?? string.Empty;
            if (localization.KeyField.Length == 0)
            {
                error = "本地化 Key 字段不能为空。";
                return false;
            }

            if (IsValidLocale(localization.PreviewLocale) is false)
            {
                error = $"预览语言标识无效：{localization.PreviewLocale}";
                return false;
            }

            var locales = new HashSet<string>(StringComparer.Ordinal);
            for (var i = localization.LocaleFields.Count - 1; i >= 0; i--)
            {
                var field = localization.LocaleFields[i];
                if (field == null)
                {
                    localization.LocaleFields.RemoveAt(i);
                    continue;
                }

                field.Locale = field.Locale?.Trim() ?? string.Empty;
                field.FieldName = field.FieldName?.Trim() ?? string.Empty;
                if (field.Locale.Length == 0 && field.FieldName.Length == 0)
                {
                    localization.LocaleFields.RemoveAt(i);
                    continue;
                }

                if (field.Locale.Length == 0 || field.FieldName.Length == 0)
                {
                    error = "本地化语言映射必须同时填写 Locale 和字段名。";
                    return false;
                }

                if (IsValidLocale(field.Locale) is false)
                {
                    error = $"本地化语言标识无效：{field.Locale}";
                    return false;
                }

                if (locales.Add(field.Locale) is false)
                {
                    error = $"本地化语言重复：{field.Locale}";
                    return false;
                }
            }

            luban.TableDirectory = tableDirectory;
            luban.GeneratedCodeDirectory = codeDirectory;
            luban.GeneratedDataDirectory = dataDirectory;
            luban.CodeNamespace = codeNamespace;
            return true;
        }

        private static bool TryNormalizeProjectPath(
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
                error = $"{label}必须是项目相对路径：{value}";
                return false;
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
                    error = $"{label}不能跳出项目目录：{value}";
                    return false;
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

        private static bool IsValidLocale(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var segments = value.Split('-');
            if (segments.Length == 0 || segments[0].Length < 2 || segments[0].Length > 8 || IsAlphaNumeric(segments[0], false) is false)
            {
                return false;
            }

            for (var i = 1; i < segments.Length; i++)
            {
                if (segments[i].Length < 1 || segments[i].Length > 8 || IsAlphaNumeric(segments[i], true) is false)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAlphaNumeric(string value, bool allowDigits)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (char.IsLetter(value[i]) || allowDigits && char.IsDigit(value[i]))
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
