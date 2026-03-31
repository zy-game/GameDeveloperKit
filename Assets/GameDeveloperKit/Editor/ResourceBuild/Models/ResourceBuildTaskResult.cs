using System.Collections.Generic;

namespace GameDeveloperKit.Editor
{
    internal sealed class ResourceBuildTaskResult
    {
        public bool Success { get; private set; }

        public string ErrorMessage { get; private set; }

        public List<string> Warnings { get; private set; }

        public static ResourceBuildTaskResult Succeed(IEnumerable<string> warnings = null)
        {
            return new ResourceBuildTaskResult
            {
                Success = true,
                Warnings = warnings == null ? new List<string>() : new List<string>(warnings)
            };
        }

        public static ResourceBuildTaskResult Failed(string errorMessage, IEnumerable<string> warnings = null)
        {
            return new ResourceBuildTaskResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Warnings = warnings == null ? new List<string>() : new List<string>(warnings)
            };
        }
    }
}
