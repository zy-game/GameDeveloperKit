using UnityEngine;
using UnityEngine.Audio;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Audio
{
    /// <summary>
    /// 音效管理器接口
    /// </summary>
    public interface IAudioManager : IModule
    {
        void SetAudioMixer(AudioMixer mixer);
        UniTask<AudioTrack> PlayAsync(string clipName, AudioConfig config = null);
        AudioTrack Play(AudioClip clip, AudioConfig config = null);
        void Stop(AudioTrack track);
        void StopAll();
        void Pause(AudioTrack track);
        void PauseAll();
        void Resume(AudioTrack track);
        void ResumeAll();
        void SetMasterVolume(float volume);
        void SetGroupVolume(string groupName, float volume);
        float GetMasterVolume();
        float GetGroupVolume(string groupName);
        void SetMute(bool mute);
        AudioMixerGroup GetMixerGroup(string groupName);
        AudioGroup CreateGroup(string groupName);
        AudioGroup GetGroup(string groupName);
        void StopGroup(string groupName);
        void PauseGroup(string groupName);
        void ResumeGroup(string groupName);
        int GetActiveTrackCount();
        (int available, int total) GetPoolStats();
    }
}
