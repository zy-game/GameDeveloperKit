using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    internal static class WebGLHlsBuildProcessor
    {
        private const string ScriptAssetSuffix =
            "/Plugins/AVProVideo/Runtime/Plugins/WebGL/hls.min.js.txt";
        private const string LicenseAssetSuffix =
            "/Plugins/AVProVideo/Runtime/Plugins/WebGL/hls-LICENSE.txt";
        private const string DisabledAutoSyncSetting =
            "// config.autoSyncPersistentDataPath = true;";
        private const string EnabledAutoSyncSetting =
            "config.autoSyncPersistentDataPath = true;";

        [PostProcessBuild(1000)]
        private static void CopyHlsRuntime(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.WebGL)
            {
                return;
            }

            var script = LoadRequiredTextAsset(ScriptAssetSuffix);
            var license = LoadRequiredTextAsset(LicenseAssetSuffix);
            var outputDirectory = Path.Combine(pathToBuiltProject, "StreamingAssets", "AVProVideo");
            Directory.CreateDirectory(outputDirectory);
            System.IO.File.WriteAllBytes(Path.Combine(outputDirectory, "hls.min.js"), script.bytes);
            System.IO.File.WriteAllBytes(Path.Combine(outputDirectory, "hls-LICENSE.txt"), license.bytes);
            EnableAutomaticPersistentDataSync(pathToBuiltProject);
            Debug.Log($"[WebGLHlsBuildProcessor] Copied hls.js runtime to '{outputDirectory}'.");
        }

        private static void EnableAutomaticPersistentDataSync(string buildPath)
        {
            var indexPath = Path.Combine(buildPath, "index.html");
            if (!System.IO.File.Exists(indexPath))
            {
                throw new FileNotFoundException("WebGL build index was not found.", indexPath);
            }

            var html = System.IO.File.ReadAllText(indexPath);
            if (html.Contains(DisabledAutoSyncSetting))
            {
                html = html.Replace(DisabledAutoSyncSetting, EnabledAutoSyncSetting);
                System.IO.File.WriteAllText(indexPath, html, new UTF8Encoding(false));
                return;
            }

            if (!html.Contains(EnabledAutoSyncSetting))
            {
                throw new InvalidDataException(
                    "WebGL template does not expose config.autoSyncPersistentDataPath.");
            }
        }

        private static TextAsset LoadRequiredTextAsset(string assetSuffix)
        {
            var paths = AssetDatabase.GetAllAssetPaths();
            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i].Replace('\\', '/');
                if (path.EndsWith(assetSuffix, StringComparison.OrdinalIgnoreCase) is false)
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(paths[i]);
                if (asset != null && asset.bytes.Length > 0)
                {
                    return asset;
                }
            }

            throw new FileNotFoundException($"Required WebGL HLS asset was not found: {assetSuffix}");
        }
    }
}
