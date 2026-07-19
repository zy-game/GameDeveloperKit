using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ChannelBuild
{
    public static partial class ChannelBuildResponderRunner
    {
        public static ChannelBuildResponderExecution Execute(
            ChannelBuildContext context,
            IReadOnlyList<IChannelBuildResponder> responders,
            ChannelBuildOperation operation)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            var nodes = CreatePlanNodes(responders);
            var plan = CopyResponderPlan(nodes);
            var results = new List<ChannelBuildStepResult>();

            for (var i = 0; i < nodes.Count; i++)
            {
                ChannelBuildStepResult result;
                try
                {
                    result = nodes[i].Responder.Prepare(context);
                    ValidateResponderResult(result, nodes[i].Id, ChannelBuildResponderPhase.Prepare);
                }
                catch (Exception exception)
                {
                    throw new GameException("Channel build responder prepare failed with an exception.", exception);
                }

                results.Add(result);
                if (result.Success is false)
                {
                    return new ChannelBuildResponderExecution(plan, results, result);
                }
            }

            var applied = new List<ResponderNode>();
            ChannelBuildStepResult primaryFailure = null;
            Exception forwardException = null;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                applied.Add(node);
                try
                {
                    var result = node.Responder.Apply(context);
                    ValidateResponderResult(result, node.Id, ChannelBuildResponderPhase.Apply);
                    results.Add(result);
                    if (result.Success is false)
                    {
                        primaryFailure = result;
                        break;
                    }
                }
                catch (Exception exception)
                {
                    forwardException = exception;
                    break;
                }
            }

            if (primaryFailure == null && forwardException == null)
            {
                try
                {
                    var operationResult = operation(context);
                    ValidateOperationResult(operationResult);
                    results.Add(operationResult);
                    if (operationResult.Success is false)
                    {
                        primaryFailure = operationResult;
                    }
                }
                catch (Exception exception)
                {
                    forwardException = exception;
                }
            }

            var exceptions = new List<Exception>();
            if (forwardException != null)
            {
                exceptions.Add(forwardException);
            }
            for (var i = applied.Count - 1; i >= 0; i--)
            {
                var node = applied[i];
                try
                {
                    var restoreResult = node.Responder.Restore(context);
                    ValidateResponderResult(
                        restoreResult,
                        node.Id,
                        ChannelBuildResponderPhase.Restore);
                    results.Add(restoreResult);
                    if (restoreResult.Success is false && primaryFailure == null)
                    {
                        primaryFailure = restoreResult;
                    }
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            if (exceptions.Count > 0)
            {
                var innerException = exceptions.Count == 1
                    ? exceptions[0]
                    : new AggregateException(exceptions);
                throw new GameException(
                    "Channel build responder execution failed with an exception after cleanup.",
                    innerException);
            }

            return new ChannelBuildResponderExecution(plan, results, primaryFailure);
        }

        private static IReadOnlyList<IChannelBuildResponder> CopyResponderPlan(
            IReadOnlyList<ResponderNode> nodes)
        {
            var plan = new List<IChannelBuildResponder>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                plan.Add(nodes[i].Responder);
            }
            return plan.AsReadOnly();
        }

        private static void ValidateResponderResult(
            ChannelBuildStepResult result,
            string expectedId,
            ChannelBuildResponderPhase expectedPhase)
        {
            if (result == null)
            {
                throw new GameException("Channel build responder returned a null result.");
            }
            if (string.Equals(result.ResponderId, expectedId, StringComparison.Ordinal) is false ||
                result.Phase != expectedPhase)
            {
                throw new GameException("Channel build responder result does not match the invoked phase.");
            }
        }

        private static void ValidateOperationResult(ChannelBuildStepResult result)
        {
            if (result == null || result.Phase != ChannelBuildResponderPhase.Operation)
            {
                throw new GameException("Channel build operation returned an invalid result.");
            }
        }
    }
}
