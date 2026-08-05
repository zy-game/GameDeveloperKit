using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.MediaEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed partial class HlsMediaLibraryWindow
    {
        private UniTask SelectMp4Async()
        {
            if (EnsureCloudCredentialConfigured() is false)
            {
                return UniTask.CompletedTask;
            }

            HlsMp4MultiSelectWindow.Open(
                InitialDirectory(),
                sourcePaths => Run(PrepareMp4BatchAsync(sourcePaths)));
            return UniTask.CompletedTask;
        }

        private async UniTask PrepareMp4BatchAsync(IReadOnlyList<string> sourcePaths)
        {
            if (sourcePaths == null || sourcePaths.Count == 0)
            {
                return;
            }

            if (EnsureCloudCredentialConfigured() is false)
            {
                return;
            }

            SetBusy(true);
            m_Status.text = $"正在预检 {sourcePaths.Count} 个源视频…";
            try
            {
                var origin = await m_CatalogRepository.LoadOriginAsync(
                    m_LifetimeCancellation.Token,
                    true);
                var preflight = await HlsBatchPublishPreflight.CreateAsync(
                    sourcePaths,
                    origin.Document,
                    HlsPublishWorkflow.ComputeSourceSha256Async,
                    m_LifetimeCancellation.Token,
                    (index, count, path) => EditorUtility.DisplayProgressBar(
                        "添加 HLS 流媒体",
                        $"正在计算源视频指纹 ({index}/{count})：{Path.GetFileName(path)}",
                        count <= 0 ? 0f : index / (float)count));
                var overwriteExisting = false;
                if (preflight.ExistingCount > 0)
                {
                    var choice = EditorUtility.DisplayDialogComplex(
                        "发现已存在的 HLS 流媒体",
                        $"{preflight.ExistingCount} 个源视频已存在于媒体库。",
                        "跳过重复项",
                        "取消整批",
                        "覆盖重复项");
                    if (choice == 1)
                    {
                        m_Status.text = "已取消批次添加。";
                        return;
                    }

                    overwriteExisting = choice == 2;
                }

                var intents = preflight.CreateIntents(overwriteExisting);
                if (intents.Count == 0)
                {
                    m_Status.text = BuildPreflightSummary(preflight, 0);
                    EditorUtility.DisplayDialog(
                        "没有可发布的视频",
                        BuildPreflightDetails(preflight),
                        "确定");
                    return;
                }

                Action refresh = () =>
                {
                    Focus();
                    Run(LoadPageAsync(null, true));
                };
                if (intents.Count == 1)
                {
                    HlsTranscodeWindow.OpenForPublish(intents[0], _ => refresh());
                }
                else
                {
                    HlsBatchPublishWindow.OpenForPublish(intents, refresh);
                }

                m_Status.text = BuildPreflightSummary(preflight, intents.Count);
            }
            catch (OperationCanceledException)
            {
                m_Status.text = "已取消添加视频。";
            }
            catch (Exception exception)
            {
                m_Status.text = "无法添加视频：" + exception.Message;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                SetBusy(false);
            }
        }

        private void RegisterBatchDragAndDrop()
        {
            rootVisualElement.RegisterCallback<DragUpdatedEvent>(OnBatchDragUpdated);
            rootVisualElement.RegisterCallback<DragPerformEvent>(OnBatchDragPerform);
        }

        private void UnregisterBatchDragAndDrop()
        {
            rootVisualElement.UnregisterCallback<DragUpdatedEvent>(OnBatchDragUpdated);
            rootVisualElement.UnregisterCallback<DragPerformEvent>(OnBatchDragPerform);
        }

        private void OnBatchDragUpdated(DragUpdatedEvent evt)
        {
            if (m_Busy is false && DragAndDrop.paths.Length > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            }
        }

        private void OnBatchDragPerform(DragPerformEvent evt)
        {
            var sourcePaths = DragAndDrop.paths.ToArray();
            if (m_Busy || sourcePaths.Length == 0)
            {
                return;
            }

            DragAndDrop.AcceptDrag();
            Run(PrepareMp4BatchAsync(sourcePaths));
            evt.StopPropagation();
        }

        private static string BuildPreflightSummary(
            HlsBatchPublishPreflightResult preflight,
            int intentCount)
        {
            return $"已准备 {intentCount} 个发布任务；" +
                   $"无效或批内重复 {preflight.Rejected.Count} 个，" +
                   $"媒体库重复源 {preflight.ExistingCount} 个。";
        }

        private static string BuildPreflightDetails(HlsBatchPublishPreflightResult preflight)
        {
            var details = preflight.Rejected
                .Take(8)
                .Select(item => $"{Path.GetFileName(item.SourcePath)}：{item.Reason}")
                .ToList();
            if (preflight.ExistingCount > 0)
            {
                details.Add($"另有 {preflight.ExistingCount} 个媒体库重复源被跳过。");
            }

            return details.Count == 0
                ? "所选文件都已存在于媒体库。"
                : string.Join(Environment.NewLine, details);
        }
    }
}
