using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameDeveloperKit.MediaEditor
{
    public sealed class HlsRenditionEligibility
    {
        internal HlsRenditionEligibility(
            HlsRenditionPreset preset,
            bool exceedsSourceHeight,
            bool exceedsSourceVideoBitrate)
        {
            Preset = preset;
            ExceedsSourceHeight = exceedsSourceHeight;
            ExceedsSourceVideoBitrate = exceedsSourceVideoBitrate;
        }

        public HlsRenditionPreset Preset { get; }
        public bool ExceedsSourceHeight { get; }
        public bool ExceedsSourceVideoBitrate { get; }
        public bool IsEligible => ExceedsSourceHeight is false && ExceedsSourceVideoBitrate is false;

        public string IneligibilityReason
        {
            get
            {
                if (ExceedsSourceHeight && ExceedsSourceVideoBitrate)
                {
                    return "超过源分辨率和源视频码率";
                }

                if (ExceedsSourceHeight)
                {
                    return "超过源分辨率";
                }

                return ExceedsSourceVideoBitrate ? "超过源视频码率" : string.Empty;
            }
        }
    }

    public sealed class HlsRenditionEligibilityResult
    {
        internal HlsRenditionEligibilityResult(
            IReadOnlyList<HlsRenditionEligibility> renditions,
            HlsRenditionPreset highestEligiblePreset)
        {
            Renditions = renditions;
            HighestEligiblePreset = highestEligiblePreset;
        }

        public IReadOnlyList<HlsRenditionEligibility> Renditions { get; }
        public HlsRenditionPreset HighestEligiblePreset { get; }
    }

    public static class HlsRenditionEligibilityPolicy
    {
        public static HlsRenditionEligibilityResult Evaluate(
            MediaProbeInfo source,
            IReadOnlyList<HlsRenditionPreset> presets)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (presets == null)
            {
                throw new ArgumentNullException(nameof(presets));
            }

            var renditions = new List<HlsRenditionEligibility>(presets.Count);
            for (var i = 0; i < presets.Count; i++)
            {
                var preset = presets[i] ?? throw new ArgumentException(
                    "Rendition presets cannot contain null entries.",
                    nameof(presets));
                renditions.Add(new HlsRenditionEligibility(
                    preset,
                    preset.Height > source.Height,
                    preset.VideoBitrate > source.VideoBitrate));
            }

            var highest = renditions
                .Where(rendition => rendition.IsEligible)
                .OrderByDescending(rendition => rendition.Preset.Height)
                .Select(rendition => rendition.Preset)
                .FirstOrDefault();
            return new HlsRenditionEligibilityResult(
                new ReadOnlyCollection<HlsRenditionEligibility>(renditions),
                highest);
        }
    }
}
