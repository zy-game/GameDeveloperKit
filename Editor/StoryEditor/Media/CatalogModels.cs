using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Media;

namespace GameDeveloperKit.StoryEditor.Media
{
    public enum CatalogErrorKind
    {
        InvalidSettings,
        RequestFailed,
        InvalidResponse,
        DuplicateMediaId,
        UnsupportedMediaKind,
        InvalidLocation,
        UnavailableReference
    }

    public sealed class CatalogException : Exception
    {
        public CatalogException(CatalogErrorKind kind, string message, Exception innerException = null)
            : base(message, innerException)
        {
            Kind = kind;
        }

        public CatalogErrorKind Kind { get; }
    }

    public sealed class CatalogPage
    {
        public CatalogPage(IReadOnlyList<CatalogItem> items, string nextCursor)
        {
            Items = items ?? Array.Empty<CatalogItem>();
            NextCursor = nextCursor ?? string.Empty;
        }

        public IReadOnlyList<CatalogItem> Items { get; }

        public string NextCursor { get; }
    }

    public sealed class CatalogItem
    {
        public CatalogItem(
            string mediaId,
            string name,
            MediaKind kind,
            string location,
            VideoFormat format,
            string thumbnailLocation,
            int width,
            int height,
            int bitrate,
            long durationMs,
            IReadOnlyList<CatalogRendition> renditions)
        {
            MediaId = mediaId;
            Name = name;
            Kind = kind;
            Location = location;
            Format = format;
            ThumbnailLocation = thumbnailLocation;
            Width = width;
            Height = height;
            Bitrate = bitrate;
            DurationMs = durationMs;
            Renditions = renditions ?? Array.Empty<CatalogRendition>();
        }

        public string MediaId { get; }

        public string Name { get; }

        public MediaKind Kind { get; }

        public string Location { get; }

        public VideoFormat Format { get; }

        public string ThumbnailLocation { get; }

        public int Width { get; }

        public int Height { get; }

        public int Bitrate { get; }

        public long DurationMs { get; }

        public IReadOnlyList<CatalogRendition> Renditions { get; }
    }

    public readonly struct CatalogRendition
    {
        public CatalogRendition(
            string label,
            string mediaId,
            string location,
            int width,
            int height,
            int bitrate,
            long durationMs)
        {
            Label = label;
            MediaId = mediaId;
            Location = location;
            Width = width;
            Height = height;
            Bitrate = bitrate;
            DurationMs = durationMs;
        }

        public string Label { get; }

        public string MediaId { get; }

        public string Location { get; }

        public int Width { get; }

        public int Height { get; }

        public int Bitrate { get; }

        public long DurationMs { get; }
    }
}
