using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    internal static class SnapshotStore
    {
        internal const string ManifestPath = "Library/GameDeveloperKit/ResourceEditor/manifest.json";

        public static void Commit(
            Snapshot snapshot,
            MutationPlan mutationPlan,
            Action saveSettings,
            string manifestPath = ManifestPath)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (mutationPlan == null)
            {
                throw new ArgumentNullException(nameof(mutationPlan));
            }

            if (saveSettings == null)
            {
                throw new ArgumentNullException(nameof(saveSettings));
            }

            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new ArgumentException("Manifest path cannot be empty.", nameof(manifestPath));
            }

            var fullPath = Path.GetFullPath(manifestPath);
            var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException(
                $"Unable to resolve manifest directory: {fullPath}");
            var tempPath = fullPath + ".tmp";
            var settingsSaved = false;
            try
            {
                IODirectory.CreateDirectory(directory);
                WriteTemp(tempPath, JsonConvert.SerializeObject(snapshot.Manifest, Formatting.Indented));
                if (mutationPlan.HasChanges)
                {
                    saveSettings();
                    settingsSaved = true;
                }

                Replace(tempPath, fullPath);
            }
            catch
            {
                if (settingsSaved is false)
                {
                    mutationPlan.Rollback();
                }

                throw;
            }
            finally
            {
                if (IOFile.Exists(tempPath))
                {
                    IOFile.Delete(tempPath);
                }
            }
        }

        private static void WriteTemp(string path, string content)
        {
            var bytes = new UTF8Encoding(false).GetBytes(content);
            using (var stream = new FileStream(
                       path,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static void Replace(string tempPath, string manifestPath)
        {
            if (IOFile.Exists(manifestPath))
            {
                IOFile.Replace(tempPath, manifestPath, null);
                return;
            }

            IOFile.Move(tempPath, manifestPath);
        }
    }
}
