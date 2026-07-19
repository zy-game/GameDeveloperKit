using System;

namespace GameDeveloperKit.Story.Publishing
{
    public static class IdentityId
    {
        public static string New()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string RootEdge(string episodeId)
        {
            Validate(episodeId, nameof(episodeId));
            return $"root_{episodeId.Length}_{episodeId}";
        }

        public static string ExitEdge(string episodeId, string exitId)
        {
            Validate(episodeId, nameof(episodeId));
            Validate(exitId, nameof(exitId));
            return $"route_{episodeId.Length}_{episodeId}_{exitId.Length}_{exitId}";
        }

        private static void Validate(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identity cannot be empty.", parameterName);
            }
        }
    }
}
