using System;
using System.Linq;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Events;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.UI;
using NUnit.Framework;

namespace GameDeveloperKit.Tests.Runtime
{
    public sealed class StoryPlayerWindowContractTests
    {
        [Test]
        public void PlayerWindows_DirectlyUseUIWindowAndExposeBusinessHooks()
        {
            Assert.IsTrue(typeof(UIWindow).IsAssignableFrom(typeof(VideoPlayerWindow)));
            Assert.IsTrue(typeof(UIWindow).IsAssignableFrom(typeof(ImagePlayerWindow)));
            Assert.IsTrue(typeof(VideoPlayerWindow).GetMethod(nameof(VideoPlayerWindow.PlayAsync)).IsVirtual);
            Assert.IsTrue(typeof(ImagePlayerWindow).GetMethod(nameof(ImagePlayerWindow.PlayAsync)).IsVirtual);
            Assert.IsNotNull(typeof(ImagePlayerWindow).GetEvent(nameof(ImagePlayerWindow.ImageClicked)));
        }

        [Test]
        public void UnlockNode_IsRegisteredAsAnEventActionAndLogicIsNotAnAuthoringNode()
        {
            Assert.IsTrue(NodeSchemaRegistry.TryGet(NodeKind.Unlock, out var unlock));
            Assert.AreEqual(NodeCategory.Action, unlock.Category);
            Assert.IsTrue(unlock.Parameters.Any(item => item.Key == StoryCommandNames.UnlockIdArgument));
            Assert.IsFalse(NodeSchemaRegistry.IsDefaultAuthoringNode(NodeKind.Logic));
        }

        [Test]
        public void StoryUnlockEvent_PreservesPlaybackContext()
        {
            var args = new StoryUnlockEvent("story", "volume", "episode", "step", "chapter-2");
            Assert.AreEqual("story", args.StoryId);
            Assert.AreEqual("volume", args.VolumeId);
            Assert.AreEqual("episode", args.EpisodeId);
            Assert.AreEqual("step", args.StepId);
            Assert.AreEqual("chapter-2", args.UnlockId);
            Assert.IsFalse(args.HasUse());
        }
    }
}
