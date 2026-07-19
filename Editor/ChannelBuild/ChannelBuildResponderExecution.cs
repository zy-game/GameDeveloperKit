using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelBuildResponderExecution
    {
        internal ChannelBuildResponderExecution(
            IReadOnlyList<IChannelBuildResponder> plan,
            IReadOnlyList<ChannelBuildStepResult> results,
            ChannelBuildStepResult primaryFailure)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            var planCopy = new List<IChannelBuildResponder>(plan.Count);
            for (var i = 0; i < plan.Count; i++)
            {
                if (plan[i] == null)
                {
                    throw new ArgumentException("Responder plan cannot contain null.", nameof(plan));
                }
                planCopy.Add(plan[i]);
            }

            var resultCopy = new List<ChannelBuildStepResult>(results.Count);
            var success = primaryFailure == null;
            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result == null)
                {
                    throw new ArgumentException("Execution results cannot contain null.", nameof(results));
                }
                success &= result.Success;
                resultCopy.Add(result);
            }

            Plan = planCopy.AsReadOnly();
            Results = resultCopy.AsReadOnly();
            PrimaryFailure = primaryFailure;
            Success = success;
        }

        public bool Success { get; }

        public IReadOnlyList<IChannelBuildResponder> Plan { get; }

        public IReadOnlyList<ChannelBuildStepResult> Results { get; }

        public ChannelBuildStepResult PrimaryFailure { get; }
    }
}
