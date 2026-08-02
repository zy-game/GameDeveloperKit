using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using IOFile = System.IO.File;

namespace GameDeveloperKit.DesignImporter
{
    internal static class DesignPreviewFallbackComposer
    {
        private const float OpaqueThreshold = 0.98f;
        private const float VisibleThreshold = 0.01f;

        public static byte[] Compose(
            DesignPage page,
            IReadOnlyDictionary<string, DesignAsset> assetsById)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (string.IsNullOrWhiteSpace(page.CachedPreviewPath) || !IOFile.Exists(page.CachedPreviewPath))
            {
                throw new FileNotFoundException("设计稿预览缓存不存在。", page.CachedPreviewPath);
            }

            var preview = LoadPixels(page.CachedPreviewPath);
            try
            {
                var width = Mathf.Max(1, Mathf.RoundToInt(page.Width));
                var height = Mathf.Max(1, Mathf.RoundToInt(page.Height));
                if (preview.Width != width || preview.Height != height)
                {
                    throw new InvalidDataException(
                        $"设计稿预览尺寸 {preview.Width}x{preview.Height} 与页面 {width}x{height} 不一致。" );
                }

                var pixels = ToTopDown(preview);
                var unknown = new bool[pixels.Length];
                var items = new List<RenderItem>();
                CollectRenderItems(page.Root, 0f, 0f, true, items);
                var sourceCache = new Dictionary<string, PixelSource>(StringComparer.Ordinal);
                try
                {
                    for (var index = items.Count - 1; index >= 0; index--)
                    {
                        var item = items[index];
                        switch (item.Node.Kind)
                        {
                            case DesignNodeKind.Image:
                                RemoveImage(item, width, height, pixels, unknown, assetsById, sourceCache);
                                break;
                            case DesignNodeKind.Text:
                                MarkUnknown(item, width, height, unknown, 2);
                                break;
                            default:
                                RemoveSolidFill(item, width, height, pixels, unknown);
                                break;
                        }
                    }

                    InpaintUnknown(width, height, pixels, unknown);
                    return EncodeTopDown(width, height, pixels);
                }
                finally
                {
                    foreach (var source in sourceCache.Values)
                    {
                        source.Dispose();
                    }
                }
            }
            finally
            {
                preview.Dispose();
            }
        }

        private static void CollectRenderItems(
            DesignNode node,
            float parentX,
            float parentY,
            bool isRoot,
            ICollection<RenderItem> output)
        {
            if (node == null || !node.Visible || node.Width <= 0f || node.Height <= 0f)
            {
                return;
            }

            var x = isRoot ? 0f : parentX + node.X;
            var y = isRoot ? 0f : parentY + node.Y;
            if (node.Kind == DesignNodeKind.Image ||
                node.Kind == DesignNodeKind.Text ||
                !string.IsNullOrWhiteSpace(node.BackgroundColor))
            {
                output.Add(new RenderItem(node, x, y));
            }

            foreach (var child in node.Children)
            {
                CollectRenderItems(child, x, y, false, output);
            }
        }

        private static void RemoveImage(
            RenderItem item,
            int pageWidth,
            int pageHeight,
            Color32[] destination,
            bool[] unknown,
            IReadOnlyDictionary<string, DesignAsset> assetsById,
            IDictionary<string, PixelSource> sourceCache)
        {
            if (!assetsById.TryGetValue(item.Node.AssetId, out var asset) ||
                asset == null ||
                string.IsNullOrWhiteSpace(asset.CachedFilePath) ||
                !IOFile.Exists(asset.CachedFilePath))
            {
                MarkUnknown(item, pageWidth, pageHeight, unknown, 1);
                return;
            }

            if (!sourceCache.TryGetValue(asset.Id, out var source))
            {
                try
                {
                    source = LoadPixels(asset.CachedFilePath);
                    sourceCache.Add(asset.Id, source);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"无法读取切图 {asset.Name} 以合成背景兜底：{exception.Message}");
                    MarkUnknown(item, pageWidth, pageHeight, unknown, 1);
                    return;
                }
            }

