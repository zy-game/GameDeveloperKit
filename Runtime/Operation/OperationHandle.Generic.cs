namespace GameDeveloperKit.Operation
{
    /// <summary>
    /// 操作手柄
    /// </summary>
    /// <typeparam name="T">返回数据类型。</typeparam>
    public abstract class OperationHandle<T> : OperationHandle
    {
        private T _value;

        /// <summary>
        /// 返回结果
        /// </summary>
        public T Value => _value;
        /// <summary>
        /// 设置结果
        /// </summary>
        public void SetResult(T _value)
        {
            if (IsDone)
            {
                return;
            }

            this._value = _value;
            base.SetResult();
        }

        /// <summary>
        /// 通过对象形式设置泛型操作结果。
        /// </summary>
        /// <param name="value">操作结果。</param>
        /// <exception cref="GameException">结果为空或类型不匹配时抛出。</exception>
        internal override void SetResultObject(object value)
        {
            if (value is null)
            {
                if (default(T) is not null)
                {
                    throw new GameException($"Result value cannot be null for {typeof(T).Name}.");
                }

                SetResult(default);
                return;
            }

            if (value is not T result)
            {
                throw new GameException($"Result value type '{value.GetType().Name}' does not match '{typeof(T).Name}'.");
            }

            SetResult(result);
        }
    }
}
