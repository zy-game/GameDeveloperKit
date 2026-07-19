namespace GameDeveloperKit.StoryEditor.Authoring
{
    public readonly struct LayoutMutationResult
    {
        private LayoutMutationResult(bool succeeded, string errorCode, string message, string layoutId)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            Message = message;
            LayoutId = layoutId;
        }

        public bool Succeeded { get; }

        public string ErrorCode { get; }

        public string Message { get; }

        public string LayoutId { get; }

        internal static LayoutMutationResult Success(string message, string layoutId)
        {
            return new LayoutMutationResult(true, null, message, layoutId);
        }

        internal static LayoutMutationResult Failure(string errorCode, string message)
        {
            return new LayoutMutationResult(false, errorCode, message, null);
        }
    }
}
