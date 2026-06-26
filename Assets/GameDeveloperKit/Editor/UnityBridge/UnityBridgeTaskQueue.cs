using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using SysIO = System.IO;

namespace GameDeveloperKit.UnityBridge
{
    [InitializeOnLoad]
    public static class UnityBridgeTaskQueue
    {
        private static string PendingDir => SysIO.Path.Combine(Application.dataPath, "..", "Temp", "UnityBridge", "pending");
        private static string ResultsDir => SysIO.Path.Combine(Application.dataPath, "..", "Temp", "UnityBridge", "results");

        private static volatile bool s_Running;
        private static readonly List<TaskLogEntry> s_TaskLog = new List<TaskLogEntry>();
        private const int MaxTaskLogEntries = 50;
        private static double s_LastPollTime;

        public static bool IsRunning => s_Running;
        public static IReadOnlyList<TaskLogEntry> TaskLog => s_TaskLog;
        public static int PendingCount { get; private set; }

        static UnityBridgeTaskQueue()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;

            var settings = UnityBridgeSettings.LoadOrCreate();
            if (settings.AutoStart)
                Start();
        }

        private static void OnBeforeAssemblyReload()
        {
            Stop();
        }

        public static void Start()
        {
            if (s_Running) return;
            SysIO.Directory.CreateDirectory(PendingDir);
            SysIO.Directory.CreateDirectory(ResultsDir);
            s_Running = true;
            UnityBridgeConsoleCapture.StartCapture();
            Debug.Log("[UnityBridge] Task queue started. Dir: Temp/UnityBridge/");
        }

        public static void Stop()
        {
            s_Running = false;
        }

        private static void Poll()
        {
            if (!s_Running) return;

            // Throttle to ~10 polls/sec to avoid excessive disk I/O
            var now = EditorApplication.timeSinceStartup;
            if (now - s_LastPollTime < 0.1) return;
            s_LastPollTime = now;

            if (EditorApplication.isCompiling) return;

            try
            {
                ProcessPendingTasks();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityBridge] Task poll error: {ex.Message}");
            }
        }

        private static void ProcessPendingTasks()
        {
            if (!SysIO.Directory.Exists(PendingDir)) return;

            var files = SysIO.Directory.GetFiles(PendingDir, "*.json");
            PendingCount = files.Length;
            if (files.Length == 0) return;

            foreach (var file in files)
            {
                if (!s_Running) break;

                try
                {
                    ProcessTaskFile(file);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UnityBridge] Failed to process task {SysIO.Path.GetFileName(file)}: {ex.Message}");
                    // Move to failed status so CLI gets feedback
                    var taskId = SysIO.Path.GetFileNameWithoutExtension(file);
                    WriteError(taskId, $"Task processing failed: {ex.Message}");
                    DeleteFileSafe(file);
                }
            }
        }

        private static void ProcessTaskFile(string filePath)
        {
            var json = SysIO.File.ReadAllText(filePath);
            var task = JsonUtility.FromJson<TaskFile>(json);
            if (task == null || string.IsNullOrWhiteSpace(task.taskId))
            {
                DeleteFileSafe(filePath);
                return;
            }

            var skillRequest = new UnityBridgeSkillRequest
            {
                Method = task.method ?? "GET",
                Path = task.path ?? "",
                Body = task.body
            };

            UnityBridgeSkillResponse response = null;
            foreach (var skill in UnityBridgeSkillRegistry.Skills)
            {
                if (skill.CanExecute(skillRequest))
                {
                    response = skill.Execute(skillRequest);
                    break;
                }
            }

            if (response == null)
            {
                response = UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{task.path?.Trim('/')}");
            }

            // Write result
            var result = new TaskResult
            {
                taskId = task.taskId,
                completedAt = DateTime.UtcNow.ToString("O"),
                success = response.StatusCode == 200,
                statusCode = response.StatusCode,
                data = response.Json,
                error = response.StatusCode != 200 ? response.Json : null
            };
            var resultJson = JsonUtility.ToJson(result);

            var resultPath = SysIO.Path.Combine(ResultsDir, task.taskId + ".json");
            var tmpPath = resultPath + ".tmp";
            SysIO.File.WriteAllText(tmpPath, resultJson);
            try { SysIO.File.Move(tmpPath, resultPath); } catch { SysIO.File.Copy(tmpPath, resultPath, true); }

            // Log
            lock (s_TaskLog)
            {
                s_TaskLog.Add(new TaskLogEntry
                {
                    Time = DateTime.Now,
                    TaskId = task.taskId,
                    Path = task.path ?? "",
                    Method = task.method ?? "GET",
                    StatusCode = response.StatusCode
                });
                if (s_TaskLog.Count > MaxTaskLogEntries)
                    s_TaskLog.RemoveRange(0, s_TaskLog.Count - MaxTaskLogEntries);
            }

            // Delete processed pending file
            DeleteFileSafe(filePath);
        }

        private static void WriteError(string taskId, string errorMessage)
        {
            var result = new TaskResult
            {
                taskId = taskId,
                completedAt = DateTime.UtcNow.ToString("O"),
                success = false,
                statusCode = 500,
                data = null,
                error = errorMessage
            };
            var json = JsonUtility.ToJson(result);
            var resultPath = SysIO.Path.Combine(ResultsDir, taskId + ".json");
            var tmpPath = resultPath + ".tmp";
            try
            {
                SysIO.File.WriteAllText(tmpPath, json);
                SysIO.File.Move(tmpPath, resultPath);
            }
            catch { }
        }

        private static void DeleteFileSafe(string path)
        {
            try { SysIO.File.Delete(path); } catch { }
        }

        public static void CleanupOldTasks()
        {
            CleanupDir(PendingDir, TimeSpan.FromMinutes(10));
            CleanupDir(ResultsDir, TimeSpan.FromHours(1));
        }

        private static void CleanupDir(string dir, TimeSpan maxAge)
        {
            if (!SysIO.Directory.Exists(dir)) return;
            foreach (var f in SysIO.Directory.GetFiles(dir))
            {
                try
                {
                    var info = new SysIO.FileInfo(f);
                    if (DateTime.Now - info.LastWriteTime > maxAge)
                        SysIO.File.Delete(f);
                }
                catch { }
            }
        }

        [Serializable]
        private class TaskFile
        {
            public string taskId;
            public string createdAt;
            public string method;
            public string path;
            public string body;
        }

        [Serializable]
        private class TaskResult
        {
            public string taskId;
            public string completedAt;
            public bool success;
            public int statusCode;
            public string data;
            public string error;
        }

        public struct TaskLogEntry
        {
            public DateTime Time;
            public string TaskId;
            public string Method;
            public string Path;
            public int StatusCode;
        }
    }
}
