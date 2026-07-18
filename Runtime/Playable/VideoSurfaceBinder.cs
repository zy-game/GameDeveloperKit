using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Playable
{
    public enum VideoDisplayMode
    {
        Cover = 0
    }

    public static class VideoSurfaceBinder
    {
        public static void BindCover(RawImage output, Texture texture, bool verticalFlip)
        {
            if (output == null)
            {
                throw new System.ArgumentNullException(nameof(output));
            }

            output.texture = texture;
            if (texture == null)
            {
                output.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            var rect = output.rectTransform.rect;
            var targetAspect = rect.height > 0f ? rect.width / rect.height : 1f;
            var videoAspect = texture.height > 0 ? (float)texture.width / texture.height : 1f;
            output.uvRect = CalculateCoverUvRect(targetAspect, videoAspect, verticalFlip);
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
    }
}
