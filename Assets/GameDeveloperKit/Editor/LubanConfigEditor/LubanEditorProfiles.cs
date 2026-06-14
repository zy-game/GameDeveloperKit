using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameDeveloperKit.LubanConfigEditor
{
    /// <summary>
    /// 定义 Luban Workspace Profile 类型。
    /// </summary>
    [Serializable]
    public sealed class LubanWorkspaceProfile
    {
        [SerializeField] private string m_Name = "Default";

        [SerializeField] private string m_WorkspaceRoot = "DataTables";

        [SerializeField] private string m_ConfPath = "DataTables/luban.conf";

        [SerializeField] private string m_SchemaDirectory = "DataTables/Defines";

        [SerializeField] private string m_DataDirectory = "DataTables/Datas";

        [SerializeField] private string m_DefaultTarget = "client";

        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public string WorkspaceRoot
        {
            get => m_WorkspaceRoot;
            set => m_WorkspaceRoot = value;
        }

        public string ConfPath
        {
            get => m_ConfPath;
            set => m_ConfPath = value;
        }

        public string SchemaDirectory
        {
            get => m_SchemaDirectory;
            set => m_SchemaDirectory = value;
        }

        public string DataDirectory
        {
            get => m_DataDirectory;
            set => m_DataDirectory = value;
        }

        public string DefaultTarget
        {
            get => m_DefaultTarget;
            set => m_DefaultTarget = value;
        }

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_Name))
            {
                m_Name = "Default";
            }

            if (string.IsNullOrWhiteSpace(m_WorkspaceRoot))
            {
                m_WorkspaceRoot = "DataTables";
            }

            if (string.IsNullOrWhiteSpace(m_ConfPath))
            {
                m_ConfPath = "DataTables/luban.conf";
            }

            if (string.IsNullOrWhiteSpace(m_SchemaDirectory))
            {
                m_SchemaDirectory = "DataTables/Defines";
            }

            if (string.IsNullOrWhiteSpace(m_DataDirectory))
            {
                m_DataDirectory = "DataTables/Datas";
            }

            if (string.IsNullOrWhiteSpace(m_DefaultTarget))
            {
                m_DefaultTarget = "client";
            }
        }
    }

    /// <summary>
    /// 定义 Luban Generation Profile 类型。
    /// </summary>
    [Serializable]
    public sealed class LubanGenerationProfile
    {
        [SerializeField] private string m_Name = "Client Json";

        [SerializeField] private string m_Target = "client";

        [SerializeField] private string m_CodeTarget = "cs-simple-json";

        [SerializeField] private string m_DataTarget = "json";

        [SerializeField] private string m_IncludeTag;

        [SerializeField] private string m_ExcludeTag;

        [SerializeField] private string m_Variant;

        [SerializeField] private string m_Pipeline;

        [SerializeField] private string m_Xargs;

        [SerializeField] private string m_OutputCodeDirectory = "Assets/GameDeveloperKit/Generated/Luban/Code";

        [SerializeField] private string m_OutputDataDirectory = "Assets/GameDeveloperKit/Generated/Luban/Data";

        [SerializeField] private bool m_UseCustomTemplateDir;

        [SerializeField] private string m_CustomTemplateDirectory = "DataTables/Templates";

        [SerializeField] private bool m_ValidationFailAsError = true;

        [SerializeField] private LubanTableSelection m_TableSelection;

        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public string Target
        {
            get => m_Target;
            set => m_Target = value;
        }

        public string CodeTarget
        {
            get => m_CodeTarget;
            set => m_CodeTarget = value;
        }

        public string DataTarget
        {
            get => m_DataTarget;
            set => m_DataTarget = value;
        }

        public string IncludeTag
        {
            get => m_IncludeTag;
            set => m_IncludeTag = value;
        }

        public string ExcludeTag
        {
            get => m_ExcludeTag;
            set => m_ExcludeTag = value;
        }

        public string Variant
        {
            get => m_Variant;
            set => m_Variant = value;
        }

        public string Pipeline
        {
            get => m_Pipeline;
            set => m_Pipeline = value;
        }

        public string Xargs
        {
            get => m_Xargs;
            set => m_Xargs = value;
        }

        public string OutputCodeDirectory
        {
            get => m_OutputCodeDirectory;
            set => m_OutputCodeDirectory = value;
        }

        public string OutputDataDirectory
        {
            get => m_OutputDataDirectory;
            set => m_OutputDataDirectory = value;
        }

        public bool UseCustomTemplateDir
        {
            get => m_UseCustomTemplateDir;
            set => m_UseCustomTemplateDir = value;
        }

        public string CustomTemplateDirectory
        {
            get => m_CustomTemplateDirectory;
            set => m_CustomTemplateDirectory = value;
        }

        public bool ValidationFailAsError
        {
            get => m_ValidationFailAsError;
            set => m_ValidationFailAsError = value;
        }

        public LubanTableSelection TableSelection => m_TableSelection;

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_Name))
            {
                m_Name = "Client Json";
            }

            if (string.IsNullOrWhiteSpace(m_Target))
            {
                m_Target = "client";
            }

            if (string.IsNullOrWhiteSpace(m_CodeTarget))
            {
                m_CodeTarget = "cs-simple-json";
            }

            if (string.IsNullOrWhiteSpace(m_DataTarget))
            {
                m_DataTarget = "json";
            }

            if (string.IsNullOrWhiteSpace(m_OutputCodeDirectory))
            {
                m_OutputCodeDirectory = "Assets/GameDeveloperKit/Generated/Luban/Code";
            }

            if (string.IsNullOrWhiteSpace(m_OutputDataDirectory))
            {
                m_OutputDataDirectory = "Assets/GameDeveloperKit/Generated/Luban/Data";
            }

            if (string.IsNullOrWhiteSpace(m_CustomTemplateDirectory))
            {
                m_CustomTemplateDirectory = "DataTables/Templates";
            }

            m_TableSelection ??= new LubanTableSelection();
            m_TableSelection.EnsureDefaults();
        }
    }

    /// <summary>
    /// 定义 Luban Table Scope 类型。
    /// </summary>
    public enum LubanTableScope
    {
        AllTables,
        SelectedTables
    }

    /// <summary>
    /// 定义 Luban Table Selection 类型。
    /// </summary>
    [Serializable]
    public sealed class LubanTableSelection
    {
        [SerializeField] private LubanTableScope m_Scope = LubanTableScope.AllTables;

        [SerializeField] private List<string> m_SelectedTableNames;

        public LubanTableScope Scope
        {
            get => m_Scope;
            set => m_Scope = value;
        }

        /// <summary>
        /// 存储 Selected Table Names。
        /// </summary>
        public List<string> SelectedTableNames => m_SelectedTableNames;

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public void EnsureDefaults()
        {
            m_SelectedTableNames ??= new List<string>();
            for (var i = m_SelectedTableNames.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(m_SelectedTableNames[i]))
                {
                    m_SelectedTableNames.RemoveAt(i);
                }
                else
                {
                    m_SelectedTableNames[i] = m_SelectedTableNames[i].Trim();
                }
            }

            var uniqueNames = m_SelectedTableNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            m_SelectedTableNames.Clear();
            m_SelectedTableNames.AddRange(uniqueNames);
        }

        /// <summary>
        /// 设置 Selected。
        /// </summary>
        /// <param name="tableName">table Name 参数。</param>
        /// <param name="selected">selected 参数。</param>
        public void SetSelected(string tableName, bool selected)
        {
            EnsureDefaults();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return;
            }

            var existing = m_SelectedTableNames.FirstOrDefault(x => string.Equals(x, tableName, StringComparison.OrdinalIgnoreCase));
            if (selected)
            {
                if (existing == null)
                {
                    m_SelectedTableNames.Add(tableName.Trim());
                }

                return;
            }

            if (existing != null)
            {
                m_SelectedTableNames.Remove(existing);
            }
        }
    }
}
