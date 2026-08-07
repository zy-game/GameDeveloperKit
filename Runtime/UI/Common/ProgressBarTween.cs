using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// 进度条填充动画工具（GDK 公用，基于 DOTween）。
    /// 支持 Slider 与 Image.fillAmount 两种进度载体，动画可等待完成。
    /// </summary>
    public static class ProgressBarTween
    {
        private const float DefaultDurationSeconds = 0.3f;

        /// <summary>
        /// Slider 填充动画（value 从当前值平滑到 target，0~1）。
        /// </summary>
        public static Tween ToSlider(Slider slider, float target, float durationSeconds = DefaultDurationSeconds)
        {
            if (slider == null)
            {
                return null;
            }

            var targetClamped = Mathf.Clamp01(target);
            return slider.DOValue(targetClamped, Mathf.Max(0f, durationSeconds))
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(slider.gameObject);
        }

        /// <summary>
        /// Image.fillAmount 填充动画（0~1）。
        /// </summary>
        public static Tween ToFill(Image image, float target, float durationSeconds = DefaultDurationSeconds)
        {
            if (image == null)
            {
                return null;
            }

            var targetClamped = Mathf.Clamp01(target);
            return image.DOFillAmount(targetClamped, Mathf.Max(0f, durationSeconds))
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(image.gameObject);
        }

        /// <summary>
        /// Slider 填充动画并等待完成。
        /// </summary>
        public static async UniTask ToSliderAsync(Slider slider, float target, float durationSeconds = DefaultDurationSeconds)
        {
            var tween = ToSlider(slider, target, durationSeconds);
            if (tween == null)
            {
                return;
            }

            var completion = new UniTaskCompletionSource();
            tween.OnComplete(() => completion.TrySetResult())
                 .OnKill(() => completion.TrySetResult());
            await completion.Task;
        }

        /// <summary>
        /// Image.fillAmount 填充动画并等待完成。
        /// </summary>
        public static async UniTask ToFillAsync(Image image, float target, float durationSeconds = DefaultDurationSeconds)
        {
            var tween = ToFill(image, target, durationSeconds);
            if (tween == null)
            {
                return;
            }

            var completion = new UniTaskCompletionSource();
            tween.OnComplete(() => completion.TrySetResult())
                 .OnKill(() => completion.TrySetResult());
            await completion.Task;
        }
    }
}
