namespace GameDeveloperKit.ResourceEditor.Registry
{
    [FilterRule(
        GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.CollectAllFilterRuleId,
        "收集全部",
        order: 0,
        Description = "保留 Collector 产生的全部候选资源。")]
    public sealed class CollectAllFilterRule : FilterRule
    {
        public override bool IsMatch(
            GameDeveloperKit.ResourceEditor.Authoring.Package package,
            GameDeveloperKit.ResourceEditor.Authoring.Bundle group,
            ResourceGroupPreview resource)
        {
            return true;
        }
    }
}
