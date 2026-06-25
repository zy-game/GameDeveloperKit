using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 解锁状态提供器。
    /// </summary>
    public interface IUnlockStateProvider
    {
        /// <summary>
        /// 尝试读取解锁状态。
        /// </summary>
        /// <param name="unlockId">解锁 ID。</param>
        /// <param name="unlocked">解锁状态。</param>
        /// <returns>存在状态时返回 true。</returns>
        bool TryGetUnlockState(string unlockId, out bool unlocked);

        /// <summary>
        /// 尝试写入解锁状态。
        /// </summary>
        /// <param name="unlockId">解锁 ID。</param>
        /// <param name="unlocked">解锁状态。</param>
        /// <param name="errorMessage">写入失败错误。</param>
        /// <returns>写入成功时返回 true。</returns>
        bool TrySetUnlockState(string unlockId, bool unlocked, out string errorMessage);
    }

    /// <summary>
    /// 播放会话内存解锁状态提供器。
    /// </summary>
    public sealed class SessionUnlockStateProvider : IUnlockStateProvider
    {
        private readonly Dictionary<string, bool> m_States = new Dictionary<string, bool>();

        /// <inheritdoc />
        public bool TryGetUnlockState(string unlockId, out bool unlocked)
        {
            if (string.IsNullOrWhiteSpace(unlockId))
            {
                unlocked = false;
                return false;
            }

            return m_States.TryGetValue(unlockId, out unlocked);
        }

        /// <inheritdoc />
        public bool TrySetUnlockState(string unlockId, bool unlocked, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(unlockId))
            {
                errorMessage = "Unlock id cannot be empty.";
                return false;
            }

            m_States[unlockId] = unlocked;
            errorMessage = null;
            return true;
        }
    }
}
