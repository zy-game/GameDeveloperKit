using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class EventModule
    {
        private void RaiseInternal(EventRegistrationKey key, object sender, CancellationToken cancellationToken, object[] args)
        {
            PrepareRaise(key);

            if (!_handlers.TryGetValue(key, out var handlers) || handlers.Count == 0)
            {
                return;
            }

            var context = AcquireContext(sender, key, args, cancellationToken);
            try
            {
                InvokeSyncHandlers(handlers, context);
            }
            finally
            {
                ReleaseContext(context);
            }
        }

        private void RaiseInternal(EventRegistrationKey key, object sender, CancellationToken cancellationToken)
        {
            PrepareRaise(key);

            if (!_handlers.TryGetValue(key, out var handlers) || handlers.Count == 0)
            {
                return;
            }

            var context = AcquireContext(sender, key, cancellationToken);
            try
            {
                InvokeSyncHandlers(handlers, context);
            }
            finally
            {
                ReleaseContext(context);
            }
        }

        private void RaiseInternal<TArg0>(EventRegistrationKey key, object sender, CancellationToken cancellationToken, TArg0 arg0)
        {
            PrepareRaise(key);

            if (!_handlers.TryGetValue(key, out var handlers) || handlers.Count == 0)
            {
                return;
            }

            var context = AcquireContext(sender, key, cancellationToken, arg0);
            try
            {
                InvokeSyncHandlers(handlers, context);
            }
            finally
            {
                ReleaseContext(context);
            }
        }

        private void RaiseInternal<TArg0, TArg1>(EventRegistrationKey key, object sender, CancellationToken cancellationToken, TArg0 arg0, TArg1 arg1)
        {
            PrepareRaise(key);

            if (!_handlers.TryGetValue(key, out var handlers) || handlers.Count == 0)
            {
                return;
            }

            var context = AcquireContext(sender, key, cancellationToken, arg0, arg1);
            try
            {
                InvokeSyncHandlers(handlers, context);
            }
            finally
            {
                ReleaseContext(context);
            }
        }

        private async UniTask RaiseAsyncInternal(EventRegistrationKey key, object sender, CancellationToken cancellationToken, object[] args)
        {
            PrepareRaise(key);

            var context = AcquireContext(sender, key, args, cancellationToken);
            try
            {
                InvokeRegisteredSyncHandlers(key, context);
                await InvokeRegisteredAsyncHandlers(key, context);
            }
            finally
            {
                ReleaseContext(context);
            }
        }

        private async UniTask RaiseAsyncInternal(EventRegistrationKey key, object sender, CancellationToken cancellationToken)
        {
            PrepareRaise(key);

            var context = AcquireContext(sender, key, cancellationToken);
            try
            {
                InvokeRegisteredSyncHandlers(key, context);
                await InvokeRegisteredAsyncHandlers(key, context);
            }
            finally
            {
                ReleaseContext(context);
            }
        }

        private async UniTask RaiseAsyncInternal<TArg0>(EventRegistrationKey key, object sender, CancellationToken cancellationToken, TArg0 arg0)
        {
            PrepareRaise(key);

            var context = AcquireContext(sender, key, cancellationToken, arg0);
            try
            {
                InvokeRegisteredSyncHandlers(key, context);
                await InvokeRegisteredAsyncHandlers(key, context);
            }
            finally
            {
                ReleaseContext(context);
            }
        }

        private async UniTask RaiseAsyncInternal<TArg0, TArg1>(EventRegistrationKey key, object sender, CancellationToken cancellationToken, TArg0 arg0, TArg1 arg1)
        {
            PrepareRaise(key);

            var context = AcquireContext(sender, key, cancellationToken, arg0, arg1);
            try
            {
                InvokeRegisteredSyncHandlers(key, context);
                await InvokeRegisteredAsyncHandlers(key, context);
            }
            finally
            {
                ReleaseContext(context);
            }
        }

        private void PrepareRaise(EventRegistrationKey key)
        {
            EnsureBindingsInitialized();
            _raiseCount++;
            _lastEventName = key.Name;
            EnsureDiagnosticsSnapshotProviders();
        }

        private void InvokeRegisteredSyncHandlers(EventRegistrationKey key, EventContext context)
        {
            if (!_handlers.TryGetValue(key, out var handlers) || handlers.Count == 0)
            {
                return;
            }

            InvokeSyncHandlers(handlers, context);
        }

        private void InvokeSyncHandlers(List<IEventHandle> handlers, EventContext context)
        {
            var snapshot = handlers.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
            {
                InvokeSyncHandler(snapshot[i], context);
            }
        }

        private async UniTask InvokeRegisteredAsyncHandlers(EventRegistrationKey key, EventContext context)
        {
            if (!_asyncHandlers.TryGetValue(key, out var handlers) || handlers.Count == 0)
            {
                return;
            }

            var snapshot = handlers.ToArray();
            List<Exception> exceptions = null;
            for (var i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    await InvokeAsyncHandler(snapshot[i], context);
                }
                catch (Exception exception)
                {
                    if (!ContinueOnAsyncHandlerException)
                    {
                        throw;
                    }

                    exceptions ??= new List<Exception>();
                    exceptions.Add(exception);
                }
            }

            ThrowIfAsyncHandlerFailed(key, exceptions);
        }

        private static void ThrowIfAsyncHandlerFailed(EventRegistrationKey key, List<Exception> exceptions)
        {
            if (exceptions?.Count == 1)
            {
                throw exceptions[0];
            }

            if (exceptions?.Count > 1)
            {
                throw new AggregateException($"Event '{key.Name}' encountered {exceptions.Count} async handler failures.", exceptions);
            }
        }

        private static EventContext AcquireContext(object sender, EventRegistrationKey key, CancellationToken cancellationToken)
        {
            var context = Game.Pool.ReferencePool.Acquire<EventContext>();
            context.Initialize(sender, key.Value, key.Name, cancellationToken);
            return context;
        }

        private static EventContext AcquireContext<TArg0>(object sender, EventRegistrationKey key, CancellationToken cancellationToken, TArg0 arg0)
        {
            var context = Game.Pool.ReferencePool.Acquire<EventContext>();
            context.Initialize(sender, key.Value, key.Name, arg0, cancellationToken);
            return context;
        }

        private static EventContext AcquireContext<TArg0, TArg1>(object sender, EventRegistrationKey key, CancellationToken cancellationToken, TArg0 arg0, TArg1 arg1)
        {
            var context = Game.Pool.ReferencePool.Acquire<EventContext>();
            context.Initialize(sender, key.Value, key.Name, arg0, arg1, cancellationToken);
            return context;
        }

        private static EventContext AcquireContext(object sender, EventRegistrationKey key, object[] args, CancellationToken cancellationToken)
        {
            var context = Game.Pool.ReferencePool.Acquire<EventContext>();
            context.Initialize(sender, key.Value, key.Name, args, cancellationToken);
            return context;
        }

        private static void ReleaseContext(EventContext context)
        {
            Game.Pool.ReferencePool.Release(context);
        }

        private void InvokeSyncHandler(IEventHandle handler, EventContext context)
        {
            if (handler == null)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                handler.Handle(context);
                stopwatch.Stop();
                RecordHandlerInvocation(handler.GetType(), stopwatch.ElapsedMilliseconds, null);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                RecordHandlerInvocation(handler.GetType(), stopwatch.ElapsedMilliseconds, exception);
                throw;
            }
        }

        private async UniTask InvokeAsyncHandler(IAsyncEventHandle handler, EventContext context)
        {
            if (handler == null)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await handler.HandleAsync(context);
                stopwatch.Stop();
                RecordHandlerInvocation(handler.GetType(), stopwatch.ElapsedMilliseconds, null);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                RecordHandlerInvocation(handler.GetType(), stopwatch.ElapsedMilliseconds, exception);
                throw;
            }
        }

        private void RecordHandlerInvocation(Type handlerType, long durationMilliseconds, Exception exception)
        {
            _handlerInvocationCount++;
            _totalHandlerDurationMilliseconds += Math.Max(0L, durationMilliseconds);
            _lastHandlerType = handlerType?.FullName ?? string.Empty;

            if (exception != null)
            {
                _handlerFailureCount++;
                _lastError = exception.Message;
            }

            if (!DebugEnabled)
            {
                return;
            }

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Event.LastEvent", _lastEventName ?? string.Empty);
                diagnostics.CaptureSnapshot("Event.LastHandlerType", _lastHandlerType ?? string.Empty);
                diagnostics.CaptureSnapshot("Event.LastError", _lastError ?? string.Empty);
            }
        }

        private void EnsureDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Event.RegisteredEventCount", () => EventCount.ToString());
            diagnostics.RegisterSnapshotProvider("Event.RaiseCount", () => _raiseCount.ToString());
            diagnostics.RegisterSnapshotProvider("Event.HandlerInvocationCount", () => _handlerInvocationCount.ToString());
            diagnostics.RegisterSnapshotProvider("Event.HandlerFailureCount", () => _handlerFailureCount.ToString());
            diagnostics.RegisterSnapshotProvider("Event.AverageHandlerDurationMs", () => _handlerInvocationCount == 0 ? "0" : (_totalHandlerDurationMilliseconds / _handlerInvocationCount).ToString());
            diagnostics.RegisterSnapshotProvider("Event.LastEvent", () => _lastEventName ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Event.LastHandlerType", () => _lastHandlerType ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Event.LastError", () => _lastError ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Event.DebugEnabled", () => DebugEnabled.ToString());
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Event.RegisteredEventCount");
            diagnostics.RemoveSnapshotProvider("Event.RaiseCount");
            diagnostics.RemoveSnapshotProvider("Event.HandlerInvocationCount");
            diagnostics.RemoveSnapshotProvider("Event.HandlerFailureCount");
            diagnostics.RemoveSnapshotProvider("Event.AverageHandlerDurationMs");
            diagnostics.RemoveSnapshotProvider("Event.LastEvent");
            diagnostics.RemoveSnapshotProvider("Event.LastHandlerType");
            diagnostics.RemoveSnapshotProvider("Event.LastError");
            diagnostics.RemoveSnapshotProvider("Event.DebugEnabled");
            _diagnosticsRegistered = false;
        }
    }
}

