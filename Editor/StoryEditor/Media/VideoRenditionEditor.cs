using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Media;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal static class VideoRenditionEditor
    {
        public static VideoReference WithPrimaryMetadata(
            VideoReference reference,
            int width,
            int height,
            int bitrate,
            long durationMs)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (reference.Format != VideoFormat.Mp4)
            {
                throw new ArgumentException("Only MP4 references require editable primary metadata.");
            }

            var primary = reference.Primary;
            var rendition = new VideoRendition(
                string.Empty,
                primary.MediaId,
                primary.Location,
                width,
                height,
                bitrate,
                durationMs);
            return new VideoReference(primary, VideoFormat.Mp4, new[] { rendition });
        }

        public static VideoReference Add(VideoReference current, VideoReference candidate)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (current.Format != VideoFormat.Mp4 || candidate.Format != VideoFormat.Mp4)
            {
                throw new ArgumentException("Only MP4 references can compose multiple encoded clips.");
            }

            if (current.Primary.Source != candidate.Primary.Source)
            {
                throw new ArgumentException("MP4 quality clips must use the same media source.");
            }

            if (current.Renditions.Count == 0 || candidate.Renditions.Count == 0)
            {
                throw new ArgumentException("MP4 quality clips require width, height, and duration metadata.");
            }

            var items = new List<VideoRendition>(current.Renditions.Count + 1);
            for (var i = 0; i < current.Renditions.Count; i++)
            {
                items.Add(current.Renditions[i]);
            }

            items.Add(candidate.Renditions[0]);
            return new VideoReference(current.Primary, VideoFormat.Mp4, items);
        }

        public static VideoReference Remove(VideoReference current, int renditionIndex)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (current.Format != VideoFormat.Mp4 || renditionIndex <= 0 || renditionIndex >= current.Renditions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(renditionIndex));
            }

            var items = new List<VideoRendition>(current.Renditions.Count - 1);
            for (var i = 0; i < current.Renditions.Count; i++)
            {
                if (i != renditionIndex)
                {
                    items.Add(current.Renditions[i]);
                }
            }

            return new VideoReference(current.Primary, VideoFormat.Mp4, items);
        }
    }
}
