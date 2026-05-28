namespace GameDeveloperKit.Logger
{
    public interface ILogSink
    {
        void Write(LogEntry entry);
    }
}
