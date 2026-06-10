namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Debug Console 类型。
    /// </summary>
    public sealed class DebugConsole
    {
        /// <summary>
        /// 存储 Selected Tab。
        /// </summary>
        private int m_SelectedTab;

        public bool Visible { get; set; }

        public int SelectedTab
        {
            get => m_SelectedTab;
            set => m_SelectedTab = value < 0 ? 0 : value;
        }

        /// <summary>
        /// 执行 Open。
        /// </summary>
        public void Open()
        {
            Visible = true;
        }

        /// <summary>
        /// 执行 Close。
        /// </summary>
        public void Close()
        {
            Visible = false;
        }
    }
}
