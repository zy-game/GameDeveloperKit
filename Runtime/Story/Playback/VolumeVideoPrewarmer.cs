using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using UnityEngine;
using StoryCommand = GameDeveloperKit.Story.Model.Command;
using StoryVolume = GameDeveloperKit.Story.Model.Volume;

namespace GameDeveloperKit.Story.Playback
{
    public static class VolumeVideoPrewarmer
    {
        public static VolumeVideoPrewarmSession PrewarmVolume(
            StoryModule storyModule,
            PlayableModule playableModule,
            string storyId,
            string volumeId)
        {
            if (storyModule == null)
            {
                throw new ArgumentNullException(nameof(storyModule));
            }

            if (playableModule == null)
            {
                throw new ArgumentNullException(nameof(playableModule));
            }

            ValidateId(storyId, nameof(storyId));
            ValidateId(volumeId, nameof(volumeId));
            if (storyModule.TryGetVolume(storyId, volumeId, out var volume) is false)
            {
                throw new GameException(
                    $"Story volume is not registered. story:{storyId} volume:{volumeId}");
            }

            return new VolumeVideoPrewarmSession(
                playableModule.Video,
                storyId,
                volumeId,
                CollectVideoRequests(volume));
        }

        internal static IReadOnlyList<StoryCommand> CollectVideoCommands(StoryVolume volume)
        {
            if (volume == null)
            {
                throw new ArgumentNullException(nameof(volume));
            }

            var commands = new List<StoryCommand>();
            for (var episodeIndex = 0; episodeIndex < volume.Episodes.Count; episodeIndex++)
            {
                var episode = volume.Episodes[episodeIndex];
                for (var stepIndex = 0; stepIndex < (episode?.Steps.Count ?? 0); stepIndex++)
                {
                    var step = episode.Steps[stepIndex];
                    var command = step?.Data?.Command;
                    if (step?.Kind == StepKind.Command &&
                        command != null &&
                        string.Equals(command.Name, MediaCommandNames.PlayVideo, StringComparison.Ordinal))
                    {
                        commands.Add(command);
                    }
                }
            }

            return commands;
        }

        internal static IReadOnlyList<VideoPlayableRequest> CollectVideoRequests(StoryVolume volume)
        {
            var commands = CollectVideoCommands(volume);
            var requests = new List<VideoPlayableRequest>(commands.Count);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < commands.Count; i++)
            {
                var request = MediaCommandHandler.CreateVideoRequest(commands[i], null, true);
                if (paths.Add(request.Path))
                {
                    requests.Add(request);
                }
            }

            return requests;
        }

        private static void ValidateId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Story playback id cannot be empty.", parameterName);
            }
        }
    }

    public sealed class VolumeVideoPrewarmSession : IDisposable
    {
        private readonly VideoPlayable m_Video;
        private readonly IReadOnlyList<VideoPlayableRequest> m_Requests;
        private readonly Dictionary<string, VideoPlayableRequest> m_RequestsByPath;
        private readonly HashSet<string> m_ReplenishingPaths = new HashSet<string>(StringComparer.Ordinal);
        private readonly UniTaskCompletionSource m_Completion = new UniTaskCompletionSource();
        private CancellationTokenSource m_Cancellation = new CancellationTokenSource();
        private bool m_Disposed;

        internal VolumeVideoPrewarmSession(
            VideoPlayable video,
            string storyId,
            string volumeId,
            IReadOnlyList<VideoPlayableRequest> requests)
        {
            m_Video = video ?? throw new ArgumentNullException(nameof(video));
            m_Requests = requests ?? throw new ArgumentNullException(nameof(requests));
            m_RequestsByPath = new Dictionary<string, VideoPlayableRequest>(StringComparer.Ordinal);
            for (var i = 0; i < requests.Count; i++)
            {
                m_RequestsByPath[requests[i].Path] = requests[i];
            }

            StoryId = storyId;
            VolumeId = volumeId;
            m_Video.PlaybackStarted += OnPlaybackStarted;
            PrewarmAsync().Forget(Debug.LogException);
        }

        public string StoryId { get; }

        public string VolumeId { get; }

        public int VideoCount => m_Requests.Count;

        public UniTask Completion => m_Completion.Task;

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            m_Video.PlaybackStarted -= OnPlaybackStarted;
            var cancellation = m_Cancellation;
            m_Cancellation = null;
            cancellation?.Cancel();
            for (var i = 0; i < m_Requests.Count; i++)
            {
                m_Video.ReleasePreload(m_Requests[i].Path);
            }

            cancellation?.Dispose();
        }

        private async UniTask PrewarmAsync()
        {
            var cancellation = m_Cancellation;
            try
            {
                var tasks = new UniTask[m_Requests.Count];
                for (var i = 0; i < m_Requests.Count; i++)
                {
                    tasks[i] = m_Video.PreloadAsync(m_Requests[i], cancellation.Token);
                }

                await UniTask.WhenAll(tasks);
                cancellation.Token.ThrowIfCancellationRequested();
                m_Completion.TrySetResult();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                m_Completion.TrySetCanceled(cancellation.Token);
            }
            catch (Exception exception)
            {
                m_Completion.TrySetException(exception);
            }
        }

        private void OnPlaybackStarted(VideoPlayableHandle playback)
        {
            if (m_Disposed ||
                playback == null ||
                m_RequestsByPath.TryGetValue(playback.Path, out var request) is false ||
                m_ReplenishingPaths.Add(playback.Path) is false)
            {
                return;
            }

            ReplenishAsync(request).Forget(Debug.LogException);
        }

        private async UniTask ReplenishAsync(VideoPlayableRequest request)
        {
            var cancellation = m_Cancellation;
            if (cancellation == null)
            {
                m_ReplenishingPaths.Remove(request.Path);
                return;
            }

            try
            {
                await m_Video.PreloadAsync(request, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                Debug.Log(
                    $"Volume video replenishment canceled. story:{StoryId} volume:{VolumeId} " +
                    $"path:{request.Path}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Volume video replenishment failed. story:{StoryId} volume:{VolumeId} " +
                    $"path:{request.Path} error:{exception.Message}");
            }
            finally
            {
                m_ReplenishingPaths.Remove(request.Path);
            }
        }
    }
}
