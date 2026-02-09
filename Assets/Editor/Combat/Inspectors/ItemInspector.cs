using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Combat;

namespace GameDeveloperKit.Editor.Combat
{
    /// <summary>
    /// ItemConfig 默认Inspector
    /// 子类可以继承此类并调用 base.OnDraw() 来复用父类的绘制逻辑
    /// </summary>
    [CombatInspector(typeof(ItemConfig))]
    public class ItemInspector : CombatInspectorBase
    {
        protected ItemConfig Item => Target as ItemConfig;

        public override void OnDraw()
        {
            // Header
            var header = CreateDetailHeader(Item.ItemName, "道具配置", new Color(0.95f, 0.75f, 0.2f));
            RootElement.Add(header);

            // Basic Info
            var basicSection = CreateSection("基本信息");
            AddHelpBox(basicSection, "道具的基本属性配置。", HelpBoxType.Info);
            AddPropertyField(basicSection, "ItemId", "道具ID");
            AddPropertyField(basicSection, "ItemName", "道具名称");
            AddPropertyField(basicSection, "Description", "描述");
            AddPropertyField(basicSection, "Icon", "图标");
            RootElement.Add(basicSection);

            // Stack Settings
            var stackSection = CreateSection("堆叠设置");
            AddHelpBox(stackSection, "控制道具在背包中的堆叠行为。", HelpBoxType.Info);
            AddPropertyField(stackSection, "Stackable", "可堆叠");
            AddPropertyField(stackSection, "MaxStackCount", "最大堆叠数量");
            RootElement.Add(stackSection);

            // Use Settings
            var useSection = CreateSection("使用设置");
            AddHelpBox(useSection, "道具使用相关的配置，包括冷却时间和使用效果。", HelpBoxType.Info);
            AddPropertyField(useSection, "Usable", "可使用");
            AddPropertyField(useSection, "UseCooldown", "使用冷却时间(秒)");
            AddEffectArrayField(useSection, "UseEffects", "使用效果");
            RootElement.Add(useSection);

            // 绘制扩展字段
            DrawExtensionFields();
        }
    }
}
