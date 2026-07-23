using System;
using System.Text;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Excel;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Model;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.UI
{
    public sealed partial class MainWindow
    {
        private void NewAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject("新建剧情资源", "NewStoryAuthoring", "asset", "选择剧情资源保存位置。");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var asset = AuthoringAssetStore.CreateProjectAtPath(path);
            if (asset == null)
            {
                return;
            }

            m_Asset = asset;
            ResetCompilerDiagnostics();
            SelectDefaults();
            RefreshAll("已新建资源。");
        }

        private void OpenAsset()
        {
            var path = EditorUtility.OpenFilePanel("打开剧情资源", Application.dataPath, "asset");
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase) is false)
            {
                return;
            }

            var assetPath = "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/');
            var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(assetPath);
            if (asset == null)
            {
                RefreshReport("打开失败：请选择 AuthoringAsset。");
                return;
            }

            m_Asset = asset;
            ResetCompilerDiagnostics();
            SelectDefaults();
            RefreshAll("已打开资源。");
        }

        private void SaveAsset()
        {
            if (m_EditorMode == EditorMode.Overview)
            {
                AuthoringAssetStore.Save(m_Asset);
            }
            else
            {
                AuthoringAssetStore.Save(m_SelectedVolumeAsset);
            }
            RefreshAll("已保存。");
        }

        private void CompileProgram()
        {
            m_LastCompiledProgram = ProgramCompiler.Compile(m_Asset, out m_Report);
            m_CompilerDiagnosticsStale = false;
            m_CompilerDiagnostics = Diagnostics.FromReport(m_Report, m_Asset, m_SelectedEpisode, false);
            var message = "编译失败。";
            if (m_Report.HasErrors is false && m_LastCompiledProgram != null)
            {
                var export = ProgramAssetExporter.ExportCompiled(m_Asset, m_LastCompiledProgram);
                var episodeCount = 0;
                for (var i = 0; i < m_LastCompiledProgram.Volumes.Count; i++)
                {
                    episodeCount += m_LastCompiledProgram.Volumes[i].Episodes.Count;
                }

                var summary = $"编译通过：{m_LastCompiledProgram.Volumes.Count} 卷，{episodeCount} 剧情段，{m_LastCompiledProgram.CommandSchema.Definitions.Count} 命令。";
                if (export.Exported)
                {
                    message = $"{summary}已导出 {export.OutputPath}。";
                }
                else if (export.Canceled)
                {
                    message = $"{summary}已取消导出运行时资源。";
                }
                else
                {
                    message = $"{summary}当前资源未保存到项目内，跳过运行时资源导出。";
                }
            }

            RefreshAll(message);
        }

        private void ExportExcel()
        {
            if (m_Asset == null)
            {
                EditorUtility.DisplayDialog("导出 Excel", "请先打开一个剧情编辑资源。", "确定");
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(m_Asset);
            var directory = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            var fileName = string.IsNullOrWhiteSpace(m_Asset.StoryId) ? "story_export" : m_Asset.StoryId;

            var outputPath = EditorUtility.SaveFilePanel("导出 Excel", directory, fileName, "xlsx");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            try
            {
                Exporter.Export(m_Asset, outputPath);
                EditorUtility.DisplayDialog("导出 Excel", $"成功导出到:\n{outputPath}", "确定");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导出失败", ex.Message, "确定");
                Debug.LogException(ex);
            }
        }

        private void ImportExcel()
        {
            if (m_Asset == null)
            {
                EditorUtility.DisplayDialog("导入 Excel", "请先打开一个剧情编辑资源。", "确定");
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(m_Asset);
            var directory = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');

            var inputPath = EditorUtility.OpenFilePanel("导入 Excel", directory, "xlsx");
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return;
            }

            try
            {
                var report = Importer.Import(inputPath, m_Asset);
                if (report.HasErrors)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("导入失败，以下校验未通过：");
                    builder.AppendLine();
                    for (var i = 0; i < report.Issues.Count; i++)
                    {
                        builder.AppendLine($"  {report.Issues[i]}");
                    }

                    EditorUtility.DisplayDialog("导入失败", builder.ToString(), "确定");
                }
                else
                {
                    AssetDatabase.Refresh();
                    RefreshAll("Excel 导入成功。");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导入失败", ex.Message, "确定");
                Debug.LogException(ex);
            }
        }
    }
}
