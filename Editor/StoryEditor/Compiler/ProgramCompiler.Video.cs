using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;

namespace GameDeveloperKit.StoryEditor.Compiler
{
    public static partial class ProgramCompiler
    {
        private static Dictionary<string, Value> BuildVideoArguments(
            string storyId,
            string chapterId,
            AuthoringNode node,
            ValidationReport report)
        {
            var arguments = new Dictionary<string, Value>(StringComparer.Ordinal);
            var fieldSource = $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:{MediaCommandNames.ClipArgument}";
            var rawReference = GetString(node.Parameters, MediaCommandNames.ClipArgument);
            if (string.IsNullOrWhiteSpace(rawReference))
            {
                report.AddError(fieldSource, "Required video reference is missing.");
                return arguments;
            }

            VideoReference reference;
            if (VideoReferenceCodec.TryDeserialize(rawReference, out reference, out var error) is false)
            {
                var legacySource = GetString(node.Parameters, MediaCommandNames.VideoSourceArgument);
                var legacyArguments = new ArgumentBag(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    [MediaCommandNames.VideoSourceArgument] = Value.FromString(legacySource),
                    [MediaCommandNames.ClipArgument] = Value.FromString(rawReference)
                });
                if (VideoReferenceCodec.TryDeserializeCommand(legacyArguments, out reference, out var legacy, out error) is false || legacy is false)
                {
                    report.AddError(fieldSource, $"Video reference is invalid. {error}");
                    return arguments;
                }

                report.AddWarning(fieldSource, "Legacy StreamingAssets video reference is supported but should be reselected in the video picker.");
            }

            arguments[MediaCommandNames.MediaSourceArgument] = Value.FromString(ToSourceText(reference.Primary.Source));
            arguments[MediaCommandNames.MediaIdArgument] = Value.FromString(reference.Primary.MediaId);
            arguments[MediaCommandNames.ClipArgument] = Value.FromString(reference.Primary.Location);
            arguments[MediaCommandNames.VideoFormatArgument] = Value.FromString(reference.Format == VideoFormat.Hls ? "hls" : "mp4");
            arguments[MediaCommandNames.VideoRenditionsArgument] = Value.FromString(VideoReferenceCodec.SerializeRenditions(reference.Renditions));

            var loopText = GetString(node.Parameters, "loop");
            if (string.IsNullOrWhiteSpace(loopText) is false)
            {
                if (bool.TryParse(loopText, out var loop))
                {
                    arguments["loop"] = Value.FromBoolean(loop);
                }
                else
                {
                    report.AddError(
                        $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:loop",
                        "Command field must be a boolean.");
                }
            }

            var allowSeekText = GetString(node.Parameters, "allowSeek");
            if (string.IsNullOrWhiteSpace(allowSeekText))
            {
                arguments[MediaCommandNames.VideoSeekableArgument] = Value.FromBoolean(false);
            }
            else if (bool.TryParse(allowSeekText, out var allowSeek))
            {
                arguments[MediaCommandNames.VideoSeekableArgument] = Value.FromBoolean(allowSeek);
            }
            else
            {
                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:allowSeek",
                    "Command field must be a boolean.");
            }

            return arguments;
        }

        private static IReadOnlyList<CommandArgumentDefinition> BuildVideoArgumentDefinitions()
        {
            return new[]
            {
                new CommandArgumentDefinition(MediaCommandNames.MediaSourceArgument, "媒体来源", ParameterValueType.Option, true, options: new[]
                {
                    MediaCommandNames.VideoSourceCdn,
                    MediaCommandNames.VideoSourceStreamingAssets
                }),
                new CommandArgumentDefinition(MediaCommandNames.MediaIdArgument, "媒体 ID"),
                new CommandArgumentDefinition(MediaCommandNames.ClipArgument, "视频位置", ParameterValueType.String, true),
                new CommandArgumentDefinition(MediaCommandNames.VideoFormatArgument, "视频格式", ParameterValueType.Option, true, options: new[] { "hls", "mp4" }),
                new CommandArgumentDefinition(MediaCommandNames.VideoRenditionsArgument, "清晰度元数据", ParameterValueType.String, true),
                new CommandArgumentDefinition("loop", "循环播放", ParameterValueType.Boolean),
                new CommandArgumentDefinition(MediaCommandNames.VideoSeekableArgument, "允许 Seek", ParameterValueType.Boolean)
            };
        }

        private static Dictionary<string, Value> BuildAudioArguments(
            string storyId,
            string chapterId,
            AuthoringNode node,
            ValidationReport report)
        {
            var arguments = new Dictionary<string, Value>(StringComparer.Ordinal);
            var fieldSource = $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:{MediaCommandNames.ClipArgument}";
            var rawReference = GetString(node.Parameters, MediaCommandNames.ClipArgument);
            if (string.IsNullOrWhiteSpace(rawReference))
            {
                report.AddError(fieldSource, "Required audio reference is missing.");
                return arguments;
            }

            if (AudioReferenceCodec.TryDeserialize(rawReference, out var reference, out _) is false)
            {
                try
                {
                    reference = new MediaReference(MediaKind.Audio, MediaSource.Resource, string.Empty, rawReference);
                    report.AddWarning(fieldSource, "Legacy Resource audio reference is supported but should be reselected in the audio picker.");
                }
                catch (ArgumentException exception)
                {
                    report.AddError(fieldSource, $"Audio reference is invalid. {exception.Message}");
                    return arguments;
                }
            }

            arguments[MediaCommandNames.MediaSourceArgument] = Value.FromString(AudioReferenceCodec.ToText(reference.Source));
            arguments[MediaCommandNames.MediaIdArgument] = Value.FromString(reference.MediaId);
            arguments[MediaCommandNames.ClipArgument] = Value.FromString(reference.Location);
            var loopText = GetString(node.Parameters, "loop");
            if (string.IsNullOrWhiteSpace(loopText) is false && bool.TryParse(loopText, out var loop))
            {
                arguments["loop"] = Value.FromBoolean(loop);
            }
            else if (string.IsNullOrWhiteSpace(loopText) is false)
            {
                report.AddError(
                    $"story:{storyId}/chapter:{chapterId}/node:{node.NodeId}/field:loop",
                    "Command field must be a boolean.");
            }

            return arguments;
        }

        private static IReadOnlyList<CommandArgumentDefinition> BuildAudioArgumentDefinitions()
        {
            return new[]
            {
                new CommandArgumentDefinition(MediaCommandNames.MediaSourceArgument, "媒体来源", ParameterValueType.Option, true, options: new[]
                {
                    MediaCommandNames.MediaSourceCdn,
                    MediaCommandNames.MediaSourceStreamingAssets,
                    MediaCommandNames.MediaSourceResource
                }),
                new CommandArgumentDefinition(MediaCommandNames.MediaIdArgument, "媒体 ID"),
                new CommandArgumentDefinition(MediaCommandNames.ClipArgument, "音频位置", ParameterValueType.String, true),
                new CommandArgumentDefinition("loop", "循环播放", ParameterValueType.Boolean)
            };
        }

        private static string ToSourceText(MediaSource source)
        {
            switch (source)
            {
                case MediaSource.Cdn:
                    return MediaCommandNames.VideoSourceCdn;
                case MediaSource.StreamingAssets:
                    return MediaCommandNames.VideoSourceStreamingAssets;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source));
            }
        }
    }
}
