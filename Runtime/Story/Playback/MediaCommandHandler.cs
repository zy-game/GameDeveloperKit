using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using UnityEngine;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Media;

namespace GameDeveloperKit.Story.Playback
{
    public sealed class MediaCommandHandler : ICommandHandler, IDisposable
    {
        private readonly PlayableModule m_PlayableModule;
        private readonly Func<UnityEngine.UI.RawImage> m_ImageOutput;
        private readonly Transform m_VideoParent;
        private bool m_Disposed;

        public MediaCommandHandler(
            PlayableModule playableModule,
            Func<UnityEngine.UI.RawImage> imageOutput,
            Transform videoParent)
        {
            m_PlayableModule = playableModule ?? throw new ArgumentNullException(nameof(playableModule));
            m_ImageOutput = imageOutput;
            m_VideoParent = videoParent;
        }

        public VideoPlayable Video => m_PlayableModule.Video;

        public bool CanHandle(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command != null && command.Name is
                MediaCommandNames.PlayVideo or
                MediaCommandNames.ShowImage or
                MediaCommandNames.PlayAudio;
        }

        public ICommandHandle Execute(global::GameDeveloperKit.Story.Model.Command command, RuntimeContext context)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            ThrowIfDisposed();
            var storyHandle = new CommandHandle(command);
            ExecuteAsync(storyHandle).Forget(Debug.LogException);
            return storyHandle;
        }

        public UniTask PreloadVideoAsync(global::GameDeveloperKit.Story.Model.Command command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var request = CreateVideoRequest(command);
            return Video.PreloadAsync(request, cancellationToken);
        }

        public void Dispose()
        {
            m_Disposed = true;
        }

        private async UniTask ExecuteAsync(CommandHandle storyHandle)
        {
            PlayableHandle playableHandle = null;
            void StopPlayable(ICommandHandle _)
            {
                playableHandle?.Stop();
            }

            storyHandle.Canceled += StopPlayable;
            storyHandle.Stopped += StopPlayable;
            try
            {
                switch (storyHandle.Command.Name)
                {
                    case MediaCommandNames.PlayVideo:
                        playableHandle = await Video.PlayAsync(CreateVideoRequest(storyHandle.Command));
                        break;
                    case MediaCommandNames.ShowImage:
                        playableHandle = await m_PlayableModule.Image.PlayAsync(CreateImageRequest(storyHandle.Command));
                        break;
                    case MediaCommandNames.PlayAudio:
                        playableHandle = await m_PlayableModule.Audio.PlayAsync(CreateAudioRequest(storyHandle.Command));
                        break;
                    default:
                        throw new GameException($"Story media command is not supported: {storyHandle.Command.Name}");
                }

                if (MediaCommandUtility.IsTerminal(storyHandle))
                {
                    playableHandle.Stop();
                    return;
                }

                if (storyHandle.Command.Name == MediaCommandNames.ShowImage)
                {
                    storyHandle.Complete(MediaCommandUtility.GetCompletedOutcome(storyHandle.Command));
                    return;
                }

                await playableHandle.WaitForCompletionAsync();
                if (MediaCommandUtility.IsTerminal(storyHandle) is false)
                {
                    storyHandle.Complete(MediaCommandUtility.GetCompletedOutcome(storyHandle.Command));
                }
            }
            catch (Exception exception)
            {
                if (MediaCommandUtility.IsTerminal(storyHandle) is false)
                {
                    storyHandle.Fail(exception);
                }
            }
            finally
            {
                storyHandle.Canceled -= StopPlayable;
                storyHandle.Stopped -= StopPlayable;
            }
        }

        private VideoPlayableRequest CreateVideoRequest(global::GameDeveloperKit.Story.Model.Command command)
        {
            if (VideoReferenceCodec.TryDeserializeCommand(command.Arguments, out var reference, out _, out var error) is false)
            {
                throw new GameException($"Story video reference is invalid. command:{command.CommandId} reason:{error}");
            }

            return VideoRequestFactory.Create(
                reference,
                command.Arguments.GetBoolean("loop", false),
                command.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument, false),
                m_VideoParent,
                false);
        }

        private ImagePlayableRequest CreateImageRequest(global::GameDeveloperKit.Story.Model.Command command)
        {
            if (m_ImageOutput == null)
            {
                throw new GameException("Story image output surface is missing.");
            }

            return new ImagePlayableRequest(
                RequireArgument(command, MediaCommandNames.ImageArgument),
                texture =>
                {
                    var output = m_ImageOutput();
                    if (output == null)
                    {
                        throw new GameException("Story image output surface is missing.");
                    }

                    output.texture = texture;
                    output.gameObject.SetActive(texture != null);
                });
        }

        private static AudioPlayableRequest CreateAudioRequest(global::GameDeveloperKit.Story.Model.Command command)
        {
            if (AudioReferenceCodec.TryDeserializeCommand(command.Arguments, out var reference, out _, out var error) is false)
            {
                throw new GameException($"Story audio reference is invalid. command:{command.CommandId} error:{error}");
            }

            return new AudioPlayableRequest(
                reference.Location,
                ToAudioLocationKind(reference.Source),
                new AudioPlayableOptions
                {
                    Loop = command.Arguments.GetBoolean("loop", false),
                    Volume = (float)command.Arguments.GetNumber("volume", 1d),
                    Priority = (int)command.Arguments.GetNumber("priority", 0d)
                });
        }

        private static AudioLocationKind ToAudioLocationKind(MediaSource source)
        {
            switch (source)
            {
                case MediaSource.Cdn: return AudioLocationKind.Url;
                case MediaSource.StreamingAssets: return AudioLocationKind.StreamingAssets;
                case MediaSource.Resource: return AudioLocationKind.Resource;
                default: throw new ArgumentOutOfRangeException(nameof(source));
            }
        }

        private static string RequireArgument(global::GameDeveloperKit.Story.Model.Command command, string key)
        {
            var value = command.Arguments.GetString(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new GameException($"Story media argument is missing. command:{command.CommandId} argument:{key}");
            }

            return value;
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(MediaCommandHandler));
            }
        }
    }
}
