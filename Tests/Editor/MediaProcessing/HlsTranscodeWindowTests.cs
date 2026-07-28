using System;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Tests
{
    public sealed class HlsTranscodeWindowTests
    {
        private HlsTranscodeWindow m_Window;

        [SetUp]
        public void SetUp()
        {
            m_Window = ScriptableObject.CreateInstance<HlsTranscodeWindow>();
            Invoke("BuildUi");
            SetField("m_Toolchain", new FfmpegToolchainStatus(
                FfmpegToolchainState.Ready,
                FfmpegToolchainSource.Manual,
                "ffmpeg",
                "ffprobe",
                "ready",
                null));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_Window);
        }

        [Test]
        public void ApplySourceProbe_When2KSourceIs5Point2Mbps_CapsUiAt1080P()
        {
            ApplySourceProbe("source-a.mp4", 1, new MediaProbeInfo(
                2560,
                1440,
                30d,
                30d,
                5200000L,
                true));

            Assert.IsFalse(GetToggle("4K").enabledSelf);
            Assert.IsFalse(GetToggle("2K").enabledSelf);
            Assert.IsTrue(GetToggle("1080P").enabledSelf);
            Assert.IsTrue(GetToggle("720P").enabledSelf);
            StringAssert.Contains("超过源视频码率", GetToggle("2K").tooltip);
            StringAssert.Contains("最高可选：1080P · 4 Mbps", GetSourceInfo().text);
            Assert.IsTrue(GetTranscodeButton().enabledSelf);
        }

        [Test]
        public void ApplySourceProbe_WhenBelowLowestPreset_DisablesAllRenditionsAndGenerate()
        {
            ApplySourceProbe("source-low.mp4", 2, new MediaProbeInfo(
                1920,
                1080,
                30d,
                30d,
                349999L,
                true));

            Assert.IsTrue(GetToggles().All(toggle => toggle.enabledSelf is false));
            Assert.IsTrue(GetToggles().All(toggle => toggle.value is false));
            StringAssert.Contains("无可选档位", GetSourceInfo().text);
            Assert.IsFalse(GetTranscodeButton().enabledSelf);
        }

        [Test]
        public void ApplySourceProbe_WhenResultIsStale_DoesNotReplaceCurrentUi()
        {
            ApplySourceProbe("source-current.mp4", 5, new MediaProbeInfo(
                1920,
                1080,
                30d,
                30d,
                4000000L,
                true));
            var currentText = GetSourceInfo().text;

            var applied = InvokeApplySourceProbe(
                "source-old.mp4",
                4,
                new MediaProbeInfo(3840, 2160, 30d, 30d, 16000000L, true));

            Assert.IsFalse(applied);
            Assert.AreEqual(currentText, GetSourceInfo().text);
            Assert.IsFalse(GetToggle("4K").enabledSelf);
        }

        [Test]
        public void ApplySourceProbe_WhenReprobing_PreservesStillEligibleSelection()
        {
            ApplySourceProbe("source-first.mp4", 7, new MediaProbeInfo(
                1920,
                1080,
                30d,
                30d,
                4000000L,
                true));
            foreach (var toggle in GetToggles().Where(toggle =>
                         ((HlsRenditionPreset)toggle.userData).Label != "720P"))
            {
                toggle.value = false;
            }

            ApplySourceProbe("source-second.mp4", 8, new MediaProbeInfo(
                2560,
                1440,
                30d,
                30d,
                5200000L,
                true));

            Assert.IsTrue(GetToggle("720P").value);
            Assert.AreEqual(1, GetToggles().Count(toggle => toggle.value));
        }

        [Test]
        public void StartSourceProbe_WhenPathIsInvalid_DisablesGenerateAndReportsError()
        {
            var inputField = (TextField)GetField("m_InputField");
            inputField.SetValueWithoutNotify("\0");

            Assert.DoesNotThrow(() => typeof(HlsTranscodeWindow)
                .GetMethod("StartSourceProbe", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_Window, new object[] { true }));

            StringAssert.Contains("源视频路径无效", GetSourceInfo().text);
            Assert.IsTrue(GetToggles().All(toggle => toggle.enabledSelf is false));
            Assert.IsFalse(GetTranscodeButton().enabledSelf);
        }

        [Test]
        public void StartSourceProbe_WhenToolchainRecovers_RestoresRenditionsAfterSuccessfulProbe()
        {
            SetField("m_Toolchain", new FfmpegToolchainStatus(
                FfmpegToolchainState.Missing,
                FfmpegToolchainSource.None,
                null,
                null,
                "FFmpeg 工具链不可用。",
                null));
            var inputField = (TextField)GetField("m_InputField");
            inputField.SetValueWithoutNotify("source.mp4");

            typeof(HlsTranscodeWindow)
                .GetMethod("StartSourceProbe", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_Window, new object[] { true });

            StringAssert.Contains("工具链不可用", GetSourceInfo().text);
            Assert.IsTrue(GetToggles().All(toggle => toggle.enabledSelf is false));
            Assert.IsFalse(GetTranscodeButton().enabledSelf);

            SetField("m_Toolchain", new FfmpegToolchainStatus(
                FfmpegToolchainState.Ready,
                FfmpegToolchainSource.Manual,
                "ffmpeg",
                "ffprobe",
                "ready",
                null));
            ApplySourceProbe("source.mp4", 1, new MediaProbeInfo(
                1920,
                1080,
                30d,
                30d,
                4000000L,
                true));

            Assert.IsTrue(GetToggle("1080P").enabledSelf);
            Assert.IsTrue(GetTranscodeButton().enabledSelf);
        }

        [Test]
        public void AppendLog_WhenCapacityIsExceeded_KeepsLatestOutputWithinVertexBudget()
        {
            var log = (TextField)GetField("m_Log");
            log.value = new string('a', 10000);

            Invoke("AppendLog", "latest-output");

            Assert.LessOrEqual(log.value.Length, 10000);
            StringAssert.EndsWith("latest-output", log.value);
        }

        [Test]
        public void SetLogText_WhenCompletedOutputIsLarge_KeepsLatestOutputWithinVertexBudget()
        {
            var expectedTail = new string('b', 10000);

            Invoke("SetLogText", new string('a', 10000) + expectedTail);

            var log = (TextField)GetField("m_Log");
            Assert.AreEqual(10000, log.value.Length);
            Assert.AreEqual(expectedTail, log.value);
        }

        private void ApplySourceProbe(string path, int version, MediaProbeInfo source)
        {
            SetField("m_ProbeInputPath", path);
            SetField("m_ProbeVersion", version);
            Assert.IsTrue(InvokeApplySourceProbe(path, version, source));
        }

        private bool InvokeApplySourceProbe(string path, int version, MediaProbeInfo source)
        {
            return (bool)typeof(HlsTranscodeWindow)
                .GetMethod("TryApplySourceProbe", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_Window, new object[] { path, version, source });
        }

        private object Invoke(string method, params object[] arguments)
        {
            return typeof(HlsTranscodeWindow)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_Window, arguments);
        }

        private void SetField(string field, object value)
        {
            typeof(HlsTranscodeWindow)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(m_Window, value);
        }

        private object GetField(string field)
        {
            return typeof(HlsTranscodeWindow)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(m_Window);
        }

        private Toggle[] GetToggles()
        {
            return m_Window.rootVisualElement.Query<Toggle>()
                .ToList()
                .Where(toggle => toggle.name.StartsWith("hls-rendition-", StringComparison.Ordinal))
                .ToArray();
        }

        private Toggle GetToggle(string label)
        {
            return GetToggles().Single(toggle =>
                ((HlsRenditionPreset)toggle.userData).Label == label);
        }

        private Label GetSourceInfo()
        {
            return m_Window.rootVisualElement.Q<Label>("hls-source-info");
        }

        private Button GetTranscodeButton()
        {
            return m_Window.rootVisualElement.Q<Button>("hls-transcode-button");
        }
    }
}
