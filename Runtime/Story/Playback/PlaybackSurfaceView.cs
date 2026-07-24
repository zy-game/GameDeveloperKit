using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
            RawImage videoOutput = null,
            RawImage imageOutput = null,
            TMP_Text speakerText = null,
            TMP_Text bodyText = null,
            Button continueButton = null,
            IReadOnlyList<Button> choiceButtons = null,
            VideoSeekSurface videoSeek = null,
            VideoQualitySurface videoQuality = null)
        {
            VideoOutput = videoOutput;
            ImageOutput = imageOutput;
            SpeakerText = speakerText;
            BodyText = bodyText;
            ContinueButton = continueButton;
            ChoiceButtons = choiceButtons ?? Array.Empty<Button>();
            VideoSeek = videoSeek;
            VideoQuality = videoQuality;
        }

        /// <summary>
        /// 视频输出。
        /// </summary>
        public RawImage VideoOutput { get; }

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

        /// <summary>
        /// 视频 seek 控件。
        /// </summary>
        public VideoSeekSurface VideoSeek { get; }

        public VideoQualitySurface VideoQuality { get; }
    }

    public sealed class VideoQualitySurface
    {
        public VideoQualitySurface(
            RectTransform root,
            Button button,
            TMP_Text label = null,
            RectTransform menuRoot = null,
            RectTransform optionsRoot = null,
            Button optionTemplate = null)
        {
            Root = root != null ? root : button != null ? button.transform as RectTransform : null;
            Button = button;
            Label = label;
            MenuRoot = menuRoot;
            OptionsRoot = optionsRoot;
            OptionTemplate = optionTemplate;
        }

        public RectTransform Root { get; }

        public Button Button { get; }

        public TMP_Text Label { get; }

        public RectTransform MenuRoot { get; }

        public RectTransform OptionsRoot { get; }

        public Button OptionTemplate { get; }
    }

    /// <summary>
    /// 视频 seek UI surface。
    /// </summary>
    public sealed class VideoSeekSurface
    {
        /// <summary>
        /// 初始化视频 seek UI surface。
        /// </summary>
        public VideoSeekSurface(RectTransform root, Slider slider, TMP_Text timeText = null, Button pauseButton = null)
        {
            Root = root != null ? root : slider != null ? slider.transform as RectTransform : null;
            Slider = slider;
            TimeText = timeText;
            PauseButton = pauseButton;
        }

        /// <summary>
        /// 根节点。
        /// </summary>
        public RectTransform Root { get; }

        /// <summary>
        /// 时间条。
        /// </summary>
        public Slider Slider { get; }

        /// <summary>
        /// 时间文本。
        /// </summary>
        public TMP_Text TimeText { get; }

        /// <summary>
        /// 暂停 / 继续按钮。
        /// </summary>
        public Button PauseButton { get; }
    }
}
