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
            string episodeId,
            AuthoringNode node,
            ValidationReport report)
        {
            var arguments = new Dictionary<string, Value>(StringComparer.Ordinal);
            var fieldSource = $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:{MediaCommandNames.ClipArgument}";
            var rawReference = GetString(node.Parameters, MediaCommandNames.ClipArgument);
            if (string.IsNullOrWhiteSpace(rawReference))
            {
                report.AddError(fieldSource, "Required video reference is missing.");
                return arguments;
            }

            VideoReference reference;
            if (VideoReferenceCodec.TryDeserialize(rawReference, out reference, out var error) is false)
            {
                report.AddError(fieldSource, $"Video reference is invalid. {error}");
                return arguments;
            }

            arguments[MediaCommandNames.ClipArgument] = Value.FromString(reference.Primary.Value);
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
                        $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:loop",
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
                    $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:allowSeek",
                    "Command field must be a boolean.");
            }

            return arguments;
        }

        private static IReadOnlyList<CommandArgumentDefinition> BuildVideoArgumentDefinitions()
        {
            return new[]
            {
                new CommandArgumentDefinition(MediaCommandNames.ClipArgument, "视频相对路径", ParameterValueType.String, true),
                new CommandArgumentDefinition(MediaCommandNames.VideoFormatArgument, "视频格式", ParameterValueType.Option, true, options: new[] { "hls", "mp4" }),
                new CommandArgumentDefinition(MediaCommandNames.VideoRenditionsArgument, "清晰度元数据", ParameterValueType.String, true),
                new CommandArgumentDefinition("loop", "循环播放", ParameterValueType.Boolean),
                new CommandArgumentDefinition(MediaCommandNames.VideoSeekableArgument, "允许 Seek", ParameterValueType.Boolean)
            };
        }

        private static Dictionary<string, Value> BuildAudioArguments(
            string storyId,
            string episodeId,
            AuthoringNode node,
            ValidationReport report)
        {
            var arguments = new Dictionary<string, Value>(StringComparer.Ordinal);
            var fieldSource = $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:{MediaCommandNames.ClipArgument}";
            var rawReference = GetString(node.Parameters, MediaCommandNames.ClipArgument);
            if (string.IsNullOrWhiteSpace(rawReference))
            {
                report.AddError(fieldSource, "Required audio reference is missing.");
                return arguments;
            }

            if (AudioReferenceCodec.TryDeserialize(rawReference, out var reference, out _) is false)
            {
                report.AddError(fieldSource, "Audio reference is invalid or unsupported.");
                return arguments;
            }

            arguments[MediaCommandNames.MediaSourceArgument] = Value.FromString(AudioReferenceCodec.ToText(reference.Source));
            arguments[MediaCommandNames.ClipArgument] = Value.FromString(reference.Location);
            var loopText = GetString(node.Parameters, "loop");
            if (string.IsNullOrWhiteSpace(loopText) is false && bool.TryParse(loopText, out var loop))
            {
                arguments["loop"] = Value.FromBoolean(loop);
            }
            else if (string.IsNullOrWhiteSpace(loopText) is false)
            {
                report.AddError(
                    $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:loop",
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
                new CommandArgumentDefinition(MediaCommandNames.ClipArgument, "音频位置", ParameterValueType.String, true),
                new CommandArgumentDefinition("loop", "循环播放", ParameterValueType.Boolean)
            };
        }

    }
}
