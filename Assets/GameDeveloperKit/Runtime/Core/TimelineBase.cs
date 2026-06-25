namespace GameDeveloperKit
{
    /// <summary>
    /// 时间线基类。
    /// </summary>
    public abstract class TimelineBase : IReference
    {
        /// <summary>
        /// 时间线名称。
        /// </summary>
        public virtual string Name => GetType().Name;

        /// <summary>
        /// 时间线时长。
        /// </summary>
        public float Duration { get; protected set; }

        /// <summary>
        /// 当前时间。
        /// </summary>
        public float CurrentTime { get; private set; }

        /// <summary>
        /// 跳转到指定时间。
        /// </summary>
        /// <param name="time">目标时间。</param>
        public void Seek(float time)
        {
            CurrentTime = ClampTime(time);
        }

        /// <summary>
        /// 求值指定时间。
        /// </summary>
        /// <param name="time">目标时间。</param>
        public void Evaluate(float time)
        {
            Seek(time);
            OnEvaluate(CurrentTime);
        }

        /// <summary>
        /// 执行时间线求值。
        /// </summary>
        /// <param name="time">当前时间。</param>
        protected abstract void OnEvaluate(float time);

        /// <summary>
        /// 释放时间线。
        /// </summary>
        public virtual void Release()
        {
            Duration = 0f;
            CurrentTime = 0f;
        }

        private float ClampTime(float time)
        {
            if (time <= 0f || Duration <= 0f)
            {
                return 0f;
            }

            return time > Duration ? Duration : time;
        }
    }
}
