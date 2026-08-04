using System;
using System.Globalization;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;

namespace GameDeveloperKit.StoryEditor.Compiler
{
    public static partial class ProgramCompiler
    {
        private static VideoReference BuildVideoReference(
            string storyId,
            string episodeId,
            AuthoringNode node,
            ValidationReport report)
        {
            var fieldSource = $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:{NodeSchemaRegistry.VideoReferenceParameter}";
            var rawReference = GetString(node.Parameters, NodeSchemaRegistry.VideoReferenceParameter);
            if (string.IsNullOrWhiteSpace(rawReference))
            {
                report.AddError(fieldSource, "Required video reference is missing.");
                return null;
            }

            VideoReference reference;
            if (VideoReferenceCodec.TryDeserialize(rawReference, out reference, out var error) is false)
            {
                report.AddError(fieldSource, $"Video reference is invalid. {error}");
                return null;
            }
            return reference;
        }

        private static AudioReference BuildAudioReference(
            string storyId,
            string episodeId,
            AuthoringNode node,
            ValidationReport report)
        {
            var fieldSource = $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:{NodeSchemaRegistry.AudioReferenceParameter}";
            var rawReference = GetString(node.Parameters, NodeSchemaRegistry.AudioReferenceParameter);
            if (string.IsNullOrWhiteSpace(rawReference))
            {
                report.AddError(fieldSource, "Required audio reference is missing.");
                return null;
            }

            if (AudioReferenceCodec.TryDeserialize(rawReference, out var reference, out _) is false)
            {
                report.AddError(fieldSource, "Audio reference is invalid or unsupported.");
                return null;
            }
            return reference;
        }

        private static bool ReadBooleanParameter(
            string storyId,
            string episodeId,
            AuthoringNode node,
            string key,
            bool defaultValue,
            ValidationReport report)
        {
            var text = GetString(node.Parameters, key);
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            if (bool.TryParse(text, out var value))
            {
                return value;
            }

            report.AddError(
                $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:{key}",
                "Value must be a boolean.");
            return defaultValue;
        }

        private static float ReadAudioVolume(
            string storyId,
            string episodeId,
            AuthoringNode node,
            ValidationReport report)
        {
            var value = ReadNumberParameter(
                storyId,
                episodeId,
                node,
                NodeSchemaRegistry.VolumeParameter,
                1d,
                report);
            if (value < 0d || value > 1d)
            {
                report.AddError(
                    $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:{NodeSchemaRegistry.VolumeParameter}",
                    "Audio volume must be between 0 and 1.");
                return 1f;
            }

            return (float)value;
        }

        private static int ReadAudioPriority(
            string storyId,
            string episodeId,
            AuthoringNode node,
            ValidationReport report)
        {
            var value = ReadNumberParameter(
                storyId,
                episodeId,
                node,
                NodeSchemaRegistry.PriorityParameter,
                0d,
                report);
            if (value < 0d || value > 256d || Math.Abs(value - Math.Truncate(value)) > double.Epsilon)
            {
                report.AddError(
                    $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:{NodeSchemaRegistry.PriorityParameter}",
                    "Audio priority must be an integer between 0 and 256.");
                return 0;
            }

            return (int)value;
        }

        private static double ReadNumberParameter(
            string storyId,
            string episodeId,
            AuthoringNode node,
            string key,
            double defaultValue,
            ValidationReport report)
        {
            var text = GetString(node.Parameters, key);
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                double.IsNaN(value) is false &&
                double.IsInfinity(value) is false)
            {
                return value;
            }

            report.AddError(
                $"story:{storyId}/episode:{episodeId}/node:{node.NodeId}/field:{key}",
                "Value must be a finite number.");
            return defaultValue;
        }
    }
}
