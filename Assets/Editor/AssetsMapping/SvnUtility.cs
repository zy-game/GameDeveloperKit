using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameDeveloperKit.Editor.ArtMapping
{
    /// <summary>
    /// SVN 操作结果
    /// </summary>
    public class SvnResult
    {
        public bool success;
        public string output;
        public string error;
        public int exitCode;
    }

    /// <summary>
    /// SVN 工具类
    /// </summary>
    public static class SvnUtility
    {
        /// <summary>
        /// SVN 更新进度回调
        /// </summary>
        public static event Action<string> OnProgressUpdate;

        /// <summary>
        /// 同步执行 SVN 更新
        /// </summary>
        /// <param name="path">要更新的目录路径</param>
        /// <returns>操作结果</returns>
        public static SvnResult Update(string path)
        {
            return ExecuteSvnCommand($"update \"{path}\"");
        }

        /// <summary>
        /// 异步执行 SVN 更新
        /// </summary>
        /// <param name="path">要更新的目录路径</param>
        /// <param name="onProgress">进度回调</param>
        /// <returns>操作结果</returns>
        public static async Task<SvnResult> UpdateAsync(string path, Action<string> onProgress = null)
        {
            return await ExecuteSvnCommandAsync($"update \"{path}\"", onProgress);
        }

        /// <summary>
        /// 获取 SVN 状态
        /// </summary>
        public static SvnResult GetStatus(string path)
        {
            return ExecuteSvnCommand($"status \"{path}\"");
        }

        /// <summary>
        /// 获取 SVN 信息
        /// </summary>
        public static SvnResult GetInfo(string path)
        {
            return ExecuteSvnCommand($"info \"{path}\"");
        }

        /// <summary>
        /// 检查目录是否为 SVN 工作副本
        /// </summary>
        public static bool IsSvnWorkingCopy(string path)
        {
            var result = ExecuteSvnCommand($"info \"{path}\"");
            return result.success && !string.IsNullOrEmpty(result.output);
        }

        /// <summary>
        /// 执行 SVN 命令（同步）
        /// </summary>
        private static SvnResult ExecuteSvnCommand(string arguments)
        {
            var result = new SvnResult();

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "svn",
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                process.Start();
                result.output = process.StandardOutput.ReadToEnd();
                result.error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                result.exitCode = process.ExitCode;
                result.success = process.ExitCode == 0;

                if (!result.success && !string.IsNullOrEmpty(result.error))
                {
                    Debug.LogWarning($"[SvnUtility] SVN 命令执行警告: {result.error}");
                }
            }
            catch (Exception ex)
            {
                result.success = false;
                result.error = ex.Message;
                result.exitCode = -1;
                Debug.LogError($"[SvnUtility] SVN 命令执行失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 执行 SVN 命令（异步）
        /// </summary>
        private static async Task<SvnResult> ExecuteSvnCommandAsync(string arguments, Action<string> onProgress = null)
        {
            var result = new SvnResult();
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "svn",
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        onProgress?.Invoke(e.Data);
                        OnProgressUpdate?.Invoke(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                result.output = outputBuilder.ToString();
                result.error = errorBuilder.ToString();
                result.exitCode = process.ExitCode;
                result.success = process.ExitCode == 0;

                if (!result.success && !string.IsNullOrEmpty(result.error))
                {
                    Debug.LogWarning($"[SvnUtility] SVN 命令执行警告: {result.error}");
                }
            }
            catch (Exception ex)
            {
                result.success = false;
                result.error = ex.Message;
                result.exitCode = -1;
                Debug.LogError($"[SvnUtility] SVN 命令执行失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 检查 SVN 是否可用
        /// </summary>
        public static bool IsSvnAvailable()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "svn",
                        Arguments = "--version --quiet",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取 SVN 版本
        /// </summary>
        public static string GetSvnVersion()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "svn",
                        Arguments = "--version --quiet",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var version = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return version;
            }
            catch
            {
                return null;
            }
        }
    }
}
