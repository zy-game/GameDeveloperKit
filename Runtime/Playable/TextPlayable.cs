using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Playable
{
    public sealed class TextPlayableRequest
    {
        public TextPlayableRequest(string text, Action<string> output)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public string Text { get; }

        public Action<string> Output { get; }
    }

    public sealed class TextPlayable : PlayableBase<TextPlayableRequest, TextPlayableHandle>
    {
        public override UniTask<TextPlayableHandle> PlayAsync(
            TextPlayableRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            request.Output(request.Text);
            var handle = new TextPlayableHandle(request.Text, request.Output);
            handle.Start();
            return UniTask.FromResult(handle);
        }

        public override void Dispose()
        {
        }
    }

    public sealed class TextPlayableHandle : PlayableHandle
    {
        private readonly Action<string> m_Output;

        internal TextPlayableHandle(string text, Action<string> output)
        {
            Text = text;
            m_Output = output;
        }

        public string Text { get; }

        internal void Start()
        {
            SetPlaying();
        }

        protected override void OnPause()
        {
        }

        protected override void OnResume()
        {
            m_Output(Text);
        }

        protected override void OnStop()
        {
            m_Output(string.Empty);
        }
    }
}
