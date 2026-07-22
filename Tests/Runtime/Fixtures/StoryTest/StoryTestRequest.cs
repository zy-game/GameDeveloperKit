using System;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Model;

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
        /// <param name="volumeId">Volume id.</param>
        /// <param name="episodeId">Episode id.</param>
        public StoryTestRequest(
            Program program,
            string volumeId,
            string episodeId)
            : this(program, program?.StoryId, volumeId, episodeId)
        {
        }

        /// <summary>
        /// Creates a story test request from an already registered story id.
        /// </summary>
        /// <param name="storyId">Registered story id.</param>
        /// <param name="volumeId">Volume id.</param>
        /// <param name="episodeId">Episode id.</param>
        public StoryTestRequest(
            string storyId,
            string volumeId,
            string episodeId)
            : this(null, storyId, volumeId, episodeId)
        {
        }

        /// <summary>
        /// Creates a story test request.
        /// </summary>
        /// <param name="program">Runtime story program.</param>
        /// <param name="storyId">Story id used for registered playback.</param>
        /// <param name="volumeId">Volume id.</param>
        /// <param name="episodeId">Episode id.</param>
        public StoryTestRequest(
            Program program,
            string storyId,
            string volumeId,
            string episodeId)
        {
            if (program == null && string.IsNullOrWhiteSpace(storyId))
            {
                throw new ArgumentException("StoryTestRequest requires a Program or story id.", nameof(storyId));
            }

            if (string.IsNullOrWhiteSpace(volumeId))
            {
                throw new ArgumentException("StoryTestRequest requires a volume id.", nameof(volumeId));
            }

            if (string.IsNullOrWhiteSpace(episodeId))
            {
                throw new ArgumentException("StoryTestRequest requires an episode id.", nameof(episodeId));
            }

            Program = program;
            StoryId = program != null ? program.StoryId : storyId;
            VolumeId = volumeId;
            EpisodeId = episodeId;
        }

        /// <summary>
        /// Runtime story program to register and play.
        /// </summary>
        public Program Program { get; }

        /// <summary>
        /// Story id for registered playback.
        /// </summary>
        public string StoryId { get; }

        /// <summary>
        /// Volume id.
        /// </summary>
        public string VolumeId { get; }

        /// <summary>
        /// Episode id.
        /// </summary>
        public string EpisodeId { get; }

    }
}
