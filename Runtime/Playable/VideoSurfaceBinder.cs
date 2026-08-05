using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Playable
{
    /// <summary>
    /// 视频显示模式，语义对齐 Unity VideoPlayer 的 VideoAspectRatio。
    /// </summary>
    public enum VideoDisplayMode
    {
        /// <summary>
        /// 保持视频原始像素尺寸显示（不放大）；超过容器时等比缩小以避免溢出。
        /// </summary>
        NoScaling = 0,

        /// <summary>
        /// 高度对齐容器，宽度等比缩放；超出容器宽度时水平裁剪。
        /// </summary>
        FitVertically = 1,

        /// <summary>
        /// 宽度对齐容器，高度等比缩放；超出容器高度时垂直裁剪。
        /// </summary>
        FitHorizontally = 2,

        /// <summary>
        /// 等比缩放使完整内容显示在容器内，剩余区域露出背景（黑边）。
        /// </summary>
        FitInside = 3,

        /// <summary>
        /// 等比缩放填满容器，超出容器的部分被裁剪。
        /// </summary>
        FitOutside = 4,

        /// <summary>
        /// 非等比拉伸填满容器。
        /// </summary>
        Stretch = 5
    }

    public static class VideoSurfaceBinder
    {
        private const float SizeEpsilon = 0.01f;

        /// <summary>
        /// 按显示模式绑定视频画面到输出 surface。
        /// </summary>
        public static void Bind(
            RawImage output,
            Texture texture,
            bool verticalFlip,
            VideoDisplayMode mode)
        {
            if (output == null)
            {
                throw new System.ArgumentNullException(nameof(output));
            }

            output.texture = texture;
            // 统一颜色：视频首帧前为黑色（避免白色 RawImage 泛白），首帧/预览图就绪后为白色。
            output.color = texture == null ? Color.black : Color.white;
            if (texture == null)
            {
                output.uvRect = new Rect(0f, 0f, 1f, 1f);
                RestoreStretchRect(output.rectTransform);
                return;
            }

            var rectTransform = output.rectTransform;
            var container = rectTransform.parent as RectTransform;
            if (container == null ||
                container.rect.width <= 0f ||
                container.rect.height <= 0f ||
                texture.width <= 0 ||
                texture.height <= 0)
            {
                output.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            CalculateLayout(
                container.rect.width,
                container.rect.height,
                texture.width,
                texture.height,
                mode,
                out var size,
                out var uvRect);
            ApplyRectSize(rectTransform, size.x, size.y);
            if (verticalFlip)
            {
                uvRect.y += uvRect.height;
                uvRect.height = -uvRect.height;
            }

            output.uvRect = uvRect;
        }

        public static Rect CalculateCoverUvRect(float targetAspect, float videoAspect, bool verticalFlip)
        {
            if (targetAspect <= 0f || float.IsNaN(targetAspect) || float.IsInfinity(targetAspect))
            {
                throw new System.ArgumentOutOfRangeException(nameof(targetAspect));
            }

            if (videoAspect <= 0f || float.IsNaN(videoAspect) || float.IsInfinity(videoAspect))
            {
                throw new System.ArgumentOutOfRangeException(nameof(videoAspect));
            }

            Rect result;
            if (videoAspect > targetAspect)
            {
                var width = targetAspect / videoAspect;
                result = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else
            {
                var height = videoAspect / targetAspect;
                result = new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }

            if (verticalFlip)
            {
                result.y += result.height;
                result.height = -result.height;
            }

            return result;
        }

        /// <summary>
        /// 计算视频在容器内等比完整显示（FitInside）后的尺寸。
        /// </summary>
        public static Vector2 CalculateFitSize(
            float containerWidth,
            float containerHeight,
            float videoWidth,
            float videoHeight)
        {
            if (containerWidth <= 0f ||
                containerHeight <= 0f ||
                float.IsNaN(containerWidth) ||
                float.IsNaN(containerHeight) ||
                float.IsInfinity(containerWidth) ||
                float.IsInfinity(containerHeight))
            {
                throw new System.ArgumentOutOfRangeException(nameof(containerWidth));
            }

            if (videoWidth <= 0f ||
                videoHeight <= 0f ||
                float.IsNaN(videoWidth) ||
                float.IsNaN(videoHeight) ||
                float.IsInfinity(videoWidth) ||
                float.IsInfinity(videoHeight))
            {
                throw new System.ArgumentOutOfRangeException(nameof(videoWidth));
            }

            var videoAspect = videoWidth / videoHeight;
            var containerAspect = containerWidth / containerHeight;
            if (videoAspect > containerAspect)
            {
                return new Vector2(containerWidth, containerWidth / videoAspect);
            }

            return new Vector2(containerHeight * videoAspect, containerHeight);
        }

        /// <summary>
        /// 按显示模式计算视频在容器内的布局：rect 尺寸与采样 UV 区域。
        /// 裁剪类模式通过 UV 区域裁剪实现，rect 始终不超出容器。
        /// </summary>
        public static void CalculateLayout(
            float containerWidth,
            float containerHeight,
            float videoWidth,
            float videoHeight,
            VideoDisplayMode mode,
            out Vector2 size,
            out Rect uvRect)
        {
            ValidatePositive(containerWidth, nameof(containerWidth));
            ValidatePositive(containerHeight, nameof(containerHeight));
            ValidatePositive(videoWidth, nameof(videoWidth));
            ValidatePositive(videoHeight, nameof(videoHeight));

            var videoAspect = videoWidth / videoHeight;
            var containerAspect = containerWidth / containerHeight;
            var fullUv = new Rect(0f, 0f, 1f, 1f);
            switch (mode)
            {
                case VideoDisplayMode.NoScaling:
                {
                    var scale = Mathf.Min(1f, containerWidth / videoWidth, containerHeight / videoHeight);
                    size = new Vector2(videoWidth * scale, videoHeight * scale);
                    uvRect = fullUv;
                    return;
                }

                case VideoDisplayMode.FitVertically:
                {
                    if (videoAspect <= containerAspect)
                    {
                        size = new Vector2(containerHeight * videoAspect, containerHeight);
                        uvRect = fullUv;
                        return;
                    }

                    var width = containerAspect / videoAspect;
                    size = new Vector2(containerWidth, containerHeight);
                    uvRect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
                    return;
                }

                case VideoDisplayMode.FitHorizontally:
                {
                    if (videoAspect >= containerAspect)
                    {
                        size = new Vector2(containerWidth, containerWidth / videoAspect);
                        uvRect = fullUv;
                        return;
                    }

                    var height = videoAspect / containerAspect;
                    size = new Vector2(containerWidth, containerHeight);
                    uvRect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
                    return;
                }

                case VideoDisplayMode.FitInside:
                    size = CalculateFitSize(containerWidth, containerHeight, videoWidth, videoHeight);
                    uvRect = fullUv;
                    return;

                case VideoDisplayMode.FitOutside:
                    size = new Vector2(containerWidth, containerHeight);
                    uvRect = CalculateCoverUvRect(containerAspect, videoAspect, false);
                    return;

                case VideoDisplayMode.Stretch:
                    size = new Vector2(containerWidth, containerHeight);
                    uvRect = fullUv;
                    return;

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(mode));
            }
        }

        private static void ValidatePositive(float value, string parameterName)
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new System.ArgumentOutOfRangeException(parameterName);
            }
        }

        /// <summary>
        /// 将 rect 改为居中锚点并设置布局尺寸；尺寸未变化时避免重复写入。
        /// </summary>
        private static void ApplyRectSize(RectTransform rectTransform, float width, float height)
        {
            var centered = rectTransform.anchorMin.x > 0.49f &&
                rectTransform.anchorMin.y > 0.49f &&
                rectTransform.anchorMax.x < 0.51f &&
                rectTransform.anchorMax.y < 0.51f;
            if (centered is false)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;
            }

            var current = rectTransform.sizeDelta;
            if (Mathf.Abs(current.x - width) > SizeEpsilon ||
                Mathf.Abs(current.y - height) > SizeEpsilon)
            {
                rectTransform.sizeDelta = new Vector2(width, height);
            }
        }

        /// <summary>
        /// 恢复为撑满父容器的拉伸布局，用于无画面时的默认状态。
        /// </summary>
        private static void RestoreStretchRect(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }
    }
}
