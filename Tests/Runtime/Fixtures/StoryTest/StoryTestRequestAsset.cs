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
        private string m_ChapterId;

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
        /// Optional chapter id.
        /// </summary>
        public string ChapterId => m_ChapterId;

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
            return new StoryTestRequest(program, m_StoryId, m_ChapterId, m_PlayerView, m_PlayerViewPrefab);
        }
    }
}
