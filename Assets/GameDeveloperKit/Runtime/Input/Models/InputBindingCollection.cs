using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 输入绑定集合，包含多个输入绑定数据
    /// </summary>
    [Serializable]
    public sealed class InputBindingCollection
    {
        /// <summary>
        /// 输入绑定数据数组
        /// </summary>
        public InputBindingData[] Bindings;
    }
}
