using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOFileInfo = System.IO.FileInfo;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.EditorCloud
{
    public sealed class CloudCredentialStore
    {
        private const int CurrentVersion = 1;
        private readonly string m_Path;

        public CloudCredentialStore()
            : this(CredentialsPath)
        {
        }

        internal CloudCredentialStore(string path)
        {
            m_Path = IOPath.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        }

        public static string CredentialsPath => IOPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".gamedeveloperkit",
            "cloud-credentials.json");

        public string Path => m_Path;

        public IReadOnlyList<string> GetProfileNames(string providerId)
        {
            var normalizedProviderId = NormalizeKey(providerId, "provider ID");
            return LoadDocument().Profiles
                .Where(profile => string.Equals(
                    profile.ProviderId,
                    normalizedProviderId,
                    StringComparison.Ordinal))
                .Select(profile => profile.ProfileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        public bool TryGet(string providerId, string profileName, out CloudCredential credential)
        {
            var normalizedProviderId = NormalizeKey(providerId, "provider ID");
            var normalizedProfileName = NormalizeKey(profileName, "credential profile name");
            var profile = LoadDocument().Profiles.FirstOrDefault(item =>
                string.Equals(item.ProviderId, normalizedProviderId, StringComparison.Ordinal) &&
                string.Equals(item.ProfileName, normalizedProfileName, StringComparison.Ordinal));
            if (profile == null)
            {
                credential = null;
                return false;
            }

            credential = new CloudCredential(
                profile.AccessKeyId,
                profile.SecretAccessKey,
                profile.SessionToken);
            return true;
        }

        public void Save(string providerId, string profileName, CloudCredential credential)
        {
            var normalizedProviderId = NormalizeKey(providerId, "provider ID");
            var normalizedProfileName = NormalizeKey(profileName, "credential profile name");
            ValidateCredential(credential, normalizedProviderId);

            var document = LoadDocument();
            var profile = document.Profiles.FirstOrDefault(item =>
                string.Equals(item.ProviderId, normalizedProviderId, StringComparison.Ordinal) &&
                string.Equals(item.ProfileName, normalizedProfileName, StringComparison.Ordinal));
            if (profile == null)
            {
                profile = new CredentialProfileDocument();
                document.Profiles.Add(profile);
            }

            profile.ProviderId = normalizedProviderId;
            profile.ProfileName = normalizedProfileName;
            profile.AccessKeyId = credential.AccessKeyId;
            profile.SecretAccessKey = credential.SecretAccessKey;
            profile.SessionToken = credential.SessionToken;
            document.Profiles = document.Profiles
                .OrderBy(item => item.ProviderId, StringComparer.Ordinal)
                .ThenBy(item => item.ProfileName, StringComparer.Ordinal)
                .ToList();
            WriteAtomic(JsonConvert.SerializeObject(document, Formatting.Indented));
        }

        private CredentialDocument LoadDocument()
        {
            if (IOFile.Exists(m_Path) is false)
            {
                return new CredentialDocument();
            }

            try
            {
                var document = JsonConvert.DeserializeObject<CredentialDocument>(
                    IOFile.ReadAllText(m_Path, Encoding.UTF8));
                ValidateDocument(document);
                return document;
            }
            catch (CloudException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw InvalidCredentialFile(exception);
            }
        }

        private void ValidateDocument(CredentialDocument document)
        {
            if (document == null || document.Version != CurrentVersion || document.Profiles == null)
            {
                throw InvalidCredentialFile();
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in document.Profiles)
            {
                if (profile == null)
                {
                    throw InvalidCredentialFile();
                }

                profile.ProviderId = NormalizeKey(profile.ProviderId, "provider ID");
                profile.ProfileName = NormalizeKey(profile.ProfileName, "credential profile name");
                profile.AccessKeyId = profile.AccessKeyId?.Trim() ?? string.Empty;
                profile.SecretAccessKey = profile.SecretAccessKey ?? string.Empty;
                profile.SessionToken = profile.SessionToken ?? string.Empty;
                ValidateCredential(
                    new CloudCredential(
                        profile.AccessKeyId,
                        profile.SecretAccessKey,
                        profile.SessionToken),
                    profile.ProviderId);

                if (keys.Add(profile.ProviderId + "\n" + profile.ProfileName) is false)
                {
                    throw InvalidCredentialFile();
                }
            }
        }

        private void WriteAtomic(string content)
        {
            var directory = IOPath.GetDirectoryName(m_Path) ?? string.Empty;
            IODirectory.CreateDirectory(directory);
            var temporaryPath = IOPath.Combine(
                directory,
                $".{IOPath.GetFileName(m_Path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                IOFile.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
                if (IOFile.Exists(m_Path))
                {
                    IOFile.Replace(temporaryPath, m_Path, null);
                }
                else
                {
                    IOFile.Move(temporaryPath, m_Path);
                }

                RestrictToCurrentUser(m_Path);
            }
            catch (Exception exception)
            {
                if (IOFile.Exists(temporaryPath))
                {
                    IOFile.Delete(temporaryPath);
                }

                throw new CloudException(
                    CloudFailureKind.LocalFile,
                    $"Unable to save cloud credential file: {m_Path}",
                    innerException: exception);
            }
        }

        private static void RestrictToCurrentUser(string path)
        {
#if UNITY_EDITOR_WIN
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var security = new System.Security.AccessControl.FileSecurity();
                security.SetAccessRuleProtection(true, false);
                security.SetOwner(identity.User);
                security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    identity.User,
                    System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));
                new IOFileInfo(path).SetAccessControl(security);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"Unable to restrict cloud credential file permissions: {path}. " +
                    exception.GetType().Name);
            }
#endif
        }

        private CloudException InvalidCredentialFile(Exception innerException = null)
        {
            return new CloudException(
                CloudFailureKind.InvalidConfiguration,
                $"Cloud credential file is invalid and was not changed: {m_Path}",
                innerException: innerException);
        }

        private static string NormalizeKey(string value, string label)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length == 0 ||
                normalized.IndexOf('/') >= 0 ||
                normalized.IndexOf('\\') >= 0 ||
                normalized.Any(char.IsControl))
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    $"Cloud {label} is invalid.");
            }

            return normalized;
        }

        private static void ValidateCredential(CloudCredential credential, string providerId)
        {
            if (credential == null ||
                string.IsNullOrWhiteSpace(credential.AccessKeyId) ||
                string.IsNullOrWhiteSpace(credential.SecretAccessKey))
            {
                throw new CloudException(
                    CloudFailureKind.CredentialsMissing,
                    $"Cloud credentials are missing for provider '{providerId}'.",
                    providerId);
            }
        }

        [Serializable]
        private sealed class CredentialDocument
        {
            [JsonProperty("version")]
            public int Version { get; set; } = CurrentVersion;

            [JsonProperty("profiles")]
            public List<CredentialProfileDocument> Profiles { get; set; } =
                new List<CredentialProfileDocument>();
        }

        [Serializable]
        private sealed class CredentialProfileDocument
        {
            [JsonProperty("providerId")]
            public string ProviderId { get; set; }

            [JsonProperty("profileName")]
            public string ProfileName { get; set; }

            [JsonProperty("accessKeyId")]
            public string AccessKeyId { get; set; }

            [JsonProperty("secretAccessKey")]
            public string SecretAccessKey { get; set; }

            [JsonProperty("sessionToken")]
            public string SessionToken { get; set; }
        }
    }
}
