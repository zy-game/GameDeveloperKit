namespace GameDeveloperKit.Logger
{
    public sealed class DebugConsole
    {
        private int m_SelectedTab;

        public bool Visible { get; set; }

        public int SelectedTab
        {
            get => m_SelectedTab;
            set => m_SelectedTab = value < 0 ? 0 : value;
        }

        public void Open()
        {
            Visible = true;
        }

        public void Close()
        {
            Visible = false;
        }
    }
}
