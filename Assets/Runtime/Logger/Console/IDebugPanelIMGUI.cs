namespace GameDeveloperKit.Log
{
    /// <summary>
    /// IMGUI 调试面板接口
    /// </summary>
    public interface IDebugPanelIMGUI
    {
        string Name { get; }
        int Order { get; }
        void OnGUI();
        void OnActivate() { }
        void OnDeactivate() { }
        void OnUpdate() { }
        void OnDestroy() { }
    }
}
