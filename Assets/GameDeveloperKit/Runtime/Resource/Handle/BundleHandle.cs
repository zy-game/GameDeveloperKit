using System;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    public class BundleHandle : ResourceHandle<BundleInfo>
    {
        public AssetBundle Asset { get; private set; }

        public override void Release()
        {
            base.Release();
            Asset.Unload(true);
        }

        public static BundleHandle Success(BundleInfo info, AssetBundle bundle)
        {
            return new BundleHandle()
            {
                Asset = bundle,
                Error = null,
                Info = info
            };
        }

        public static BundleHandle Failure(BundleInfo info, Exception exception)
        {
            return new BundleHandle()
            {
                Asset = null,
                Error = exception,
                Info = info
            };
        }
    }
}