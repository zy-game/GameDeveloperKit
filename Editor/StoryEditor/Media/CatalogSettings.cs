using System;
using GameDeveloperKit.EditorConfiguration;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Media
{
    public sealed class CatalogSettings : ScriptableObject
    {
    }

    internal static class CatalogSettingsValidation
    {
        public static void ValidateForRequest(
            StoryMediaProjectConfig settings,
            string publicBaseUrl)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.EnsureDefaults();
            ValidateHttpsUrl(publicBaseUrl, "Cloud public base URL");
            if (settings.TimeoutSeconds <= 0)
            {
                throw new CatalogException(CatalogErrorKind.InvalidSettings, "Catalog timeout must be greater than zero.");
            }
        }

        private static void ValidateHttpsUrl(string value, string name)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) is false ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                string.IsNullOrWhiteSpace(uri.UserInfo) is false)
            {
                throw new CatalogException(CatalogErrorKind.InvalidSettings, $"{name} must be an absolute HTTPS URL.");
            }
        }
    }
}
