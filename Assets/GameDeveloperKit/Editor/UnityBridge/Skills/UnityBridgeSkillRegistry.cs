using System.Collections.Generic;

namespace GameDeveloperKit.UnityBridge
{
    public static class UnityBridgeSkillRegistry
    {
        private static readonly IReadOnlyList<IUnityBridgeSkill> s_Skills = new IUnityBridgeSkill[]
        {
            new UnityStatusSkill(),
            new UnityConsoleSkill(),
            new UnityMenuCommandSkill(),
            new UnityAssetCreationSkill(),
            new UnityReflectionSkill(),
            new UnitySceneSkill(),
            new UnityGameObjectSkill(),
            new UnityProjectSkill(),
            new UnitySelectionSkill(),
            new UnityScreenshotSkill(),
            new UnityAssetPreviewSkill(),
            new UnityEditorCameraSkill(),
            new UnityCompileSkill(),
            new UnityBrowseSkill(),
            new UnitySerializedPropertySkill(),
            new UnityAssetImporterSkill(),
            new UnityMaterialSkill(),
            new UnityAnimationSkill(),
            new UnityAnimatorSkill(),
            new UnityUISkill(),
            new UnityBuildSkill(),
            new UnityEditorSettingsSkill(),
            new UnityPrefabSkill(),
            new UnityLightingSkill(),
            new UnityNavMeshSkill(),
            new UnityAudioSkill()
        };

        public static IReadOnlyList<IUnityBridgeSkill> Skills => s_Skills;
    }
}
