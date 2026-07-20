using GameDeveloperKit.Story;
using UnityEngine;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;

namespace GameDeveloperKit.Scripts.StoryTest
{
    /// <summary>
    /// Inspector bridge for FrameworkStartup user data.
    /// </summary>
    [CreateAssetMenu(fileName = "StoryTestRequest", menuName = "GameDeveloperKit/Story/Test Request")]
    public sealed class StoryTestRequestAsset : ScriptableObject
    {
        [SerializeField]
        private ProgramAsset m_ProgramAsset;

        [SerializeField]
        private string m_StoryId;

        [SerializeField]
        private string m_VolumeId;

        [SerializeField]
        private string m_EpisodeId;

        [SerializeField]
        private PlayerView m_PlayerView;

        [SerializeField]
        private PlayerView m_PlayerViewPrefab;

        /// <summary>
        /// Runtime story program asset.
        /// </summary>
        public ProgramAsset ProgramAsset => m_ProgramAsset;

        /// <summary>
        /// Registered story id.
        /// </summary>
        public string StoryId => m_StoryId;

        /// <summary>
        /// Volume id.
        /// </summary>
        public string VolumeId => m_VolumeId;

        /// <summary>
        /// Episode id.
        /// </summary>
        public string EpisodeId => m_EpisodeId;

        /// <summary>
        /// Optional scene player view.
        /// </summary>
        public PlayerView PlayerView => m_PlayerView;

        /// <summary>
        /// Optional player view prefab.
        /// </summary>
        public PlayerView PlayerViewPrefab => m_PlayerViewPrefab;

        /// <summary>
        /// Converts this asset into a runtime request.
        /// </summary>
        /// <returns>Runtime request.</returns>
        public StoryTestRequest ToRequest()
        {
            var program = m_ProgramAsset != null ? m_ProgramAsset.ToProgram() : null;
            return new StoryTestRequest(program, m_StoryId, m_VolumeId, m_EpisodeId, m_PlayerView, m_PlayerViewPrefab);
        }
    }
}
