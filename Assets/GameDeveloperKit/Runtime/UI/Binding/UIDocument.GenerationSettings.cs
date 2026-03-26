using System;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public partial class UIDocument
    {
        /// <summary>
        /// UI 文档代码生成设置。
        /// </summary>
        [Serializable]
        public sealed class GenerationSettings
        {
            [SerializeField] private string outputDirectoryPath = "Assets";
            [SerializeField] private string windowClassName;
            [SerializeField] private string windowNamespace;

            /// <summary>
            /// 获取或设置生成代码的输出目录路径。
            /// </summary>
            public string OutputDirectoryPath
            {
                get => outputDirectoryPath;
                set => outputDirectoryPath = value;
            }

            /// <summary>
            /// 获取或设置窗口类名。
            /// </summary>
            public string WindowClassName
            {
                get => windowClassName;
                set => windowClassName = value;
            }

            /// <summary>
            /// 获取或设置窗口命名空间。
            /// </summary>
            public string WindowNamespace
            {
                get => windowNamespace;
                set => windowNamespace = value;
            }
        }
    }
}
