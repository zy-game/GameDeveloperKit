namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 任务执行结果
    /// </summary>
    public class TaskResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// 警告消息列表
        /// </summary>
        public System.Collections.Generic.List<string> Warnings { get; private set; }

        private TaskResult()
        {
            Warnings = new System.Collections.Generic.List<string>();
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static TaskResult Succeed()
        {
            return new TaskResult { Success = true };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static TaskResult Failed(string errorMessage)
        {
            return new TaskResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }
    }
}
