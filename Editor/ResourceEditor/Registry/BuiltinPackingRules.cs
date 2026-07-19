using System;
using System.Linq;

namespace GameDeveloperKit.ResourceEditor.Registry
{
    [PackRule(
        GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.PackTogetherRuleId,
        "整组一包",
        order: 0,
        Description = "将 Group 中的全部有效资源放入同一个逻辑桶。")]
    public sealed class PackTogetherRule : PackRule
    {
        public override string GetPackKey(
            GameDeveloperKit.ResourceEditor.Authoring.Package package,
            GameDeveloperKit.ResourceEditor.Authoring.Bundle group,
            ResourceGroupPreview resource)
        {
            return "group";
        }
    }

    [PackRule(
        GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.PackByLabelRuleId,
        "按 Label 分包",
        order: 10,
        Description = "按资源的完整 Label 集合形成唯一逻辑桶。")]
    public sealed class PackByLabelRule : PackRule
    {
        public override string GetPackKey(
            GameDeveloperKit.ResourceEditor.Authoring.Package package,
            GameDeveloperKit.ResourceEditor.Authoring.Bundle group,
            ResourceGroupPreview resource)
        {
            var labels = (resource?.Labels ?? Array.Empty<string>())
                .Where(label => string.IsNullOrWhiteSpace(label) is false)
                .Select(label => label.Trim())
                .Where(label => label.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(label => label, StringComparer.Ordinal)
                .Select(Uri.EscapeDataString)
                .ToArray();
            return labels.Length == 0 ? "unlabeled" : string.Join("+", labels);
        }
    }
}
