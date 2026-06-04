using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗 ECS 模块。
    /// </summary>
    public sealed class CombatModule : GameModuleBase
    {
        internal const string RootName = "GameDeveloperKit.CombatRoot";

        private GameObject m_Root;
        private CombatRuntimeDriver m_Driver;

        /// <summary>
        /// 默认战斗世界。
        /// </summary>
        public World World { get; private set; }

        /// <summary>
        /// 启动战斗模块。
        /// </summary>
        /// <returns>模块启动任务。</returns>
        public override UniTask Startup()
        {
            if (m_Root != null)
            {
                return UniTask.CompletedTask;
            }

            World = new World();
            m_Root = new GameObject(RootName);
            Object.DontDestroyOnLoad(m_Root);
            m_Driver = m_Root.AddComponent<CombatRuntimeDriver>();
            m_Driver.Initialize(this);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 关闭战斗模块。
        /// </summary>
        /// <returns>模块关闭任务。</returns>
        public override UniTask Shutdown()
        {
            m_Driver = null;
            World?.Dispose();
            World = null;
            DestroyGameObject(m_Root);
            m_Root = null;
            return UniTask.CompletedTask;
        }

        private void Update(float deltaTime)
        {
            World?.Update(deltaTime);
        }

        private static void DestroyGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(gameObject);
            }
            else
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class CombatRuntimeDriver : MonoBehaviour
        {
            private CombatModule m_Module;

            public void Initialize(CombatModule module)
            {
                m_Module = module;
            }

            private void Update()
            {
                m_Module?.Update(Time.deltaTime);
            }
        }
    }
}
