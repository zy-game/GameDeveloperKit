using System;
using GameDeveloperKit.Media;

namespace GameDeveloperKit.Config
{
    public sealed partial class ConfigModule
    {
        private MediaDeliverySettings m_MediaDelivery;

        /// <summary>
        /// Public media delivery endpoints, or null when the project has not configured them.
        /// </summary>
        public MediaDeliverySettings MediaDelivery => m_MediaDelivery;

        private void LoadMediaDeliverySettings(GdkSettings settings)
        {
            LoadMediaDeliverySettings(s => s?.MediaDelivery, settings);
        }

        internal void LoadMediaDeliverySettings(Func<GdkSettings, MediaDeliverySettings> resolver, GdkSettings settings)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            m_MediaDelivery = resolver(settings);
        }
    }
}
