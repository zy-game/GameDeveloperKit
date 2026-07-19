using System;
using GameDeveloperKit.Story.Protocol;

namespace GameDeveloperKit.Story.Playback
{
    public static class MediaCommandUtility
    {
        public static bool IsTerminal(ICommandHandle handle)
        {
            return handle == null ||
                   handle.IsCompleted ||
                   handle.IsCanceled ||
                   handle.IsStopped ||
                   handle.Error != null;
        }

        public static string GetCompletedOutcome(global::GameDeveloperKit.Story.Model.Command command)
        {
            if (command?.OutcomePorts == null)
            {
                return null;
            }

            for (var i = 0; i < command.OutcomePorts.Count; i++)
            {
                if (string.Equals(command.OutcomePorts[i], MediaCommandNames.CompletedOutcome, StringComparison.Ordinal))
                {
                    return MediaCommandNames.CompletedOutcome;
                }
            }

            return null;
        }
    }
}
