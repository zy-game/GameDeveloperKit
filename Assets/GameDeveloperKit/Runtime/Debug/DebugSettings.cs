using System;
using UnityEngine;

namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Debug Settings 类型。
    /// </summary>
    public sealed class DebugSettings
    {
        /// <summary>
        /// 存储 Log Capacity。
        /// </summary>
        private int m_LogCapacity = 256;
        /// <summary>
        /// 存储 Metric Sample Interval。
        /// </summary>
        private float m_MetricSampleInterval = 0.5f;

        public int LogCapacity
        {
            get => m_LogCapacity;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Log capacity must be greater than zero.", nameof(value));
                }

                m_LogCapacity = value;
            }
        }

        public float MetricSampleInterval
        {
            get => m_MetricSampleInterval;
            set
            {
                if (value <= 0f)
                {
                    throw new ArgumentException("Metric sample interval must be greater than zero.", nameof(value));
                }

                m_MetricSampleInterval = value;
            }
        }

        public bool ConsoleEnabled { get; set; } = UnityEngine.Debug.isDebugBuild;

        public bool OverlayEnabled
        {
            get => ConsoleEnabled;
            set => ConsoleEnabled = value;
        }

        public bool CommandEnabled { get; set; } = UnityEngine.Debug.isDebugBuild;

        public bool UnityLogCaptureEnabled { get; set; } = UnityEngine.Debug.isDebugBuild;

        public bool RedactionEnabled { get; set; } = true;

        public bool MetricsEnabled { get; set; } = true;
    }
}
