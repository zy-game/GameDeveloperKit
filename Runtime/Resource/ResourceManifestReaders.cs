using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using Newtonsoft.Json;

namespace GameDeveloperKit.Resource
{
    internal static class ResourceManifestReader
    {
        public static async UniTask<ManifestInfo> ReadAsync(string location)
        {
            return Deserialize(await ReadBytesAsync(location));
        }

        internal static ManifestInfo Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new GameException("Manifest file is empty.");
            }

            try
            {
                var manifest = JsonConvert.DeserializeObject<ManifestInfo>(DecodeUtf8(bytes));
                return manifest ?? throw new GameException("Unable to deserialize manifest.");
            }
            catch (JsonException exception)
            {
                throw new GameException("Unable to deserialize manifest.", exception);
            }
        }

        internal static async UniTask<byte[]> ReadBytesAsync(string location)
        {
            if (Uri.TryCreate(location, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp)
                {
                    throw new GameException($"Remote resource transport must use HTTPS: {location}");
                }

                if (uri.Scheme == Uri.UriSchemeHttps)
                {
                    return await ReadDownloadedBytesAsync(location, "Manifest");
                }
            }

            if (Path.IsPathRooted(location))
            {
                using (var stream = await App.File.OpenExternalReadAsync(location))
                {
                    return await ReadAllBytesAsync(stream);
                }
            }

            using (var stream = await App.File.OpenPackagedReadAsync(location))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException($"Packaged resource manifest was not found: {location}", location);
                }

                return await ReadAllBytesAsync(stream);
            }
        }

        internal static async UniTask<byte[]> ReadDownloadedBytesAsync(string location, string source)
        {
            var operation = App.Download.DownloadAsync(location);
            Exception operationException = null;
            try
            {
                await operation.WaitCompletionAsync();
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    throw operation.Error ?? new GameException($"{source} download failed: {location}");
                }

                return await operation.ReadAsync();
            }
            catch (Exception exception)
            {
                operationException = exception;
                throw;
            }
            finally
            {
                try
                {
                    await App.Download.ReleaseAsync(operation);
                }
                catch (Exception cleanupException)
                {
                    if (operationException == null)
                    {
                        throw;
                    }

                    throw new AggregateException(
                        $"{source} download failed and its result could not be released: {location}",
                        operationException,
                        cleanupException);
                }
            }
        }

        internal static string DecodeUtf8(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes);
            return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }

        private static async UniTask<byte[]> ReadAllBytesAsync(Stream stream)
        {
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory);
                return memory.ToArray();
            }
        }
    }

    public sealed class ResourcePublishPointer
    {
        [JsonProperty("protocolVersion", Required = Required.Always)]
        public int ProtocolVersion { get; set; }

        [JsonProperty("channel", Required = Required.Always)]
        public string Channel { get; set; }

        [JsonProperty("platform", Required = Required.Always)]
        public string Platform { get; set; }

        [JsonProperty("version", Required = Required.Always)]
        public string Version { get; set; }

        [JsonProperty("manifestSha256", Required = Required.Always)]
        public string ManifestSha256 { get; set; }

        [JsonProperty("minimumClientBuild", Required = Required.Always)]
        public long MinimumClientBuild { get; set; }

        [JsonProperty("maximumClientBuild", Required = Required.Always)]
        public long MaximumClientBuild { get; set; }

        [JsonProperty("keyId", Required = Required.Always)]
        public string KeyId { get; set; }

        [JsonProperty("signature", Required = Required.Always)]
        public string Signature { get; set; }
    }

    internal static class ResourcePublishProtocol
    {
        internal const int CurrentProtocolVersion = ResourcePublishSigningContract.CurrentProtocolVersion;

        internal static void VerifyPointer(ResourcePublishPointer pointer, ResourceSettings settings)
        {
            if (pointer == null)
            {
                throw new ArgumentNullException(nameof(pointer));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            ValidatePointer(pointer, settings);
            var trustKey = settings.FindTrustedKey(pointer.KeyId);
            if (trustKey == null)
            {
                throw new GameException($"Resource publish key is not trusted: {pointer.KeyId}");
            }

            try
            {
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(new RSAParameters
                    {
                        Modulus = Convert.FromBase64String(trustKey.Modulus),
                        Exponent = Convert.FromBase64String(trustKey.Exponent)
                    });
                    var signature = Convert.FromBase64String(pointer.Signature);
                    if (!rsa.VerifyData(
                            BuildSigningPayload(pointer),
                            signature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1))
                    {
                        throw new GameException("Resource publish signature verification failed.");
                    }
                }
            }
            catch (GameException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is FormatException || exception is CryptographicException)
            {
                throw new GameException("Resource publish signing key or signature is invalid.", exception);
            }
        }

        internal static void VerifyManifest(
            ResourcePublishPointer pointer,
            byte[] manifestBytes,
            ManifestInfo manifest)
        {
            var actualHash = ComputeSha256(manifestBytes);
            if (!FixedTimeEquals(pointer.ManifestSha256, actualHash))
            {
                throw new GameException("Resource manifest SHA-256 does not match the signed publish pointer.");
            }

            if (manifest.FormatVersion != ManifestInfo.CurrentFormatVersion)
            {
                throw new GameException(
                    $"Resource manifest format '{manifest.FormatVersion}' is not supported.");
            }

            if (!string.Equals(manifest.Version, pointer.Version, StringComparison.Ordinal))
            {
                throw new GameException(
                    $"Resource manifest version '{manifest.Version}' does not match publish version '{pointer.Version}'.");
            }
        }

        internal static byte[] BuildSigningPayload(ResourcePublishPointer pointer)
        {
            return ResourcePublishSigningContract.BuildPayload(
                pointer.ProtocolVersion,
                pointer.Channel,
                pointer.Platform,
                pointer.Version,
                pointer.ManifestSha256,
                pointer.MinimumClientBuild,
                pointer.MaximumClientBuild);
        }

        internal static string ComputeSha256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes ?? throw new ArgumentNullException(nameof(bytes)));
                var text = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return text.ToString();
            }
        }

        private static void ValidatePointer(ResourcePublishPointer pointer, ResourceSettings settings)
        {
            if (pointer.ProtocolVersion != CurrentProtocolVersion)
            {
                throw new GameException(
                    $"Resource publish protocol '{pointer.ProtocolVersion}' is not supported.");
            }

            ValidateText(pointer.Channel, nameof(pointer.Channel));
            ValidateText(pointer.Platform, nameof(pointer.Platform));
            ValidateText(pointer.Version, nameof(pointer.Version));
            ValidateText(pointer.KeyId, nameof(pointer.KeyId));
            ValidateText(pointer.Signature, nameof(pointer.Signature));
            if (pointer.ManifestSha256 == null || pointer.ManifestSha256.Length != 64 ||
                !pointer.ManifestSha256.All(IsHex))
            {
                throw new GameException("Resource publish manifestSha256 must be a 64-character hexadecimal SHA-256.");
            }

            if (pointer.MinimumClientBuild <= 0 ||
                pointer.MaximumClientBuild < pointer.MinimumClientBuild)
            {
                throw new GameException("Resource publish client build range is invalid.");
            }

            if (settings.ClientBuild < pointer.MinimumClientBuild ||
                settings.ClientBuild > pointer.MaximumClientBuild)
            {
                throw new GameException(
                    $"Client build '{settings.ClientBuild}' is outside resource range " +
                    $"'{pointer.MinimumClientBuild}-{pointer.MaximumClientBuild}'.");
            }

            if (!string.Equals(pointer.Channel, settings.ResolveChannelSegment(), StringComparison.Ordinal) ||
                !string.Equals(pointer.Platform, ResourceSettings.ResolvePlatformSegment(), StringComparison.Ordinal))
            {
                throw new GameException("Resource publish channel or platform does not match this client.");
            }
        }

        private static void ValidateText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                throw new GameException($"Resource publish field '{name}' is invalid.");
            }
        }

        private static bool FixedTimeEquals(string expected, string actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Length)
            {
                return false;
            }

            var difference = 0;
            for (var i = 0; i < expected.Length; i++)
            {
                difference |= char.ToLowerInvariant(expected[i]) ^ char.ToLowerInvariant(actual[i]);
            }

            return difference == 0;
        }

        private static bool IsHex(char value)
            => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }

    internal static class ResourcePublishPointerReader
    {
        public static async UniTask<ResourcePublishPointer> ReadAsync(string location)
        {
            var bytes = await ReadPublishBytesAsync(location);
            if (bytes == null || bytes.Length == 0)
            {
                throw new GameException("Publish file is empty.");
            }

            try
            {
                return JsonConvert.DeserializeObject<ResourcePublishPointer>(
                           ResourceManifestReader.DecodeUtf8(bytes)) ??
                       throw new GameException("Unable to deserialize resource publish pointer.");
            }
            catch (JsonException exception)
            {
                throw new GameException("Unable to deserialize resource publish pointer.", exception);
            }
        }

        private static async UniTask<byte[]> ReadPublishBytesAsync(string location)
        {
            if (Uri.TryCreate(location, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp)
                {
                    throw new GameException($"Remote resource transport must use HTTPS: {location}");
                }

                if (uri.Scheme == Uri.UriSchemeHttps)
                {
                    return await ResourceManifestReader.ReadDownloadedBytesAsync(location, "Publish");
                }
            }

            if (Path.IsPathRooted(location))
            {
                using (var stream = await App.File.OpenExternalReadAsync(location))
                using (var memory = new MemoryStream())
                {
                    await stream.CopyToAsync(memory);
                    return memory.ToArray();
                }
            }

            using (var stream = await App.File.OpenPackagedReadAsync(location))
            using (var memory = new MemoryStream())
            {
                if (stream == null)
                {
                    throw new FileNotFoundException(
                        $"Packaged resource publish file was not found: {location}",
                        location);
                }

                await stream.CopyToAsync(memory);
                return memory.ToArray();
            }
        }
    }

    internal static class ResourceRemoteManifestLoader
    {
        public static async UniTask<ManifestInfo> LoadAsync(ResourceSettings setting)
        {
            setting.ValidateRemoteSecurity();
            var publishLocation = App.Resource.GetPublishAddress(setting);
            App.Debug.Info($"Resource publish source. Mode: {setting.Mode}, Location: {publishLocation}");
            var pointer = await ResourcePublishPointerReader.ReadAsync(publishLocation);
            ResourcePublishProtocol.VerifyPointer(pointer, setting);

            var manifestLocation = App.Resource.GetManifestAddress(setting, pointer.Version);
            App.Debug.Info($"Resource manifest source. Mode: {setting.Mode}, Location: {manifestLocation}");
            var manifestBytes = await ResourceManifestReader.ReadBytesAsync(manifestLocation);
            var manifest = ResourceManifestReader.Deserialize(manifestBytes);
            ResourcePublishProtocol.VerifyManifest(pointer, manifestBytes, manifest);
            return manifest;
        }
    }
}
