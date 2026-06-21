using System;
using UnityEngine;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// StoryProcedure 进入参数。
    /// </summary>
    public sealed class StoryProcedureRequest
    {
        /// <summary>
        /// 已注册剧情 ID。
        /// </summary>
        public string StoryId { get; set; }

        /// <summary>
        /// 直接播放的剧情程序。
        /// </summary>
        public StoryProgram Program { get; set; }

        /// <summary>
        /// 入口章节 ID。
        /// </summary>
        public string ChapterId { get; set; }

        /// <summary>
        /// 场景中已有播放器。
        /// </summary>
        public StoryPlayerView PlayerView { get; set; }

        /// <summary>
        /// 播放器预制体。
        /// </summary>
        public StoryPlayerView PlayerPrefab { get; set; }

        /// <summary>
        /// 播放器实例父节点。
        /// </summary>
        public Transform Parent { get; set; }

        /// <summary>
        /// 退出 StoryProcedure 时是否销毁由本流程实例化的播放器。
        /// </summary>
        public bool DestroyInstantiatedPlayerOnLeave { get; set; } = true;

        /// <summary>
        /// 创建已注册剧情播放请求。
        /// </summary>
        /// <param name="storyId">剧情 ID。</param>
        /// <param name="chapterId">章节 ID。</param>
        /// <returns>播放请求。</returns>
        public static StoryProcedureRequest Registered(string storyId, string chapterId = null)
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                throw new ArgumentException("Story id cannot be empty.", nameof(storyId));
            }

            return new StoryProcedureRequest
            {
                StoryId = storyId,
                ChapterId = chapterId
            };
        }

        /// <summary>
        /// 创建直接播放 StoryProgram 的请求。
        /// </summary>
        /// <param name="program">剧情程序。</param>
        /// <param name="chapterId">章节 ID。</param>
        /// <returns>播放请求。</returns>
        public static StoryProcedureRequest Direct(StoryProgram program, string chapterId = null)
        {
            return new StoryProcedureRequest
            {
                Program = program ?? throw new ArgumentNullException(nameof(program)),
                ChapterId = chapterId
            };
        }
    }
}
