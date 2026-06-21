using GameDeveloperKit.Story;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor
{
    /// <summary>
    /// Exports editor authoring assets into runtime-loadable StoryProgramAsset files.
    /// </summary>
    internal static class StoryProgramAssetExporter
    {
        public static ExportCompiledResult ExportCompiled(StoryAuthoringAsset authoring, StoryProgram program)
        {
            if (authoring == null || program == null)
            {
                return ExportCompiledResult.Skipped(null);
            }

            var sourcePath = AssetDatabase.GetAssetPath(authoring);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return ExportCompiledResult.Skipped(null);
            }

            var outputPath = ResolveOutputPath(authoring, sourcePath);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return ExportCompiledResult.CreateCanceled(null);
            }

            Export(program, outputPath);
            if (string.Equals(authoring.RuntimeProgramAssetPath, outputPath, System.StringComparison.Ordinal) is false)
            {
                authoring.RuntimeProgramAssetPath = outputPath;
                EditorUtility.SetDirty(authoring);
                AssetDatabase.SaveAssets();
            }

            return ExportCompiledResult.CreateExported(outputPath);
        }

        private static void Export(StoryProgram program, string outputPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<StoryProgramAsset>(outputPath);
            if (asset == null)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(outputPath)?.Replace('\\', '/'));
                asset = ScriptableObject.CreateInstance<StoryProgramAsset>();
                AssetDatabase.CreateAsset(asset, outputPath);
            }

            asset.SetProgram(program);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string ResolveOutputPath(StoryAuthoringAsset authoring, string sourcePath)
        {
            var previousPath = authoring.RuntimeProgramAssetPath;
            if (string.IsNullOrWhiteSpace(previousPath) is false &&
                AssetDatabase.LoadAssetAtPath<StoryProgramAsset>(previousPath) != null)
            {
                return previousPath;
            }

            var directory = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            var fileName = string.IsNullOrWhiteSpace(authoring.StoryId) ? "StoryProgram" : authoring.StoryId;
            if (string.IsNullOrWhiteSpace(previousPath) is false)
            {
                directory = System.IO.Path.GetDirectoryName(previousPath)?.Replace('\\', '/') ?? directory;
                fileName = System.IO.Path.GetFileNameWithoutExtension(previousPath);
            }

            return EditorUtility.SaveFilePanelInProject(
                "导出运行时剧情资源",
                fileName,
                "asset",
                "选择编译后的 StoryProgramAsset 保存位置。下次编译会默认覆盖这个文件。",
                directory);
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(folder);
            EnsureFolder(parent);
            if (string.IsNullOrWhiteSpace(parent) is false && string.IsNullOrWhiteSpace(name) is false && AssetDatabase.IsValidFolder(folder) is false)
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        public readonly struct ExportCompiledResult
        {
            private ExportCompiledResult(bool exported, bool canceled, string outputPath)
            {
                Exported = exported;
                Canceled = canceled;
                OutputPath = outputPath;
            }

            public bool Exported { get; }

            public bool Canceled { get; }

            public string OutputPath { get; }

            public static ExportCompiledResult CreateExported(string outputPath)
            {
                return new ExportCompiledResult(true, false, outputPath);
            }

            public static ExportCompiledResult CreateCanceled(string outputPath)
            {
                return new ExportCompiledResult(false, true, outputPath);
            }

            public static ExportCompiledResult Skipped(string outputPath)
            {
                return new ExportCompiledResult(false, false, outputPath);
            }
        }
    }
}
