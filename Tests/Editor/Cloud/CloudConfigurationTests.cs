using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Media;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.StoryEditor.Media;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.Tests.Cloud
{
    public sealed class CloudConfigurationTests
    {
        private const string TempDirectory = "Library/GameDeveloperKit/Tests/CloudConfiguration";
        private byte[] m_ProjectConfigBackup;

        [SetUp]
        public void SetUp()
        {
            m_ProjectConfigBackup = IOFile.Exists(EditorGlobalConfig.SettingsPath)
                ? IOFile.ReadAllBytes(EditorGlobalConfig.SettingsPath)
                : null;
            if (IOFile.Exists(EditorGlobalConfig.SettingsPath))
            {
                IOFile.Delete(EditorGlobalConfig.SettingsPath);
            }

            EditorGlobalConfig.ResetInstance();
            if (IODirectory.Exists(TempDirectory))
            {
                IODirectory.Delete(TempDirectory, true);
            }

            IODirectory.CreateDirectory(TempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            EditorGlobalConfig.ResetInstance();
            if (IOFile.Exists(EditorGlobalConfig.SettingsPath))
            {
                IOFile.Delete(EditorGlobalConfig.SettingsPath);
            }

            if (m_ProjectConfigBackup != null)
            {
                IODirectory.CreateDirectory(IOPath.GetDirectoryName(EditorGlobalConfig.SettingsPath) ?? ".");
                IOFile.WriteAllBytes(EditorGlobalConfig.SettingsPath, m_ProjectConfigBackup);
            }

            if (IODirectory.Exists(TempDirectory))
            {
                IODirectory.Delete(TempDirectory, true);
            }
        }

        [Test]
        public void ProjectCloudConfig_SaveReloadsOnlyNonSensitiveConnectionFields()
        {
            var config = EditorGlobalConfig.LoadOrCreate();
            config.Cloud.ProviderId = CloudProviderId.TencentCos;
            config.Cloud.CredentialProfileName = " publisher ";
            config.Cloud.Bucket = " video-bucket ";
            config.Cloud.Region = " ap-chengdu ";
            config.Cloud.Endpoint = " https://cos.example.com ";
            config.Cloud.RootPrefix = " /videos/hls/ ";
            config.Cloud.CdnBaseUrl = " https://cdn.example.com/ ";
            config.Save();
            var serialized = IOFile.ReadAllText(EditorGlobalConfig.SettingsPath);

            EditorGlobalConfig.ResetInstance();
            var reloaded = EditorGlobalConfig.LoadOrCreate().Cloud;

            Assert.AreEqual(CloudProviderId.TencentCos, reloaded.ProviderId);
            Assert.AreEqual("publisher", reloaded.CredentialProfileName);
            Assert.AreEqual("video-bucket", reloaded.Bucket);
            Assert.AreEqual("ap-chengdu", reloaded.Region);
            Assert.AreEqual("https://cos.example.com", reloaded.Endpoint);
            Assert.AreEqual("videos/hls", reloaded.RootPrefix);
            Assert.AreEqual("https://cdn.example.com", reloaded.CdnBaseUrl);
            StringAssert.DoesNotContain("secretAccessKey", serialized);
            StringAssert.DoesNotContain("sessionToken", serialized);
            StringAssert.DoesNotContain("m_PublicBaseUrl", serialized);
            Assert.IsFalse(typeof(CloudProjectConfig).GetProperties().Any(property =>
                property.Name.IndexOf("Secret", StringComparison.OrdinalIgnoreCase) >= 0 ||
                property.Name.IndexOf("Token", StringComparison.OrdinalIgnoreCase) >= 0 ||
                property.Name.IndexOf("AccessKey", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void ProjectCloudConfig_KeepsProviderConnectionsIndependentAcrossSaveAndReload()
        {
            var config = EditorGlobalConfig.LoadOrCreate();
            config.Cloud.ProviderId = CloudProviderId.TencentCos;
            config.Cloud.CredentialProfileName = "cos-publisher";
            config.Cloud.Bucket = "cos-bucket-1250000000";
            config.Cloud.Region = "ap-chengdu";
            config.Cloud.Endpoint = "https://cos.example.com";
            config.Cloud.RootPrefix = "cos-videos";
            config.Cloud.CdnBaseUrl = "https://cos-cdn.example.com";

            config.Cloud.ProviderId = CloudProviderId.AliyunOss;
            config.Cloud.CredentialProfileName = "oss-publisher";
            config.Cloud.Bucket = "oss-bucket";
            config.Cloud.Region = "cn-chengdu";
            config.Cloud.Endpoint = "https://oss.example.com";
            config.Cloud.RootPrefix = "oss-videos";
            config.Cloud.CdnBaseUrl = "https://oss-cdn.example.com";
            config.Save();

            EditorGlobalConfig.ResetInstance();
            var reloaded = EditorGlobalConfig.LoadOrCreate().Cloud;
            Assert.AreEqual(CloudProviderId.AliyunOss, reloaded.ProviderId);
            Assert.AreEqual("oss-publisher", reloaded.CredentialProfileName);
            Assert.AreEqual("oss-bucket", reloaded.Bucket);
            Assert.AreEqual("cn-chengdu", reloaded.Region);
            Assert.AreEqual("https://oss.example.com", reloaded.Endpoint);
            Assert.AreEqual("oss-videos", reloaded.RootPrefix);
            Assert.AreEqual("https://oss-cdn.example.com", reloaded.CdnBaseUrl);

            reloaded.ProviderId = CloudProviderId.TencentCos;
            Assert.AreEqual("cos-publisher", reloaded.CredentialProfileName);
            Assert.AreEqual("cos-bucket-1250000000", reloaded.Bucket);
            Assert.AreEqual("ap-chengdu", reloaded.Region);
            Assert.AreEqual("https://cos.example.com", reloaded.Endpoint);
            Assert.AreEqual("cos-videos", reloaded.RootPrefix);
            Assert.AreEqual("https://cos-cdn.example.com", reloaded.CdnBaseUrl);
        }

        [TestCase(
            CloudProviderId.TencentCos,
            "bucket-1250000000",
            "ap-chengdu",
            "https://bucket-1250000000.cos.ap-chengdu.myqcloud.com/videos")]
        [TestCase(
            CloudProviderId.AliyunOss,
            "video-bucket",
            "cn-hangzhou",
            "https://video-bucket.oss-cn-hangzhou.aliyuncs.com/videos")]
        public void CloudPublicUrlResolver_DerivesMediaRootFromCurrentProvider(
            string providerId,
            string bucket,
            string region,
            string expected)
        {
            var config = new CloudProjectConfig
            {
                ProviderId = providerId,
                Bucket = bucket,
                Region = region,
                RootPrefix = "/videos/"
            };

            Assert.AreEqual(expected, CloudPublicUrlResolver.Resolve(config));
        }

        [Test]
        public void CloudPublicUrlResolver_UsesEndpointAndPublicOverrideInPriorityOrder()
        {
            var config = new CloudProjectConfig
            {
                ProviderId = CloudProviderId.TencentCos,
                Bucket = "bucket-1250000000",
                Region = "ap-chengdu",
                Endpoint = "https://origin.example.com/",
                RootPrefix = "videos"
            };

            Assert.AreEqual(
                "https://origin.example.com/videos",
                CloudPublicUrlResolver.Resolve(config));

            config.CdnBaseUrl = "https://cdn.example.com/";
            Assert.AreEqual(
                "https://cdn.example.com/videos",
                CloudPublicUrlResolver.Resolve(config));
        }

        [TestCase(
            CloudProviderId.TencentCos,
            "bucket-1250000000",
            "ap-chengdu",
            "",
            "https://bucket-1250000000.cos.ap-chengdu.myqcloud.com",
            "")]
        [TestCase(
            CloudProviderId.AliyunOss,
            "video-bucket",
            "cn-hangzhou",
            "https://cdn.example.com",
            "https://video-bucket.oss-cn-hangzhou.aliyuncs.com",
            "https://cdn.example.com")]
        public void MediaDeliverySettingsGenerator_CreatesPublicSettingsForCurrentProvider(
            string providerId,
            string bucket,
            string region,
            string cdnBaseUrl,
            string expectedOrigin,
            string expectedCdn)
        {
            var config = new CloudProjectConfig
            {
                ProviderId = providerId,
                Bucket = bucket,
                Region = region,
                RootPrefix = "videos",
                CdnBaseUrl = cdnBaseUrl
            };

            var settings = MediaDeliverySettingsGenerator.CreateSettings(config);
            try
            {
                Assert.AreEqual(expectedOrigin, settings.OriginBaseUrl);
                Assert.AreEqual(expectedCdn, settings.CdnBaseUrl);
                var serialized = EditorJsonUtility.ToJson(settings, true);
                StringAssert.DoesNotContain("CredentialProfile", serialized);
                StringAssert.DoesNotContain("SecretId", serialized);
                StringAssert.DoesNotContain("SecretKey", serialized);
                StringAssert.DoesNotContain("AccessKey", serialized);
                StringAssert.DoesNotContain("Token", serialized);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [TestCase(CloudProviderId.AliyunOss, "ap-chengdu", "阿里云 OSS")]
        [TestCase(CloudProviderId.AliyunOss, "oss-cn-chengdu", "不要填写 oss-cn-chengdu")]
        [TestCase(CloudProviderId.TencentCos, "cn-chengdu", "腾讯 COS")]
        public void CloudPublicUrlResolver_RejectsRegionFromAnotherProvider(
            string providerId,
            string region,
            string expectedError)
        {
            var config = new CloudProjectConfig
            {
                ProviderId = providerId,
                Bucket = "video-bucket",
                Region = region,
                RootPrefix = "videos"
            };

            Assert.IsFalse(CloudPublicUrlResolver.TryResolve(
                config,
                out var resolved,
                out var error));
            Assert.AreEqual(string.Empty, resolved);
            Assert.AreEqual(string.Empty, CloudPublicUrlResolver.Resolve(config));
            StringAssert.Contains(expectedError, error);
        }

        [Test]
        public void CatalogClient_InvalidProviderRegionFailsAsConfigurationBeforeRequest()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Cloud.ProviderId = CloudProviderId.AliyunOss;
            project.Cloud.Bucket = "video-bucket";
            project.Cloud.Region = "ap-chengdu";
            project.Cloud.RootPrefix = "videos";
            project.Save();

            var exception = Assert.Throws<CatalogException>(() =>
                new CatalogClient(project.StoryMedia).SearchAsync(
                        MediaKind.Video,
                        string.Empty,
                        null,
                        20,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult());

            Assert.AreEqual(CatalogErrorKind.InvalidSettings, exception.Kind);
            StringAssert.Contains("阿里云 OSS Region", exception.Message);
        }

        [Test]
        public void CredentialStore_SaveReloadsProviderProfileAndLeavesNoTemporaryFile()
        {
            var path = IOPath.Combine(TempDirectory, "cloud-credentials.json");
            var store = new CloudCredentialStore(path);
            store.Save(
                CloudProviderId.AliyunOss,
                "publisher",
                new CloudCredential("access-key-sentinel", "secret-key-sentinel", "token-sentinel"));

            var reloaded = new CloudCredentialStore(path);
            Assert.IsTrue(reloaded.TryGet(
                CloudProviderId.AliyunOss,
                "publisher",
                out var credential));
            Assert.AreEqual("access-key-sentinel", credential.AccessKeyId);
            Assert.AreEqual("secret-key-sentinel", credential.SecretAccessKey);
            Assert.AreEqual("token-sentinel", credential.SessionToken);
            CollectionAssert.AreEqual(
                new[] { "publisher" },
                reloaded.GetProfileNames(CloudProviderId.AliyunOss));
            Assert.IsEmpty(IODirectory.GetFiles(TempDirectory, "*.tmp"));
        }

        [Test]
        public void CredentialStore_SameProfileNameKeepsProviderCredentialsIndependent()
        {
            var path = IOPath.Combine(TempDirectory, "cloud-credentials.json");
            var store = new CloudCredentialStore(path);
            store.Save(
                CloudProviderId.TencentCos,
                "default",
                new CloudCredential("cos-secret-id", "cos-secret-key"));
            store.Save(
                CloudProviderId.AliyunOss,
                "default",
                new CloudCredential("oss-access-key-id", "oss-access-key-secret"));

            Assert.IsTrue(store.TryGet(CloudProviderId.TencentCos, "default", out var cos));
            Assert.IsTrue(store.TryGet(CloudProviderId.AliyunOss, "default", out var oss));
            Assert.AreEqual("cos-secret-id", cos.AccessKeyId);
            Assert.AreEqual("cos-secret-key", cos.SecretAccessKey);
            Assert.AreEqual("oss-access-key-id", oss.AccessKeyId);
            Assert.AreEqual("oss-access-key-secret", oss.SecretAccessKey);
        }

        [Test]
        public void CredentialStore_WhenDocumentIsDamaged_RefusesReadAndSaveWithoutOverwrite()
        {
            var path = IOPath.Combine(TempDirectory, "cloud-credentials.json");
            const string damaged = "{ damaged credential json";
            IOFile.WriteAllText(path, damaged);
            var store = new CloudCredentialStore(path);

            var readException = Assert.Throws<CloudException>(() => store.TryGet(
                CloudProviderId.TencentCos,
                "publisher",
                out _));
            var saveException = Assert.Throws<CloudException>(() => store.Save(
                CloudProviderId.TencentCos,
                "publisher",
                new CloudCredential("access", "secret")));

            Assert.AreEqual(CloudFailureKind.InvalidConfiguration, readException.Kind);
            Assert.AreEqual(CloudFailureKind.InvalidConfiguration, saveException.Kind);
            Assert.AreEqual(damaged, IOFile.ReadAllText(path));
        }

        [Test]
        public void CredentialStore_DefaultPathIsUnderCurrentUserAndOutsideUnityProject()
        {
            var expected = IOPath.GetFullPath(IOPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gamedeveloperkit",
                "cloud-credentials.json"));
            var projectRoot = IOPath.GetFullPath(IOPath.Combine(Application.dataPath, ".."));

            Assert.AreEqual(expected, IOPath.GetFullPath(CloudCredentialStore.CredentialsPath));
            Assert.IsFalse(expected.StartsWith(
                projectRoot + IOPath.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void CloudService_UploadObjectAsync_ResolvesProjectConfigAndCredentialProfile()
        {
            var localFile = IOPath.Combine(TempDirectory, "upload.txt");
            IOFile.WriteAllText(localFile, "upload");
            var credentialPath = IOPath.Combine(TempDirectory, "cloud-credentials.json");
            var store = new CloudCredentialStore(credentialPath);
            store.Save(
                CloudProviderId.TencentCos,
                "publisher",
                new CloudCredential("access", "secret"));
            var project = new CloudProjectConfig
            {
                ProviderId = CloudProviderId.TencentCos,
                CredentialProfileName = "publisher",
                Bucket = "video-bucket",
                Region = "ap-chengdu"
            };
            var provider = new RecordingProvider();
            var transport = new RecordingTransport();
            var service = new CloudService(
                new CloudProviderRegistry().Register(provider),
                transport,
                () => project,
                store);

            var result = service.UploadObjectAsync(
                    new CloudObjectUploadRequest(localFile, "videos/upload.txt", "text/plain"),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual("video-bucket", provider.Context.Bucket);
            Assert.AreEqual("access", provider.Context.Credential.AccessKeyId);
            Assert.AreEqual(1, transport.CallCount);
            Assert.AreEqual(CloudProviderId.TencentCos, result.ProviderId);
        }

        [Test]
        public void CloudService_InvalidAliyunRegionFailsBeforeHttpRequest()
        {
            var localFile = IOPath.Combine(TempDirectory, "invalid-region.txt");
            IOFile.WriteAllText(localFile, "upload");
            var store = new CloudCredentialStore(
                IOPath.Combine(TempDirectory, "invalid-region-credentials.json"));
            store.Save(
                CloudProviderId.AliyunOss,
                "publisher",
                new CloudCredential("access", "secret"));
            var project = new CloudProjectConfig
            {
                ProviderId = CloudProviderId.AliyunOss,
                CredentialProfileName = "publisher",
                Bucket = "video-bucket",
                Region = "ap-chengdu"
            };
            var transport = new RecordingTransport();
            var service = new CloudService(
                CloudProviderRegistry.CreateBuiltIn(),
                transport,
                () => project,
                store);

            var exception = Assert.Throws<CloudException>(() => service.UploadObjectAsync(
                    new CloudObjectUploadRequest(localFile, "videos/upload.txt", "text/plain"),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult());

            Assert.AreEqual(CloudFailureKind.InvalidConfiguration, exception.Kind);
            StringAssert.Contains("阿里云 OSS Region", exception.Message);
            Assert.AreEqual(0, transport.CallCount);
        }

        [Test]
        public void CloudConfigurationPanel_SwitchingProviderLoadsAndLabelsIndependentCredentials()
        {
            var path = IOPath.Combine(TempDirectory, "provider-panel-credentials.json");
            var store = new CloudCredentialStore(path);
            store.Save(
                CloudProviderId.TencentCos,
                "default",
                new CloudCredential("cos-secret-id", "cos-secret-key"));
            store.Save(
                CloudProviderId.AliyunOss,
                "default",
                new CloudCredential("oss-access-key-id", "oss-access-key-secret"));
            var panel = new CloudConfigurationPanel(EditorGlobalConfig.LoadOrCreate(), store);
            var provider = panel.Q<DropdownField>("cloud-provider-field");
            var accessKey = panel.Q<TextField>("cloud-access-key-field");
            var secretKey = panel.Q<TextField>("cloud-secret-key-field");
            var token = panel.Q<TextField>("cloud-session-token-field");
            var header = panel.Q<Label>("cloud-credential-header");

            Assert.AreEqual("default", panel.Q<TextField>("cloud-profile-field").value);
            Assert.AreEqual("SecretId", accessKey.label);
            Assert.AreEqual("SecretKey", secretKey.label);
            Assert.AreEqual("Session Token（可选）", token.label);
            StringAssert.Contains("腾讯 COS / default", header.text);
            Assert.AreEqual("cos-secret-id", accessKey.value);

            provider.value = CloudProviderId.AliyunOss;
            InvokePanelMethod(panel, "RefreshCredentialFields");

            Assert.AreEqual("AccessKey ID", accessKey.label);
            Assert.AreEqual("AccessKey Secret", secretKey.label);
            Assert.AreEqual("Security Token（可选）", token.label);
            StringAssert.Contains("阿里云 OSS / default", header.text);
            Assert.AreEqual("oss-access-key-id", accessKey.value);
            Assert.AreEqual("oss-access-key-secret", secretKey.value);
        }

        [Test]
        public void CloudConfigurationPanel_SwitchingProviderPersistsActiveMediaStorageImmediately()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Cloud.ProviderId = CloudProviderId.TencentCos;
            project.Cloud.CredentialProfileName = "publisher";
            project.Cloud.Bucket = "video-bucket";
            project.Cloud.Region = "ap-chengdu";
            project.Cloud.RootPrefix = "videos";
            project.Save();
            var store = new CloudCredentialStore(
                IOPath.Combine(TempDirectory, "immediate-provider.json"));
            var panel = new CloudConfigurationPanel(project, store);
            var host = ScriptableObject.CreateInstance<CloudConfigurationPanelHostWindow>();
            try
            {
                host.rootVisualElement.Add(panel);
                host.Show();
                panel.Q<DropdownField>("cloud-provider-field").value = CloudProviderId.AliyunOss;

                EditorGlobalConfig.ResetInstance();
                var reloaded = EditorGlobalConfig.LoadOrCreate();
                Assert.AreEqual(
                    CloudProviderId.AliyunOss,
                    reloaded.Cloud.ProviderId);
                Assert.AreEqual(string.Empty, reloaded.Cloud.Bucket);
                Assert.AreEqual(string.Empty, reloaded.Cloud.Region);
                reloaded.Cloud.ProviderId = CloudProviderId.TencentCos;
                Assert.AreEqual("video-bucket", reloaded.Cloud.Bucket);
                Assert.AreEqual("ap-chengdu", reloaded.Cloud.Region);
                reloaded.Cloud.ProviderId = CloudProviderId.AliyunOss;
                var reopenedPanel = new CloudConfigurationPanel(reloaded, store);
                Assert.AreEqual(
                    CloudProviderId.AliyunOss,
                    reopenedPanel.Q<DropdownField>("cloud-provider-field").value);
            }
            finally
            {
                host.Close();
            }
        }

        [Test]
        public void CloudConfigurationPanel_SwitchingProviderLoadsItsOwnConnectionFields()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            project.Cloud.ProviderId = CloudProviderId.TencentCos;
            project.Cloud.CredentialProfileName = "cos-profile";
            project.Cloud.Bucket = "cos-bucket";
            project.Cloud.Region = "ap-chengdu";
            project.Cloud.RootPrefix = "cos-videos";
            project.Cloud.ProviderId = CloudProviderId.AliyunOss;
            project.Cloud.CredentialProfileName = "oss-profile";
            project.Cloud.Bucket = "oss-bucket";
            project.Cloud.Region = "cn-chengdu";
            project.Cloud.RootPrefix = "oss-videos";
            project.Cloud.ProviderId = CloudProviderId.TencentCos;
            project.Save();
            var panel = new CloudConfigurationPanel(
                project,
                new CloudCredentialStore(IOPath.Combine(TempDirectory, "provider-fields.json")));
            var host = ScriptableObject.CreateInstance<CloudConfigurationPanelHostWindow>();
            try
            {
                host.rootVisualElement.Add(panel);
                host.Show();
                Assert.AreEqual("cos-profile", panel.Q<TextField>("cloud-profile-field").value);
                Assert.AreEqual("cos-bucket", panel.Q<TextField>("cloud-bucket-field").value);
                Assert.AreEqual("ap-chengdu", panel.Q<TextField>("cloud-region-field").value);
                panel.Q<DropdownField>("cloud-provider-field").value = CloudProviderId.AliyunOss;

                Assert.AreEqual("oss-profile", panel.Q<TextField>("cloud-profile-field").value);
                Assert.AreEqual("oss-bucket", panel.Q<TextField>("cloud-bucket-field").value);
                Assert.AreEqual("cn-chengdu", panel.Q<TextField>("cloud-region-field").value);
                Assert.AreEqual("oss-videos", panel.Q<TextField>("cloud-root-prefix-field").value);
            }
            finally
            {
                host.Close();
            }
        }

        [Test]
        public void CloudConfigurationPanel_PersistsProjectAndCredentialStoresSeparately()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            var credentialPath = IOPath.Combine(TempDirectory, "panel-credentials.json");
            var store = new CloudCredentialStore(credentialPath);
            CloudProjectConfig generatedConfig = null;
            var panel = new CloudConfigurationPanel(
                project,
                store,
                config => generatedConfig = config);

            panel.Q<DropdownField>("cloud-provider-field").value = CloudProviderId.AliyunOss;
            panel.Q<TextField>("cloud-profile-field").SetValueWithoutNotify("publisher");
            panel.Q<TextField>("cloud-bucket-field").SetValueWithoutNotify("video-bucket");
            panel.Q<TextField>("cloud-region-field").SetValueWithoutNotify("cn-hangzhou");
            panel.Q<TextField>("cloud-endpoint-field").SetValueWithoutNotify(string.Empty);
            panel.Q<TextField>("cloud-root-prefix-field").SetValueWithoutNotify("videos/hls");
            panel.Q<TextField>("cloud-cdn-base-url-field")
                .SetValueWithoutNotify("https://cdn.example.com");
            InvokePanelMethod(panel, "SaveProjectConfiguration");
            Assert.AreSame(project.Cloud, generatedConfig);

            panel.Q<TextField>("cloud-access-key-field").SetValueWithoutNotify("access-sentinel");
            panel.Q<TextField>("cloud-secret-key-field").SetValueWithoutNotify("secret-sentinel");
            panel.Q<TextField>("cloud-session-token-field").SetValueWithoutNotify("token-sentinel");
            InvokePanelMethod(panel, "SaveCredentialProfile");

            EditorGlobalConfig.ResetInstance();
            var reloaded = EditorGlobalConfig.LoadOrCreate().Cloud;
            Assert.AreEqual(CloudProviderId.AliyunOss, reloaded.ProviderId);
            Assert.AreEqual("publisher", reloaded.CredentialProfileName);
            Assert.AreEqual("video-bucket", reloaded.Bucket);
            Assert.AreEqual("cn-hangzhou", reloaded.Region);
            Assert.AreEqual("videos/hls", reloaded.RootPrefix);
            Assert.AreEqual("https://cdn.example.com", reloaded.CdnBaseUrl);
            Assert.IsTrue(store.TryGet(CloudProviderId.AliyunOss, "publisher", out var credential));
            Assert.AreEqual("access-sentinel", credential.AccessKeyId);
            Assert.AreEqual("secret-sentinel", credential.SecretAccessKey);
            Assert.AreEqual("token-sentinel", credential.SessionToken);

            var serializedProject = IOFile.ReadAllText(EditorGlobalConfig.SettingsPath);
            StringAssert.DoesNotContain("access-sentinel", serializedProject);
            StringAssert.DoesNotContain("secret-sentinel", serializedProject);
            StringAssert.DoesNotContain("token-sentinel", serializedProject);
            Assert.IsTrue(panel.Q<TextField>("cloud-secret-key-field").isPasswordField);
            Assert.IsTrue(panel.Q<TextField>("cloud-session-token-field").isPasswordField);
            Assert.AreEqual(CloudCredentialStore.CredentialsPath, panel.Q<Label>("cloud-credential-path").text);
        }

        [Test]
        public void CloudConfigurationPanel_WhenCredentialFileDamaged_ShowsErrorWithoutOverwriting()
        {
            var path = IOPath.Combine(TempDirectory, "damaged-panel-credentials.json");
            const string damaged = "{ damaged credential json";
            IOFile.WriteAllText(path, damaged);
            var panel = new CloudConfigurationPanel(
                EditorGlobalConfig.LoadOrCreate(),
                new CloudCredentialStore(path));

            panel.Q<TextField>("cloud-profile-field").SetValueWithoutNotify("publisher");
            InvokePanelMethod(panel, "RefreshCredentialFields");
            var status = panel.Q<Label>("cloud-config-validation");

            Assert.AreEqual(DisplayStyle.Flex, status.style.display.value);
            StringAssert.Contains("invalid and was not changed", status.text);
            Assert.AreEqual(damaged, IOFile.ReadAllText(path));
        }

        private static void InvokePanelMethod(CloudConfigurationPanel panel, string methodName)
        {
            typeof(CloudConfigurationPanel)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(panel, null);
        }

        private sealed class RecordingProvider : ICloudProvider
        {
            public string ProviderId => CloudProviderId.TencentCos;

            public CloudProviderCapabilities Capabilities => CloudProviderCapabilities.PutObject;

            public CloudPutObjectContext Context { get; private set; }

            public void Validate(CloudPutObjectContext context)
            {
                Context = context;
            }

            public CloudHttpRequest CreatePutObjectRequest(CloudPutObjectContext context)
            {
                return new CloudHttpRequest(
                    new Uri("https://cos.example.com/videos/upload.txt"),
                    new Dictionary<string, string>(),
                    context.Request.ContentType);
            }

            public CloudUploadResult ParsePutObjectResponse(
                CloudPutObjectContext context,
                CloudHttpResponse response)
            {
                return new CloudUploadResult(
                    ProviderId,
                    context.Bucket,
                    context.Request.ObjectKey,
                    string.Empty,
                    string.Empty);
            }
        }

        private sealed class CloudConfigurationPanelHostWindow : EditorWindow
        {
        }

        private sealed class RecordingTransport : ICloudHttpTransport
        {
            public int CallCount { get; private set; }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return UniTask.FromResult(new CloudHttpResponse(
                    200,
                    new Dictionary<string, string>(),
                    string.Empty));
            }
        }
    }
}
