using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    [CreateAssetMenu(fileName = "ResourceSettings", menuName = "GameDeveloperKit/Resource Settings")]
    public sealed class ResourceSettings : ScriptableObject
    {
        public ResourcePlayMode PlayMode;

        public List<ResourcePackageDefinition> Packages = new();
    }
}
