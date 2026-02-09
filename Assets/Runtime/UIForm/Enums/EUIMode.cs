namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI显示模式，定义UI打开时对其他UI的影响
    /// </summary>
    public enum EUIMode
    {
        Normal,           // 普通模式：不影响其他UI
        HideOthers,       // 隐藏其他：隐藏同层级其他UI
        HideLower,        // 隐藏低层：隐藏低层级所有UI
        Exclusive         // 独占模式：隐藏所有其他UI
    }
}