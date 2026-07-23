using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Model;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor.Authoring
{
    internal sealed class AuthoringProjectMutation
    {
        private readonly AuthoringAsset m_Project;

        public AuthoringProjectMutation(AuthoringAsset project)
        {
            m_Project = project ?? throw new ArgumentNullException(nameof(project));
        }

        public bool TryAdd(AuthoringVolumeAsset volume, out string error)
        {
            if (volume == null)
            {
                error = "Volume asset is missing.";
                return false;
            }

            var current = CopyVolumeAssets();
            if (current.Contains(volume))
            {
                error = "Volume asset is already referenced by this story project.";
                return false;
            }

            var owners = AuthoringProjectResolver.FindOwners(volume);
            if (owners.Count > 0)
            {
                error = "Volume asset is already referenced by another story project.";
                return false;
            }

            current.Add(volume);
            AuthoringUndo.Mutate(m_Project, "Add Story Volume", () => m_Project.ReplaceVolumeAssets(current));
            error = null;
            return true;
        }

        public bool TryRemove(AuthoringVolumeAsset volume, out string error)
        {
            var current = CopyVolumeAssets();
            if (volume == null || current.Remove(volume) is false)
            {
                error = "Volume asset is not referenced by this story project.";
                return false;
            }

            AuthoringUndo.Mutate(m_Project, "Remove Story Volume", () => m_Project.ReplaceVolumeAssets(current));
            error = null;
            return true;
        }

        public bool TryMove(AuthoringVolumeAsset volume, int targetIndex, out string error)
        {
            var current = CopyVolumeAssets();
            var sourceIndex = current.IndexOf(volume);
            if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= current.Count)
            {
                error = "Volume move is outside the story project.";
                return false;
            }

            current.RemoveAt(sourceIndex);
            current.Insert(targetIndex, volume);
            AuthoringUndo.Mutate(m_Project, "Reorder Story Volumes", () => m_Project.ReplaceVolumeAssets(current));
            error = null;
            return true;
        }

        private List<AuthoringVolumeAsset> CopyVolumeAssets()
        {
            var result = new List<AuthoringVolumeAsset>();
            for (var i = 0; i < m_Project.VolumeAssets.Count; i++)
            {
                result.Add(m_Project.VolumeAssets[i]);
            }

            return result;
        }
    }
}
