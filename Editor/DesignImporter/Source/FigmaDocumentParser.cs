using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.DesignImporter
{
    internal static class FigmaDocumentParser
    {
        private static readonly HashSet<string> s_ContainerTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "CANVAS",
            "SECTION",
            "FRAME",
            "GROUP",
            "COMPONENT",
            "COMPONENT_SET",
            "INSTANCE",
            "BOOLEAN_OPERATION"
        };

        public static FigmaParseResult Parse(string fileKey, string json, float exportScale)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Figma 响应为空。", nameof(json));
            }

            var source = JObject.Parse(json);
            var root = source["document"] as JObject
                ?? throw new InvalidOperationException("Figma 响应缺少 document。");
            var result = new FigmaParseResult
            {
                Document = new DesignDocument
                {
                    Id = fileKey,
                    Name = (string)source["name"] ?? "Figma Design",
                    Source = DesignSourceKind.Figma
                }
            };

            foreach (var canvas in Children(root))
            {
                foreach (var frame in Children(canvas).Where(IsTopLevelFrame))
                {
                    var bounds = ReadBounds(frame);
                    if (bounds.Width <= 0f || bounds.Height <= 0f)
                    {
                        continue;
                    }

                    var page = new DesignPage
                    {
                        Id = (string)frame["id"] ?? Guid.NewGuid().ToString("N"),
                        Name = (string)frame["name"] ?? "Frame",
                        Width = bounds.Width,
                        Height = bounds.Height
                    };
                    page.Root = ParseNode(frame, bounds.X, bounds.Y, result, exportScale, true);
                    result.Document.Pages.Add(page);
                    result.PreviewNodeIds.Add(page.Id);
                }
            }

            result.Document.Normalize();
            return result;
        }

        public static string ExtractFileKey(string value)
        {
            var source = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
            {
                return source;
            }

            var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i + 1 < segments.Length; i++)
            {
                if (string.Equals(segments[i], "file", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segments[i], "design", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(segments[i + 1]);
                }
            }

            return string.Empty;
        }

        private static DesignNode ParseNode(
            JObject source,
            float parentX,
            float parentY,
            FigmaParseResult result,
            float exportScale,
            bool forceContainer)
        {
            var bounds = ReadBounds(source);
            var type = ((string)source["type"] ?? string.Empty).ToUpperInvariant();
            var node = new DesignNode
            {
                Id = (string)source["id"] ?? Guid.NewGuid().ToString("N"),
                Name = (string)source["name"] ?? type,
                X = bounds.X - parentX,
                Y = bounds.Y - parentY,
                Width = bounds.Width,
                Height = bounds.Height,
                Visible = (bool?)source["visible"] ?? true,
                Opacity = (float?)source["opacity"] ?? 1f,
                CornerRadius = (float?)source["cornerRadius"] ?? 0f,
                ClipsContent = (bool?)source["clipsContent"] ?? false
            };

            if (string.Equals(type, "TEXT", StringComparison.Ordinal))
            {
                node.Kind = DesignNodeKind.Text;
                node.Text = (string)source["characters"] ?? string.Empty;
                node.FontSize = (float?)source["style"]?["fontSize"] ?? 24f;
                node.FontName = (string)source["style"]?["fontFamily"] ?? string.Empty;
                node.FontPostScriptName = (string)source["style"]?["fontPostScriptName"] ?? string.Empty;
                node.FontStyleName = (string)source["style"]?["fontWeight"] ?? string.Empty;
                node.Bold = ((float?)source["style"]?["fontWeight"] ?? 400f) >= 600f;
                node.Italic = string.Equals(
                    (string)source["style"]?["italic"],
                    "true",
                    StringComparison.OrdinalIgnoreCase) || ((bool?)source["style"]?["italic"] ?? false);
                node.Tracking = (float?)source["style"]?["letterSpacing"] ?? 0f;
                node.LineHeight = (float?)source["style"]?["lineHeightPx"] ?? 0f;
                node.TextAlignment = ((string)source["style"]?["textAlignHorizontal"] ?? "LEFT").ToLowerInvariant();
                node.Color = ReadSolidColor(source["fills"] as JArray, "#FFFFFFFF");
                return node;
            }

            var children = Children(source).ToList();
            if (forceContainer || s_ContainerTypes.Contains(type) && children.Count > 0)
            {
                node.Kind = DesignNodeKind.Container;
                node.BackgroundColor = ReadSolidColor(source["fills"] as JArray, string.Empty);
                foreach (var child in children)
                {
                    node.Children.Add(ParseNode(child, bounds.X, bounds.Y, result, exportScale, false));
                }

                return node;
            }

            node.Kind = DesignNodeKind.Image;
            node.AssetId = node.Id;
            result.RenderNodeIds.Add(node.Id);
            result.Document.Assets.Add(new DesignAsset
            {
                Id = node.Id,
                Name = node.Name,
                Format = "png",
                PixelScale = exportScale
            });
            return node;
        }

        private static bool IsTopLevelFrame(JObject node)
        {
            var type = ((string)node["type"] ?? string.Empty).ToUpperInvariant();
            return type == "FRAME" || type == "COMPONENT" || type == "INSTANCE" || type == "SECTION";
        }

        private static IEnumerable<JObject> Children(JObject node)
        {
            return (node?["children"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>();
        }

        private static FigmaBounds ReadBounds(JObject node)
        {
            var bounds = node?["absoluteBoundingBox"] as JObject;
            return new FigmaBounds(
                (float?)bounds?["x"] ?? 0f,
                (float?)bounds?["y"] ?? 0f,
                (float?)bounds?["width"] ?? 0f,
                (float?)bounds?["height"] ?? 0f);
        }

        private static string ReadSolidColor(JArray fills, string fallback)
        {
            var fill = fills?.OfType<JObject>().FirstOrDefault(x =>
                ((bool?)x["visible"] ?? true) &&
                string.Equals((string)x["type"], "SOLID", StringComparison.OrdinalIgnoreCase));
            var color = fill?["color"] as JObject;
            if (color == null)
            {
                return fallback;
            }

            var red = ToByte((float?)color["r"] ?? 0f);
            var green = ToByte((float?)color["g"] ?? 0f);
            var blue = ToByte((float?)color["b"] ?? 0f);
            var alpha = ToByte(((float?)color["a"] ?? 1f) * ((float?)fill["opacity"] ?? 1f));
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", red, green, blue, alpha);
        }

        private static int ToByte(float value)
        {
            return Math.Max(0, Math.Min(255, (int)Math.Round(value * 255f)));
        }

        private readonly struct FigmaBounds
        {
            public FigmaBounds(float x, float y, float width, float height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public float X { get; }
            public float Y { get; }
            public float Width { get; }
            public float Height { get; }
        }
    }

    internal sealed class FigmaParseResult
    {
        public DesignDocument Document;
        public readonly List<string> RenderNodeIds = new List<string>();
        public readonly List<string> PreviewNodeIds = new List<string>();
    }
}
