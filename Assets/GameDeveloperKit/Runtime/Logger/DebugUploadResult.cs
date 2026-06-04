using System;

namespace GameDeveloperKit.Logger
{
    public readonly struct DebugUploadResult
    {
        private DebugUploadResult(bool succeeded, bool disabled, string message, Exception exception, DebugBundle bundle)
        {
            Succeeded = succeeded;
            Disabled = disabled;
            Message = message;
            Exception = exception;
            Bundle = bundle;
        }

        public bool Succeeded { get; }

        public bool Disabled { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public DebugBundle Bundle { get; }

        public static DebugUploadResult Success(DebugBundle bundle, string message = null)
        {
            return new DebugUploadResult(true, false, message, null, bundle);
        }

        public static DebugUploadResult Failed(string message, Exception exception = null, DebugBundle bundle = null)
        {
            return new DebugUploadResult(false, false, message, exception, bundle);
        }

        public static DebugUploadResult DisabledResult(string message)
        {
            return new DebugUploadResult(false, true, message, null, null);
        }
    }
}
