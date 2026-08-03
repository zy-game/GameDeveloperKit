using System;
using GameDeveloperKit.Story.Events;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// 解锁命令只派发一个事件并立即完成，不持有业务状态。
    /// </summary>
    public sealed class UnlockCommandHandler : ICommandHandler
    {
        public bool CanHandle(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command != null && string.Equals(command.Name, StoryCommandNames.Unlock, StringComparison.Ordinal);
        }

        public ICommandHandle Execute(global::GameDeveloperKit.Story.Model.Command command, RuntimeContext context)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var unlockId = command.Arguments.GetString(StoryCommandNames.UnlockIdArgument);
            if (string.IsNullOrWhiteSpace(unlockId))
            {
                throw new GameException($"Unlock command is missing unlock id. command:{command.CommandId}");
            }

            var handle = new CommandHandle(command);
            App.Event.FireNow(
                new StoryUnlockEvent(
                    context.Program?.StoryId,
                    context.Volume?.VolumeId,
                    context.Episode?.EpisodeId,
                    context.Step?.StepId,
                    unlockId),
                this);
            handle.Complete(MediaCommandNames.CompletedOutcome);
            return handle;
        }
    }
}
