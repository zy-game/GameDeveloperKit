using UnityEngine;

namespace GameDeveloperKit.Config
{
    [CreateAssetMenu(fileName = "ConfigSettings", menuName = "GameDeveloperKit/ConfigSettings")]
    public sealed class ConfigSettings : ScriptableObject
    {
        [SerializeField] private ConfigSourceDefinition[] sources;

        public ConfigSourceDefinition[] Sources => sources ?? System.Array.Empty<ConfigSourceDefinition>();
    }
}
