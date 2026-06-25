using GameDeveloperKit.Story;
using UnityEngine;

namespace GameDeveloperKit.Scripts.StoryTest
{
    /// <summary>
    /// Inspector bridge for FrameworkStartup user data.
    /// </summary>
    [CreateAssetMenu(fileName = "StoryTestRequest", menuName = "GameDeveloperKit/Story/Test Request")]
    public sealed class StoryTestRequestAsset : ScriptableObject
    {
        [SerializeField]
        private StoryProgramAsset m_ProgramAsset;

        [SerializeField]
        private string m_StoryId;

        [SerializeField]
        private string m_ChapterId;

        [SerializeField]
        private StoryPlayerView m_PlayerView;

        [SerializeField]
        private StoryPlayerView m_PlayerViewPrefab;

        /// <summary>
        /// Runtime story program asset.
        /// </summary>
        public StoryProgramAsset ProgramAsset => m_ProgramAsset;

        /// <summary>
        /// Registered story id.
        /// </summary>
        public string StoryId => m_StoryId;

        /// <summary>
        /// Optional chapter id.
        /// </summary>
        public string ChapterId => m_ChapterId;

        /// <summary>
        /// Optional scene player view.
        /// </summary>
        public StoryPlayerView PlayerView => m_PlayerView;

        /// <summary>
        /// Optional player view prefab.
        /// </summary>
        public StoryPlayerView PlayerViewPrefab => m_PlayerViewPrefab;

        /// <summary>
        /// Converts this asset into a runtime request.
        /// </summary>
        /// <returns>Runtime request.</returns>
        public StoryTestRequest ToRequest()
        {
            var program = m_ProgramAsset != null ? m_ProgramAsset.ToProgram() : null;
            return new StoryTestRequest(program, m_StoryId, m_ChapterId, m_PlayerView, m_PlayerViewPrefab);
        }
    }
}
