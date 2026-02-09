namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI层级枚举，定义不同类型UI的显示层级
    /// </summary>
    public enum EUILayer
    {
        Background = 0,    // 背景层（如主菜单背景）
        HUD = 100,        // HUD层（如血条、小地图）
        Window = 200,     // 窗口层（如背包、商店）
        Popup = 300,      // 弹窗层（如确认对话框）
        System = 400,     // 系统层（如暂停菜单）
        Loading = 500,    // 加载层（如加载界面）
        Top = 1000        // 顶层（如网络错误提示）
    }
}