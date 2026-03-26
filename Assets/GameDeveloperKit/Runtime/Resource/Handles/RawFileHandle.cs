using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 原始文件句柄，用于管理已加载的原始文件数据。
    /// </summary>
    public sealed class RawFileHandle : IDisposable
    {
        private readonly ResourcePackage.RawFileRecord _record;
        private bool _released;

        /// <summary>
        /// 初始化原始文件句柄的新实例。
        /// </summary>
        /// <param name="record">原始文件记录。</param>
        /// <exception cref="ArgumentNullException">当record为null时抛出。</exception>
        internal RawFileHandle(ResourcePackage.RawFileRecord record)
        {
            _record = record ?? throw new ArgumentNullException(nameof(record));
        }

        /// <summary>
        /// 获取包名称。
        /// </summary>
        public string PackageName => _record.PackageName;

        /// <summary>
        /// 获取资源位置。
        /// </summary>
        public ResourceLocation Location => _record.Location.Clone();

        /// <summary>
        /// 获取文件数据（字节数组）。
        /// </summary>
        public byte[] Data => _record.Data;

        /// <summary>
        /// 获取文件文本内容。
        /// </summary>
        public string Text => _record.Text;

        /// <summary>
        /// 释放原始文件句柄。
        /// </summary>
        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            if (Game.HasModule<ResourceModule>())
            {
                Game.Resource.NotifyHandleReleased(PackageName, Location);
            }

            _record.Release();
        }

        /// <summary>
        /// 释放原始文件句柄。
        /// </summary>
        public void Dispose()
        {
            Release();
        }
    }
}
