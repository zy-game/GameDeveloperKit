using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class ResourcePackage
    {
        /// <summary>
        /// 表示资源包中已加载的原始文件记录。
        /// </summary>
        /// <remarks>
        /// 此类跟踪已加载原始文件的引用计数和释放时间。
        /// 同时提供二进制数据和文本格式访问。
        /// 支持引用计数管理和延迟释放机制。
        /// </remarks>
        internal sealed class RawFileRecord
        {
            private readonly ResourcePackage _package;
            private float? _pendingReleaseTime;

            /// <summary>
            /// 初始化 RawFileRecord 的新实例。
            /// </summary>
            /// <param name="package">所属的资源包。</param>
            /// <param name="location">资源位置信息。</param>
            /// <param name="fullPath">文件的完整路径。</param>
            /// <param name="data">文件的二进制数据。</param>
            public RawFileRecord(ResourcePackage package, ResourceLocation location, string fullPath, byte[] data)
            {
                _package = package;
                PackageName = package.PackageName;
                Location = location;
                FullPath = fullPath;
                Data = data;
                Text = TryReadText(data);
                RefCount = 1;
            }

            /// <summary>
            /// 获取资源包名称。
            /// </summary>
            public string PackageName { get; }

            /// <summary>
            /// 获取资源位置信息。
            /// </summary>
            public ResourceLocation Location { get; }

            /// <summary>
            /// 获取文件的完整路径。
            /// </summary>
            public string FullPath { get; }

            /// <summary>
            /// 获取或设置文件的二进制数据。
            /// </summary>
            public byte[] Data { get; private set; }

            /// <summary>
            /// 获取或设置文件的文本内容。
            /// </summary>
            public string Text { get; private set; }

            /// <summary>
            /// 获取或设置当前的引用计数。
            /// </summary>
            public int RefCount { get; private set; }

            /// <summary>
            /// 增加引用计数，保持文件不被释放。
            /// </summary>
            public void Retain()
            {
                RefCount++;
                _pendingReleaseTime = null;
            }

            /// <summary>
            /// 减少引用计数，在引用计数归零后安排延迟释放。
            /// </summary>
            public void Release()
            {
                if (RefCount <= 0)
                {
                    return;
                }

                RefCount--;
                if (RefCount == 0)
                {
                    _pendingReleaseTime = Time.realtimeSinceStartup + Mathf.Max(0f, _package.Options.ReleaseDelaySeconds);
                }
            }

            /// <summary>
            /// 检查文件是否可以卸载。
            /// </summary>
            /// <param name="now">当前时间。</param>
            /// <param name="force">是否强制卸载，忽略延迟。</param>
            /// <returns>如果可以卸载则返回 true，否则返回 false。</returns>
            public bool CanUnload(float now, bool force)
            {
                if (RefCount > 0)
                {
                    return false;
                }

                if (force)
                {
                    return true;
                }

                return _pendingReleaseTime.HasValue && now >= _pendingReleaseTime.Value;
            }

            /// <summary>
            /// 卸载文件数据。
            /// </summary>
            public void Unload()
            {
                Data = null;
                Text = null;
            }

            /// <summary>
            /// 尝试将二进制数据读取为 UTF-8 文本。
            /// </summary>
            /// <param name="data">二进制数据。</param>
            /// <returns>解码后的文本，如果失败则返回空字符串。</returns>
            private static string TryReadText(byte[] data)
            {
                if (data == null || data.Length == 0)
                {
                    return string.Empty;
                }

                try
                {
                    return System.Text.Encoding.UTF8.GetString(data);
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }
}
