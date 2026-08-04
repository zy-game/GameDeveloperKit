using System;
using System.Collections.Generic;
using GameDeveloperKit.Media;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Media;
using UnityEngine;

namespace GameDeveloperKit.Story.Playback
{
    public static class VideoRequestFactory
    {
        public static VideoPlayableRequest Create(
            VideoReference reference,
            MediaDeliverySettings settings,
            bool loop,
            bool seekable,
            Transform parent = null,
            bool dontDestroyOnLoad = false,
            int preloadTargetHeight = 0)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var primaryPath = MediaUrlResolver.Resolve(reference.Primary, settings);
            var options = new List<VideoQualityOption>(reference.Renditions.Count);
            for (var i = 0; i < reference.Renditions.Count; i++)
            {
                var rendition = reference.Renditions[i];
                if (rendition.Width <= 0 || rendition.Height <= 0)
                {
                    return CreateSingle(primaryPath, loop, seekable, parent, dontDestroyOnLoad);
                }

                options.Add(new VideoQualityOption(
                    rendition.Label,
                    rendition.Width,
                    rendition.Height,
                    rendition.Bitrate,
                    MediaUrlResolver.Resolve(rendition.Path, settings)));
            }

            var supportsAuto = reference.Format == VideoFormat.Hls;
            var initialHeight = reference.Format == VideoFormat.Mp4 && reference.Renditions.Count > 0
                ? reference.Renditions[0].Height
                : 0;
            options.Sort((left, right) => left.Height.CompareTo(right.Height));
            var targetHeight = preloadTargetHeight > 0 && HasHeight(options, preloadTargetHeight)
                ? preloadTargetHeight
                : 0;
            var initialQuality = supportsAuto
                ? targetHeight > 0
                    ? new VideoQualitySelection(VideoQualityMode.FixedHeight, targetHeight)
                    : new VideoQualitySelection(VideoQualityMode.Auto)
                : options.Count == 0
                    ? new VideoQualitySelection(VideoQualityMode.Auto)
                    : targetHeight > 0
                        ? new VideoQualitySelection(VideoQualityMode.FixedHeight, targetHeight)
                        : new VideoQualitySelection(VideoQualityMode.FixedHeight, initialHeight);
            return new VideoPlayableRequest(primaryPath, new VideoPlayableOptions
            {
                Loop = loop,
                Seekable = seekable,
                Parent = parent,
                DontDestroyOnLoad = dontDestroyOnLoad,
                SupportsAutoQuality = supportsAuto,
                InitialQuality = initialQuality,
                PreloadTargetHeight = targetHeight,
                QualityOptions = options
            });
        }

        private static bool HasHeight(IReadOnlyList<VideoQualityOption> options, int height)
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].Height == height)
                {
                    return true;
                }
            }

            return false;
        }

        private static VideoPlayableRequest CreateSingle(
            string primaryPath,
            bool loop,
            bool seekable,
            Transform parent,
            bool dontDestroyOnLoad)
        {
            return new VideoPlayableRequest(primaryPath, new VideoPlayableOptions
            {
                Loop = loop,
                Seekable = seekable,
                Parent = parent,
                DontDestroyOnLoad = dontDestroyOnLoad
            });
        }

    }
}
