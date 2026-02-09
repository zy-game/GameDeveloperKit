using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Combat;

namespace GameDeveloperKit.Editor.Combat
{
    /// <summary>
    /// CharacterConfig 默认Inspector
    /// 子类可以继承此类并调用 base.OnDraw() 来复用父类的绘制逻辑
    /// </summary>
    [CombatInspector(typeof(CharacterConfig))]
    public class CharacterInspector : CombatInspectorBase
    {
        protected CharacterConfig Character => Target as CharacterConfig;

        public override void OnDraw()
        {
            // Header
            var header = CreateDetailHeader(Character.CharacterName, "角色配置", new Color(0.8f, 0.4f, 0.8f));
            RootElement.Add(header);

            // Basic Info
            var basicSection = CreateSection("基本信息");
            AddHelpBox(basicSection, "角色的基本属性配置。", HelpBoxType.Info);
            AddPropertyField(basicSection, "CharacterName", "角色名称");
            AddPropertyField(basicSection, "Prefab", "角色模型");
            RootElement.Add(basicSection);

            // Health Attributes
            var healthSection = CreateSection("生命属性");
            AddHelpBox(healthSection, "角色的生命值相关属性。", HelpBoxType.Info);
            AddPropertyField(healthSection, "MaxHealth", "最大生命值");
            AddPropertyField(healthSection, "HealthRegen", "生命回复速度（每秒）");
            RootElement.Add(healthSection);

            // Combat Attributes
            var combatSection = CreateSection("战斗属性");
            AddHelpBox(combatSection, "角色的攻击、防御和暴击相关属性。", HelpBoxType.Info);
            AddPropertyField(combatSection, "Attack", "攻击力");
            AddPropertyField(combatSection, "Defense", "防御力");
            AddPropertyField(combatSection, "CritRate", "暴击率（0-1）");
            AddPropertyField(combatSection, "CritDamage", "暴击伤害倍率");
            RootElement.Add(combatSection);

            // Resource Attributes
            var resourceSection = CreateSection("资源属性");
            AddHelpBox(resourceSection, "角色的法力值等资源属性。", HelpBoxType.Info);
            AddPropertyField(resourceSection, "MaxMana", "最大法力值");
            AddPropertyField(resourceSection, "ManaRegen", "法力回复速度（每秒）");
            RootElement.Add(resourceSection);

            // Movement Attributes
            var movementSection = CreateSection("移动属性");
            AddHelpBox(movementSection, "角色的移动相关属性。", HelpBoxType.Info);
            AddPropertyField(movementSection, "EnableMovement", "启用移动");
            AddPropertyField(movementSection, "MoveSpeed", "移动速度（m/s）");
            AddPropertyField(movementSection, "JumpHeight", "跳跃高度（m）");
            AddPropertyField(movementSection, "Gravity", "重力加速度（m/s²）");
            AddPropertyField(movementSection, "Acceleration", "加速度");
            AddPropertyField(movementSection, "Mass", "质量（kg）");
            RootElement.Add(movementSection);

            // Movement Settings
            var movementSettingsSection = CreateSection("移动设置");
            AddHelpBox(movementSettingsSection, "角色移动的高级设置。", HelpBoxType.Info);
            AddPropertyField(movementSettingsSection, "RunThreshold", "奔跑阈值速度（m/s）");
            AddPropertyField(movementSettingsSection, "RotationSharpness", "旋转平滑度");
            AddPropertyField(movementSettingsSection, "DashSpeed", "冲刺速度（m/s）");
            AddPropertyField(movementSettingsSection, "DashDistance", "冲刺距离（m）");
            AddPropertyField(movementSettingsSection, "DashCooldown", "冲刺冷却时间（s）");
            RootElement.Add(movementSettingsSection);

            // Abilities
            var abilitiesSection = CreateSection("技能");
            AddHelpBox(abilitiesSection, "角色初始拥有的技能列表。", HelpBoxType.Info);
            AddArrayField(abilitiesSection, "InitialAbilities", "初始技能列表");
            RootElement.Add(abilitiesSection);

            // 绘制扩展字段
            DrawExtensionFields();
        }
    }
}
