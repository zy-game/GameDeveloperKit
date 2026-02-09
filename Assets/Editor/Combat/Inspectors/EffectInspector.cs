using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Combat;

namespace GameDeveloperKit.Editor.Combat
{
    /// <summary>
    /// GameplayEffect 默认Inspector
    /// 子类可以继承此类并调用 base.OnDraw() 来复用父类的绘制逻辑
    /// </summary>
    [CombatInspector(typeof(GameplayEffect))]
    public class EffectInspector : CombatInspectorBase
    {
        protected GameplayEffect Effect => Target as GameplayEffect;

        public override void OnDraw()
        {
            // Header
            var header = CreateDetailHeader(Effect.EffectName, "游戏效果", new Color(0.06f, 0.73f, 0.51f));
            RootElement.Add(header);

            // Basic Info
            var basicSection = CreateSection("基本信息");
            AddHelpBox(basicSection, "定义效果的基本属性。效果可以修改角色属性、添加状态标签等。", HelpBoxType.Info);
            AddPropertyField(basicSection, "EffectName", "效果名称");
            AddPropertyField(basicSection, "Description", "描述");
            RootElement.Add(basicSection);

            // Duration
            var durationSection = CreateSection("持续时间");
            AddHelpBox(durationSection, "效果的持续类型：\n" +
                "• 即时效果：立即生效并结束（如直接伤害）\n" +
                "• 持续效果：持续一段时间后自动移除\n" +
                "• 永久效果：需要手动移除（如被动技能）", HelpBoxType.Info);
            AddEnumField<EffectDurationType>(durationSection, "DurationType", "持续类型", GetDurationTypeDisplayName);
            AddPropertyField(durationSection, "Duration", "持续时间 (秒)");
            RootElement.Add(durationSection);

            // Periodic
            var periodicSection = CreateSection("周期设置");
            AddHelpBox(periodicSection, "周期效果会按固定间隔重复触发，适用于持续伤害（DOT）或持续治疗（HOT）。", HelpBoxType.Info);
            AddPropertyField(periodicSection, "IsPeriodic", "是否周期触发");
            AddPropertyField(periodicSection, "Period", "周期间隔 (秒)");
            AddPropertyField(periodicSection, "ExecuteOnApply", "应用时立即执行");
            RootElement.Add(periodicSection);

            // Stacking
            var stackSection = CreateSection("堆叠设置");
            AddHelpBox(stackSection, "当重复应用相同效果时的处理方式：\n" +
                "• 不堆叠：忽略新效果\n" +
                "• 刷新时间：重置持续时间\n" +
                "• 堆叠层数：增加层数，效果叠加\n" +
                "• 覆盖：移除旧效果，应用新效果", HelpBoxType.Info);
            AddEnumField<EffectStackPolicy>(stackSection, "StackPolicy", "堆叠策略", GetStackPolicyDisplayName);
            AddPropertyField(stackSection, "MaxStacks", "最大堆叠数");
            RootElement.Add(stackSection);

            // Modifiers
            var modifiersSection = CreateSection("属性修改器");
            AddHelpBox(modifiersSection, "修改角色属性的方式：\n" +
                "• 加法(+)：直接增加数值\n" +
                "• 百分比加成(%+)：按基础值百分比增加\n" +
                "• 乘法(×)：乘以系数\n" +
                "• 覆盖(=)：直接设置为指定值\n" +
                "计算公式：(基础值 + 加法) × (1 + 百分比加成) × 乘法", HelpBoxType.Info);
            AddArrayField(modifiersSection, "Modifiers", "修改器列表");
            RootElement.Add(modifiersSection);

            // Tags
            var tagsSection = CreateSection("标签配置");
            AddHelpBox(tagsSection, "效果的标签控制：\n" +
                "• 授予标签：效果激活期间添加到角色\n" +
                "• 必需标签：角色必须拥有才能应用效果\n" +
                "• 阻止标签：角色拥有时无法应用效果\n" +
                "• 移除效果：应用时移除带有指定标签的其他效果", HelpBoxType.Info);
            AddTagArrayField(tagsSection, "GrantedTags", "授予标签");
            AddTagArrayField(tagsSection, "RequiredTags", "必需标签");
            AddTagArrayField(tagsSection, "BlockedTags", "阻止标签");
            AddTagArrayField(tagsSection, "RemoveEffectsWithTags", "移除带有标签的效果");
            RootElement.Add(tagsSection);

            // Cue
            var cueSection = CreateSection("表现");
            AddHelpBox(cueSection, "效果触发时的视觉/音效表现，可以选择多个 Cue 定义资源。", HelpBoxType.Info);
            AddCueArrayField(cueSection, "Cues", "表现列表");
            RootElement.Add(cueSection);

            // 绘制扩展字段
            DrawExtensionFields();
        }

        private string GetDurationTypeDisplayName(EffectDurationType type)
        {
            return type switch
            {
                EffectDurationType.Instant => "即时效果",
                EffectDurationType.Duration => "持续效果",
                EffectDurationType.Infinite => "永久效果",
                _ => type.ToString()
            };
        }

        private string GetStackPolicyDisplayName(EffectStackPolicy policy)
        {
            return policy switch
            {
                EffectStackPolicy.None => "不堆叠",
                EffectStackPolicy.Refresh => "刷新时间",
                EffectStackPolicy.Stack => "堆叠层数",
                EffectStackPolicy.Override => "覆盖",
                _ => policy.ToString()
            };
        }
    }
}
