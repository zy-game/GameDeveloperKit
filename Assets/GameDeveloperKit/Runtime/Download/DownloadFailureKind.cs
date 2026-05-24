namespace GameDeveloperKit.Download
{
    public enum DownloadFailureKind
    {
        None,
        Network,
        Timeout,
        HttpStatus,
        FileIO,
        InvalidResponse,
        Canceled
    }
}
