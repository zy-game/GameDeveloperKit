using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GameDeveloperKit.DesignImporter
{
    internal static class DesignManifestCodec
    {
        public static DesignDocument ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("清单路径不能为空。", nameof(path));
            }

            var document = Parse(System.IO.File.ReadAllText(path));
            document.SourceLocation = Path.GetFullPath(path);
            return document;
        }

        public static DesignDocument Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("设计清单为空。");
            }

            DesignDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<DesignDocument>(json);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("设计清单不是有效 JSON。", exception);
            }

            if (document == null)
            {
                throw new InvalidDataException("无法读取设计清单。");
            }

            document.Normalize();
            var errors = Validate(document).Where(x => x.IsError).Select(x => x.Message).ToArray();
            if (errors.Length > 0)
            {
                throw new InvalidDataException(string.Join(Environment.NewLine, errors));
            }

            return document;
        }

        public static string Serialize(DesignDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            document.Normalize();
            return JsonConvert.SerializeObject(document, Formatting.Indented);
        }

        public static IReadOnlyList<DesignValidationIssue> Validate(DesignDocument document)
        {
            var issues = new List<DesignValidationIssue>();
            if (document == null)
            {
                issues.Add(DesignValidationIssue.Error("设计清单为空。"));
                return issues;
            }

            if (!string.Equals(document.SchemaVersion, DesignManifestSchema.CurrentVersion, StringComparison.Ordinal))
            {
                issues.Add(DesignValidationIssue.Error(
                    $"不支持清单版本 {document.SchemaVersion}，当前版本为 {DesignManifestSchema.CurrentVersion}。"));
            }

            if (document.Pages == null || document.Pages.Count == 0)
            {
                issues.Add(DesignValidationIssue.Error("设计清单没有页面。"));
                return issues;
            }

            if (document.Source == DesignSourceKind.Lanhu &&
                document.Pages.Any(page => page?.Root != null) &&
                document.Pages.Where(page => page?.Root != null).All(IsFlattenedLanhuPage))
            {
                issues.Add(DesignValidationIssue.Error(
                    "这是旧版蓝湖整页截图清单，只有单个 Image，没有图层、文本和切图。" +
                    "请在设计导入器中重新点击“复制脚本”，并导入文件名包含 layered-design-manifest 的新清单。"));
                return issues;
            }

            var pageIds = new HashSet<string>(StringComparer.Ordinal);
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in document.Assets ?? Enumerable.Empty<DesignAsset>())
            {
                if (asset == null)
                {
                    issues.Add(DesignValidationIssue.Warning("已忽略空资源记录。"));
                    continue;
                }

                if (!assetIds.Add(asset.Id))
                {
                    issues.Add(DesignValidationIssue.Error($"资源 ID 重复：{asset.Id}"));
                }

                if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    issues.Add(DesignValidationIssue.Error($"资源 {asset.Name} 的 URL 无效。"));
                }
            }

            foreach (var page in document.Pages)
            {
                if (page == null)
                {
                    issues.Add(DesignValidationIssue.Error("设计清单包含空页面。"));
                    continue;
                }

                if (!pageIds.Add(page.Id))
                {
                    issues.Add(DesignValidationIssue.Error($"页面 ID 重复：{page.Id}"));
                }

                if (page.Width <= 0f || page.Height <= 0f)
                {
                    issues.Add(DesignValidationIssue.Error($"页面 {page.Name} 的尺寸无效。"));
                }

                if (page.Root == null)
                {
                    issues.Add(DesignValidationIssue.Error($"页面 {page.Name} 没有根节点。"));
                    continue;
                }

                foreach (var node in page.Root.DescendantsAndSelf())
                {
                    if (!nodeIds.Add(page.Id + ":" + node.Id))
                    {
                        issues.Add(DesignValidationIssue.Error($"页面 {page.Name} 的节点 ID 重复：{node.Id}"));
                    }

                    if (node.Width <= 0f || node.Height <= 0f)
                    {
                        issues.Add(DesignValidationIssue.Warning($"节点 {node.Name} 的尺寸无效，将不会生成。"));
                    }

                    if (node.Kind == DesignNodeKind.Image && !assetIds.Contains(node.AssetId))
                    {
                        issues.Add(DesignValidationIssue.Error($"节点 {node.Name} 引用了不存在的资源：{node.AssetId}"));
                    }
                }
            }

            return issues;
        }

        private static bool IsFlattenedLanhuPage(DesignPage page)
        {
            return page.Root.Kind == DesignNodeKind.Image &&
                   (page.Root.Children == null || page.Root.Children.Count == 0);
        }
    }

    internal readonly struct DesignValidationIssue
    {
        private DesignValidationIssue(bool isError, string message)
        {
            IsError = isError;
            Message = message;
        }

        public bool IsError { get; }

        public string Message { get; }

        public static DesignValidationIssue Error(string message)
        {
            return new DesignValidationIssue(true, message);
        }

        public static DesignValidationIssue Warning(string message)
        {
            return new DesignValidationIssue(false, message);
        }
    }
}
