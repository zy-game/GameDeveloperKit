using System;
using System.Text;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 二进制资源句柄
    /// </summary>
    public sealed class RawAssetHandle : ResourceHandle
    {
        private BundleHandle m_Bundle;

        /// <summary>
        /// 资源数据
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// 获取字符串
        /// </summary>
        public string GetString()
        {
            return Encoding.UTF8.GetString(Data);
        }

        /// <summary>
        /// 执行最终资源释放。
        /// </summary>
        protected override void ReleaseCore()
        {
            var bundle = m_Bundle;
            m_Bundle = null;
            Data = Array.Empty<byte>();
            bundle?.Release();
            base.ReleaseCore();
        }

        /// <summary>
        /// 创建二进制资源加载成功句柄
        /// </summary>
        /// <param name="location">资源信息</param>
        /// <param name="asset">资源数据</param>
        /// <returns>二进制资源句柄</returns>
        public static RawAssetHandle Success(AssetInfo location, byte[] asset, BundleHandle bundle = null)
        {
            bundle?.Retain();
            return new RawAssetHandle()
            {
                Info = location,
                Data = asset,
                m_Bundle = bundle,
                Error = null,
                Status = ResourceStatus.Succeeded,
            };
        }

        /// <summary>
        /// 创建资源加载失败句柄
        /// </summary>
        /// <param name="error">错误信息</param>
        /// <returns>二进制资源句柄</returns>
        public static RawAssetHandle Failure(Exception error)
        {
            return new RawAssetHandle()
            {
                Info = null,
                Error = error,
                Data = Array.Empty<byte>(),
                Status = ResourceStatus.Failed,
            };
        }
    }
}
