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
        /// <summary>
        /// 战斗运行时根对象名称。
        /// </summary>
        internal const string RootName = "GameDeveloperKit.CombatRoot";

        /// <summary>
        /// 挂载战斗运行时驱动的根对象。
        /// </summary>
        private GameObject m_Root;

        /// <summary>
        /// 负责把 Unity Update 转发给战斗模块的运行时驱动。
        /// </summary>
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

        /// <summary>
        /// 按真实帧时间更新默认战斗世界。
        /// </summary>
        /// <param name="deltaTime">真实帧时间。</param>
        private void Update(float deltaTime)
        {
            World?.Update(deltaTime);
        }

        /// <summary>
        /// 根据当前运行模式销毁 Unity 对象。
        /// </summary>
        /// <param name="gameObject">待销毁的对象。</param>
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

        /// <summary>
        /// Unity 运行时驱动，把帧更新转发给战斗模块。
        /// </summary>
        private sealed class CombatRuntimeDriver : MonoBehaviour
        {
            /// <summary>
            /// 被驱动的战斗模块。
            /// </summary>
            private CombatModule m_Module;

            /// <summary>
            /// 初始化运行时驱动。
            /// </summary>
            /// <param name="module">战斗模块。</param>
            public void Initialize(CombatModule module)
            {
                m_Module = module;
            }

            /// <summary>
            /// Unity 每帧回调。
            /// </summary>
            private void Update()
            {
                m_Module?.Update(Time.deltaTime);
            }
        }
    }
}
