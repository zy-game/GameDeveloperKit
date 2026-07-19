using System;
using System.IO;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Publishing;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Compiler
{
    /// <summary>
    /// Exports editor authoring assets into runtime-loadable ProgramAsset files.
    /// </summary>
    internal static class ProgramAssetExporter
    {
        public static ExportCompiledResult ExportCompiled(AuthoringAsset authoring, Program program)
        {
            return ExportCompiled(authoring, program, ConfirmBreakingChanges);
        }

        internal static ExportCompiledResult ExportCompiled(
            AuthoringAsset authoring,
            Program program,
            Func<IdentityChangeReport, bool> confirmBreakingChanges)
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

            if (!authoring.TryGetPublishedIdentity(out var baseline, out var baselineError) &&
                !string.IsNullOrWhiteSpace(baselineError))
            {
                throw new GameException(
                    $"Story published identity baseline is invalid. story:{authoring.StoryId} reason:{baselineError}");
            }

            var manifest = IdentityManifest.Create(program);
            var changes = IdentityChangeReport.Compare(baseline, manifest);
            if (changes.HasBreakingChanges &&
                (confirmBreakingChanges == null || !confirmBreakingChanges(changes)))
            {
                return ExportCompiledResult.CreateCanceled(null, changes);
            }

            var outputPath = ResolveOutputPath(authoring, sourcePath);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return ExportCompiledResult.CreateCanceled(null, changes);
            }

            var manifestPath = IdentityManifestPath(outputPath);
            var changeReportPath = IdentityChangeReportPath(outputPath);
            var previousProgramPath = authoring.RuntimeProgramAssetPath;
            var manifestBackup = TextFileBackup.Capture(manifestPath);
            var reportBackup = TextFileBackup.Capture(changeReportPath);
            var programAsset = AssetDatabase.LoadAssetAtPath<ProgramAsset>(outputPath);
            var previousProgramAssetJson = programAsset == null
                ? null
                : EditorJsonUtility.ToJson(programAsset);
            var createdProgramAsset = false;
            try
            {
                EnsureFolder(Path.GetDirectoryName(outputPath)?.Replace('\\', '/'));
                if (programAsset == null)
                {
                    programAsset = ScriptableObject.CreateInstance<ProgramAsset>();
                    AssetDatabase.CreateAsset(programAsset, outputPath);
                    createdProgramAsset = true;
                }

                programAsset.SetProgram(program);
                EditorUtility.SetDirty(programAsset);
                WriteTextAtomic(manifestPath, IdentityJson.SerializeManifest(manifest));
                WriteTextAtomic(
                    changeReportPath,
                    IdentityJson.SerializeChangeReport(baseline, manifest, changes));
                AssetDatabase.SaveAssets();

                authoring.RuntimeProgramAssetPath = outputPath;
                authoring.CommitPublishedIdentity(manifest);
                EditorUtility.SetDirty(authoring);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(changeReportPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                return ExportCompiledResult.CreateExported(
                    outputPath,
                    manifestPath,
                    changeReportPath,
                    changes);
            }
            catch (Exception exception)
            {
                RestoreProgramAsset(outputPath, programAsset, previousProgramAssetJson, createdProgramAsset);
                manifestBackup.Restore();
                reportBackup.Restore();
                authoring.RuntimeProgramAssetPath = previousProgramPath;
                authoring.RestorePublishedIdentity(baseline);
                EditorUtility.SetDirty(authoring);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                throw new GameException(
                    $"Story identity export failed. story:{program.StoryId} output:{outputPath}",
                    exception);
            }
        }

        private static void RestoreProgramAsset(
            string outputPath,
            ProgramAsset asset,
            string previousAssetJson,
            bool created)
        {
            if (created)
            {
                AssetDatabase.DeleteAsset(outputPath);
                return;
            }

            if (asset == null || previousAssetJson == null)
            {
                return;
            }

            EditorJsonUtility.FromJsonOverwrite(previousAssetJson, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static bool ConfirmBreakingChanges(IdentityChangeReport changes)
        {
            return EditorUtility.DisplayDialog(
                "确认删除已发布剧情身份",
                $"本次导出将删除 {changes.RemovedEpisodeIds.Count} 个剧情段、" +
                $"{changes.RemovedEdgeIds.Count} 条路线边和 {changes.RemovedExits.Count} 个出口身份。\n\n" +
                "这些身份可能已被本地或服务器业务状态引用。确认继续导出？",
                "继续导出",
                "取消");
        }

        private static string ResolveOutputPath(AuthoringAsset authoring, string sourcePath)
        {
            var previousPath = authoring.RuntimeProgramAssetPath;
            if (string.IsNullOrWhiteSpace(previousPath) is false &&
                AssetDatabase.LoadAssetAtPath<ProgramAsset>(previousPath) != null)
            {
                return previousPath;
            }

            var directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            var fileName = string.IsNullOrWhiteSpace(authoring.StoryId) ? "StoryProgram" : authoring.StoryId;
            if (string.IsNullOrWhiteSpace(previousPath) is false)
            {
                directory = Path.GetDirectoryName(previousPath)?.Replace('\\', '/') ?? directory;
                fileName = Path.GetFileNameWithoutExtension(previousPath);
            }

            return EditorUtility.SaveFilePanelInProject(
                "导出运行时剧情资源",
                fileName,
                "asset",
                "选择编译后的 ProgramAsset 保存位置。下次编译会默认覆盖这个文件。",
                directory);
        }

        private static string IdentityManifestPath(string programAssetPath)
        {
            return Path.ChangeExtension(programAssetPath, null) + ".identity-manifest.json";
        }

        private static string IdentityChangeReportPath(string programAssetPath)
        {
            return Path.ChangeExtension(programAssetPath, null) + ".identity-change.json";
        }

        private static void WriteTextAtomic(string assetPath, string contents)
        {
            var path = Path.GetFullPath(assetPath);
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                System.IO.File.WriteAllText(temporaryPath, contents);
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Replace(temporaryPath, path, null);
                }
                else
                {
                    System.IO.File.Move(temporaryPath, path);
                }
            }
            finally
            {
                if (System.IO.File.Exists(temporaryPath))
                {
                    System.IO.File.Delete(temporaryPath);
                }
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            EnsureFolder(parent);
            if (string.IsNullOrWhiteSpace(parent) is false &&
                string.IsNullOrWhiteSpace(name) is false &&
                AssetDatabase.IsValidFolder(folder) is false)
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private readonly struct TextFileBackup
        {
            private TextFileBackup(string assetPath, bool existed, string contents)
            {
                AssetPath = assetPath;
                Existed = existed;
                Contents = contents;
            }

            private string AssetPath { get; }

            private bool Existed { get; }

            private string Contents { get; }

            public static TextFileBackup Capture(string assetPath)
            {
                var path = Path.GetFullPath(assetPath);
                return System.IO.File.Exists(path)
                    ? new TextFileBackup(assetPath, true, System.IO.File.ReadAllText(path))
                    : new TextFileBackup(assetPath, false, null);
            }

            public void Restore()
            {
                var path = Path.GetFullPath(AssetPath);
                if (Existed)
                {
                    WriteTextAtomic(AssetPath, Contents);
                }
                else if (!string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(AssetPath)))
                {
                    AssetDatabase.DeleteAsset(AssetPath);
                }
                else if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
        }

        public readonly struct ExportCompiledResult
        {
            private ExportCompiledResult(
                bool exported,
                bool canceled,
                string outputPath,
                string identityManifestPath,
                string identityChangeReportPath,
                IdentityChangeReport identityChanges)
            {
                Exported = exported;
                Canceled = canceled;
                OutputPath = outputPath;
                IdentityManifestPath = identityManifestPath;
                IdentityChangeReportPath = identityChangeReportPath;
                IdentityChanges = identityChanges;
            }

            public bool Exported { get; }

            public bool Canceled { get; }

            public string OutputPath { get; }

            public string IdentityManifestPath { get; }

            public string IdentityChangeReportPath { get; }

            public IdentityChangeReport IdentityChanges { get; }

            public static ExportCompiledResult CreateExported(
                string outputPath,
                string identityManifestPath,
                string identityChangeReportPath,
                IdentityChangeReport identityChanges)
            {
                return new ExportCompiledResult(
                    true,
                    false,
                    outputPath,
                    identityManifestPath,
                    identityChangeReportPath,
                    identityChanges);
            }

            public static ExportCompiledResult CreateCanceled(
                string outputPath,
                IdentityChangeReport identityChanges = null)
            {
                return new ExportCompiledResult(false, true, outputPath, null, null, identityChanges);
            }

            public static ExportCompiledResult Skipped(string outputPath)
            {
                return new ExportCompiledResult(false, false, outputPath, null, null, null);
            }
        }
    }
}
