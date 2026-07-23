using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor.Model
{
    internal static class AuthoringProjectResolver
    {
        public static bool TryResolveOwner(
            AuthoringVolumeAsset volume,
            out AuthoringAsset project,
            out string error)
        {
            project = null;
            error = null;
            if (volume == null)
            {
                error = "Volume asset is missing.";
                return false;
            }

            var owners = FindOwners(volume);
            if (owners.Count == 1)
            {
                project = owners[0];
                return true;
            }

            if (owners.Count == 0)
            {
                error = $"Volume asset is not referenced by a story project: {AssetDatabase.GetAssetPath(volume)}";
                return false;
            }

            var builder = new StringBuilder("Volume asset is referenced by multiple story projects:");
            for (var i = 0; i < owners.Count; i++)
            {
                builder.Append(' ');
                builder.Append(owners[i].StoryId);
                builder.Append(" (");
                builder.Append(AssetDatabase.GetAssetPath(owners[i]));
                builder.Append(')');
            }

            error = builder.ToString();
            return false;
        }

        internal static IReadOnlyList<AuthoringAsset> FindOwners(AuthoringVolumeAsset volume)
        {
            var owners = new List<AuthoringAsset>();
            if (volume == null)
            {
                return owners;
            }

            var guids = AssetDatabase.FindAssets($"t:{nameof(AuthoringAsset)}");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var candidate = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(path);
                if (candidate == null)
                {
                    continue;
                }

                var volumeAssets = candidate.VolumeAssets;
                for (var volumeIndex = 0; volumeIndex < volumeAssets.Count; volumeIndex++)
                {
                    if (volumeAssets[volumeIndex] == volume)
                    {
                        owners.Add(candidate);
                        break;
                    }
                }
            }

            return owners;
        }
    }
}
