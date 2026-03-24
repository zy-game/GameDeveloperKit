namespace GameDeveloperKit.Runtime
{
    public enum ResourceUpdateState
    {
        Idle,
        Checking,
        Downloading,
        Verifying,
        Applying,
        Completed,
        Failed
    }
}
