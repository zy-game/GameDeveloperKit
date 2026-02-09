using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class CLICommand
    {
        public string id;
        public string command;
        public Dictionary<string, object> arguments;
        public string status;
        public long created_at;
        public string result;
        public string error;
    }

    [InitializeOnLoad]
    public static class CLICommandExecutor
    {
        private static readonly string CommandsDirectory;
        private static readonly Dictionary<string, ICLIHandler> Handlers = new();
        private static double _lastScanTime;
        private const double ScanInterval = 0.5;

        static CLICommandExecutor()
        {
            CommandsDirectory = Path.Combine(Application.dataPath, "..", "Library", "CLI", "commands");
            
            if (!Directory.Exists(CommandsDirectory))
            {
                Directory.CreateDirectory(CommandsDirectory);
            }

            RegisterHandlers();
            EditorApplication.update += ScanCommands;
            
            Debug.Log($"[CLI] Command executor initialized. Watching: {CommandsDirectory}");
        }

        private static void RegisterHandlers()
        {
            Handlers.Clear();
            
            RegisterHandler(new SceneHandler());
            RegisterHandler(new PrefabHandler());
            RegisterHandler(new ScriptableObjectHandler());
            RegisterHandler(new GameObjectHandler());
            RegisterHandler(new MaterialHandler());
            RegisterHandler(new TextureHandler());
            RegisterHandler(new AnimationHandler());
            RegisterHandler(new AudioHandler());
            RegisterHandler(new AssetHandler());
            RegisterHandler(new ConsoleHandler());
            RegisterHandler(new EditorHandler());
        }

        private static void RegisterHandler(ICLIHandler handler)
        {
            foreach (var cmd in handler.GetCommands())
            {
                Handlers[cmd] = handler;
            }
        }

        private static void ScanCommands()
        {
            if (EditorApplication.timeSinceStartup - _lastScanTime < ScanInterval)
                return;

            _lastScanTime = EditorApplication.timeSinceStartup;

            if (!Directory.Exists(CommandsDirectory))
                return;

            var files = Directory.GetFiles(CommandsDirectory, "*.json");
            foreach (var file in files)
            {
                ProcessCommandFile(file);
            }
        }

        private static void ProcessCommandFile(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var cmd = JsonConvert.DeserializeObject<CLICommand>(json);

                if (cmd == null || cmd.status != "pending")
                    return;

                Debug.Log($"[CLI] Processing command: {cmd.command} (id: {cmd.id})");

                string result = null;
                string error = null;
                string status = "completed";

                try
                {
                    if (Handlers.TryGetValue(cmd.command, out var handler))
                    {
                        var argsJson = cmd.arguments != null 
                            ? JsonConvert.SerializeObject(cmd.arguments) 
                            : "{}";
                        result = handler.Execute(cmd.command, argsJson);
                    }
                    else
                    {
                        error = $"Unknown command: {cmd.command}";
                        status = "failed";
                    }
                }
                catch (Exception e)
                {
                    error = e.Message;
                    status = "failed";
                    Debug.LogError($"[CLI] Command failed: {e.Message}");
                }

                cmd.status = status;
                cmd.result = result;
                cmd.error = error;

                var updatedJson = JsonConvert.SerializeObject(cmd, Formatting.Indented);
                File.WriteAllText(filePath, updatedJson);

                Debug.Log($"[CLI] Command {cmd.id} {status}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CLI] Error processing command file: {e.Message}");
            }
        }

        public static List<string> GetAvailableCommands()
        {
            return Handlers.Keys.ToList();
        }
    }
}
