using System;
using UnityEngine;

namespace GameDeveloperKit.Logger
{
    public sealed class DebugSettings
    {
        private int m_LogCapacity = 256;
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

        public bool OverlayEnabled { get; set; } = UnityEngine.Debug.isDebugBuild;

        public bool UploadEnabled { get; set; } = UnityEngine.Debug.isDebugBuild;

        public bool CommandEnabled { get; set; } = UnityEngine.Debug.isDebugBuild;

        public bool AnalyticsEnabled { get; set; } = true;

        public bool MetricsEnabled { get; set; } = true;
    }
}
