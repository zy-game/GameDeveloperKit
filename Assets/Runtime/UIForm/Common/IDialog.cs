namespace GameDeveloperKit.UI
{
    public interface IDialog : IUIForm
    {
        void SetInfo(string title, string message, string confirm, string cancel);
    }
}