using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.MediaEditor
{
    internal sealed class MediaProcessRequest
    {
        public MediaProcessRequest(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout,
            Action<string> standardOutputLine = null,
            Action<string> standardErrorLine = null)
        {
            ExecutablePath = string.IsNullOrWhiteSpace(executablePath)
                ? throw new ArgumentException("Executable path cannot be empty.", nameof(executablePath))
                : executablePath;
            Arguments = arguments == null
                ? throw new ArgumentNullException(nameof(arguments))
                : new ReadOnlyCollection<string>(new List<string>(arguments));
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Directory.GetCurrentDirectory()
                : workingDirectory;
            Timeout = timeout;
            StandardOutputLine = standardOutputLine;
            StandardErrorLine = standardErrorLine;
        }

        public string ExecutablePath { get; }
        public IReadOnlyList<string> Arguments { get; }
        public string WorkingDirectory { get; }
        public TimeSpan Timeout { get; }
        public Action<string> StandardOutputLine { get; }
        public Action<string> StandardErrorLine { get; }
    }

    internal sealed class MediaProcessResult
    {
        public MediaProcessResult(
            int exitCode,
            string command,
            string standardOutput,
            string standardError,
            TimeSpan elapsed)
        {
            ExitCode = exitCode;
            Command = command;
            StandardOutput = standardOutput;
            StandardError = standardError;
            Elapsed = elapsed;
        }

        public int ExitCode { get; }
        public string Command { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
        public TimeSpan Elapsed { get; }
        public bool Succeeded => ExitCode == 0;
    }

    internal interface IMediaProcessRunner
    {
        UniTask<MediaProcessResult> RunAsync(
            MediaProcessRequest request,
            CancellationToken cancellationToken);
    }

    internal sealed class MediaProcessRunner : IMediaProcessRunner
    {
        private const int MaximumLogCharacters = 512 * 1024;

        public async UniTask<MediaProcessResult> RunAsync(
            MediaProcessRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var output = new BoundedLineBuffer(MaximumLogCharacters);
            var error = new BoundedLineBuffer(MaximumLogCharacters);
            var arguments = BuildArguments(request.Arguments);
            var command = QuoteArgument(request.ExecutablePath) + " " + arguments;
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = request.ExecutablePath,
                    Arguments = arguments,
                    WorkingDirectory = request.WorkingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                process.OutputDataReceived += (_, args) => OnLine(
                    output,
                    args.Data,
                    request.StandardOutputLine);
                process.ErrorDataReceived += (_, args) => OnLine(
                    error,
                    args.Data,
                    request.StandardErrorLine);

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    process.Start();
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"无法启动媒体工具：{request.ExecutablePath}",
                        exception);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                while (process.HasExited is false)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        KillProcessTree(process);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (request.Timeout > TimeSpan.Zero && stopwatch.Elapsed >= request.Timeout)
                    {
                        KillProcessTree(process);
                        throw new TimeoutException($"媒体工具执行超时：{command}");
                    }

                    await UniTask.Yield();
                }

                process.WaitForExit();
                stopwatch.Stop();
                return new MediaProcessResult(
                    process.ExitCode,
                    command,
                    output.Read(),
                    error.Read(),
                    stopwatch.Elapsed);
            }
        }

        internal static string BuildArguments(IReadOnlyList<string> arguments)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < arguments.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(QuoteArgument(arguments[i] ?? string.Empty));
            }

            return builder.ToString();
        }

        internal static string QuoteArgument(string argument)
        {
            if (argument.Length == 0)
            {
                return "\"\"";
            }

            if (argument.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
            {
                return argument;
            }

            var builder = new StringBuilder(argument.Length + 2);
            builder.Append('"');
            var backslashes = 0;
            for (var i = 0; i < argument.Length; i++)
            {
                var character = argument[i];
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }

                builder.Append('\\', backslashes);
                backslashes = 0;
                builder.Append(character);
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }

        private static void OnLine(
            BoundedLineBuffer buffer,
            string line,
            Action<string> observer)
        {
            if (line == null)
            {
                return;
            }

            buffer.Append(line);
            try
            {
                observer?.Invoke(line);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        private static void KillProcessTree(Process process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                if (process.HasExited)
                {
                    return;
                }

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    using (var taskKill = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = $"/PID {process.Id} /T /F",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }))
                    {
                        taskKill?.WaitForExit(5000);
                    }
                }

                if (process.HasExited is false)
                {
                    process.Kill();
                }
            }
            catch (Exception)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogWarning($"无法终止媒体工具进程：{exception.Message}");
                }
            }
        }

        private sealed class BoundedLineBuffer
        {
            private readonly int m_MaximumCharacters;
            private readonly StringBuilder m_Buffer = new StringBuilder();
            private bool m_Truncated;

            public BoundedLineBuffer(int maximumCharacters)
            {
                m_MaximumCharacters = maximumCharacters;
            }

            public void Append(string line)
            {
                lock (m_Buffer)
                {
                    if (m_Buffer.Length >= m_MaximumCharacters)
                    {
                        m_Truncated = true;
                        return;
                    }

                    var remaining = m_MaximumCharacters - m_Buffer.Length;
                    m_Buffer.AppendLine(line.Length <= remaining ? line : line.Substring(0, remaining));
                    if (line.Length > remaining)
                    {
                        m_Truncated = true;
                    }
                }
            }

            public string Read()
            {
                lock (m_Buffer)
                {
                    return m_Truncated
                        ? m_Buffer + Environment.NewLine + "[日志已截断]"
                        : m_Buffer.ToString();
                }
            }
        }
    }
}
