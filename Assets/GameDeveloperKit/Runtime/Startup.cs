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
        /// <summary>
        /// 存储 Instance。
        /// </summary>
        private static Startup s_Instance;

        /// <summary>
        /// 记录 Is Owner 状态。
        /// </summary>
        private bool m_IsOwner;
        /// <summary>
        /// 记录 Shutdown Requested 状态。
        /// </summary>
        private bool m_ShutdownRequested;

        /// <summary>
        /// Unity Awake 回调。
        /// </summary>
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

        /// <summary>
        /// Unity Start 回调。
        /// </summary>
        private void Start()
        {
            if (!m_IsOwner)
            {
                return;
            }

            App.Startup().Forget();
        }

        /// <summary>
        /// Unity OnApplicationQuit 回调。
        /// </summary>
        private void OnApplicationQuit()
        {
            RequestShutdown();
        }

        /// <summary>
        /// Unity OnDestroy 回调。
        /// </summary>
        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }

            RequestShutdown();
        }

        /// <summary>
        /// 执行 Request Shutdown。
        /// </summary>
        private void RequestShutdown()
        {
            if (!m_IsOwner || m_ShutdownRequested)
            {
                return;
            }

            m_ShutdownRequested = true;
            App.Shutdown().Forget();
        }

        /// <summary>
        /// 销毁 Duplicate。
        /// </summary>
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
