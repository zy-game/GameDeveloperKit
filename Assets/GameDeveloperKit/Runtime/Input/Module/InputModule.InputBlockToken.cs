using System;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class InputModule
    {
        /// <summary>
        /// 输入拦截令牌，用于控制输入拦截的生命周期
        /// </summary>
        private sealed class InputBlockToken : IDisposable
        {
            private readonly InputModule _owner;
            private readonly int _tokenId;
            private bool _disposed;

            /// <summary>
            /// 初始化输入拦截令牌
            /// </summary>
            /// <param name="owner">拥有者模块</param>
            /// <param name="tokenId">令牌标识</param>
            public InputBlockToken(InputModule owner, int tokenId)
            {
                _owner = owner;
                _tokenId = tokenId;
            }

            /// <summary>
            /// 释放输入拦截令牌
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _owner.ReleaseBlock(_tokenId);
                _disposed = true;
            }
        }
    }
}
