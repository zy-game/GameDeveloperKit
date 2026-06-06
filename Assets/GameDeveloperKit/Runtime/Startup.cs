using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit
{
    /// <summary>
    /// Unity场景中的框架启动脚本。
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class Startup : MonoBehaviour
    {
        private static Startup s_Instance;

        private bool m_IsOwner;
        private bool m_ShutdownRequested;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                DestroyDuplicate();
                return;
            }

            s_Instance = this;
            m_IsOwner = true;
            Object.DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (!m_IsOwner)
            {
                return;
            }

            App.Startup().Forget();
        }

        private void OnApplicationQuit()
        {
            RequestShutdown();
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }

            RequestShutdown();
        }

        private void RequestShutdown()
        {
            if (!m_IsOwner || m_ShutdownRequested)
            {
                return;
            }

            m_ShutdownRequested = true;
            App.Shutdown().Forget();
        }

        private void DestroyDuplicate()
        {
            if (Application.isPlaying)
            {
                Object.Destroy(gameObject);
                return;
            }

            Object.DestroyImmediate(gameObject);
        }
    }
}
