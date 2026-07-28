using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using NUnit.Framework;
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
            StringAssert.DoesNotContain("secretAccessKey", serialized);
            StringAssert.DoesNotContain("sessionToken", serialized);
            Assert.IsFalse(typeof(CloudProjectConfig).GetProperties().Any(property =>
                property.Name.IndexOf("Secret", StringComparison.OrdinalIgnoreCase) >= 0 ||
                property.Name.IndexOf("Token", StringComparison.OrdinalIgnoreCase) >= 0 ||
                property.Name.IndexOf("AccessKey", StringComparison.OrdinalIgnoreCase) >= 0));
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
        public void CloudConfigurationPanel_PersistsProjectAndCredentialStoresSeparately()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            var credentialPath = IOPath.Combine(TempDirectory, "panel-credentials.json");
            var store = new CloudCredentialStore(credentialPath);
            var panel = new CloudConfigurationPanel(project, store);

            panel.Q<DropdownField>("cloud-provider-field").value = CloudProviderId.AliyunOss;
            panel.Q<TextField>("cloud-profile-field").SetValueWithoutNotify("publisher");
            panel.Q<TextField>("cloud-bucket-field").SetValueWithoutNotify("video-bucket");
            panel.Q<TextField>("cloud-region-field").SetValueWithoutNotify("cn-hangzhou");
            panel.Q<TextField>("cloud-endpoint-field").SetValueWithoutNotify(string.Empty);
            panel.Q<TextField>("cloud-root-prefix-field").SetValueWithoutNotify("videos/hls");
            InvokePanelMethod(panel, "SaveProjectConfiguration");

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
