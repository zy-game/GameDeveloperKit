using System;
using GameDeveloperKit.Media;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    public sealed partial class ConfigModule
    {
        private MediaDeliverySettings m_MediaDelivery;

        /// <summary>
        /// Public media delivery endpoints, or null when the project has not generated them.
        /// </summary>
        public MediaDeliverySettings MediaDelivery => m_MediaDelivery;

        private void LoadMediaDeliverySettings()
        {
            LoadMediaDeliverySettings(Resources.Load<MediaDeliverySettings>);
        }

        internal void LoadMediaDeliverySettings(Func<string, MediaDeliverySettings> loader)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            m_MediaDelivery = loader(MediaDeliverySettings.ResourcePath);
        }
    }
}
