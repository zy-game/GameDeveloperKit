using System;

namespace GameDeveloperKit.LubanConfigEditor
{
    /// <summary>
    /// 定义 Luban Run Report 类型。
    /// </summary>
    public sealed class LubanRunReport
    {
        public string Command { get; set; }

        public string WorkingDirectory { get; set; }

        public int ExitCode { get; set; }

        public TimeSpan Elapsed { get; set; }

        public string StandardOutput { get; set; }

        public string StandardError { get; set; }

        public string VersionLine { get; set; }

        public bool Success { get; set; }

        public string ErrorMessage { get; set; }

        /// <summary>
        /// 创建 Failure。
        /// </summary>
        /// <param name="command">command 参数。</param>
        /// <param name="workingDirectory">working Directory 参数。</param>
        /// <param name="errorMessage">error Message 参数。</param>
        /// <param name="standardOutput">standard Output 参数。</param>
        /// <param name="standardError">standard Error 参数。</param>
        /// <param name="exitCode">exit Code 参数。</param>
        /// <param name="elapsed">elapsed 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanRunReport CreateFailure(
            string command,
            string workingDirectory,
            string errorMessage,
            string standardOutput = "",
            string standardError = "",
            int exitCode = -1,
            TimeSpan elapsed = default)
        {
            return new LubanRunReport
            {
                Command = command,
                WorkingDirectory = workingDirectory,
                ExitCode = exitCode,
                Elapsed = elapsed,
                StandardOutput = standardOutput,
                StandardError = standardError,
                VersionLine = string.Empty,
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
