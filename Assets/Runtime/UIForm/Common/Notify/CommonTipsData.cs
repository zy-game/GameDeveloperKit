using GameDeveloperKit.UI;

public class CommonTipsData : UIDataBase
{
    /// <summary>
    /// 提示消息
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 显示时长（秒）
    /// </summary>
    public float Duration { get; set; } = 2f;

    public override void OnClearup()
    {
        Message = null;
        Duration = 2f;
    }
}
