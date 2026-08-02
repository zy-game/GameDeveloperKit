using UnityEngine;

namespace GameDeveloperKit.DesignImporter
{
    internal readonly struct DesignViewportTransform
    {
        public DesignViewportTransform(Vector2 scale, Vector2 offset)
        {
            Scale = scale;
            Offset = offset;
        }

        public Vector2 Scale { get; }
        public Vector2 Offset { get; }
    }

    internal static class DesignLayoutUtility
    {
        public static DesignViewportTransform CalculateViewport(
            Vector2 source,
            Vector2 target,
            DesignScaleMode scaleMode)
        {
            var sourceWidth = Mathf.Max(1f, source.x);
            var sourceHeight = Mathf.Max(1f, source.y);
            var targetWidth = Mathf.Max(1f, target.x);
            var targetHeight = Mathf.Max(1f, target.y);
            var widthScale = targetWidth / sourceWidth;
            var heightScale = targetHeight / sourceHeight;

            if (scaleMode == DesignScaleMode.Stretch)
            {
                return new DesignViewportTransform(new Vector2(widthScale, heightScale), Vector2.zero);
            }

            var uniform = scaleMode == DesignScaleMode.Fill
                ? Mathf.Max(widthScale, heightScale)
                : Mathf.Min(widthScale, heightScale);
            var content = new Vector2(sourceWidth * uniform, sourceHeight * uniform);
            var offset = (new Vector2(targetWidth, targetHeight) - content) * 0.5f;
            return new DesignViewportTransform(new Vector2(uniform, uniform), offset);
        }
    }
}
