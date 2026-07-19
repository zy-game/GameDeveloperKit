namespace GameDeveloperKit.StoryEditor.Authoring
{
    public readonly struct RouteMutationResult
    {
        private RouteMutationResult(
            bool succeeded,
            string errorCode,
            string message,
            string episodeId,
            string edgeId)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            Message = message;
            EpisodeId = episodeId;
            EdgeId = edgeId;
        }

        public bool Succeeded { get; }

        public string ErrorCode { get; }

        public string Message { get; }

        public string EpisodeId { get; }

        public string EdgeId { get; }

        internal static RouteMutationResult Success(string message, string episodeId = null, string edgeId = null)
        {
            return new RouteMutationResult(true, null, message, episodeId, edgeId);
        }

        internal static RouteMutationResult Failure(string errorCode, string message)
        {
            return new RouteMutationResult(false, errorCode, message, null, null);
        }
    }
}