            var left = Mathf.Clamp(Mathf.FloorToInt(item.X), 0, pageWidth);
            var top = Mathf.Clamp(Mathf.FloorToInt(item.Y), 0, pageHeight);
            var right = Mathf.Clamp(Mathf.CeilToInt(item.X + item.Node.Width), 0, pageWidth);
            var bottom = Mathf.Clamp(Mathf.CeilToInt(item.Y + item.Node.Height), 0, pageHeight);
            for (var y = top; y < bottom; y++)
            {
                var v = Mathf.Clamp01((y + 0.5f - item.Y) / item.Node.Height);
                var sourceY = Mathf.Clamp(Mathf.FloorToInt(v * source.Height), 0, source.Height - 1);
                for (var x = left; x < right; x++)
                {
                    var destinationIndex = y * pageWidth + x;
                    if (unknown[destinationIndex])
                    {
                        continue;
                    }

                    var u = Mathf.Clamp01((x + 0.5f - item.X) / item.Node.Width);
                    var sourceX = Mathf.Clamp(Mathf.FloorToInt(u * source.Width), 0, source.Width - 1);
                    var foreground = source.GetTopDown(sourceX, sourceY);
                    var alpha = foreground.a / 255f * Mathf.Clamp01(item.Node.Opacity);
                    if (alpha <= VisibleThreshold)
                    {
                        continue;
                    }

                    if (alpha >= OpaqueThreshold)
                    {
                        unknown[destinationIndex] = true;
                        continue;
                    }

                    destination[destinationIndex] = InverseComposite(
                        destination[destinationIndex],
                        foreground,
                        alpha);
                }
            }
        }

        private static void RemoveSolidFill(
            RenderItem item,
            int pageWidth,
            int pageHeight,
            Color32[] destination,
            bool[] unknown)
        {
            if (!ColorUtility.TryParseHtmlString(item.Node.BackgroundColor, out var color))
            {
                return;
            }

            var alpha = color.a * Mathf.Clamp01(item.Node.Opacity);
            if (alpha <= VisibleThreshold)
            {
                return;
            }

            var foreground = (Color32)color;
            VisitRect(item, pageWidth, pageHeight, 0, (index, _, __) =>
            {
                if (unknown[index])
                {
                    return;
                }

                if (alpha >= OpaqueThreshold)
                {
                    unknown[index] = true;
                }
                else
                {
                    destination[index] = InverseComposite(destination[index], foreground, alpha);
                }
            });
        }

        private static Color32 InverseComposite(Color32 composite, Color32 foreground, float alpha)
        {
            var inverseAlpha = Mathf.Max(0.001f, 1f - alpha);
            return new Color32(
                ToByte((composite.r / 255f - foreground.r / 255f * alpha) / inverseAlpha),
                ToByte((composite.g / 255f - foreground.g / 255f * alpha) / inverseAlpha),
                ToByte((composite.b / 255f - foreground.b / 255f * alpha) / inverseAlpha),
                255);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        private static void MarkUnknown(
            RenderItem item,
            int pageWidth,
            int pageHeight,
            bool[] unknown,
            int padding)
        {
            VisitRect(item, pageWidth, pageHeight, padding, (index, _, __) => unknown[index] = true);
        }

        private static void VisitRect(
            RenderItem item,
            int pageWidth,
            int pageHeight,
            int padding,
            Action<int, int, int> visitor)
        {
            var left = Mathf.Clamp(Mathf.FloorToInt(item.X) - padding, 0, pageWidth);
            var top = Mathf.Clamp(Mathf.FloorToInt(item.Y) - padding, 0, pageHeight);
            var right = Mathf.Clamp(Mathf.CeilToInt(item.X + item.Node.Width) + padding, 0, pageWidth);
            var bottom = Mathf.Clamp(Mathf.CeilToInt(item.Y + item.Node.Height) + padding, 0, pageHeight);
            for (var y = top; y < bottom; y++)
            {
                for (var x = left; x < right; x++)
                {
                    visitor(y * pageWidth + x, x, y);
                }
            }
        }

        private static void InpaintUnknown(int width, int height, Color32[] pixels, bool[] unknown)
        {
            var queued = new bool[unknown.Length];
            var queue = new Queue<int>();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (unknown[index] && HasKnownNeighbour(x, y, width, height, unknown))
                    {
                        queued[index] = true;
                        queue.Enqueue(index);
                    }
                }
            }

            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                var x = index % width;
                var y = index / width;
                var red = 0;
                var green = 0;
                var blue = 0;
                var count = 0;
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                        {
                            continue;
                        }

                        var neighbourX = x + offsetX;
                        var neighbourY = y + offsetY;
                        if (neighbourX < 0 || neighbourX >= width || neighbourY < 0 || neighbourY >= height)
                        {
                            continue;
                        }

                        var neighbourIndex = neighbourY * width + neighbourX;
                        if (unknown[neighbourIndex])
                        {
                            continue;
                        }

                        var colour = pixels[neighbourIndex];
                        red += colour.r;
                        green += colour.g;
                        blue += colour.b;
                        count++;
                    }
                }

                if (count == 0)
                {
                    queued[index] = false;
                    continue;
                }

                pixels[index] = new Color32(
                    (byte)(red / count),
                    (byte)(green / count),
                    (byte)(blue / count),
                    255);
                unknown[index] = false;
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        var neighbourX = x + offsetX;
                        var neighbourY = y + offsetY;
                        if (neighbourX < 0 || neighbourX >= width || neighbourY < 0 || neighbourY >= height)
                        {
                            continue;
                        }

                        var neighbourIndex = neighbourY * width + neighbourX;
                        if (unknown[neighbourIndex] && !queued[neighbourIndex])
                        {
                            queued[neighbourIndex] = true;
                            queue.Enqueue(neighbourIndex);
                        }
                    }
                }
            }
        }

        private static bool HasKnownNeighbour(int x, int y, int width, int height, bool[] unknown)
        {
            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    var neighbourX = x + offsetX;
                    var neighbourY = y + offsetY;
                    if (neighbourX >= 0 && neighbourX < width && neighbourY >= 0 && neighbourY < height &&
                        !unknown[neighbourY * width + neighbourX])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static PixelSource LoadPixels(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(IOFile.ReadAllBytes(path), false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidDataException("无法解码图片：" + path);
            }

            return new PixelSource(texture);
        }

        private static Color32[] ToTopDown(PixelSource source)
        {
            var result = new Color32[source.Pixels.Length];
            for (var y = 0; y < source.Height; y++)
            {
                Array.Copy(
                    source.Pixels,
                    (source.Height - 1 - y) * source.Width,
                    result,
                    y * source.Width,
                    source.Width);
            }

            return result;
        }

        private static byte[] EncodeTopDown(int width, int height, Color32[] pixels)
        {
            var bottomUp = new Color32[pixels.Length];
            for (var y = 0; y < height; y++)
            {
                Array.Copy(pixels, y * width, bottomUp, (height - 1 - y) * width, width);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(bottomUp);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private readonly struct RenderItem
        {
            public RenderItem(DesignNode node, float x, float y)
            {
                Node = node;
                X = x;
                Y = y;
            }

            public DesignNode Node { get; }
            public float X { get; }
            public float Y { get; }
        }

        private sealed class PixelSource : IDisposable
        {
            private readonly Texture2D m_Texture;

            public PixelSource(Texture2D texture)
            {
                m_Texture = texture;
                Width = texture.width;
                Height = texture.height;
                Pixels = texture.GetPixels32();
            }

            public int Width { get; }
            public int Height { get; }
            public Color32[] Pixels { get; }

            public Color32 GetTopDown(int x, int y)
            {
                return Pixels[(Height - 1 - y) * Width + x];
            }

            public void Dispose()
            {
                if (m_Texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_Texture);
                }
            }
        }
    }
}
