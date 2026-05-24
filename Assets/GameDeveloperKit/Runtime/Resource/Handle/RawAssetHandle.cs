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
        /// <summary>
        /// 资源数据
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <returns></returns>
        public string GetString()
        {
            return Encoding.UTF8.GetString(Data);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public override void Release()
        {
            base.Release();
            Data = Array.Empty<byte>();
        }

        /// <summary>
        /// 创建二进制资源加载成功句柄
        /// </summary>
        /// <param name="location">资源信息</param>
        /// <param name="asset">资源数据</param>
        /// <returns>二进制资源句柄</returns>
        public static RawAssetHandle Success(AssetInfo location, byte[] asset)
        {
            return new RawAssetHandle()
            {
                Info = location,
                Data = asset
            };
        }

        /// <summary>
        /// 创建资源加载失败句柄
        /// </summary>
        /// <param name="location">资源信息</param>
        /// <param name="error">错误信息</param>
        /// <returns>二进制资源句柄</returns>
        public static RawAssetHandle Failure(Exception error)
        {
            return new RawAssetHandle()
            {
                Info = null,
                Error = error,
                Data = Array.Empty<byte>()
            };
        }
    }
}