using System;
using System.Collections.Generic;
using GameDeveloperKit.EditorConfiguration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorCloud
{
    internal sealed class CloudConfigurationPanel : VisualElement
    {
        private const string DefaultProfileName = "default";
        private readonly EditorGlobalConfig m_ProjectConfig;
        private readonly CloudCredentialStore m_CredentialStore;

        private DropdownField m_ProviderField;
        private TextField m_ProfileField;
        private TextField m_BucketField;
        private TextField m_RegionField;
        private TextField m_EndpointField;
        private TextField m_RootPrefixField;
        private TextField m_AccessKeyField;
        private TextField m_SecretKeyField;
        private TextField m_SessionTokenField;
        private Label m_CredentialHeader;
        private Label m_StatusLabel;

        public CloudConfigurationPanel()
            : this(EditorGlobalConfig.LoadOrCreate(), new CloudCredentialStore())
        {
        }

        internal CloudConfigurationPanel(
            EditorGlobalConfig projectConfig,
            CloudCredentialStore credentialStore)
        {
            m_ProjectConfig = projectConfig ?? throw new ArgumentNullException(nameof(projectConfig));
            m_CredentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
            name = "cloud-configuration-panel";
            style.flexGrow = 1;
            style.minWidth = 0;
            style.paddingLeft = 24;
            style.paddingRight = 24;
            style.paddingTop = 20;
            style.paddingBottom = 24;
            Build();
        }

        private void Build()
        {
            var content = new VisualElement { name = "cloud-configuration-form" };
            content.style.width = Length.Percent(100);
            content.style.maxWidth = 920;
            content.style.alignSelf = Align.Center;
            Add(content);

            content.Add(CreateTitle("云配置"));
            content.Add(CreateSectionHeader("项目连接"));

            var configuredProvider = m_ProjectConfig.Cloud.ProviderId;
            var selectedProvider = configuredProvider == CloudProviderId.AliyunOss
                ? CloudProviderId.AliyunOss
                : CloudProviderId.TencentCos;
            m_ProviderField = new DropdownField(
                "Provider",
                new List<string> { CloudProviderId.TencentCos, CloudProviderId.AliyunOss },
                selectedProvider == CloudProviderId.AliyunOss ? 1 : 0)
            {
                name = "cloud-provider-field"
            };
            ConfigureField(m_ProviderField);
            m_ProviderField.RegisterValueChangedCallback(_ => RefreshCredentialFields());
            content.Add(m_ProviderField);

            m_ProfileField = CreateTextField(
                "cloud-profile-field",
                "凭证 Profile",
                NormalizeProfileName(m_ProjectConfig.Cloud.CredentialProfileName));
            m_ProfileField.isDelayed = true;
            m_ProfileField.RegisterValueChangedCallback(_ => RefreshCredentialFields());
            content.Add(m_ProfileField);

            m_BucketField = CreateTextField(
                "cloud-bucket-field",
                "Bucket",
                m_ProjectConfig.Cloud.Bucket);
            content.Add(m_BucketField);

            m_RegionField = CreateTextField(
                "cloud-region-field",
                "Region",
                m_ProjectConfig.Cloud.Region);
            content.Add(m_RegionField);

            m_EndpointField = CreateTextField(
                "cloud-endpoint-field",
                "自定义 Endpoint",
                m_ProjectConfig.Cloud.Endpoint);
            m_EndpointField.tooltip = "可选。填写完整 Bucket 的 HTTPS Endpoint，不能包含对象路径。";
            content.Add(m_EndpointField);

            m_RootPrefixField = CreateTextField(
                "cloud-root-prefix-field",
                "对象根前缀",
                m_ProjectConfig.Cloud.RootPrefix);
            content.Add(m_RootPrefixField);

            var saveProjectButton = new Button(SaveProjectConfiguration)
            {
                name = "cloud-save-project-button",
                text = "保存项目连接"
            };
            content.Add(CreateButtonRow(saveProjectButton));

            m_CredentialHeader = CreateSectionHeader(string.Empty);
            m_CredentialHeader.name = "cloud-credential-header";
            m_CredentialHeader.tooltip = "凭证按 Provider 和 Profile 独立保存在本机，不会写入项目。";
            content.Add(m_CredentialHeader);
            m_AccessKeyField = CreateTextField("cloud-access-key-field", string.Empty, string.Empty);
            content.Add(m_AccessKeyField);

            m_SecretKeyField = CreateTextField("cloud-secret-key-field", string.Empty, string.Empty);
            m_SecretKeyField.isPasswordField = true;
            content.Add(m_SecretKeyField);

            m_SessionTokenField = CreateTextField("cloud-session-token-field", string.Empty, string.Empty);
            m_SessionTokenField.isPasswordField = true;
            content.Add(m_SessionTokenField);

            var credentialPath = new Label(CloudCredentialStore.CredentialsPath)
            {
                name = "cloud-credential-path"
            };
            credentialPath.style.whiteSpace = WhiteSpace.Normal;
            credentialPath.style.marginLeft = 150;
            credentialPath.style.marginBottom = 8;
            credentialPath.style.color = EditorGUIUtility.isProSkin
                ? new Color(0.68f, 0.7f, 0.73f)
                : new Color(0.3f, 0.32f, 0.35f);
            content.Add(credentialPath);

            var saveCredentialButton = new Button(SaveCredentialProfile)
            {
                name = "cloud-save-credentials-button",
                text = "保存本机凭证"
            };
            content.Add(CreateButtonRow(saveCredentialButton));

            m_StatusLabel = new Label { name = "cloud-config-validation" };
            m_StatusLabel.style.whiteSpace = WhiteSpace.Normal;
            m_StatusLabel.style.marginLeft = 150;
            m_StatusLabel.style.marginTop = 8;
            content.Add(m_StatusLabel);
            SetStatus(null, false);
            RefreshCredentialFields();
        }

        private void SaveProjectConfiguration()
        {
            var cloud = m_ProjectConfig.Cloud;
            cloud.ProviderId = m_ProviderField.value;
            cloud.CredentialProfileName = NormalizeProfileName(m_ProfileField.value);
            m_ProfileField.SetValueWithoutNotify(cloud.CredentialProfileName);
            cloud.Bucket = m_BucketField.value;
            cloud.Region = m_RegionField.value;
            cloud.Endpoint = m_EndpointField.value;
            cloud.RootPrefix = m_RootPrefixField.value;

            try
            {
                if (m_ProjectConfig.TryValidate(out var error) is false)
                {
                    SetStatus(error, true);
                    return;
                }

                m_ProjectConfig.Save();
                m_ProfileField.SetValueWithoutNotify(cloud.CredentialProfileName);
                m_BucketField.SetValueWithoutNotify(cloud.Bucket);
                m_RegionField.SetValueWithoutNotify(cloud.Region);
                m_EndpointField.SetValueWithoutNotify(cloud.Endpoint);
                m_RootPrefixField.SetValueWithoutNotify(cloud.RootPrefix);
                SetStatus("项目连接已保存。", false);
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, true);
            }
        }

        private void SaveCredentialProfile()
        {
            try
            {
                var profileName = NormalizeProfileName(m_ProfileField.value);
                m_ProfileField.SetValueWithoutNotify(profileName);
                m_CredentialStore.Save(
                    m_ProviderField.value,
                    profileName,
                    new CloudCredential(
                        m_AccessKeyField.value,
                        m_SecretKeyField.value,
                        m_SessionTokenField.value));
                RefreshCredentialPresentation();
                SetStatus($"本机凭证已保存：{ProviderDisplayName(m_ProviderField.value)} / {profileName}。", false);
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, true);
            }
        }

        private void RefreshCredentialFields()
        {
            if (m_AccessKeyField == null)
            {
                return;
            }

            var profileName = NormalizeProfileName(m_ProfileField.value);
            m_ProfileField.SetValueWithoutNotify(profileName);
            RefreshCredentialPresentation();
            if (string.IsNullOrWhiteSpace(m_ProviderField.value))
            {
                m_AccessKeyField.SetValueWithoutNotify(string.Empty);
                m_SecretKeyField.SetValueWithoutNotify(string.Empty);
                m_SessionTokenField.SetValueWithoutNotify(string.Empty);
                SetStatus(null, false);
                return;
            }

            try
            {
                if (m_CredentialStore.TryGet(
                        m_ProviderField.value,
                        profileName,
                        out var credential))
                {
                    m_AccessKeyField.SetValueWithoutNotify(credential.AccessKeyId);
                    m_SecretKeyField.SetValueWithoutNotify(credential.SecretAccessKey);
                    m_SessionTokenField.SetValueWithoutNotify(credential.SessionToken);
                }
                else
                {
                    m_AccessKeyField.SetValueWithoutNotify(string.Empty);
                    m_SecretKeyField.SetValueWithoutNotify(string.Empty);
                    m_SessionTokenField.SetValueWithoutNotify(string.Empty);
                }

                SetStatus(null, false);
            }
            catch (Exception exception)
            {
                m_AccessKeyField.SetValueWithoutNotify(string.Empty);
                m_SecretKeyField.SetValueWithoutNotify(string.Empty);
                m_SessionTokenField.SetValueWithoutNotify(string.Empty);
                SetStatus(exception.Message, true);
            }
        }

        private void RefreshCredentialPresentation()
        {
            if (m_CredentialHeader == null)
            {
                return;
            }

            var providerId = m_ProviderField.value;
            var profileName = NormalizeProfileName(m_ProfileField.value);
            m_CredentialHeader.text = $"本机凭证（{ProviderDisplayName(providerId)} / {profileName}）";
            if (string.Equals(providerId, CloudProviderId.AliyunOss, StringComparison.Ordinal))
            {
                m_AccessKeyField.label = "AccessKey ID";
                m_SecretKeyField.label = "AccessKey Secret";
                m_SessionTokenField.label = "Security Token（可选）";
                return;
            }

            m_AccessKeyField.label = "SecretId";
            m_SecretKeyField.label = "SecretKey";
            m_SessionTokenField.label = "Session Token（可选）";
        }

        private static string NormalizeProfileName(string profileName)
        {
            return string.IsNullOrWhiteSpace(profileName)
                ? DefaultProfileName
                : profileName.Trim();
        }

        private static string ProviderDisplayName(string providerId)
        {
            return string.Equals(providerId, CloudProviderId.AliyunOss, StringComparison.Ordinal)
                ? "阿里云 OSS"
                : "腾讯 COS";
        }

        private void SetStatus(string message, bool error)
        {
            if (m_StatusLabel == null)
            {
                return;
            }

            m_StatusLabel.text = message ?? string.Empty;
            m_StatusLabel.style.display = string.IsNullOrWhiteSpace(message)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            m_StatusLabel.style.color = error
                ? new Color(0.95f, 0.35f, 0.3f)
                : new Color(0.35f, 0.75f, 0.45f);
        }

        private static TextField CreateTextField(string name, string label, string value)
        {
            var field = new TextField(label)
            {
                name = name,
                value = value ?? string.Empty
            };
            ConfigureField(field);
            return field;
        }

        private static void ConfigureField<TValue>(BaseField<TValue> field)
        {
            field.style.flexGrow = 1;
            field.style.minWidth = 0;
            field.style.marginBottom = 8;
            field.labelElement.style.width = 150;
            field.labelElement.style.minWidth = 150;
            field.labelElement.style.maxWidth = 150;
        }

        private static VisualElement CreateButtonRow(Button button)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginBottom = 14;
            button.style.minWidth = 112;
            button.style.height = 24;
            row.Add(button);
            return row;
        }

        private static Label CreateTitle(string text)
        {
            var title = new Label(text);
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 18;
            return title;
        }

        private static Label CreateSectionHeader(string text)
        {
            var header = new Label(text);
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 6;
            header.style.marginBottom = 12;
            header.style.paddingBottom = 6;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = EditorGUIUtility.isProSkin
                ? new Color(0.3f, 0.31f, 0.33f)
                : new Color(0.72f, 0.74f, 0.77f);
            return header;
        }
    }
}
