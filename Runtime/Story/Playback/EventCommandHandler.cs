using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Event;
using GameDeveloperKit.Story.Execution;

namespace GameDeveloperKit.Story.Playback
{
    public sealed class EventCommandHandler : ICommandHandler
    {
        private readonly Func<IEventHandler> m_HandlerProvider;

        public EventCommandHandler(Func<IEventHandler> handlerProvider)
        {
            m_HandlerProvider = handlerProvider ?? throw new ArgumentNullException(nameof(handlerProvider));
        }

        public bool CanHandle(global::GameDeveloperKit.Story.Model.Command command)
        {
            return EventCommandCodec.HasEventMarker(command);
        }

        public ICommandHandle Execute(
            global::GameDeveloperKit.Story.Model.Command command,
            RuntimeContext context)
        {
            if (!EventCommandCodec.TryDecode(command, out var request, out var error))
            {
                throw new GameException($"Story event command is invalid. {FormatLocation(context, command)} reason:{error}");
            }

            var handler = m_HandlerProvider();
            if (handler == null)
            {
                throw new GameException($"Story event handler is not registered. {FormatLocation(context, command)} event:{request.EventId}");
            }

            var handle = new CommandHandle(command);
            new EventDispatch(handler, request, context, handle).Start();
            return handle;
        }

        private static string FormatLocation(
            RuntimeContext context,
            global::GameDeveloperKit.Story.Model.Command command)
        {
            return $"story:{context.Program?.StoryId ?? "<unknown>"} " +
                   $"volume:{context.Volume?.VolumeId ?? "<unknown>"} " +
                   $"episode:{context.Episode?.EpisodeId ?? "<unknown>"} " +
                   $"step:{context.Step?.StepId ?? "<unknown>"} " +
                   $"request:{command?.CommandId ?? "<unknown>"}";
        }

        private sealed class EventDispatch
        {
            private readonly IEventHandler m_Handler;
            private readonly EventRequest m_Request;
            private readonly RuntimeContext m_Context;
            private readonly CommandHandle m_Handle;
            private readonly CancellationTokenSource m_Cancellation = new CancellationTokenSource();

            public EventDispatch(
                IEventHandler handler,
                EventRequest request,
                RuntimeContext context,
                CommandHandle handle)
            {
                m_Handler = handler;
                m_Request = request;
                m_Context = context;
                m_Handle = handle;
            }

            public void Start()
            {
                m_Handle.Canceled += OnHandleFinished;
                m_Handle.Stopped += OnHandleFinished;
                RunAsync().Forget(OnUnexpectedFailure);
            }

            private async UniTask RunAsync()
            {
                try
                {
                    var result = await m_Handler.HandleAsync(m_Request, m_Cancellation.Token);
                    if (IsTerminal())
                    {
                        return;
                    }

                    if (m_Request.Mode == EventMode.Request && !ContainsOutcome(result.OutcomeId))
                    {
                        m_Handle.Fail(new GameException(
                            $"Story event handler returned an undeclared outcome. " +
                            $"{FormatLocation(m_Context, m_Handle.Command)} event:{m_Request.EventId} " +
                            $"outcome:{result.OutcomeId ?? "<empty>"}"));
                        return;
                    }

                    m_Handle.Complete(m_Request.Mode == EventMode.Request ? result.OutcomeId : null);
                }
                catch (OperationCanceledException) when (m_Cancellation.IsCancellationRequested)
                {
                    if (!IsTerminal())
                    {
                        m_Handle.Cancel();
                    }
                }
                catch (Exception exception)
                {
                    if (!IsTerminal())
                    {
                        m_Handle.Fail(new GameException(
                            $"Story event handler failed. {FormatLocation(m_Context, m_Handle.Command)} event:{m_Request.EventId}",
                            exception));
                    }
                }
                finally
                {
                    m_Handle.Canceled -= OnHandleFinished;
                    m_Handle.Stopped -= OnHandleFinished;
                    m_Cancellation.Dispose();
                }
            }

            private void OnUnexpectedFailure(Exception exception)
            {
                if (!IsTerminal())
                {
                    m_Handle.Fail(new GameException(
                        $"Story event handler failed unexpectedly. " +
                        $"{FormatLocation(m_Context, m_Handle.Command)} event:{m_Request.EventId}",
                        exception));
                }
            }

            private bool ContainsOutcome(string outcomeId)
            {
                if (string.IsNullOrWhiteSpace(outcomeId))
                {
                    return false;
                }

                for (var i = 0; i < m_Request.Outcomes.Count; i++)
                {
                    if (string.Equals(m_Request.Outcomes[i], outcomeId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool IsTerminal()
            {
                return m_Handle.IsCompleted ||
                       m_Handle.IsCanceled ||
                       m_Handle.IsStopped ||
                       m_Handle.Error != null;
            }

            private void OnHandleFinished(ICommandHandle handle)
            {
                if (!m_Cancellation.IsCancellationRequested)
                {
                    m_Cancellation.Cancel();
                }
            }
        }
    }
}
