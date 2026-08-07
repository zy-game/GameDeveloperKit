using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;
using UnityEngine;

namespace GameDeveloperKit.UI
{
    public sealed partial class UIModule : GameModuleBase
    {
        private const string DefaultConfirmText = "确定";
        private const string DefaultCancelText = "取消";

        private readonly Queue<Func<UniTask<bool>>> m_TipsQueue = new Queue<Func<UniTask<bool>>>();
        private bool m_TipsRunning;

        /// <summary>
        /// 飘字提示（默认时长 2 秒），无交互、自动消失。
        /// 若上一个飘字仍在展示，将被立即替换为本次内容。
        /// </summary>
        public void Toast(string text)
        {
            Toast(text, ToastWindow.DefaultDurationSeconds);
        }

        /// <summary>
        /// 飘字提示，指定展示时长（秒），无交互、自动消失。
        /// </summary>
        public void Toast(string text, float time)
        {
            ShowToastAsync(text, time).Forget(Debug.LogException);
        }

        /// <summary>
        /// 模态提示弹窗：仅确认按钮（默认文案"确定"），确认返回 true。
        /// </summary>
        public UniTask<bool> TipsAsync(string text)
        {
            return TipsAsync(text, DefaultConfirmText, null, 0f);
        }

        /// <summary>
        /// 模态提示弹窗：仅确认按钮，自定义确认按钮文案，确认返回 true。
        /// </summary>
        public UniTask<bool> TipsAsync(string text, string confirm)
        {
            return TipsAsync(text, confirm, null, 0f);
        }

        /// <summary>
        /// 模态提示弹窗：确认 + 取消按钮，自定义按钮文案，确认返回 true，取消返回 false。
        /// </summary>
        public UniTask<bool> TipsAsync(string text, string confirm, string cancel)
        {
            return TipsAsync(text, confirm, cancel, 0f);
        }

        /// <summary>
        /// 模态提示弹窗：确认 + 取消按钮，自定义按钮文案 + 倒计时自动确认（秒，&gt;0 启用），超时视为确认返回 true。
        /// 多个 Tips 并发调用时按顺序排队展示。
        /// </summary>
        public UniTask<bool> TipsAsync(
            string text,
            string confirm,
            string cancel,
            float autoConfirmTime)
        {
            var completion = new UniTaskCompletionSource<bool>();
            m_TipsQueue.Enqueue(() => ShowTipsInternalAsync(
                text,
                confirm,
                cancel,
                autoConfirmTime,
                completion));
            DrainTipsQueueAsync().Forget(Debug.LogException);
            return completion.Task;
        }

        private async UniTask ShowToastAsync(string text, float durationSeconds)
        {
            if (TryGet<ToastWindow>(out var existing))
            {
                existing.Show(text);
                await existing.DismissNowAsync();
            }

            var toast = await OpenAsync<ToastWindow>();
            toast.Show(text);
            await toast.PlayAndDismissAsync(durationSeconds);
        }

        private async UniTask DrainTipsQueueAsync()
        {
            if (m_TipsRunning)
            {
                return;
            }

            m_TipsRunning = true;
            try
            {
                while (m_TipsQueue.Count > 0)
                {
                    var job = m_TipsQueue.Dequeue();
                    await job();
                }
            }
            finally
            {
                m_TipsRunning = false;
            }
        }

        private async UniTask<bool> ShowTipsInternalAsync(
            string text,
            string confirmText,
            string cancelText,
            float autoConfirmTime,
            UniTaskCompletionSource<bool> completion)
        {
            try
            {
                var tips = await OpenAsync<TipsWindow>();
                tips.Configure(text, confirmText, cancelText, autoConfirmTime);
                tips.StartAutoConfirmCountdown();
                var result = await tips.WaitForCloseAsync();
                completion.TrySetResult(result);
                return result;
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                return false;
            }
        }
    }
}
