using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Combat;

namespace GameDeveloperKit.Editor.Combat
{
    /// <summary>
    /// CueDefinition 默认Inspector
    /// 子类可以继承此类并调用 base.OnDraw() 来复用父类的绘制逻辑
    /// </summary>
    [CombatInspector(typeof(CueDefinition))]
    public class CueInspector : CombatInspectorBase
    {
        protected CueDefinition Cue => Target as CueDefinition;

        public override void OnDraw()
        {
            // Header
            var header = CreateDetailHeader(Cue.CueName, "Cue定义", new Color(0.96f, 0.62f, 0.04f));
            RootElement.Add(header);

            // Basic Info
            var basicSection = CreateSection("基本信息");
            AddHelpBox(basicSection, "Cue的基本信息，用于标识和描述这个表现效果。", HelpBoxType.Info);
            AddPropertyField(basicSection, "CueName", "名称");
            AddPropertyField(basicSection, "Description", "描述");
            RootElement.Add(basicSection);

            // Tags
            var tagsSection = CreateSection("触发标签");
            AddHelpBox(tagsSection, "当这些标签的Cue被触发时，会执行此定义的表现效果。支持多个标签。", HelpBoxType.Info);
            AddTagArrayField(tagsSection, "CueTags", "Cue标签");
            RootElement.Add(tagsSection);

            // Visual
            var visualSection = CreateSection("视觉效果");
            AddHelpBox(visualSection, "配置特效预制体及其生成位置、偏移、缩放等参数。", HelpBoxType.Info);
            AddPropertyField(visualSection, "EffectPrefab", "特效预制体");
            AddEnumField<CueSpawnLocation>(visualSection, "SpawnLocation", "生成位置", GetSpawnLocationDisplayName);
            AddPropertyField(visualSection, "PositionOffset", "位置偏移");
            AddPropertyField(visualSection, "RotationOffset", "旋转偏移");
            AddPropertyField(visualSection, "Scale", "缩放");
            AddPropertyField(visualSection, "AttachToTarget", "跟随目标");
            RootElement.Add(visualSection);

            // Audio
            var audioSection = CreateSection("音效");
            AddHelpBox(audioSection, "配置播放的音效及其参数。", HelpBoxType.Info);
            AddPropertyField(audioSection, "SoundEffect", "音效文件");
            AddPropertyField(audioSection, "Volume", "音量");
            AddPropertyField(audioSection, "Pitch", "音调");
            AddPropertyField(audioSection, "Is3DSound", "3D音效");
            RootElement.Add(audioSection);

            // Timing
            var timingSection = CreateSection("时间");
            AddHelpBox(timingSection, "控制表现效果的延迟和持续时间。", HelpBoxType.Info);
            AddPropertyField(timingSection, "Delay", "延迟(秒)");
            AddPropertyField(timingSection, "Duration", "持续时间(秒)");
            RootElement.Add(timingSection);

            // Conditions
            var condSection = CreateSection("条件");
            AddHelpBox(condSection, "设置触发条件，可以根据目标身上的标签来决定是否执行表现。", HelpBoxType.Info);
            AddTagArrayField(condSection, "RequiredTags", "需要的标签");
            AddTagArrayField(condSection, "BlockedTags", "阻止的标签");
            RootElement.Add(condSection);

            // Custom Handler
            var handlerSection = CreateSection("自定义处理器");
            AddHelpBox(handlerSection, "指定自定义的 ICueHandler 实现类来处理此 Cue。留空则使用默认的 CueDefinitionHandler。", HelpBoxType.Info);
            AddHandlerTypeField(handlerSection, "CustomHandlerTypeName", "处理器类型");
            RootElement.Add(handlerSection);

            // 绘制扩展字段
            DrawExtensionFields();
        }

        private string GetSpawnLocationDisplayName(CueSpawnLocation location)
        {
            return location switch
            {
                CueSpawnLocation.Source => "来源位置",
                CueSpawnLocation.Target => "目标位置",
                CueSpawnLocation.WorldLocation => "世界坐标",
                CueSpawnLocation.BetweenSourceAndTarget => "来源与目标之间",
                _ => location.ToString()
            };
        }
    }
}
