using UnityEngine;

namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 时间设置
    /// </summary>
    [CreateAssetMenu(menuName = "GameDeveloperKit/TimerModule")]
    public class TimerSettings : ScriptableObject
    {
        public int FPS;
    }
}