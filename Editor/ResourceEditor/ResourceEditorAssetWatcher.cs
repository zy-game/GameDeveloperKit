using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.ResourceEditor
{
    [InitializeOnLoad]
    internal sealed class ResourceEditorAssetWatcher : AssetPostprocessor
    {
        private static readonly HashSet<string> s_ImportedAssets = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> s_DeletedAssets = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<ResourceAssetMove> s_MovedAssets = new HashSet<ResourceAssetMove>();
        private static bool s_FullReconcile;
        private static bool s_Scheduled;

        static ResourceEditorAssetWatcher()
        {
            s_FullReconcile = true;
            ScheduleDrain();
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            AddPaths(s_ImportedAssets, importedAssets);
            AddPaths(s_DeletedAssets, deletedAssets);
            var movedCount = Math.Min(movedAssets?.Length ?? 0, movedFromAssetPaths?.Length ?? 0);
            for (var i = 0; i < movedCount; i++)
            {
                s_MovedAssets.Add(new ResourceAssetMove(movedFromAssetPaths[i], movedAssets[i]));
            }

            if ((movedAssets?.Length ?? 0) != (movedFromAssetPaths?.Length ?? 0))
            {
                Debug.LogError("[ResourceEditor] Asset move callback contained unpaired paths.");
            }

            if (s_ImportedAssets.Count > 0 || s_DeletedAssets.Count > 0 || s_MovedAssets.Count > 0)
            {
                ScheduleDrain();
            }
        }

        private static void ScheduleDrain()
        {
            if (s_Scheduled)
            {
                return;
            }

            s_Scheduled = true;
            EditorApplication.delayCall += Drain;
        }

        private static void Drain()
        {
            s_Scheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleDrain();
                return;
            }

            var changes = new ResourceAssetChangeSet(
                s_ImportedAssets,
                s_DeletedAssets,
                s_MovedAssets,
                s_FullReconcile);
            s_ImportedAssets.Clear();
            s_DeletedAssets.Clear();
            s_MovedAssets.Clear();
            s_FullReconcile = false;
            try
            {
                ResourceAuthoringService.Reconcile(changes);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ResourceEditor] Resource reconciliation failed: {exception}");
            }
        }

        private static void AddPaths(ISet<string> destination, IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return;
            }

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                destination.Add(path.Replace('\\', '/').Trim());
            }
        }
    }
}
