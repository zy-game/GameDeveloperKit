using UnityEngine;

namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 时间设置
    /// </summary>
    [CreateAssetMenu(menuName = "GameDeveloperKit/TimerModule")]
    public class TimerSettings : ScriptableObject
    {
        /// <summary>
        /// 计时器目标帧率。
        /// </summary>
        public int FPS;
    }
}