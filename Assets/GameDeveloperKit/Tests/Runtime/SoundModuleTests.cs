using System;
using System.Collections;
using GameDeveloperKit.Sound;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class SoundModuleTests : RuntimeTestBase
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                App.Unregister<SoundModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }

            var root = GameObject.Find(SoundModule.RootName);
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                yield return null;
            }
        }

        [Test]
        public void Register_WhenSoundModuleIsRegistered_ReturnsSound()
        {
            App.Register<SoundModule>().GetAwaiter().GetResult();

            Assert.IsNotNull(App.Sound);
        }

        [Test]
        public void Startup_CreatesSoundRoot()
        {
            var module = new SoundModule();

            module.Startup();

            Assert.IsNotNull(GameObject.Find(SoundModule.RootName));
            module.Shutdown();
            Assert.IsNull(GameObject.Find(SoundModule.RootName));
        }

        [Test]
        public void SetVolume_WhenTrackIsValid_StoresVolume()
        {
            var module = new SoundModule();
            module.Startup();

            module.SetVolume(SoundTrack.Music, 0.25f);

            Assert.AreEqual(0.25f, module.GetVolume(SoundTrack.Music));
        }

        [Test]
        public void SetVolume_WhenOutOfRange_ThrowsAndDoesNotChangeVolume()
        {
            var module = new SoundModule();
            module.Startup();
            module.SetVolume(SoundTrack.Sfx, 0.5f);

            Assert.Throws<ArgumentOutOfRangeException>(() => module.SetVolume(SoundTrack.Sfx, 1.1f));

            Assert.AreEqual(0.5f, module.GetVolume(SoundTrack.Sfx));
        }

        [Test]
        public void PlayMusicAsync_WhenLocationIsNull_Throws()
        {
            var module = new SoundModule();

            Assert.Throws<ArgumentNullException>(() => module.PlayMusicAsync(null).GetAwaiter().GetResult());
        }

        [Test]
        public void PlaySfxAsync_WhenLocationIsEmpty_Throws()
        {
            var module = new SoundModule();

            Assert.Throws<ArgumentException>(() => module.PlaySfxAsync(" ").GetAwaiter().GetResult());
        }

        [Test]
        public void MixerCalls_WhenSettingsAreMissing_ReturnFalse()
        {
            var module = new SoundModule();
            module.Startup();

            Assert.IsFalse(module.SetMixerFloat("SfxReverbLevel", -1200f));
            Assert.IsFalse(module.TransitionSnapshot("Cave", 0.25f));
        }
    }
}
