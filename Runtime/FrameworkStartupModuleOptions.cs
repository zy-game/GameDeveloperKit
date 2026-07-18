using System;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Resource;
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
        private bool m_ResolvePlayableModule;

        [SerializeField]
        private AudioMixerSettings m_AudioMixerSettings = new AudioMixerSettings();

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
        /// Whether PlayableModule should be resolved and configured before entering the target procedure.
        /// </summary>
        public bool ResolvePlayableModule => m_ResolvePlayableModule;

        /// <summary>
        /// Audio mixer settings applied when PlayableModule is resolved.
        /// </summary>
        public AudioMixerSettings AudioMixerSettings => m_AudioMixerSettings;

    }
}
