using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.Playable
{
    [ModuleDependency(typeof(ResourceModule))]
    public sealed class PlayableModule : GameModuleBase
    {
        private readonly Dictionary<Type, IPlayable> m_Playables = new Dictionary<Type, IPlayable>();
        private readonly List<Type> m_RegistrationOrder = new List<Type>();

        public override void Startup()
        {
            Register(new AudioPlayable());
            Register(new TextPlayable());
            Register(new ImagePlayable());
            Register(new VideoPlayable());
        }

        public AudioPlayable Audio => Get<AudioPlayable>();

        public TextPlayable Text => Get<TextPlayable>();

        public ImagePlayable Image => Get<ImagePlayable>();

        public VideoPlayable Video => Get<VideoPlayable>();

        public UniTask<AudioPlayableHandle> PlayAudioAsync(
            string location,
            AudioPlayableOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return Audio.PlayAsync(new AudioPlayableRequest(location, options), cancellationToken);
        }

        public UniTask<TextPlayableHandle> PlayTextAsync(
            string text,
            Action<string> output,
            CancellationToken cancellationToken = default)
        {
            return Text.PlayAsync(new TextPlayableRequest(text, output), cancellationToken);
        }

        public UniTask<ImagePlayableHandle> PlayImageAsync(
            string location,
            Action<UnityEngine.Texture> output,
            CancellationToken cancellationToken = default)
        {
            return Image.PlayAsync(new ImagePlayableRequest(location, output), cancellationToken);
        }

        public UniTask<VideoPlayableHandle> PlayVideoAsync(
            string path,
            VideoPlayableOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return Video.PlayAsync(new VideoPlayableRequest(path, options), cancellationToken);
        }

        public override void Shutdown()
        {
            List<Exception> exceptions = null;
            for (var i = m_RegistrationOrder.Count - 1; i >= 0; i--)
            {
                var type = m_RegistrationOrder[i];
                if (!m_Playables.TryGetValue(type, out var playable))
                {
                    continue;
                }

                try
                {
                    playable.Dispose();
                }
                catch (Exception exception)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(exception);
                }
            }

            m_Playables.Clear();
            m_RegistrationOrder.Clear();
            if (exceptions == null)
            {
                return;
            }

            throw exceptions.Count == 1
                ? exceptions[0]
                : new AggregateException("Playable shutdown failed.", exceptions);
        }

        public void Register<TPlayable>(TPlayable playable) where TPlayable : class, IPlayable
        {
            if (playable == null)
            {
                throw new ArgumentNullException(nameof(playable));
            }

            var type = typeof(TPlayable);
            if (playable.GetType() != type)
            {
                throw new ArgumentException("Playable must be registered by its concrete type.", nameof(playable));
            }

            if (m_Playables.ContainsKey(type))
            {
                throw new GameException($"Playable '{type.Name}' has already been registered.");
            }

            m_Playables.Add(type, playable);
            m_RegistrationOrder.Add(type);
        }

        public TPlayable Get<TPlayable>() where TPlayable : class, IPlayable
        {
            if (TryGet<TPlayable>(out var playable))
            {
                return playable;
            }

            throw new GameException($"Playable '{typeof(TPlayable).Name}' is not registered.");
        }

        public bool TryGet<TPlayable>(out TPlayable playable) where TPlayable : class, IPlayable
        {
            if (m_Playables.TryGetValue(typeof(TPlayable), out var value))
            {
                playable = (TPlayable)value;
                return true;
            }

            playable = null;
            return false;
        }
    }
}
