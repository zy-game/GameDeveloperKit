using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Logger
{
    public interface IAnalyticsSink
    {
        UniTask TrackAsync(AnalyticsEvent analyticsEvent);
    }
}
