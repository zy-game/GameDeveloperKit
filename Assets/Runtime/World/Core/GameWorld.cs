using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using Massive;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// 游戏世界
    /// 协调各个模块，提供统一的ECS接口
    /// </summary>
    public sealed class GameWorld : IReference
    {
        // 核心模块
        private readonly WorldContext _context;
        private readonly SystemManager _systemManager;
        private readonly WorldSceneManager _sceneManager;

        // 时间系统
        private readonly WorldTimeConfig _timeConfig;
        private readonly WorldTime _time;

        // 元数据
        private static int _nextWorldId;
        public string Name { get; }
        public int Id { get; }
        public WorldTimeConfig TimeConfig => _timeConfig;
        public WorldTime Time => _time;

        public GameWorld(string name) : this(name, null) { }

        public GameWorld(string name, WorldTimeConfig timeConfig)
        {
            Name = name ?? "Unnamed";
            Id = _nextWorldId++;

            _timeConfig = timeConfig ?? new WorldTimeConfig();
            _time = new WorldTime();

            _context = new WorldContext();
            _systemManager = new SystemManager(_context);
            _sceneManager = new WorldSceneManager(this);
        }

        #region System Management

        /// <summary>
        /// 添加系统（泛型）
        /// </summary>
        public void AddSystem<T>() where T : ISystem, new()
        {
            AddSystem(new T());
        }

        /// <summary>
        /// 添加系统
        /// </summary>
        public void AddSystem(ISystem system)
        {
            _systemManager.AddSystem(system, this);
        }

        /// <summary>
        /// 移除系统（泛型）
        /// </summary>
        public void RemoveSystem<T>() where T : ISystem
        {
            _systemManager.RemoveSystem<T>(this);
        }

        /// <summary>
        /// 移除系统
        /// </summary>
        public void RemoveSystem(ISystem system)
        {
            _systemManager.RemoveSystem(system, this);
        }

        /// <summary>
        /// 获取系统
        /// </summary>
        public T GetSystem<T>() where T : class, ISystem
        {
            return _systemManager.GetSystem<T>();
        }

        #endregion

        #region Entity Operations

        /// <summary>
        /// 创建实体
        /// </summary>
        public int CreateEntity()
        {
            return _context.CreateEntity();
        }

        /// <summary>
        /// 通过Baker创建实体
        /// </summary>
        public int CreateEntity(IBaker baker)
        {
            var entityId = CreateEntity();
            baker.Bake(this, entityId);
            return entityId;
        }

        /// <summary>
        /// 销毁实体
        /// </summary>
        public void DestroyEntity(int entityId)
        {
            _context.DestroyEntity(entityId);
        }

        /// <summary>
        /// 检查实体是否存活
        /// </summary>
        public bool IsAlive(int entityId)
        {
            return _context.IsAlive(entityId);
        }

        #endregion

        #region Component Operations

        /// <summary>
        /// 设置组件（添加或更新）
        /// </summary>
        public void AddComponent<T>(int entityId, T component) where T : struct, IComponent
        {
            var hadComponent = _context.HasComponent<T>(entityId);
            _context.SetComponent(entityId, component);

            if (!hadComponent)
            {
                _systemManager.DispatchSetup(this, entityId, typeof(T));
            }
        }

        /// <summary>
        /// 获取组件引用
        /// </summary>
        public ref T GetComponent<T>(int entityId) where T : struct, IComponent
        {
            return ref _context.GetComponent<T>(entityId);
        }

        /// <summary>
        /// 检查是否拥有组件
        /// </summary>
        public bool HasComponent<T>(int entityId) where T : struct, IComponent
        {
            return _context.HasComponent<T>(entityId);
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        public void RemoveComponent<T>(int entityId) where T : struct, IComponent
        {
            if (!_context.HasComponent<T>(entityId))
                return;

            _systemManager.DispatchTeardown(this, entityId, typeof(T));
            _context.RemoveComponent<T>(entityId);
        }

        #endregion

        #region Query API
        /// <summary>
        /// 包含指定组件的实体
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <param name="action"></param>
        public void Include<T1>(IdActionRef<T1> action) where T1 : struct, IComponent
        {
            _context.Include(action);
        }

        /// <summary>
        /// 包含指定组件的实体
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="action"></param>
        public void Include<T1, T2>(IdActionRef<T1, T2> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
        {
            _context.Include(action);
        }

        /// <summary>
        /// 包含指定组件的实体
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <param name="action"></param>
        public void Include<T1, T2, T3>(IdActionRef<T1, T2, T3> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
        {
            _context.Include(action);
        }

        /// <summary>
        /// 包含指定组件的实体
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <param name="action"></param>
        public void Include<T1, T2, T3, T4>(IdActionRef<T1, T2, T3, T4> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where T4 : struct, IComponent
        {
            _context.Include(action);
        }

        /// <summary>
        /// 排除指定组件的实体
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <param name="action"></param>
        public void Excluded<T1>(IdActionRef<T1> action) where T1 : struct, IComponent
        {
            _context.Excluded(action);
        }

        /// <summary>
        /// 排除指定组件的实体
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="action"></param>
        public void Excluded<T1, T2>(IdActionRef<T1, T2> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
        {
            _context.Excluded(action);
        }

        /// <summary>
        /// 排除指定组件的实体
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <param name="action"></param>
        public void Excluded<T1, T2, T3>(IdActionRef<T1, T2, T3> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
        {
            _context.Excluded(action);
        }
        /// <summary>
        /// 排除指定组件的实体
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <param name="action"></param>
        public void Excluded<T1, T2, T3, T4>(IdActionRef<T1, T2, T3, T4> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where T4 : struct, IComponent
        {
            _context.Excluded(action);
        }

        /// <summary>
        /// 查询符合Filter条件的实体
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Query Query(Filter filter) => _context.Query(filter);

        /// <summary>
        /// 查询符合Filter条件的实体
        /// </summary>
        public Query Query(IIncludeSelector includeSelector, IExcludeSelector excludeSelector)
        {
            return _context.Query(includeSelector, excludeSelector);
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// 更新World
        /// </summary>
        internal void OnUpdate(float unscaledDeltaTime)
        {
            if (_timeConfig.IsPaused)
            {
                _time.DeltaTime = 0f;
                _time.UnscaledDeltaTime = unscaledDeltaTime;
                return;
            }

            float scaledDeltaTime = unscaledDeltaTime * _timeConfig.TimeScale;

            _time.UnscaledDeltaTime = unscaledDeltaTime;
            _time.DeltaTime = scaledDeltaTime;
            _time.TotalTime += scaledDeltaTime;
            _time.FrameCount++;

            if (_timeConfig.TargetFrameRate > 0)
            {
                float targetFrameTime = 1.0f / _timeConfig.TargetFrameRate;
                if (_time.DeltaTime < targetFrameTime * 0.95f)
                {
                    return;
                }
            }

            _systemManager.UpdateAll(this, scaledDeltaTime);
        }

        /// <summary>
        /// 清理
        /// </summary>
        public void OnClearup()
        {
            _systemManager.Clear(this);
            _sceneManager.Clear();
            _context.Clear();
        }

        #endregion

        #region Time Control
        /// <summary>
        /// 设置目标帧率
        /// </summary>
        /// <param name="fps"></param>
        public void SetTargetFrameRate(int fps) => _timeConfig.TargetFrameRate = fps;

        /// <summary>
        /// 设置固定时间步长
        /// </summary>
        /// <param name="timeStep"></param>
        public void SetFixedTimeStep(float timeStep) => _timeConfig.FixedTimeStep = timeStep;

        /// <summary>
        /// 设置时间缩放比例
        /// </summary>
        /// <param name="scale"></param>
        public void SetTimeScale(float scale) => _timeConfig.TimeScale = UnityEngine.Mathf.Max(0f, scale);

        /// <summary>
        /// 暂停时间
        /// </summary>
        public void Pause() => _timeConfig.IsPaused = true;

        /// <summary>
        /// 恢复时间
        /// </summary>
        public void Resume() => _timeConfig.IsPaused = false;

        /// <summary>
        /// 切换暂停状态
        /// </summary>
        public void TogglePause() => _timeConfig.IsPaused = !_timeConfig.IsPaused;

        /// <summary>
        /// 重置时间
        /// </summary>
        public void ResetTime()
        {
            _time.TotalTime = 0f;
            _time.FixedTime = 0f;
            _time.FrameCount = 0;
            _time.FixedFrameCount = 0;
        }

        #endregion

        #region Scene Management

        /// <summary>
        /// 加载场景
        /// </summary>
        public UniTask<SceneHandle> AddScene(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode = UnityEngine.SceneManagement.LoadSceneMode.Additive,
                           System.Action<float> onProgress = null, System.Action onComplete = null)
        {
            return _sceneManager.AddScene(sceneName, mode, onProgress, onComplete);
        }

        /// <summary>
        /// 卸载场景
        /// </summary>
        public void RemoveScene(string sceneName, System.Action onComplete = null)
        {
            _sceneManager.RemoveScene(sceneName, onComplete);
        }

        /// <summary>
        /// 检查场景是否已加载
        /// </summary>
        public bool HasScene(string sceneName)
        {
            return _sceneManager.HasScene(sceneName);
        }

        /// <summary>
        /// 获取当前场景名称
        /// </summary>
        public string GetCurrentScene()
        {
            return _sceneManager.GetCurrentScene();
        }

        /// <summary>
        /// 返回上一个场景
        /// </summary>
        public void BackScene(System.Action<float> onProgress = null)
        {
            _sceneManager.Back(onProgress);
        }

        /// <summary>
        /// 返回到指定场景
        /// </summary>
        public void BackToScene(string sceneName, System.Action<float> onProgress = null)
        {
            _sceneManager.BackTo(sceneName, onProgress);
        }

        #endregion
    }
}
