using System;
using UnityEngine;

namespace GameDeveloperKit.Localization
{
    [Serializable]
    public sealed class LocalizationKeyEntry
    {
        [SerializeField] private long m_Id;
        [SerializeField] private string m_Key;

        public LocalizationKeyEntry(long id, string key)
        {
            m_Id = id;
            m_Key = key;
        }

        public long Id => m_Id;

        public string Key => m_Key;
    }

    [Serializable]
    public sealed class LocalizationLocaleDescriptor
    {
        [SerializeField] private string m_Locale;
        [SerializeField] private string m_ResourceLocation;
        [SerializeField] private string m_FallbackLocale;

        public LocalizationLocaleDescriptor(string locale, string resourceLocation, string fallbackLocale = null)
        {
            m_Locale = locale;
            m_ResourceLocation = resourceLocation;
            m_FallbackLocale = fallbackLocale;
        }

        public string Locale => m_Locale;

        public string ResourceLocation => m_ResourceLocation;

        public string FallbackLocale => m_FallbackLocale;
    }

    [Serializable]
    public sealed class LocalizationValueEntry
    {
        [SerializeField] private long m_KeyId;
        [SerializeField] private string m_Value;

        public LocalizationValueEntry(long keyId, string value)
        {
            m_KeyId = keyId;
            m_Value = value;
        }

        public long KeyId => m_KeyId;

        public string Value => m_Value;
    }
}
