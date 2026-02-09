using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameDeveloperKit.Editor.ArtMapping
{
    /// <summary>
    /// 符号链接状态
    /// </summary>
    public enum SymlinkStatus
    {
        NotLinked,
        Linked,
        Error,
        SourceNotFound,
        TargetExists
    }

    /// <summary>
    /// 符号链接工具类
    /// </summary>
    public static class SymlinkUtility
    {
        /// <summary>
        /// 创建符号链接
        /// </summary>
        /// <param name="linkPath">链接路径（目标位置）</param>
        /// <param name="targetPath">目标路径（源目录）</param>
        /// <returns>是否成功</returns>
        public static bool CreateSymlink(string linkPath, string targetPath)
        {
            if (string.IsNullOrEmpty(linkPath) || string.IsNullOrEmpty(targetPath))
            {
                Debug.LogError("[SymlinkUtility] 路径不能为空");
                return false;
            }

            // 规范化路径
            linkPath = Path.GetFullPath(linkPath);
            targetPath = Path.GetFullPath(targetPath);

            // 检查源目录是否存在
            if (!Directory.Exists(targetPath))
            {
                Debug.LogError($"[SymlinkUtility] 源目录不存在: {targetPath}");
                return false;
            }

            // 检查目标位置是否已存在
            if (Directory.Exists(linkPath) || File.Exists(linkPath))
            {
                // 检查是否已经是符号链接
                if (IsSymlink(linkPath))
                {
                    Debug.LogWarning($"[SymlinkUtility] 符号链接已存在: {linkPath}");
                    return true;
                }
                Debug.LogError($"[SymlinkUtility] 目标位置已存在且不是符号链接: {linkPath}");
                return false;
            }

            // 确保父目录存在
            var parentDir = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CreateSymlinkWindows(linkPath, targetPath);
            }
            else
            {
                return CreateSymlinkUnix(linkPath, targetPath);
            }
        }

        /// <summary>
        /// Windows 平台创建符号链接
        /// </summary>
        private static bool CreateSymlinkWindows(string linkPath, string targetPath)
        {
            try
            {
                // 使用 mklink /D 创建目录符号链接
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c mklink /D \"{linkPath}\" \"{targetPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Debug.Log($"[SymlinkUtility] 符号链接创建成功: {linkPath} -> {targetPath}");
                    return true;
                }
                else
                {
                    Debug.LogError($"[SymlinkUtility] 创建符号链接失败: {error}\n{output}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SymlinkUtility] 创建符号链接异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unix/macOS 平台创建符号链接
        /// </summary>
        private static bool CreateSymlinkUnix(string linkPath, string targetPath)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/ln",
                        Arguments = $"-s \"{targetPath}\" \"{linkPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Debug.Log($"[SymlinkUtility] 符号链接创建成功: {linkPath} -> {targetPath}");
                    return true;
                }
                else
                {
                    Debug.LogError($"[SymlinkUtility] 创建符号链接失败: {error}\n{output}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SymlinkUtility] 创建符号链接异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除符号链接
        /// </summary>
        public static bool DeleteSymlink(string linkPath)
        {
            if (string.IsNullOrEmpty(linkPath))
                return false;

            linkPath = Path.GetFullPath(linkPath);

            if (!IsSymlink(linkPath))
            {
                Debug.LogWarning($"[SymlinkUtility] 路径不是符号链接: {linkPath}");
                return false;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows 上删除目录符号链接使用 rmdir
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c rmdir \"{linkPath}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Debug.Log($"[SymlinkUtility] 符号链接已删除: {linkPath}");
                        return true;
                    }
                }
                else
                {
                    // Unix/macOS 使用 rm
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "/bin/rm",
                            Arguments = $"\"{linkPath}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Debug.Log($"[SymlinkUtility] 符号链接已删除: {linkPath}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SymlinkUtility] 删除符号链接异常: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 检查路径是否为符号链接
        /// </summary>
        public static bool IsSymlink(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                path = Path.GetFullPath(path);
                var fileInfo = new FileInfo(path);
                return fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取符号链接状态
        /// </summary>
        public static SymlinkStatus GetSymlinkStatus(string sourcePath, string linkPath)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(linkPath))
                return SymlinkStatus.Error;

            try
            {
                sourcePath = Path.GetFullPath(sourcePath);
                linkPath = Path.GetFullPath(linkPath);

                // 检查源目录
                if (!Directory.Exists(sourcePath))
                    return SymlinkStatus.SourceNotFound;

                // 检查链接位置
                if (!Directory.Exists(linkPath) && !File.Exists(linkPath))
                    return SymlinkStatus.NotLinked;

                // 检查是否为符号链接
                if (IsSymlink(linkPath))
                    return SymlinkStatus.Linked;

                // 目标位置存在但不是符号链接
                return SymlinkStatus.TargetExists;
            }
            catch
            {
                return SymlinkStatus.Error;
            }
        }

        /// <summary>
        /// 获取符号链接的目标路径
        /// </summary>
        public static string GetSymlinkTarget(string linkPath)
        {
            if (string.IsNullOrEmpty(linkPath) || !IsSymlink(linkPath))
                return null;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows 使用 dir 命令获取链接目标
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c dir \"{Path.GetDirectoryName(linkPath)}\" | findstr \"{Path.GetFileName(linkPath)}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // 解析输出获取目标路径
                    var startIndex = output.IndexOf('[');
                    var endIndex = output.IndexOf(']');
                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        return output.Substring(startIndex + 1, endIndex - startIndex - 1);
                    }
                }
                else
                {
                    // Unix/macOS 使用 readlink
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "/usr/bin/readlink",
                            Arguments = $"\"{linkPath}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                        return output;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SymlinkUtility] 获取符号链接目标失败: {ex.Message}");
            }

            return null;
        }
    }
}
