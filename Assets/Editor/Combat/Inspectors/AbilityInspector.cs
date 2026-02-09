using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Combat;

namespace GameDeveloperKit.Editor.Combat
{
    /// <summary>
    /// AbilityBase 默认Inspector
    /// 子类可以继承此类并调用 base.OnDraw() 来复用父类的绘制逻辑
    /// </summary>
    [CombatInspector(typeof(AbilityBase))]
    public class AbilityInspector : CombatInspectorBase
    {
        protected AbilityBase Ability => Target as AbilityBase;

        public override void OnDraw()
        {
            // Header
            var header = CreateDetailHeader(Ability.AbilityName, "技能定义", new Color(0.23f, 0.51f, 0.96f));
            RootElement.Add(header);

            // Basic Info
            var basicSection = CreateSection("基本信息");
            AddHelpBox(basicSection, "定义技能的基本属性，包括名称、描述和图标。", HelpBoxType.Info);
            AddPropertyField(basicSection, "AbilityName", "技能名称");
            AddPropertyField(basicSection, "Description", "描述");
            AddPropertyField(basicSection, "Icon", "图标");
            RootElement.Add(basicSection);

            // Cooldown
            var cooldownSection = CreateSection("冷却设置");
            AddHelpBox(cooldownSection, "技能释放后需要等待的时间。冷却效果可用于在冷却期间给角色添加特殊状态。", HelpBoxType.Info);
            AddPropertyField(cooldownSection, "Cooldown", "冷却时间 (秒)");
            AddPropertyField(cooldownSection, "CooldownEffect", "冷却效果");
            RootElement.Add(cooldownSection);

            // Costs
            var costsSection = CreateSection("消耗");
            AddHelpBox(costsSection, "释放技能需要消耗的资源。选择属性名称（如 Mana、Energy）并设置消耗数值。", HelpBoxType.Info);
            AddArrayField(costsSection, "Costs", "技能消耗");
            RootElement.Add(costsSection);

            // Tags
            var tagsSection = CreateSection("标签配置");
            AddHelpBox(tagsSection, "标签用于控制技能的激活条件和效果：\n" +
                "• 技能标签：标识技能类型，可被其他技能查询\n" +
                "• 激活必需标签：角色必须拥有这些标签才能释放\n" +
                "• 激活阻止标签：角色拥有这些标签时无法释放\n" +
                "• 激活授予标签：释放时临时添加到角色\n" +
                "• 取消/阻止标签：影响其他技能的释放", HelpBoxType.Info);
            AddTagArrayField(tagsSection, "AbilityTags", "技能标签");
            AddTagArrayField(tagsSection, "ActivationRequiredTags", "激活必需标签");
            AddTagArrayField(tagsSection, "ActivationBlockedTags", "激活阻止标签");
            AddTagArrayField(tagsSection, "ActivationGrantedTags", "激活授予标签");
            AddTagArrayField(tagsSection, "CancelAbilitiesWithTags", "取消带有标签的技能");
            AddTagArrayField(tagsSection, "BlockAbilitiesWithTags", "阻止带有标签的技能");
            RootElement.Add(tagsSection);

            // Effects
            var effectsSection = CreateSection("应用效果");
            AddHelpBox(effectsSection, "技能释放时自动应用的游戏效果（GameplayEffect）。可用于造成伤害、添加Buff等。", HelpBoxType.Info);
            AddEffectArrayField(effectsSection, "EffectsToApply", "效果列表");
            RootElement.Add(effectsSection);

            // Cue
            var cueSection = CreateSection("表现");
            AddHelpBox(cueSection, "技能释放时触发的视觉/音效表现，可以选择多个 Cue 定义资源。", HelpBoxType.Info);
            AddCueArrayField(cueSection, "ActivationCues", "激活表现");
            RootElement.Add(cueSection);

            // 绘制扩展字段（子类新增的字段）
            DrawExtensionFields();
        }
    }
}
