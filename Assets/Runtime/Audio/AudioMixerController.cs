using UnityEngine;
using UnityEngine.Audio;

namespace GameDeveloperKit.Audio
{
    /// <summary>
    /// Audio Mixer 控制器
    /// </summary>
    public class AudioMixerController
    {
        private readonly AudioMixer _mixer;

        public AudioMixerController(AudioMixer mixer)
        {
            _mixer = mixer;
        }

        /// <summary>
        /// 设置音量（线性 0-1）
        /// </summary>
        public void SetVolume(string parameterName, float linearVolume)
        {
            if (_mixer == null) return;

            float db = LinearToDecibel(linearVolume);
            _mixer.SetFloat(parameterName, db);
        }

        /// <summary>
        /// 获取音量（线性 0-1）
        /// </summary>
        public float GetVolume(string parameterName)
        {
            if (_mixer == null || !_mixer.GetFloat(parameterName, out float db))
            {
                return 1f;
            }

            return DecibelToLinear(db);
        }

        /// <summary>
        /// 设置主音量
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            SetVolume("MasterVolume", volume);
        }

        /// <summary>
        /// 获取主音量
        /// </summary>
        public float GetMasterVolume()
        {
            return GetVolume("MasterVolume");
        }

        /// <summary>
        /// 获取 Mixer Group
        /// </summary>
        public AudioMixerGroup GetGroup(string groupName)
        {
            if (_mixer == null) return null;

            var groups = _mixer.FindMatchingGroups(groupName);
            return groups.Length > 0 ? groups[0] : null;
        }

        /// <summary>
        /// 线性音量转换为分贝
        /// </summary>
        private float LinearToDecibel(float linear)
        {
            // Unity AudioMixer标准范围：-80dB ~ 0dB
            // 极小值（0.0001）对应 -80dB
            if (linear <= 0.0001f)
                return -80f;
            
            float db = 20f * Mathf.Log10(linear);
            return Mathf.Max(db, -80f); // 限制下限
        }

        /// <summary>
        /// 分贝转换为线性音量
        /// </summary>
        private float DecibelToLinear(float decibel)
        {
            return Mathf.Pow(10f, decibel / 20f);
        }
    }
}
