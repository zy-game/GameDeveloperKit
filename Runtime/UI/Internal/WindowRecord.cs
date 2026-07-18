using System;
using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.UI.Internal
{
    internal sealed class WindowRecord
    {
        public Type WindowType;
        public UIOption Option;
        public UIWindow Window;
        public UIDocument Document;
        public GameObject Instance;
        public AssetHandle AssetHandle;
        public UILayer Layer;
        public WindowStatus Status;
    }
}
