using System;
using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.UI.Internal
{
    /// <summary>
    /// 定义 UI Window Record 类型。
    /// </summary>
    internal sealed class UIWindowRecord
    {
        /// <summary>
        /// 存储 Window Type。
        /// </summary>
        public Type WindowType;
        /// <summary>
        /// 存储 Option。
        /// </summary>
        public UIOption Option;
        /// <summary>
        /// 存储 Window。
        /// </summary>
        public UIWindow Window;
        /// <summary>
        /// 存储 Document。
        /// </summary>
        public UIDocument Document;
        /// <summary>
        /// 存储 Instance。
        /// </summary>
        public GameObject Instance;
        /// <summary>
        /// 存储 Asset Handle。
        /// </summary>
        public AssetHandle AssetHandle;
        /// <summary>
        /// 存储 Layer。
        /// </summary>
        public UILayer Layer;
        /// <summary>
        /// 存储 Status。
        /// </summary>
        public UIWindowStatus Status;
    }
}
