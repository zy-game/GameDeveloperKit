using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.LubanConfigEditor
{
    /// <summary>
    /// 定义 Luban Command Runner 类型。
    /// </summary>
    public static class LubanCommandRunner
    {
        /// <summary>
        /// 定义 Dotnet Path 常量。
        /// </summary>
        private const string DotnetPath = "dotnet";
        /// <summary>
        /// 定义 Detection Timeout Milliseconds 常量。
        /// </summary>
        private const int DetectionTimeoutMilliseconds = 10000;

        private static bool s_IsRunning;

        public static bool IsRunning => s_IsRunning;

        /// <summary>
        /// 检测 Release。
        /// </summary>
        /// <param name="releasePath">release Path 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanRunReport DetectRelease(string releasePath)
        {
            var absoluteReleasePath = GetAbsoluteProjectPath(releasePath);
            if (IOFile.Exists(absoluteReleasePath) is false)
            {
                return LubanRunReport.CreateFailure(
                    BuildDotnetCommand(absoluteReleasePath, "--version"),
                    GetProjectRoot(),
                    $"未找到 Luban.dll：{absoluteReleasePath}。请确认 Luban release 位于项目根 Luban/，或重新选择 Luban.dll。");
            }

            var versionReport = RunDotnet(absoluteReleasePath, "--version");
            if (versionReport.Success)
            {
                return versionReport;
            }

            var helpReport = RunDotnet(absoluteReleasePath, "--help");
            return string.IsNullOrWhiteSpace(helpReport.VersionLine) ? versionReport : helpReport;
        }

        /// <summary>
        /// 运行。
        /// </summary>
        /// <param name="preview">preview 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanRunReport Run(LubanCommandPreview preview)
        {
            if (preview == null)
            {
                throw new ArgumentNullException(nameof(preview));
            }

            if (s_IsRunning)
            {
                return LubanRunReport.CreateFailure(preview.Command, preview.WorkingDirectory, "已有 Luban command 正在执行。");
            }

            s_IsRunning = true;
            try
            {
                return RunDotnet(preview.Arguments, preview.Command, preview.WorkingDirectory, false, 0);
            }
            finally
            {
                s_IsRunning = false;
            }
        }

        /// <summary>
        /// 获取 Project Root。
        /// </summary>
        /// <returns>执行结果。</returns>
        public static string GetProjectRoot()
        {
            return IOPath.GetFullPath(IOPath.Combine(Application.dataPath, ".."));
        }

        /// <summary>
        /// 获取 Absolute Project Path。
        /// </summary>
        /// <param name="path">path 参数。</param>
        /// <returns>执行结果。</returns>
        public static string GetAbsoluteProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = LubanEditorSettings.DefaultReleasePath;
            }

            return IOPath.IsPathRooted(path)
                ? IOPath.GetFullPath(path)
                : IOPath.GetFullPath(IOPath.Combine(GetProjectRoot(), path));
        }

        /// <summary>
        /// 执行 To Project Relative Path。
        /// </summary>
        /// <param name="path">path 参数。</param>
        /// <returns>执行结果。</returns>
        public static string ToProjectRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var absolutePath = IOPath.GetFullPath(path);
            var projectRoot = GetProjectRoot().TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
            var rootWithSeparator = projectRoot + IOPath.DirectorySeparatorChar;
            if (absolutePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return absolutePath.Substring(rootWithSeparator.Length).Replace('\\', '/');
            }

            return absolutePath;
        }

        /// <summary>
        /// 运行 Dotnet。
        /// </summary>
        /// <param name="absoluteReleasePath">absolute Release Path 参数。</param>
        /// <param name="arguments">arguments 参数。</param>
        /// <returns>执行结果。</returns>
        private static LubanRunReport RunDotnet(string absoluteReleasePath, string arguments)
        {
            var dotnetArguments = $"{QuoteArgument(absoluteReleasePath)} {arguments}";
            var command = BuildDotnetCommand(absoluteReleasePath, arguments);
            return RunDotnet(dotnetArguments, command, GetProjectRoot(), true, DetectionTimeoutMilliseconds);
        }

        /// <summary>
        /// 运行 Dotnet。
        /// </summary>
        /// <param name="arguments">arguments 参数。</param>
        /// <param name="command">command 参数。</param>
        /// <param name="workingDirectory">working Directory 参数。</param>
        /// <param name="detectVersion">detect Version 参数。</param>
        /// <param name="timeoutMilliseconds">timeout Milliseconds 参数。</param>
        /// <returns>执行结果。</returns>
        private static LubanRunReport RunDotnet(string arguments, string command, string workingDirectory, bool detectVersion, int timeoutMilliseconds)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = DotnetPath,
                        Arguments = arguments,
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    process.OutputDataReceived += (_, args) =>
                    {
                        if (args.Data != null)
                        {
                            output.AppendLine(args.Data);
                        }
                    };
                    process.ErrorDataReceived += (_, args) =>
                    {
                        if (args.Data != null)
                        {
                            error.AppendLine(args.Data);
                        }
                    };

                    var stopwatch = Stopwatch.StartNew();
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (timeoutMilliseconds > 0 && process.WaitForExit(timeoutMilliseconds) is false)
                    {
                        process.Kill();
                        stopwatch.Stop();
                        return LubanRunReport.CreateFailure(command, workingDirectory, "Luban CLI 检测超时。", output.ToString(), error.ToString(), -1, stopwatch.Elapsed);
                    }

                    process.WaitForExit();
                    stopwatch.Stop();

                    var standardOutput = output.ToString();
                    var standardError = error.ToString();
                    var versionLine = ParseVersionLine(standardOutput, standardError);
                    var success = detectVersion ? string.IsNullOrWhiteSpace(versionLine) is false : process.ExitCode == 0;
                    var errorMessage = success
                        ? string.Empty
                        : MakeProcessErrorMessage(process.ExitCode, standardOutput, standardError);
                    return new LubanRunReport
                    {
                        Command = command,
                        WorkingDirectory = workingDirectory,
                        ExitCode = process.ExitCode,
                        Elapsed = stopwatch.Elapsed,
                        StandardOutput = standardOutput,
                        StandardError = standardError,
                        VersionLine = versionLine,
                        Success = success,
                        ErrorMessage = errorMessage
                    };
                }
            }
            catch (Win32Exception exception)
            {
                return LubanRunReport.CreateFailure(command, workingDirectory, $"无法启动 dotnet：{exception.Message}。请确认 .NET SDK / Runtime 已安装并在 PATH 中。");
            }
            catch (Exception exception)
            {
                return LubanRunReport.CreateFailure(command, workingDirectory, $"Luban CLI 执行失败：{exception.Message}");
            }
        }

        /// <summary>
        /// 解析 Version Line。
        /// </summary>
        /// <param name="standardOutput">standard Output 参数。</param>
        /// <param name="standardError">standard Error 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ParseVersionLine(string standardOutput, string standardError)
        {
            var combined = $"{standardOutput}{Environment.NewLine}{standardError}";
            var lines = combined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("Luban ", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmedLine;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 创建 Process Error Message。
        /// </summary>
        /// <param name="exitCode">exit Code 参数。</param>
        /// <param name="standardOutput">standard Output 参数。</param>
        /// <param name="standardError">standard Error 参数。</param>
        /// <returns>执行结果。</returns>
        private static string MakeProcessErrorMessage(int exitCode, string standardOutput, string standardError)
        {
            var message = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "未输出诊断信息。";
            }

            return $"Luban CLI 检测失败，退出码 {exitCode}：{message.Trim()}";
        }

        /// <summary>
        /// 创建 Dotnet Command。
        /// </summary>
        /// <param name="absoluteReleasePath">absolute Release Path 参数。</param>
        /// <param name="arguments">arguments 参数。</param>
        /// <returns>执行结果。</returns>
        private static string BuildDotnetCommand(string absoluteReleasePath, string arguments)
        {
            return $"{DotnetPath} {QuoteArgument(absoluteReleasePath)} {arguments}";
        }

        /// <summary>
        /// 引用 Argument。
        /// </summary>
        /// <param name="argument">argument 参数。</param>
        /// <returns>执行结果。</returns>
        internal static string QuoteArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            if (argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                return argument;
            }

            return $"\"{argument.Replace("\"", "\\\"")}\"";
        }
    }
}
