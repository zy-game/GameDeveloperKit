using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    internal static class UnityRuntimeUtility
    {
        public static void TryDontDestroyOnLoad(Object target)
        {
            if (target == null || !Application.isPlaying)
            {
                return;
            }

            Object.DontDestroyOnLoad(target);
        }

        public static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
                return;
            }

            Object.DestroyImmediate(target);
        }
    }
}
