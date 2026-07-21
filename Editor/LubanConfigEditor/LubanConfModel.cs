using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.LubanConfigEditor
{
    /// <summary>
    /// 定义 Luban Conf Model 类型。
    /// </summary>
    public sealed class LubanConfModel
    {
        private JObject m_Root;

        private readonly List<string> m_SchemaFiles = new List<string>();

        private readonly List<string> m_Targets = new List<string>();

        public string ConfPath { get; private set; }

        public string WorkspaceRoot { get; private set; }

        public string DataDirectory { get; private set; }

        /// <summary>
        /// 存储 Schema Files。
        /// </summary>
        public IReadOnlyList<string> SchemaFiles => m_SchemaFiles;

        /// <summary>
        /// 存储 Targets。
        /// </summary>
        public IReadOnlyList<string> Targets => m_Targets;

        /// <summary>
        /// 获取 Target Top Module。
        /// </summary>
        /// <param name="targetName">target Name 参数。</param>
        /// <returns>执行结果。</returns>
        public string GetTargetTopModule(string targetName)
        {
            return FindTarget(targetName)?.Value<string>("topModule") ?? "cfg";
        }

        /// <summary>
        /// 获取 Target Manager。
        /// </summary>
        /// <param name="targetName">target Name 参数。</param>
        /// <returns>执行结果。</returns>
        public string GetTargetManager(string targetName)
        {
            return FindTarget(targetName)?.Value<string>("manager") ?? "Tables";
        }

        /// <summary>
        /// Ensures the configured target uses the requested top module.
        /// </summary>
        /// <param name="targetName">Target name.</param>
        /// <param name="topModule">Top module namespace.</param>
        /// <returns>Whether the configuration changed.</returns>
        public bool EnsureTargetTopModule(string targetName, string topModule)
        {
            var target = FindExactTarget(targetName);
            if (target == null || string.IsNullOrWhiteSpace(topModule))
            {
                return false;
            }

            var normalizedTopModule = topModule.Trim();
            if (string.Equals(
                    target.Value<string>("topModule"),
                    normalizedTopModule,
                    System.StringComparison.Ordinal))
            {
                return false;
            }

            target["topModule"] = normalizedTopModule;
            return true;
        }

        /// <summary>
        /// 初始化 Default。
        /// </summary>
        /// <param name="workspaceRoot">workspace Root 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanConfModel InitializeDefault(string workspaceRoot)
        {
            var absoluteWorkspaceRoot = IOPath.GetFullPath(workspaceRoot);
            IODirectory.CreateDirectory(absoluteWorkspaceRoot);
            IODirectory.CreateDirectory(IOPath.Combine(absoluteWorkspaceRoot, "Defines"));
            IODirectory.CreateDirectory(IOPath.Combine(absoluteWorkspaceRoot, "Datas"));

            var confPath = IOPath.Combine(absoluteWorkspaceRoot, "luban.conf");
            if (IOFile.Exists(confPath) is false)
            {
                var model = FromRoot(confPath, CreateDefaultRoot());
                model.Save();
            }

            return Load(confPath);
        }

        /// <summary>
        /// 加载。
        /// </summary>
        /// <param name="confPath">conf Path 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanConfModel Load(string confPath)
        {
            var absoluteConfPath = IOPath.GetFullPath(confPath);
            var text = IOFile.ReadAllText(absoluteConfPath, Encoding.UTF8);
            return FromRoot(absoluteConfPath, JObject.Parse(text));
        }

        /// <summary>
        /// 保存。
        /// </summary>
        public void Save()
        {
            IODirectory.CreateDirectory(WorkspaceRoot);
            IOFile.WriteAllText(ConfPath, m_Root.ToString(Formatting.Indented), new UTF8Encoding(false));
        }

        /// <summary>
        /// 执行 From Root。
        /// </summary>
        /// <param name="confPath">conf Path 参数。</param>
        /// <param name="root">root 参数。</param>
        /// <returns>执行结果。</returns>
        private static LubanConfModel FromRoot(string confPath, JObject root)
        {
            var model = new LubanConfModel
            {
                ConfPath = IOPath.GetFullPath(confPath),
                WorkspaceRoot = IOPath.GetDirectoryName(IOPath.GetFullPath(confPath)) ?? ".",
                m_Root = root
            };
            model.ReadKnownFields();
            return model;
        }

        /// <summary>
        /// 读取 Known Fields。
        /// </summary>
        private void ReadKnownFields()
        {
            DataDirectory = m_Root.Value<string>("dataDir") ?? "Datas";
            m_SchemaFiles.Clear();
            var schemaFiles = m_Root["schemaFiles"] as JArray;
            if (schemaFiles != null)
            {
                foreach (var schemaFile in schemaFiles)
                {
                    var fileName = schemaFile.Value<string>("fileName");
                    if (string.IsNullOrWhiteSpace(fileName) is false)
                    {
                        m_SchemaFiles.Add(fileName);
                    }
                }
            }

            m_Targets.Clear();
            var targets = m_Root["targets"] as JArray;
            if (targets != null)
            {
                foreach (var target in targets)
                {
                    var targetName = target.Value<string>("name");
                    if (string.IsNullOrWhiteSpace(targetName) is false)
                    {
                        m_Targets.Add(targetName);
                    }
                }
            }
        }

        /// <summary>
        /// 查找 Target。
        /// </summary>
        /// <param name="targetName">target Name 参数。</param>
        /// <returns>执行结果。</returns>
        private JObject FindTarget(string targetName)
        {
            return FindExactTarget(targetName) ??
                   (m_Root["targets"] as JArray)?.OfType<JObject>().FirstOrDefault();
        }

        private JObject FindExactTarget(string targetName)
        {
            var targets = m_Root["targets"] as JArray;
            if (targets == null)
            {
                return null;
            }

            foreach (var target in targets.OfType<JObject>())
            {
                var name = target.Value<string>("name");
                if (string.Equals(name, targetName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return target;
                }
            }

            return null;
        }

        /// <summary>
        /// 创建 Default Root。
        /// </summary>
        /// <returns>执行结果。</returns>
        private static JObject CreateDefaultRoot()
        {
            return new JObject
            {
                ["groups"] = new JArray
                {
                    new JObject
                    {
                        ["names"] = new JArray { "c" },
                        ["default"] = true
                    },
                    new JObject
                    {
                        ["names"] = new JArray { "s" },
                        ["default"] = true
                    },
                    new JObject
                    {
                        ["names"] = new JArray { "e" },
                        ["default"] = true
                    }
                },
                ["schemaFiles"] = new JArray
                {
                    new JObject
                    {
                        ["fileName"] = "Defines",
                        ["type"] = string.Empty
                    }
                },
                ["dataDir"] = "Datas",
                ["targets"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "server",
                        ["manager"] = "Tables",
                        ["groups"] = new JArray { "s" },
                        ["topModule"] = "cfg"
                    },
                    new JObject
                    {
                        ["name"] = "client",
                        ["manager"] = "Tables",
                        ["groups"] = new JArray { "c" },
                        ["topModule"] = "cfg"
                    },
                    new JObject
                    {
                        ["name"] = "all",
                        ["manager"] = "Tables",
                        ["groups"] = new JArray { "c,s,e" },
                        ["topModule"] = "cfg"
                    }
                }
            };
        }
    }
}
