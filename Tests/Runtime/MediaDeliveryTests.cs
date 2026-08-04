using System;
using GameDeveloperKit.Media;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests.Runtime
{
    public sealed class MediaDeliveryTests
    {
        private MediaDeliverySettings m_Settings;

        [SetUp]
        public void SetUp()
        {
            m_Settings = ScriptableObject.CreateInstance<MediaDeliverySettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_Settings);
        }

        [Test]
        public void Resolve_WhenCdnIsConfigured_UsesCdnBaseUrl()
        {
            m_Settings.SetPublicUrls(
                "https://bucket.oss-cn-chengdu.aliyuncs.com",
                "https://cdn.example.com/story/");

            var result = MediaUrlResolver.Resolve(
                new MediaPath("videos/media-a/master.m3u8"),
                m_Settings);

            Assert.AreEqual("https://cdn.example.com/story/videos/media-a/master.m3u8", result);
        }

        [Test]
        public void Resolve_WhenCdnIsEmpty_UsesOriginBaseUrl()
        {
            m_Settings.SetPublicUrls("https://bucket.cos.ap-chengdu.myqcloud.com/");

            var result = MediaUrlResolver.Resolve(
                new MediaPath("videos/media-a/1080P/index.m3u8"),
                m_Settings);

            Assert.AreEqual(
                "https://bucket.cos.ap-chengdu.myqcloud.com/videos/media-a/1080P/index.m3u8",
                result);
        }

        [TestCase("")]
        [TestCase("/videos/a.m3u8")]
        [TestCase("https://cdn.example.com/a.m3u8")]
        [TestCase("videos/../a.m3u8")]
        [TestCase("videos//a.m3u8")]
        [TestCase("videos\\a.m3u8")]
        public void MediaPath_WhenValueIsNotSafeRelativePath_Throws(string value)
        {
            Assert.Throws<ArgumentException>(() => new MediaPath(value));
        }

        [TestCase("http://cdn.example.com")]
        [TestCase("https://user:pass@cdn.example.com")]
        [TestCase("https://cdn.example.com?token=secret")]
        public void SetPublicUrls_WhenBaseUrlIsNotPublicHttpsRoot_Throws(string baseUrl)
        {
            Assert.Throws<ArgumentException>(() => m_Settings.SetPublicUrls(baseUrl));
        }
    }
}
