using System;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class ResourcePackage
    {
        /// <summary>
        /// 表示资源包中已加载的 Unity 资源记录。
        /// </summary>
        /// <remarks>
        /// 此类跟踪已加载资源的引用计数、依赖关系和释放时间。
        /// 支持引用计数管理和延迟释放机制。
        /// </remarks>
        internal sealed class AssetRecord
        {
            private readonly ResourcePackage _package;
            private readonly AssetRecord[] _dependencies;
            private float? _pendingReleaseTime;

            /// <summary>
            /// 初始化 AssetRecord 的新实例。
            /// </summary>
            /// <param name="package">所属的资源包。</param>
            /// <param name="location">资源位置信息。</param>
            /// <param name="asset">已加载的 Unity 资源对象。</param>
            /// <param name="recordKey">记录的唯一标识键。</param>
            /// <param name="dependencies">此资源的依赖项记录数组。</param>
            public AssetRecord(ResourcePackage package, ResourceLocation location, UnityEngine.Object asset, string recordKey, AssetRecord[] dependencies)
            {
                _package = package;
                PackageName = package.PackageName;
                Location = location;
                Asset = asset;
                RecordKey = recordKey;
                _dependencies = dependencies ?? Array.Empty<AssetRecord>();
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
            /// 获取或设置已加载的 Unity 资源对象。
            /// </summary>
            public UnityEngine.Object Asset { get; private set; }

            /// <summary>
            /// 获取记录的唯一标识键。
            /// </summary>
            public string RecordKey { get; }

            /// <summary>
            /// 获取或设置当前的引用计数。
            /// </summary>
            public int RefCount { get; private set; }

            /// <summary>
            /// 获取依赖项的数量。
            /// </summary>
            public int DependencyCount => _dependencies.Length;

            /// <summary>
            /// 获取资源是否有效（未释放）。
            /// </summary>
            public bool IsValid => Asset != null;

            /// <summary>
            /// 增加引用计数，保持资源不被释放。
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
            /// 检查资源是否可以卸载。
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
            /// 卸载资源及其依赖项。
            /// </summary>
            public void Unload()
            {
                ReleaseDependencies();
                Asset = null;
            }

            /// <summary>
            /// 释放所有依赖项的引用。
            /// </summary>
            private void ReleaseDependencies()
            {
                for (var i = 0; i < _dependencies.Length; i++)
                {
                    _dependencies[i].Release();
                }
            }
        }
    }
}
