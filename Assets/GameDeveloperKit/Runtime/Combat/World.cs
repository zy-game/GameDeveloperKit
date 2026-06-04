using System;
using Massive;
using MassiveWorld = Massive.MassiveWorld;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 战斗世界。
    /// </summary>
    public sealed class World : IDisposable
    {
        /// <summary>
        /// 默认战斗世界帧率。
        /// </summary>
        public const int DefaultFrameRate = 50;

        private readonly MassiveWorld m_World;
        private float m_Accumulator;
        private bool m_Disposed;
        private int m_FrameRate;

        /// <summary>
        /// 初始化战斗世界。
        /// </summary>
        /// <param name="frameRate">固定帧率。</param>
        public World(int frameRate = DefaultFrameRate)
        {
            ValidateFrameRate(frameRate);

            m_World = new MassiveWorld();
            EntityManager = new EntityManager(this);
            SystemManager = new SystemManager(this);
            m_FrameRate = frameRate;
            FixedDeltaTime = 1f / frameRate;
        }

        /// <summary>
        /// 实体管理器。
        /// </summary>
        public EntityManager EntityManager { get; }

        /// <summary>
        /// 系统管理器。
        /// </summary>
        public SystemManager SystemManager { get; }

        /// <summary>
        /// 固定帧率。
        /// </summary>
        public int FrameRate
        {
            get => m_FrameRate;
            set
            {
                ValidateFrameRate(value);
                m_FrameRate = value;
                FixedDeltaTime = 1f / value;
            }
        }

        /// <summary>
        /// 固定帧间隔。
        /// </summary>
        public float FixedDeltaTime { get; private set; }

        /// <summary>
        /// 当前固定帧计数。
        /// </summary>
        public long Tick { get; private set; }

        /// <summary>
        /// 当前固定时间。
        /// </summary>
        public float Time { get; private set; }

        /// <summary>
        /// 可回滚帧数。
        /// </summary>
        public int CanRollbackFrames => m_World.CanRollbackFrames;

        internal MassiveWorld MassiveWorld => m_World;

        /// <summary>
        /// 按真实时间推进战斗世界。
        /// </summary>
        /// <param name="deltaTime">真实帧时间。</param>
        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Delta time cannot be negative.");
            }

            m_Accumulator += deltaTime;
            while (m_Accumulator >= FixedDeltaTime)
            {
                m_Accumulator -= FixedDeltaTime;
                Step();
            }
        }

        /// <summary>
        /// 推进一个固定帧。
        /// </summary>
        public void Step()
        {
            Tick++;
            Time += FixedDeltaTime;
            SystemManager.Update();
        }

        /// <summary>
        /// 保存当前帧。
        /// </summary>
        public void SaveFrame()
        {
            m_World.SaveFrame();
        }

        /// <summary>
        /// 回滚指定帧数。
        /// </summary>
        /// <param name="frames">回滚帧数。</param>
        public void Rollback(int frames)
        {
            m_World.Rollback(frames);
            EntityManager.Rebuild();
            SystemManager.Rebuild();
        }

        /// <summary>
        /// 清理世界。
        /// </summary>
        public void Clear()
        {
            SystemManager.Clear();
            m_World.Clear();
            EntityManager.Clear();
            m_Accumulator = 0f;
            Tick = 0;
            Time = 0f;
        }

        /// <summary>
        /// 释放世界。
        /// </summary>
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            Clear();
            m_Disposed = true;
        }

        private static void ValidateFrameRate(int frameRate)
        {
            if (frameRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameRate), frameRate, "World frame rate must be greater than zero.");
            }
        }
    }
}
