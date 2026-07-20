using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Logic;

namespace GameDeveloperKit.Story.Playback
{
    public sealed class LogicCommandHandler : ICommandHandler
    {
        public bool CanHandle(global::GameDeveloperKit.Story.Model.Command command)
        {
            return LogicCommandCodec.IsLogicCommand(command);
        }

        public ICommandHandle Execute(
            global::GameDeveloperKit.Story.Model.Command command,
            RuntimeContext context)
        {
            if (!LogicCommandCodec.TryDecode(command, out var logicId, out var arguments, out var error))
            {
                throw new GameException(
                    $"Story logic command is invalid. {FormatLocation(context, command)} " +
                    $"logic:{command?.Name ?? "<unknown>"} reason:{error}");
            }

            ILogicNode executor;
            try
            {
                executor = LogicNodeRegistry.Create(logicId);
            }
            catch (Exception exception)
            {
                throw new GameException(
                    $"Story logic executor is unavailable. {FormatLocation(context, command)} logic:{logicId}",
                    exception);
            }

            var logicContext = new LogicContext(
                logicId,
                BuildInvocationId(context, command),
                arguments,
                context);
            var handle = new CommandHandle(command);
            new LogicDispatch(executor, logicContext, handle).Start();
            return handle;
        }

        internal static string BuildInvocationId(
            RuntimeContext context,
            global::GameDeveloperKit.Story.Model.Command command)
        {
            var historyOrdinal = 0;
            if (context.History != null)
            {
                for (var i = 0; i < context.History.Count; i++)
                {
                    var entry = context.History[i];
                    if (string.Equals(entry.EpisodeId, context.Episode?.EpisodeId, StringComparison.Ordinal) &&
                        string.Equals(entry.NodeId, context.Step?.StepId, StringComparison.Ordinal) &&
                        string.Equals(entry.ActionId, command?.CommandId, StringComparison.Ordinal))
                    {
                        historyOrdinal++;
                    }
                }
            }

            return $"story:{context.Program?.StoryId ?? "<unknown>"}/" +
                   $"volume:{context.Volume?.VolumeId ?? "<unknown>"}/" +
                   $"episode:{context.Episode?.EpisodeId ?? "<unknown>"}/" +
                   $"step:{context.Step?.StepId ?? "<unknown>"}/" +
                   $"command:{command?.CommandId ?? "<unknown>"}/" +
                   $"history:{historyOrdinal}";
        }

        private static string FormatLocation(
            RuntimeContext context,
            global::GameDeveloperKit.Story.Model.Command command)
        {
            return $"story:{context.Program?.StoryId ?? "<unknown>"} " +
                   $"volume:{context.Volume?.VolumeId ?? "<unknown>"} " +
                   $"episode:{context.Episode?.EpisodeId ?? "<unknown>"} " +
                   $"step:{context.Step?.StepId ?? "<unknown>"} " +
                   $"command:{command?.CommandId ?? "<unknown>"}";
        }

        private sealed class LogicDispatch
        {
            private readonly ILogicNode m_Executor;
            private readonly LogicContext m_Context;
            private readonly CommandHandle m_Handle;
            private readonly CancellationTokenSource m_Cancellation = new CancellationTokenSource();

            public LogicDispatch(
                ILogicNode executor,
                LogicContext context,
                CommandHandle handle)
            {
                m_Executor = executor;
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
                    var result = await m_Executor.ExecuteAsync(m_Context, m_Cancellation.Token);
                    if (IsTerminal())
                    {
                        return;
                    }

                    if (!ContainsOutput(result.OutputPortId))
                    {
                        m_Handle.Fail(new GameException(
                            $"Story logic executor returned an undeclared output. " +
                            $"{FormatLocation(m_Context.Runtime, m_Handle.Command)} " +
                            $"logic:{m_Context.LogicId} output:{result.OutputPortId ?? "<empty>"}"));
                        return;
                    }

                    m_Handle.Complete(result.OutputPortId);
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
                            $"Story logic executor failed. " +
                            $"{FormatLocation(m_Context.Runtime, m_Handle.Command)} logic:{m_Context.LogicId}",
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
                        $"Story logic executor failed unexpectedly. " +
                        $"{FormatLocation(m_Context.Runtime, m_Handle.Command)} logic:{m_Context.LogicId}",
                        exception));
                }
            }

            private bool ContainsOutput(string outputId)
            {
                if (string.IsNullOrWhiteSpace(outputId))
                {
                    return false;
                }

                for (var i = 0; i < m_Handle.Command.OutcomePorts.Count; i++)
                {
                    if (string.Equals(
                            m_Handle.Command.OutcomePorts[i],
                            outputId,
                            StringComparison.Ordinal))
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
