using System;
using System.Runtime.CompilerServices;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// StoryModule 交互通道扩展。
    /// </summary>
    public static class ModuleInteractionExtensions
    {
        private static readonly ConditionalWeakTable<StoryModule, InteractionChannelHolder> s_Channels =
            new ConditionalWeakTable<StoryModule, InteractionChannelHolder>();

        /// <summary>
        /// 设置剧情交互通道。
        /// </summary>
        /// <param name="module">剧情模块。</param>
        /// <param name="channel">交互通道。</param>
        public static void SetInteractions(this StoryModule module, IInteractionChannel channel)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (channel == null)
            {
                s_Channels.Remove(module);
                return;
            }

            s_Channels.GetOrCreateValue(module).Channel = channel;
        }

        /// <summary>
        /// 获取剧情交互通道。
        /// </summary>
        /// <param name="module">剧情模块。</param>
        /// <returns>交互通道。</returns>
        public static IInteractionChannel GetInteractions(this StoryModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            return s_Channels.TryGetValue(module, out var holder) ? holder.Channel : null;
        }

        private sealed class InteractionChannelHolder
        {
            public IInteractionChannel Channel;
        }
    }
}
