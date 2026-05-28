using UnityEngine;

namespace GameDeveloperKit.Config
{
    public sealed class ConfigSourcePayload
    {
        private ConfigSourcePayload(string text, Object asset)
        {
            Text = text;
            Asset = asset;
        }

        public string Text { get; }

        public Object Asset { get; }

        public static ConfigSourcePayload FromText(string text)
        {
            return new ConfigSourcePayload(text, null);
        }

        public static ConfigSourcePayload FromAsset(Object asset)
        {
            return new ConfigSourcePayload(null, asset);
        }
    }
}
