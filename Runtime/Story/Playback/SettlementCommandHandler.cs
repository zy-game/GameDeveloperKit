using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Settlement;
using UnityEngine;

namespace GameDeveloperKit.Story.Playback
{
    public sealed class SettlementCommandHandler : ICommandHandler
    {
        private readonly Func<ISettlementExecutor> m_Executor;

        public SettlementCommandHandler(Func<ISettlementExecutor> executor)
        {
            m_Executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public bool CanHandle(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command != null && string.Equals(command.Name, SettlementCommandNames.SettleEpisode, StringComparison.Ordinal);
        }

        public ICommandHandle Execute(global::GameDeveloperKit.Story.Model.Command command, RuntimeContext context)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var handle = new CommandHandle(command);
            ExecuteAsync(handle, context).Forget(Debug.LogException);
            return handle;
        }

        private async UniTask ExecuteAsync(CommandHandle handle, RuntimeContext context)
        {
            using var cancellation = new CancellationTokenSource();
            void CancelExecution(ICommandHandle _)
            {
                cancellation.Cancel();
            }
            handle.Canceled += CancelExecution;
            handle.Stopped += CancelExecution;
            try
            {
                var planVersion = handle.Command.Arguments.GetNumber(SettlementCommandNames.PlanVersionArgument);
                if (planVersion != SettlementPlan.CurrentVersion)
                {
                    throw new GameException($"Story settlement plan version is unsupported. command:{handle.Command.CommandId} version:{planVersion}");
                }
                if (SettlementPlanCodec.TryDeserialize(handle.Command.Arguments.GetString(SettlementCommandNames.PlanArgument), out var plan, out var error) is false)
                {
                    throw new GameException($"Story settlement plan is invalid. command:{handle.Command.CommandId} error:{error}");
                }
                var settlementId = handle.Command.Arguments.GetString(SettlementCommandNames.SettlementIdArgument);
                if (plan.Version != planVersion ||
                    !string.Equals(plan.SettlementId, settlementId, StringComparison.Ordinal))
                {
                    throw new GameException($"Story settlement command metadata does not match its plan. command:{handle.Command.CommandId}");
                }

                var executor = m_Executor();
                if (executor == null)
                {
                    handle.Complete(SettlementCommandNames.FailedOutcome);
                    return;
                }
                var settlementContext = new SettlementContext(
                    context.Program.StoryId,
                    context.Volume.VolumeId,
                    context.Episode.EpisodeId,
                    plan.SettlementId,
                    plan.Version);
                var result = await executor.ExecuteAsync(plan, settlementContext, cancellation.Token);
                if (handle.IsCanceled || handle.IsStopped) return;
                switch (result.Status)
                {
                    case SettlementStatus.Applied:
                    case SettlementStatus.AlreadyApplied:
                        handle.Complete(SettlementCommandNames.CompletedOutcome);
                        break;
                    case SettlementStatus.Failed:
                        handle.Complete(SettlementCommandNames.FailedOutcome);
                        break;
                    default:
                        throw new GameException($"Story settlement executor returned an invalid status. command:{handle.Command.CommandId} status:{result.Status}");
                }
            }
            catch (Exception exception)
            {
                if (handle.IsCanceled || handle.IsStopped) return;
                handle.Fail(exception);
            }
            finally
            {
                handle.Canceled -= CancelExecution;
                handle.Stopped -= CancelExecution;
            }
        }
    }
}
