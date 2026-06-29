using System;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Sound;
using UnityEngine;

namespace GameDeveloperKit
{
    /// <summary>
    /// FrameworkStartup module ready options.
    /// </summary>
    [Serializable]
    public sealed class FrameworkStartupModuleOptions
    {
        [SerializeField]
        private bool m_InitializeResource;

        [SerializeField]
        private ResourceSettings m_ResourceSettings = new ResourceSettings();

        [SerializeField]
        private bool m_ResolveConfigModule;

        [SerializeField]
        private bool m_ResolveDataModule;

        [SerializeField]
        private bool m_ResolveSoundModule;

        [SerializeField]
        private SoundMixerSettings m_SoundMixerSettings = new SoundMixerSettings();

        /// <summary>
        /// Whether ResourceModule should be explicitly initialized before entering the target procedure.
        /// </summary>
        public bool InitializeResource => m_InitializeResource;

        /// <summary>
        /// Optional resource settings used by ResourceModule.InitializeAsync.
        /// </summary>
        public ResourceSettings ResourceSettings => m_ResourceSettings;

        /// <summary>
        /// Whether ConfigModule should be resolved before entering the target procedure.
        /// </summary>
        public bool ResolveConfigModule => m_ResolveConfigModule;

        /// <summary>
        /// Whether DataModule should be resolved before entering the target procedure.
        /// </summary>
        public bool ResolveDataModule => m_ResolveDataModule;

        /// <summary>
        /// Whether SoundModule should be resolved and configured before entering the target procedure.
        /// </summary>
        public bool ResolveSoundModule => m_ResolveSoundModule;

        /// <summary>
        /// Sound mixer settings applied to SoundModule when sound is resolved.
        /// </summary>
        public SoundMixerSettings SoundMixerSettings => m_SoundMixerSettings;

    }
}
