using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class ConsoleLogEntry
    {
        public string message;
        public string stackTrace;
        public string type;
        public int instanceId;
    }

    [Serializable]
    public class ConsoleLogsResult
    {
        public List<ConsoleLogEntry> logs = new();
        public int totalCount;
    }

    [Serializable]
    public class ConsoleOperationResult
    {
        public bool success;
        public string message;
    }

    public class ConsoleHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_get_console_logs",
                "unity_clear_console",
                "unity_log"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_get_console_logs" => GetConsoleLogs(args),
                "unity_clear_console" => ClearConsole(),
                "unity_log" => WriteLog(args),
                _ => JsonConvert.SerializeObject(new ConsoleOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string GetConsoleLogs(JObject args)
        {
            var typeFilter = args["type"]?.ToString();
            var count = args["count"]?.ToObject<int>() ?? 50;
            if (count <= 0) count = 50;

            var result = new ConsoleLogsResult();

            try
            {
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (logEntriesType == null)
                {
                    return JsonConvert.SerializeObject(new ConsoleOperationResult { success = false, message = "Cannot access LogEntries type" });
                }

                var getCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
                var startMethod = logEntriesType.GetMethod("StartGettingEntries", BindingFlags.Static | BindingFlags.Public);
                var endMethod = logEntriesType.GetMethod("EndGettingEntries", BindingFlags.Static | BindingFlags.Public);
                var getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);

                if (getCountMethod == null || startMethod == null || endMethod == null)
                {
                    return JsonConvert.SerializeObject(new ConsoleOperationResult { success = false, message = "Cannot access LogEntries methods" });
                }

                var totalCount = (int)getCountMethod.Invoke(null, null);
                result.totalCount = totalCount;

                startMethod.Invoke(null, null);

                try
                {
                    var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor");
                    if (logEntryType == null)
                    {
                        endMethod.Invoke(null, null);
                        return JsonConvert.SerializeObject(new ConsoleOperationResult { success = false, message = "Cannot access LogEntry type" });
                    }

                    var entry = Activator.CreateInstance(logEntryType);
                    var messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public);
                    var modeField = logEntryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public);
                    var instanceIdField = logEntryType.GetField("instanceID", BindingFlags.Instance | BindingFlags.Public);

                    var startIndex = Math.Max(0, totalCount - count);
                    for (int i = startIndex; i < totalCount; i++)
                    {
                        if (getEntryMethod != null)
                        {
                            getEntryMethod.Invoke(null, new object[] { i, entry });

                            var message = messageField?.GetValue(entry) as string ?? "";
                            var mode = modeField?.GetValue(entry);
                            var instanceId = instanceIdField?.GetValue(entry) is int id ? id : 0;

                            var logType = GetLogTypeFromMode(Convert.ToInt32(mode));

                            if (!string.IsNullOrEmpty(typeFilter) && !logType.Equals(typeFilter, StringComparison.OrdinalIgnoreCase))
                                continue;

                            var parts = message.Split('\n');
                            var logMessage = parts.Length > 0 ? parts[0] : message;
                            var stackTrace = parts.Length > 1 ? string.Join("\n", parts, 1, parts.Length - 1) : "";

                            result.logs.Add(new ConsoleLogEntry
                            {
                                message = logMessage,
                                stackTrace = stackTrace,
                                type = logType,
                                instanceId = instanceId
                            });
                        }
                    }
                }
                finally
                {
                    endMethod.Invoke(null, null);
                }
            }
            catch (Exception e)
            {
                return JsonConvert.SerializeObject(new ConsoleOperationResult { success = false, message = $"Failed to get logs: {e.Message}" });
            }

            return JsonConvert.SerializeObject(result);
        }

        private string GetLogTypeFromMode(int mode)
        {
            const int kScriptCompileError = 1 << 11;
            const int kScriptCompileWarning = 1 << 12;
            
            if ((mode & kScriptCompileError) != 0) return "Error";
            if ((mode & kScriptCompileWarning) != 0) return "Warning";
            
            const int kError = 1 << 0;
            const int kAssert = 1 << 1;
            const int kWarning = 1 << 2;
            const int kException = 1 << 4;
            
            if ((mode & kError) != 0) return "Error";
            if ((mode & kAssert) != 0) return "Assert";
            if ((mode & kException) != 0) return "Exception";
            if ((mode & kWarning) != 0) return "Warning";
            return "Log";
        }

        private string ClearConsole()
        {
            try
            {
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                var clearMethod = logEntriesType?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
                clearMethod?.Invoke(null, null);

                return JsonConvert.SerializeObject(new ConsoleOperationResult { success = true, message = "Console cleared" });
            }
            catch (Exception e)
            {
                return JsonConvert.SerializeObject(new ConsoleOperationResult { success = false, message = $"Failed to clear console: {e.Message}" });
            }
        }

        private string WriteLog(JObject args)
        {
            var message = args["message"]?.ToString();
            var type = args["type"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(message))
            {
                return JsonConvert.SerializeObject(new ConsoleOperationResult { success = false, message = "message is required" });
            }

            var logType = type switch
            {
                "warning" => LogType.Warning,
                "error" => LogType.Error,
                _ => LogType.Log
            };

            Debug.unityLogger.Log(logType, $"[CLI] {message}");

            return JsonConvert.SerializeObject(new ConsoleOperationResult { success = true, message = $"Logged as {logType}" });
        }
    }
}
