using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using UnityEngine;

namespace GameDeveloperKit.Story
{
    public sealed class StoryPlayable : IStoryCommandHandler, IDisposable
    {
        private readonly PlayableModule m_PlayableModule;
        private readonly Func<UnityEngine.UI.RawImage> m_ImageOutput;
        private readonly Transform m_VideoParent;
        private bool m_Disposed;

        public StoryPlayable(
            PlayableModule playableModule,
            Func<UnityEngine.UI.RawImage> imageOutput,
            Transform videoParent)
        {
            m_PlayableModule = playableModule ?? throw new ArgumentNullException(nameof(playableModule));
            m_ImageOutput = imageOutput;
            m_VideoParent = videoParent;
        }

        public VideoPlayable Video => m_PlayableModule.Video;

        public bool CanHandle(StoryCommand command)
        {
            return command != null && command.Name is
                StoryMediaCommandNames.PlayVideo or
                StoryMediaCommandNames.ShowImage or
                StoryMediaCommandNames.PlayAudio;
        }

        public IStoryCommandHandle Execute(StoryCommand command, StoryRuntimeContext context)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            ThrowIfDisposed();
            var storyHandle = new StoryCommandHandle(command);
            ExecuteAsync(storyHandle).Forget(Debug.LogException);
            return storyHandle;
        }

        public UniTask PreloadVideoAsync(StoryCommand command, CancellationToken cancellationToken = default)
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

        private async UniTask ExecuteAsync(StoryCommandHandle storyHandle)
        {
            PlayableHandle playableHandle = null;
            void StopPlayable(IStoryCommandHandle _)
            {
                playableHandle?.Stop();
            }

            storyHandle.Canceled += StopPlayable;
            storyHandle.Stopped += StopPlayable;
            try
            {
                switch (storyHandle.Command.Name)
                {
                    case StoryMediaCommandNames.PlayVideo:
                        playableHandle = await Video.PlayAsync(CreateVideoRequest(storyHandle.Command));
                        break;
                    case StoryMediaCommandNames.ShowImage:
                        playableHandle = await m_PlayableModule.Image.PlayAsync(CreateImageRequest(storyHandle.Command));
                        break;
                    case StoryMediaCommandNames.PlayAudio:
                        playableHandle = await m_PlayableModule.Audio.PlayAsync(CreateAudioRequest(storyHandle.Command));
                        break;
                    default:
                        throw new GameException($"Story media command is not supported: {storyHandle.Command.Name}");
                }

                if (StoryMediaCommandUtility.IsTerminal(storyHandle))
                {
                    playableHandle.Stop();
                    return;
                }

                if (storyHandle.Command.Name == StoryMediaCommandNames.ShowImage)
                {
                    storyHandle.Complete(StoryMediaCommandUtility.GetCompletedOutcome(storyHandle.Command));
                    return;
                }

                await playableHandle.WaitForCompletionAsync();
                if (StoryMediaCommandUtility.IsTerminal(storyHandle) is false)
                {
                    storyHandle.Complete(StoryMediaCommandUtility.GetCompletedOutcome(storyHandle.Command));
                }
            }
            catch (Exception exception)
            {
                if (StoryMediaCommandUtility.IsTerminal(storyHandle) is false)
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

        private VideoPlayableRequest CreateVideoRequest(StoryCommand command)
        {
            var clip = RequireArgument(command, StoryMediaCommandNames.ClipArgument);
            var source = command.Arguments.GetString(StoryMediaCommandNames.VideoSourceArgument);
            if (!StoryVideoPathResolver.TryResolve(source, clip, out var path, out var error))
            {
                throw new GameException($"Story video path is invalid. command:{command.CommandId} reason:{error}");
            }

            return new VideoPlayableRequest(path, new VideoPlayableOptions
            {
                Loop = command.Arguments.GetBoolean("loop", false),
                Seekable = string.Equals(
                    command.Arguments.GetString(StoryMediaCommandNames.VideoSeekPolicyArgument),
                    StoryMediaCommandNames.VideoSeekPolicyTransition,
                    StringComparison.Ordinal),
                Parent = m_VideoParent,
                DontDestroyOnLoad = false
            });
        }

        private ImagePlayableRequest CreateImageRequest(StoryCommand command)
        {
            if (m_ImageOutput == null)
            {
                throw new GameException("Story image output surface is missing.");
            }

            return new ImagePlayableRequest(
                RequireArgument(command, StoryMediaCommandNames.ImageArgument),
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

        private static AudioPlayableRequest CreateAudioRequest(StoryCommand command)
        {
            return new AudioPlayableRequest(
                RequireArgument(command, StoryMediaCommandNames.ClipArgument),
                new AudioPlayableOptions
                {
                    Loop = command.Arguments.GetBoolean("loop", false),
                    Volume = (float)command.Arguments.GetNumber("volume", 1d),
                    Priority = (int)command.Arguments.GetNumber("priority", 0d)
                });
        }

        private static string RequireArgument(StoryCommand command, string key)
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
                throw new ObjectDisposedException(nameof(StoryPlayable));
            }
        }
    }
}
