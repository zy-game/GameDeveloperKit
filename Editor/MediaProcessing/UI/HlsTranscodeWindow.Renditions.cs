using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.MediaEditor
{
    public sealed partial class HlsTranscodeWindow
    {
        private readonly List<Toggle> m_RenditionToggles = new List<Toggle>();
        private Label m_SourceInfo;
        private CancellationTokenSource m_ProbeCancellation;
        private MediaProbeInfo m_SourceProbe;
        private HlsRenditionEligibilityResult m_RenditionEligibility;
        private string m_ProbeInputPath;
        private int m_ProbeVersion;
        private bool m_IsProbing;
        private bool m_HasSuccessfulProbe;
        private bool m_ApplyingRenditionState;
        private bool m_RenditionControlsBusy;

        private VisualElement CreateRenditionRow()
        {
            var renditionRow = CreateRow();
            m_RenditionToggles.Clear();
            foreach (var preset in HlsRenditionPresets.Default)
            {
                var toggle = new Toggle(preset.Label + "\n" + FormatVideoBitrate(preset.VideoBitrate))
                {
                    name = "hls-rendition-" + preset.Label,
                    value = true,
                    userData = preset,
                    tooltip = preset.Label
                };
                m_RenditionToggles.Add(toggle);
                ConfigureRenditionToggle(toggle);
                renditionRow.Add(toggle);
            }

            return renditionRow;
        }

        private void ConfigureRenditionToggle(Toggle toggle)
        {
            toggle.style.width = 88f;
            toggle.style.height = 44f;
            toggle.style.minWidth = 76f;
            toggle.style.marginRight = 6f;
            toggle.style.paddingLeft = 10f;
            toggle.style.paddingRight = 10f;
            toggle.style.borderLeftWidth = 1f;
            toggle.style.borderRightWidth = 1f;
            toggle.style.borderTopWidth = 1f;
            toggle.style.borderBottomWidth = 1f;
            toggle.style.alignItems = Align.Center;
            toggle.style.justifyContent = Justify.Center;
            var input = toggle.Q<VisualElement>(className: "unity-toggle__input");
            if (input != null)
            {
                input.style.display = DisplayStyle.None;
            }

            var label = toggle.Q<Label>(className: "unity-toggle__label");
            if (label != null)
            {
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.flexGrow = 1f;
                label.style.whiteSpace = WhiteSpace.Normal;
            }

            toggle.RegisterValueChangedCallback(evt =>
            {
                if (m_ApplyingRenditionState is false &&
                    evt.newValue is false &&
                    m_RenditionToggles.All(item => item.value is false))
                {
                    toggle.value = true;
                    return;
                }

                ApplyRenditionToggleStyle(toggle, evt.newValue);
                UpdateTranscodeButtonState();
            });
            ApplyRenditionToggleStyle(toggle, toggle.value);
        }

        private void StartSourceProbe(bool force)
        {
            if (m_SourceInfo == null || m_InputField == null)
            {
                return;
            }

            var inputPath = (m_InputField.value ?? string.Empty).Trim();
            if (m_Toolchain?.IsReady != true)
            {
                ResetSourceProbe(m_Toolchain?.Message ?? "FFmpeg 工具链不可用。");
                return;
            }

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                ResetSourceProbe("请选择需要转码的 MP4 文件。");
                return;
            }

            string fullPath;
            try
            {
                fullPath = NormalizeSourcePath(inputPath);
            }
            catch (InvalidOperationException exception)
            {
                ResetSourceProbe(exception.Message);
                return;
            }

            if (force is false &&
                string.Equals(fullPath, m_ProbeInputPath, StringComparison.OrdinalIgnoreCase) &&
                (m_IsProbing || m_SourceProbe != null))
            {
                return;
            }

            CancelSourceProbe();
            m_ProbeInputPath = fullPath;
            m_SourceProbe = null;
            m_RenditionEligibility = null;
            m_IsProbing = true;
            m_SourceInfo.text = "正在探测源视频…";
            if (m_TaskStatus != null)
            {
                m_TaskStatus.text = "正在探测源视频。";
            }

            ApplyUnavailableRenditionState("等待源视频探测");
            var cancellation = new CancellationTokenSource();
            m_ProbeCancellation = cancellation;
            var version = ++m_ProbeVersion;
            Run(ProbeSourceAsync(fullPath, version, cancellation));
        }

        private async UniTask ProbeSourceAsync(
            string inputPath,
            int version,
            CancellationTokenSource cancellation)
        {
            try
            {
                var source = await new MediaProbeService().ProbeAsync(
                    m_Toolchain.FfprobePath,
                    inputPath,
                    cancellation.Token);
                TryApplySourceProbe(inputPath, version, source);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                if (IsCurrentProbe(inputPath, version))
                {
                    m_SourceProbe = null;
                    m_RenditionEligibility = null;
                    m_SourceInfo.text = "源视频探测失败：" + exception.Message;
                    if (m_TaskStatus != null)
                    {
                        m_TaskStatus.text = m_SourceInfo.text;
                    }

                    ApplyUnavailableRenditionState("源视频探测失败");
                }
            }
            finally
            {
                if (ReferenceEquals(m_ProbeCancellation, cancellation))
                {
                    m_ProbeCancellation = null;
                    m_IsProbing = false;
                    cancellation.Dispose();
                    UpdateRenditionControlsEnabledState();
                    UpdateTranscodeButtonState();
                }
            }
        }

        private bool TryApplySourceProbe(string inputPath, int version, MediaProbeInfo source)
        {
            if (source == null || IsCurrentProbe(inputPath, version) is false)
            {
                return false;
            }

            var selectedLabels = m_RenditionToggles
                .Where(toggle => toggle.value)
                .Select(toggle => ((HlsRenditionPreset)toggle.userData).Label)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var eligibility = HlsRenditionEligibilityPolicy.Evaluate(
                source,
                HlsRenditionPresets.Default);
            m_SourceProbe = source;
            m_RenditionEligibility = eligibility;
            m_ApplyingRenditionState = true;
            try
            {
                foreach (var toggle in m_RenditionToggles)
                {
                    var preset = (HlsRenditionPreset)toggle.userData;
                    var rendition = FindEligibility(preset);
                    toggle.value = rendition.IsEligible &&
                                   (m_HasSuccessfulProbe is false || selectedLabels.Contains(preset.Label));
                    toggle.tooltip = rendition.IsEligible
                        ? $"{preset.Label}，目标视频码率 {FormatVideoBitrate(preset.VideoBitrate)}"
                        : $"{preset.Label} 不可选：{rendition.IneligibilityReason}。";
                    ApplyRenditionToggleStyle(toggle, toggle.value);
                }

                if (eligibility.HighestEligiblePreset != null &&
                    m_RenditionToggles.All(toggle => toggle.value is false))
                {
                    var highest = m_RenditionToggles.First(toggle =>
                        ReferenceEquals(toggle.userData, eligibility.HighestEligiblePreset));
                    highest.value = true;
                    ApplyRenditionToggleStyle(highest, true);
                }
            }
            finally
            {
                m_ApplyingRenditionState = false;
            }

            m_HasSuccessfulProbe = true;
            m_SourceInfo.text = eligibility.HighestEligiblePreset == null
                ? $"源：{source.Width}×{source.Height} · {FormatVideoBitrate(source.VideoBitrate)}；无可选档位。"
                : $"源：{source.Width}×{source.Height} · {FormatVideoBitrate(source.VideoBitrate)}；" +
                  $"最高可选：{eligibility.HighestEligiblePreset.Label} · " +
                  FormatVideoBitrate(eligibility.HighestEligiblePreset.VideoBitrate);
            if (m_TaskStatus != null)
            {
                m_TaskStatus.text = eligibility.HighestEligiblePreset == null
                    ? "没有符合源视频分辨率和码率的固定档位。"
                    : "源视频探测完成。";
            }

            UpdateRenditionControlsEnabledState();
            UpdateTranscodeButtonState();
            return true;
        }

        private bool IsCurrentProbe(string inputPath, int version)
        {
            return version == m_ProbeVersion &&
                   string.Equals(inputPath, m_ProbeInputPath, StringComparison.OrdinalIgnoreCase);
        }

        private HlsRenditionEligibility FindEligibility(HlsRenditionPreset preset)
        {
            return m_RenditionEligibility.Renditions.First(rendition =>
                ReferenceEquals(rendition.Preset, preset));
        }

        private void ApplyUnavailableRenditionState(string reason)
        {
            m_ApplyingRenditionState = true;
            try
            {
                foreach (var toggle in m_RenditionToggles)
                {
                    toggle.value = false;
                    toggle.tooltip = reason;
                    toggle.SetEnabled(false);
                    ApplyRenditionToggleStyle(toggle, false);
                }
            }
            finally
            {
                m_ApplyingRenditionState = false;
            }

            UpdateTranscodeButtonState();
        }

        private void ResetSourceProbe(string message)
        {
            CancelSourceProbe();
            m_ProbeInputPath = null;
            m_SourceProbe = null;
            m_RenditionEligibility = null;
            if (m_SourceInfo != null)
            {
                m_SourceInfo.text = message;
            }

            ApplyUnavailableRenditionState(message);
        }

        private void CancelSourceProbe()
        {
            m_ProbeVersion++;
            m_IsProbing = false;
            var cancellation = m_ProbeCancellation;
            m_ProbeCancellation = null;
            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
            cancellation.Dispose();
        }

        private void SetRenditionControlsBusy(bool busy)
        {
            m_RenditionControlsBusy = busy;
            UpdateRenditionControlsEnabledState();
        }

        private void UpdateRenditionControlsEnabledState()
        {
            foreach (var toggle in m_RenditionToggles)
            {
                var preset = (HlsRenditionPreset)toggle.userData;
                var eligible = m_RenditionEligibility?.Renditions.FirstOrDefault(rendition =>
                    ReferenceEquals(rendition.Preset, preset));
                toggle.SetEnabled(
                    m_RenditionControlsBusy is false &&
                    m_IsProbing is false &&
                    eligible?.IsEligible == true);
            }
        }

        private void UpdateTranscodeButtonState()
        {
            if (m_TranscodeButton == null)
            {
                return;
            }

            var hasSelectedEligible = m_RenditionToggles.Any(toggle =>
                toggle.value && toggle.enabledSelf);
            m_TranscodeButton.SetEnabled(
                m_RenditionControlsBusy is false &&
                m_IsProbing is false &&
                m_Cancellation == null &&
                m_Toolchain?.IsReady == true &&
                m_SourceProbe != null &&
                hasSelectedEligible);
        }

        private void ValidateSourceProbeReady()
        {
            var inputPath = NormalizeSourcePath((m_InputField.value ?? string.Empty).Trim());
            if (m_SourceProbe == null ||
                m_IsProbing ||
                string.Equals(inputPath, m_ProbeInputPath, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new InvalidOperationException("请等待当前源视频探测完成。");
            }
        }

        private static string NormalizeSourcePath(string inputPath)
        {
            try
            {
                return Path.GetFullPath(inputPath).Replace('\\', '/');
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException ||
                exception is System.Security.SecurityException)
            {
                throw new InvalidOperationException("源视频路径无效。", exception);
            }
        }

        private static string FormatVideoBitrate(long bitrate)
        {
            if (bitrate >= 1000000L)
            {
                return (bitrate / 1000000d).ToString("0.##", CultureInfo.InvariantCulture) + " Mbps";
            }

            return (bitrate / 1000d).ToString("0.##", CultureInfo.InvariantCulture) + " Kbps";
        }

        private static void ApplyRenditionToggleStyle(Toggle toggle, bool selected)
        {
            var border = selected
                ? new Color(0.20f, 0.58f, 0.92f, 1f)
                : EditorGUIUtility.isProSkin
                    ? new Color(0.48f, 0.48f, 0.48f, 1f)
                    : new Color(0.40f, 0.40f, 0.40f, 1f);
            toggle.style.borderLeftColor = border;
            toggle.style.borderRightColor = border;
            toggle.style.borderTopColor = border;
            toggle.style.borderBottomColor = border;
            toggle.style.backgroundColor = selected
                ? EditorGUIUtility.isProSkin
                    ? new Color(0.15f, 0.42f, 0.68f, 1f)
                    : new Color(0.20f, 0.55f, 0.88f, 1f)
                : Color.clear;
            toggle.style.color = selected
                ? Color.white
                : EditorGUIUtility.isProSkin
                    ? new Color(0.84f, 0.84f, 0.84f, 1f)
                    : new Color(0.15f, 0.15f, 0.15f, 1f);
        }
    }
}
