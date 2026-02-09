using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Audio
{
    /// <summary>
    /// 音效序列播放器
    /// </summary>
    public class AudioSequencer
    {
        /// <summary>
        /// 播放音效序列（按顺序）
        /// </summary>
        public static async UniTask PlaySequence(string[] clipNames, float delayBetween = 0f, AudioConfig config = null)
        {
            foreach (var clipName in clipNames)
            {
                var track = await Game.Audio.PlayAsync(clipName, config);
                
                if (track == null) continue;

                // 等待播放完成
                while (track.IsPlaying)
                {
                    await UniTask.Yield();
                }

                // 延迟
                if (delayBetween > 0)
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(delayBetween));
                }
            }
        }

        /// <summary>
        /// 同时播放多个音效
        /// </summary>
        public static async UniTask<List<AudioTrack>> PlaySimultaneous(string[] clipNames, AudioConfig config = null)
        {
            var tasks = new List<UniTask<AudioTrack>>();

            foreach (var clipName in clipNames)
            {
                tasks.Add(Game.Audio.PlayAsync(clipName, config));
            }

            var tracks = await UniTask.WhenAll(tasks);
            return new List<AudioTrack>(tracks);
        }

        /// <summary>
        /// 随机播放一个音效（从列表中）
        /// </summary>
        public static async UniTask<AudioTrack> PlayRandom(string[] clipNames, AudioConfig config = null)
        {
            if (clipNames == null || clipNames.Length == 0) return null;

            int index = UnityEngine.Random.Range(0, clipNames.Length);
            return await Game.Audio.PlayAsync(clipNames[index], config);
        }
    }
}
