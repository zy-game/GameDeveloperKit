using UnityEngine;

namespace GameDeveloperKit.Editor.PsdToUgui
{
    /// <summary>
    /// 锚点工具类
    /// </summary>
    public static class AnchorUtils
    {
        public static void GetAnchorMinMax(AnchorPreset preset, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            switch (preset)
            {
                case AnchorPreset.TopLeft:
                    anchorMin = new Vector2(0, 1); anchorMax = new Vector2(0, 1); break;
                case AnchorPreset.TopCenter:
                    anchorMin = new Vector2(0.5f, 1); anchorMax = new Vector2(0.5f, 1); break;
                case AnchorPreset.TopRight:
                    anchorMin = new Vector2(1, 1); anchorMax = new Vector2(1, 1); break;
                case AnchorPreset.MiddleLeft:
                    anchorMin = new Vector2(0, 0.5f); anchorMax = new Vector2(0, 0.5f); break;
                case AnchorPreset.MiddleCenter:
                    anchorMin = new Vector2(0.5f, 0.5f); anchorMax = new Vector2(0.5f, 0.5f); break;
                case AnchorPreset.MiddleRight:
                    anchorMin = new Vector2(1, 0.5f); anchorMax = new Vector2(1, 0.5f); break;
                case AnchorPreset.BottomLeft:
                    anchorMin = new Vector2(0, 0); anchorMax = new Vector2(0, 0); break;
                case AnchorPreset.BottomCenter:
                    anchorMin = new Vector2(0.5f, 0); anchorMax = new Vector2(0.5f, 0); break;
                case AnchorPreset.BottomRight:
                    anchorMin = new Vector2(1, 0); anchorMax = new Vector2(1, 0); break;
                case AnchorPreset.StretchTop:
                    anchorMin = new Vector2(0, 1); anchorMax = new Vector2(1, 1); break;
                case AnchorPreset.StretchMiddle:
                    anchorMin = new Vector2(0, 0.5f); anchorMax = new Vector2(1, 0.5f); break;
                case AnchorPreset.StretchBottom:
                    anchorMin = new Vector2(0, 0); anchorMax = new Vector2(1, 0); break;
                case AnchorPreset.StretchLeft:
                    anchorMin = new Vector2(0, 0); anchorMax = new Vector2(0, 1); break;
                case AnchorPreset.StretchCenter:
                    anchorMin = new Vector2(0.5f, 0); anchorMax = new Vector2(0.5f, 1); break;
                case AnchorPreset.StretchRight:
                    anchorMin = new Vector2(1, 0); anchorMax = new Vector2(1, 1); break;
                case AnchorPreset.StretchAll:
                    anchorMin = new Vector2(0, 0); anchorMax = new Vector2(1, 1); break;
                default:
                    anchorMin = new Vector2(0.5f, 0.5f); anchorMax = new Vector2(0.5f, 0.5f); break;
            }
        }
    }
}
