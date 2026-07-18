using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class AudioPlayableTests : RuntimeTestBase
    {
        [TearDown]
        public void TearDown()
        {
            if (App.TryGetRegistered<PlayableModule>(out _))
            {
                App.Unregister<PlayableModule>().GetAwaiter().GetResult();
            }

            var root = GameObject.Find(AudioPlayable.RootName);
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayableModule_WhenResolved_OwnsAudioPlayable()
        {
            Assert.AreSame(App.Playable.Audio, App.Playable.Get<AudioPlayable>());
            Assert.IsNotNull(GameObject.Find(AudioPlayable.RootName));
        }

        [Test]
        public void Dispose_DestroysAudioRoot()
        {
            var playable = new AudioPlayable();

            Assert.IsNotNull(GameObject.Find(AudioPlayable.RootName));
            playable.Dispose();

            Assert.IsNull(GameObject.Find(AudioPlayable.RootName));
        }

        [Test]
        public void SetVolume_WhenTrackIsValid_StoresVolume()
        {
            var playable = new AudioPlayable();

            playable.SetVolume(AudioTrack.Music, 0.25f);

            Assert.AreEqual(0.25f, playable.GetVolume(AudioTrack.Music));
            playable.Dispose();
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void SetVolume_WhenValueIsInvalid_ThrowsWithoutChangingVolume(float volume)
        {
            var playable = new AudioPlayable();
            playable.SetVolume(AudioTrack.Sfx, 0.5f);

            Assert.Throws<ArgumentOutOfRangeException>(() => playable.SetVolume(AudioTrack.Sfx, volume));
            Assert.AreEqual(0.5f, playable.GetVolume(AudioTrack.Sfx));
            playable.Dispose();
        }

        [Test]
        public void PlayAudioAsync_WhenLocationIsInvalid_ThrowsBeforeLoading()
        {
            var module = App.Playable;

            Assert.Throws<ArgumentNullException>(() => module.PlayAudioAsync(null).GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => module.PlayAudioAsync(" ").GetAwaiter().GetResult());
        }

        [Test]
        public void AudioPlayableRequest_WhenLocationKindVaries_ValidatesSourceContract()
        {
            Assert.AreEqual(
                AudioLocationKind.Url,
                new AudioPlayableRequest("https://cdn.example.com/audio/theme.ogg", AudioLocationKind.Url).LocationKind);
            Assert.AreEqual(
                AudioLocationKind.StreamingAssets,
                new AudioPlayableRequest("audio/theme.ogg", AudioLocationKind.StreamingAssets).LocationKind);
            Assert.Throws<ArgumentException>(() =>
                new AudioPlayableRequest("http://cdn.example.com/theme.ogg", AudioLocationKind.Url));
            Assert.Throws<ArgumentException>(() =>
                new AudioPlayableRequest("../theme.ogg", AudioLocationKind.StreamingAssets));
            Assert.Throws<ArgumentException>(() =>
                new AudioPlayableRequest("Assets/StreamingAssets/theme.ogg", AudioLocationKind.StreamingAssets));
        }

        [Test]
        public void PlayAsync_WhenFadeIsInvalid_ThrowsBeforeLoading()
        {
            var playable = new AudioPlayable();
            var request = new AudioPlayableRequest("audio/test", new AudioPlayableOptions
            {
                FadeIn = float.NaN,
            });

            Assert.Throws<ArgumentOutOfRangeException>(() => playable.PlayAsync(request).GetAwaiter().GetResult());
            playable.Dispose();
        }

        [Test]
        public void MusicRequestGate_WhenSecondRequestBegins_InvalidatesPreparingFirstRequest()
        {
            var playable = new AudioPlayable();
            var first = CreateHandle("music/first", AudioTrack.Music);
            var second = CreateHandle("music/second", AudioTrack.Music);

            var firstIdentity = playable.BeginRequest(AudioTrack.Music, first);
            var secondIdentity = playable.BeginRequest(AudioTrack.Music, second);

            Assert.AreEqual(PlayableStatus.Canceled, first.Status);
            Assert.IsFalse(playable.CanCommitRequest(AudioTrack.Music, firstIdentity));
            Assert.IsTrue(playable.CanCommitRequest(AudioTrack.Music, secondIdentity));
            playable.Dispose();
        }

        [UnityTest]
        public IEnumerator RequestCancel_WhenCalledFromWorkerThread_CommitsOnPlayerLoop()
        {
            var handle = CreateHandle("audio/test", AudioTrack.Sfx);
            handle.Start(CancellationToken.None);

            return UniTask.ToCoroutine(async () =>
            {
                await UniTask.RunOnThreadPool(() => handle.RequestCancel());
                try
                {
                    await handle.WaitForCompletionAsync();
                }
                catch (OperationCanceledException)
                {
                }

                Assert.AreEqual(PlayableStatus.Canceled, handle.Status);
            });
        }

        [Test]
        public void MixerCalls_WhenSettingsAreMissing_ReturnFalse()
        {
            var playable = new AudioPlayable();

            Assert.IsFalse(playable.SetMixerFloat("SfxReverbLevel", -1200f));
            Assert.IsFalse(playable.TransitionSnapshot("Cave", 0.25f));
            playable.Dispose();
        }

        private static AudioPlayableHandle CreateHandle(string location, AudioTrack track)
        {
            return new AudioPlayableHandle(location, track, _ => { }, _ => { }, _ => { });
        }
    }
}
