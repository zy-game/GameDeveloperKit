using System;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 标记网络消息 opcode。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class OpcodeAttribute : Attribute
    {
        /// <summary>
        /// 初始化 Opcode Attribute。
        /// </summary>
        public OpcodeAttribute(int code)
        {
            Code = code;
        }

        public int Code { get; }
    }
}
