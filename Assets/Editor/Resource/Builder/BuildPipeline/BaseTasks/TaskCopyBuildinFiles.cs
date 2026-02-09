using System.IO;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 任务：拷贝内置文件到 StreamingAssets
    /// </summary>
    public class TaskCopyBuildinFiles : IBuildTask
    {
        public string TaskName => "Copy Buildin Files";

        public TaskResult Run(BuildContext context)
        {
            var package = context.PackageSettings;

            // 检查是否需要拷贝
            if (package.packageType != PackageType.BasePackage)
            {
                context.Log($"跳过拷贝到 StreamingAssets（包类型: {package.packageType}）");
                return TaskResult.Succeed();
            }

            context.Log("开始拷贝文件到 StreamingAssets...");

            try
            {
                var streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles", context.PackageName);

                // 确保目标目录存在
                if (!Directory.Exists(streamingAssetsPath))
                {
                    Directory.CreateDirectory(streamingAssetsPath);
                }

                int copiedCount = 0;

                // 拷贝所有 Bundle 文件
                foreach (var bundleName in context.BuiltBundles)
                {
                    var sourcePath = Path.Combine(context.OutputPath, bundleName);
                    var destPath = Path.Combine(streamingAssetsPath, bundleName);

                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, destPath, true);
                        copiedCount++;
                    }
                }

                // 拷贝清单文件（文件名全小写）
                var manifestFileName = $"{context.PackageName.ToLower()}.json";
                var manifestSource = Path.Combine(context.OutputPath, manifestFileName);
                var manifestDest = Path.Combine(streamingAssetsPath, manifestFileName);
                if (File.Exists(manifestSource))
                {
                    File.Copy(manifestSource, manifestDest, true);
                    copiedCount++;
                }

                context.Log($"已拷贝 {copiedCount} 个文件到 StreamingAssets");

                // 刷新 AssetDatabase
                UnityEditor.AssetDatabase.Refresh();

                return TaskResult.Succeed();
            }
            catch (System.Exception ex)
            {
                return TaskResult.Failed($"拷贝文件到 StreamingAssets 时发生错误: {ex.Message}");
            }
        }
    }
}
