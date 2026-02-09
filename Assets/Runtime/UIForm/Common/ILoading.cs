namespace GameDeveloperKit.UI
{
    public interface ILoading : IUIForm
    {
        void SetInfo(string text);
        void SetVersion(string text);
        void SetProgress(float process);
    }
}