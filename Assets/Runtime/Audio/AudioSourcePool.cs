using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Audio
{
    /// <summary>
    /// AudioSource 对象池
    /// </summary>
    public class AudioSourcePool
    {
        private readonly Queue<AudioSource> _pool = new Queue<AudioSource>();
        private readonly Transform _parent;
        private readonly int _initialSize;
        private int _createdCount;

        public int AvailableCount => _pool.Count;
        public int TotalCreated => _createdCount;

        public AudioSourcePool(Transform parent, int initialSize = 10)
        {
            _parent = parent;
            _initialSize = initialSize;

            // 预创建
            for (int i = 0; i < initialSize; i++)
            {
                var audioSource = CreateNew();
                _pool.Enqueue(audioSource);
            }
        }

        /// <summary>
        /// 获取 AudioSource
        /// </summary>
        public AudioSource Get()
        {
            AudioSource audioSource;

            if (_pool.Count > 0)
            {
                audioSource = _pool.Dequeue();
            }
            else
            {
                audioSource = CreateNew();
            }

            audioSource.gameObject.SetActive(true);
            return audioSource;
        }

        /// <summary>
        /// 归还 AudioSource
        /// </summary>
        public void Return(AudioSource audioSource)
        {
            if (audioSource == null) return;

            // 重置状态
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
            audioSource.volume = 1f;
            audioSource.pitch = 1f;
            audioSource.spatialBlend = 0f;
            audioSource.priority = 128;
            audioSource.outputAudioMixerGroup = null;

            // 归还到对象池
            audioSource.gameObject.SetActive(false);
            audioSource.transform.SetParent(_parent);
            audioSource.transform.localPosition = Vector3.zero;

            _pool.Enqueue(audioSource);
        }

        /// <summary>
        /// 创建新的 AudioSource
        /// </summary>
        private AudioSource CreateNew()
        {
            var go = new GameObject($"PooledAudioSource_{_createdCount++}");
            go.transform.SetParent(_parent);
            go.SetActive(false);

            return go.AddComponent<AudioSource>();
        }

        /// <summary>
        /// 清理对象池
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var audioSource = _pool.Dequeue();
                if (audioSource != null && audioSource.gameObject != null)
                {
                    UnityEngine.Object.Destroy(audioSource.gameObject);
                }
            }

            _createdCount = 0;
        }
    }
}
