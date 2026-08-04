using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// 剧情播放 UI surface 集合。
    /// </summary>
    public sealed class PlaybackSurfaceView
    {
        /// <summary>
        /// 初始化剧情播放 UI surface 集合。
        /// </summary>
        public PlaybackSurfaceView(
            RawImage imageOutput = null,
            TMP_Text speakerText = null,
            TMP_Text bodyText = null,
            Button continueButton = null,
            IReadOnlyList<Button> choiceButtons = null)
        {
            ImageOutput = imageOutput;
            SpeakerText = speakerText;
            BodyText = bodyText;
            ContinueButton = continueButton;
            ChoiceButtons = choiceButtons == null || choiceButtons.Count == 0
                ? Array.Empty<Button>()
                : new List<Button>(choiceButtons).AsReadOnly();
        }

        /// <summary>
        /// 图片输出。
        /// </summary>
        public RawImage ImageOutput { get; }

        /// <summary>
        /// 说话人文本。
        /// </summary>
        public TMP_Text SpeakerText { get; }

        /// <summary>
        /// 正文文本。
        /// </summary>
        public TMP_Text BodyText { get; }

        /// <summary>
        /// 继续按钮。
        /// </summary>
        public Button ContinueButton { get; }

        /// <summary>
        /// 选项按钮列表。
        /// </summary>
        public IReadOnlyList<Button> ChoiceButtons { get; }
    }
}
