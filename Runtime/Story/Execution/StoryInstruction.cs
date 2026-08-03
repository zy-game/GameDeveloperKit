using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;

namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 剧情帧输出的有限业务指令。
    /// </summary>
    public abstract class StoryInstruction
    {
        private StoryInstruction(FrameTrack track)
        {
            if (track?.Kind != FrameTrackKind.Command || track.Command == null)
            {
                throw new ArgumentException("Story instruction requires a command track.", nameof(track));
            }

            CommandId = track.Command.CommandId;
            Step = track.Step;
            BranchId = string.IsNullOrWhiteSpace(track.BranchId) ? string.Empty : track.BranchId;
            WaitForCompletion = track.Command.WaitForCompletion;
            OutcomePorts = track.Command.OutcomePorts.Count == 0
                ? Array.Empty<string>()
                : new List<string>(track.Command.OutcomePorts);
        }

        public string CommandId { get; }

        public Step Step { get; }

        public string BranchId { get; }

        public bool WaitForCompletion { get; }

        public IReadOnlyList<string> OutcomePorts { get; }

        public bool RequiresCompletion => WaitForCompletion || OutcomePorts.Count > 0;

        public string CompletedOutcome
        {
            get
            {
                for (var i = 0; i < OutcomePorts.Count; i++)
                {
                    if (string.Equals(
                            OutcomePorts[i],
                            MediaCommandNames.CompletedOutcome,
                            StringComparison.Ordinal))
                    {
                        return MediaCommandNames.CompletedOutcome;
                    }
                }

                return null;
            }
        }

        public sealed class PlayVideo : StoryInstruction
        {
            internal PlayVideo(FrameTrack track, VideoReference reference)
                : base(track)
            {
                Reference = reference ?? throw new ArgumentNullException(nameof(reference));
                Loop = track.Command.Arguments.GetBoolean("loop", false);
                Seekable = track.Command.Arguments.GetBoolean(
                    MediaCommandNames.VideoSeekableArgument,
                    false);
            }

            public VideoReference Reference { get; }

            public bool Loop { get; }

            public bool Seekable { get; }
        }

        public sealed class ShowImage : StoryInstruction
        {
            internal ShowImage(FrameTrack track, string location)
                : base(track)
            {
                Location = location;
            }

            public string Location { get; }
        }

        public sealed class PlayAudio : StoryInstruction
        {
            internal PlayAudio(FrameTrack track, MediaReference reference)
                : base(track)
            {
                Reference = reference;
                Loop = track.Command.Arguments.GetBoolean("loop", false);
                Volume = (float)track.Command.Arguments.GetNumber("volume", 1d);
                Priority = (int)track.Command.Arguments.GetNumber("priority", 0d);
            }

            public MediaReference Reference { get; }

            public bool Loop { get; }

            public float Volume { get; }

            public int Priority { get; }
        }

        public sealed class Unlock : StoryInstruction
        {
            internal Unlock(FrameTrack track, string unlockId)
                : base(track)
            {
                UnlockId = unlockId;
            }

            public string UnlockId { get; }
        }

        internal static bool TryCreate(FrameTrack track, out StoryInstruction instruction)
        {
            instruction = null;
            var command = track?.Command;
            if (track?.Kind != FrameTrackKind.Command || command == null)
            {
                return false;
            }

            switch (command.Name)
            {
                case MediaCommandNames.PlayVideo:
                    if (VideoReferenceCodec.TryDeserializeCommand(
                            command.Arguments,
                            out var videoReference,
                            out var videoError) is false)
                    {
                        throw Invalid(command, videoError);
                    }

                    instruction = new PlayVideo(track, videoReference);
                    return true;
                case MediaCommandNames.ShowImage:
                    instruction = new ShowImage(
                        track,
                        RequireArgument(command, MediaCommandNames.ImageArgument));
                    return true;
                case MediaCommandNames.PlayAudio:
                    if (AudioReferenceCodec.TryDeserializeCommand(
                            command.Arguments,
                            out var audioReference,
                            out var audioError) is false)
                    {
                        throw Invalid(command, audioError);
                    }

                    instruction = new PlayAudio(track, audioReference);
                    return true;
                case StoryCommandNames.Unlock:
                    instruction = new Unlock(
                        track,
                        RequireArgument(command, StoryCommandNames.UnlockIdArgument));
                    return true;
                default:
                    return false;
            }
        }

        private static string RequireArgument(
            global::GameDeveloperKit.Story.Model.Command command,
            string key)
        {
            var value = command.Arguments.GetString(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Invalid(command, $"Missing argument: {key}");
            }

            return value.Trim();
        }

        private static GameException Invalid(
            global::GameDeveloperKit.Story.Model.Command command,
            string reason)
        {
            return new GameException(
                $"Story instruction is invalid. command:{command?.CommandId} reason:{reason}");
        }
    }
}
