using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格可视化配置
    /// </summary>
    [System.Serializable]
    public class GridVisualizerConfig
    {
        /// <summary>
        /// 网格线颜色
        /// </summary>
        public Color LineColor = new Color(1f, 1f, 1f, 0.3f);

        /// <summary>
        /// 高亮颜色
        /// </summary>
        public Color HighlightColor = new Color(0f, 0.8f, 1f, 0.5f);

        /// <summary>
        /// 选中颜色
        /// </summary>
        public Color SelectedColor = new Color(1f, 0.8f, 0f, 0.6f);

        /// <summary>
        /// 有效放置颜色
        /// </summary>
        public Color ValidColor = new Color(0f, 1f, 0f, 0.5f);

        /// <summary>
        /// 无效放置颜色
        /// </summary>
        public Color InvalidColor = new Color(1f, 0f, 0f, 0.5f);

        /// <summary>
        /// 预览颜色
        /// </summary>
        public Color PreviewColor = new Color(0.5f, 0.5f, 1f, 0.4f);

        /// <summary>
        /// 线宽
        /// </summary>
        public float LineWidth = 2f;

        /// <summary>
        /// 淡出开始距离
        /// </summary>
        public float FadeStartDistance = 50f;

        /// <summary>
        /// 淡出结束距离
        /// </summary>
        public float FadeEndDistance = 100f;

        /// <summary>
        /// 是否启用淡出
        /// </summary>
        public bool EnableFade = true;

        /// <summary>
        /// 根据状态获取颜色
        /// </summary>
        public Color GetStateColor(ECellVisualState state)
        {
            return state switch
            {
                ECellVisualState.Normal => LineColor,
                ECellVisualState.Hovered => HighlightColor,
                ECellVisualState.Selected => SelectedColor,
                ECellVisualState.Valid => ValidColor,
                ECellVisualState.Invalid => InvalidColor,
                ECellVisualState.Preview => PreviewColor,
                _ => Color.clear
            };
        }
    }
}
