using System;
using GameDeveloperKit.Runtime;
using UnityEditor;

namespace GameDeveloperKit.Editor
{
    [Serializable]
    internal sealed class ResourceBuildPipelineRequest
    {
        public ResourceProjectSettingsData Settings;
        public ResourcePackageDefinition Package;
        public BuildTarget BuildTarget = EditorUserBuildSettings.activeBuildTarget;
        public bool ForceRebuild;
        public string BundleExtension = ".bundle";
    }
}
