using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Sound;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 使用 SoundModule 播放 Story 音频命令。
    /// </summary>
    public sealed class StorySoundCommandPlayer : IStoryAudioCommandPlayer
    {
        private const string TrackArgument = "track";
        private const string LoopArgument = "loop";
        private const string VolumeArgument = "volume";
        private const string PriorityArgument = "priority";

        private readonly SoundModule m_SoundModule;

        /// <summary>
        /// 初始化 Story 音频命令播放器。
        /// </summary>
        /// <param name="soundModule">声音模块。</param>
        public StorySoundCommandPlayer(SoundModule soundModule)
        {
            m_SoundModule = soundModule ?? throw new ArgumentNullException(nameof(soundModule));
        }

        /// <summary>
        /// 使用 App.Sound 创建播放器。
        /// </summary>
        /// <returns>音频命令播放器。</returns>
        public static StorySoundCommandPlayer FromApp()
        {
            return new StorySoundCommandPlayer(App.Sound);
        }

        /// <inheritdoc />
        public IStoryCommandHandle PlayAudio(StoryCommand command, StoryRuntimeContext context, string clipPath)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (string.IsNullOrWhiteSpace(clipPath))
            {
                throw new ArgumentException("Clip path cannot be empty.", nameof(clipPath));
            }

            var handle = new StoryCommandHandle(command);
            PlayAudioAsync(handle, clipPath).Forget();
            return handle;
        }

        private async UniTaskVoid PlayAudioAsync(StoryCommandHandle storyHandle, string clipPath)
        {
            SoundHandle soundHandle = null;
            void StopSound(IStoryCommandHandle _)
            {
                soundHandle?.Stop();
            }

            storyHandle.Canceled += StopSound;
            storyHandle.Stopped += StopSound;

            try
            {
                var options = BuildOptions(storyHandle.Command);
                soundHandle = await PlaySoundAsync(clipPath, options);
                if (StoryMediaCommandUtility.IsTerminal(storyHandle))
                {
                    soundHandle.Stop();
                    return;
                }

                await soundHandle.WaitForCompleteAsync();
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
                storyHandle.Canceled -= StopSound;
                storyHandle.Stopped -= StopSound;
            }
        }

        private UniTask<SoundHandle> PlaySoundAsync(string clipPath, SoundPlayOptions options)
        {
            if (options.Track == SoundTrack.Music)
            {
                return m_SoundModule.PlayMusicAsync(clipPath, options);
            }

            return m_SoundModule.PlaySfxAsync(clipPath, options);
        }

        private static SoundPlayOptions BuildOptions(StoryCommand command)
        {
            return new SoundPlayOptions
            {
                Track = ParseTrack(command.Arguments.GetString(TrackArgument)),
                Loop = command.Arguments.GetBoolean(LoopArgument, false),
                Volume = Clamp01((float)command.Arguments.GetNumber(VolumeArgument, 1d)),
                Priority = ClampPriority((int)command.Arguments.GetNumber(PriorityArgument, 128d)),
            };
        }

        private static SoundTrack ParseTrack(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SoundTrack.Sfx;
            }

            return Enum.TryParse(value, true, out SoundTrack track) ? track : SoundTrack.Sfx;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        private static int ClampPriority(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 256 ? 256 : value;
        }
    }
}
