using System;
using GameDeveloperKit.Story;

namespace GameDeveloperKit.Scripts.StoryTest
{
    /// <summary>
    /// Request used by StoryTestProcedure to start a story playback.
    /// </summary>
    public sealed class StoryTestRequest
    {
        /// <summary>
        /// Creates a story test request from a runtime program.
        /// </summary>
        /// <param name="program">Runtime story program.</param>
        /// <param name="chapterId">Optional chapter id.</param>
        /// <param name="playerView">Optional scene player view.</param>
        public StoryTestRequest(
            StoryProgram program,
            string chapterId = null,
            StoryPlayerView playerView = null,
            StoryPlayerView playerViewPrefab = null)
            : this(program, program?.StoryId, chapterId, playerView, playerViewPrefab)
        {
        }

        /// <summary>
        /// Creates a story test request from an already registered story id.
        /// </summary>
        /// <param name="storyId">Registered story id.</param>
        /// <param name="chapterId">Optional chapter id.</param>
        /// <param name="playerView">Optional scene player view.</param>
        public StoryTestRequest(
            string storyId,
            string chapterId = null,
            StoryPlayerView playerView = null,
            StoryPlayerView playerViewPrefab = null)
            : this(null, storyId, chapterId, playerView, playerViewPrefab)
        {
        }

        /// <summary>
        /// Creates a story test request.
        /// </summary>
        /// <param name="program">Runtime story program.</param>
        /// <param name="storyId">Story id used for registered playback.</param>
        /// <param name="chapterId">Optional chapter id.</param>
        /// <param name="playerView">Optional scene player view.</param>
        public StoryTestRequest(
            StoryProgram program,
            string storyId,
            string chapterId,
            StoryPlayerView playerView,
            StoryPlayerView playerViewPrefab = null)
        {
            if (program == null && string.IsNullOrWhiteSpace(storyId))
            {
                throw new ArgumentException("StoryTestRequest requires a StoryProgram or story id.", nameof(storyId));
            }

            Program = program;
            StoryId = program != null ? program.StoryId : storyId;
            ChapterId = chapterId;
            PlayerView = playerView;
            PlayerViewPrefab = playerViewPrefab;
        }

        /// <summary>
        /// Runtime story program to register and play.
        /// </summary>
        public StoryProgram Program { get; }

        /// <summary>
        /// Story id for registered playback.
        /// </summary>
        public string StoryId { get; }

        /// <summary>
        /// Optional chapter id.
        /// </summary>
        public string ChapterId { get; }

        /// <summary>
        /// Optional scene player view.
        /// </summary>
        public StoryPlayerView PlayerView { get; }

        /// <summary>
        /// Optional player view prefab to instantiate when the scene does not contain one.
        /// </summary>
        public StoryPlayerView PlayerViewPrefab { get; }
    }
}
