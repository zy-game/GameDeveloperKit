using UnityEngine;

namespace GameDeveloperKit.Resource
{
    [CreateAssetMenu(fileName = "ResourceSettings", menuName = "GameDeveloperKit/ResourceSettings")]
    public sealed class ResourceSettings : ScriptableObject
    {
        public ResourceMode Mode;
        public string[] DefaultPackages;
        public string url;
    }
}